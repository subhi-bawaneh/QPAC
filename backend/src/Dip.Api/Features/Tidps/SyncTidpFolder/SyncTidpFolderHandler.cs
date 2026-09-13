using System.Security.Cryptography;
using System.Text.Json;
using Dip.Api.Features.Audit;
using Dip.Api.Hubs;
using Dip.Api.Workers;
using Dip.Application.Abstractions;
using Dip.Application.Documents;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dip.Api.Features.Tidps.SyncTidpFolder;

// Compares an uploaded TIDP folder against what the database already holds and queues
// only the difference.
//
// What happens here is deliberately cheap — hashing, comparing, upserting three sets of
// rows. Not one workbook is opened: a file that needs importing becomes an ImportBatch
// on the existing queue and is parsed by the hosted worker exactly as a single-file
// upload is, which is what keeps a 36-workbook folder off the request thread (§ 9).
//
// Nothing is ever deleted. A path that stops appearing is marked Missing, because in a
// folder people reorganise by dragging, and a file that left `06.Subcontractor -
// Unassigned` has almost always arrived somewhere else — which is what the move
// suggestions are for.
public sealed class SyncTidpFolderHandler : ICommandHandler<SyncTidpFolderCommand, TidpFolderSyncResult>
{
    private readonly DipDbContext _db;
    private readonly WorkQueue _queue;
    private readonly ISyncNotifier _notifier;
    private readonly ICurrentUser _user;
    private readonly TidpPathParser _parser;
    private readonly ILogger<SyncTidpFolderHandler> _logger;

    public SyncTidpFolderHandler(
        DipDbContext db,
        WorkQueue queue,
        ISyncNotifier notifier,
        ICurrentUser user,
        IOptions<TidpFolderOptions> options,
        ILogger<SyncTidpFolderHandler> logger)
    {
        _db = db;
        _queue = queue;
        _notifier = notifier;
        _user = user;
        _parser = new TidpPathParser(options.Value.ToRules());
        _logger = logger;
    }

    public async Task<TidpFolderSyncResult> Handle(SyncTidpFolderCommand command, CancellationToken ct)
    {
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == command.ProjectId, ct)
            ?? throw new KeyNotFoundException($"Project {command.ProjectId} not found");

        var now = DateTime.UtcNow;
        var by = _user.UserName ?? "system";

        // The root the operator picked is not part of any key: the same folder uploaded
        // as `02.TIDPs` and as `TIDPs` must match the rows it made last time.
        var rootName = string.IsNullOrWhiteSpace(command.RootName)
            ? TidpPathParser.DetectRoot(command.Files.Select(f => f.RelativePath)) ?? string.Empty
            : TidpPathParser.Normalize(command.RootName);

        var incoming = Deduplicate(command.Files, rootName, out var duplicates);

        var existingFiles = await _db.TidpFiles
            .Where(t => t.ProjectId == project.Id && t.RelativePath != null)
            .ToListAsync(ct);
        var byPath = existingFiles
            .ToDictionary(t => t.RelativePath!, StringComparer.OrdinalIgnoreCase);

        var folders = new FolderUpserter(
            _db, project.Id, now, by, _parser,
            await _db.TidpFolderOwners.Where(o => o.ProjectId == project.Id).ToListAsync(ct),
            await _db.TidpFolderDisciplines.Where(d => d.ProjectId == project.Id).ToListAsync(ct));

        var disciplines = new DisciplineResolver(
            _db, project.Id,
            await _db.Disciplines.Where(d => d.ProjectId == project.Id).ToListAsync(ct));

        var results = new List<TidpFolderSyncFile>(incoming.Count + duplicates.Count);
        results.AddRange(duplicates);

        var toImport = new List<(TidpFile File, byte[] Content)>();
        var updatedFileIds = new List<Guid>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Declared folders come first so an empty discipline folder is recorded even
        // though no file in the upload ever mentions it (`01.NAP/ID-Interior Design`).
        // Root-stripped like the file paths are: left alone, `02.TIDPs/01.NAP` reads as
        // an owner folder called `02.TIDPs` holding a discipline called `01.NAP`.
        folders.Declare(command.Folders.Select(f => TidpPathParser.StripRoot(f, rootName)));

        foreach (var (path, upload) in incoming)
        {
            seen.Add(path);
            results.Add(Classify(
                path, upload, byPath, folders, disciplines, project, now, by,
                toImport, updatedFileIds));
        }

        results.AddRange(MarkMissing(existingFiles, seen, now));
        SuggestMoves(results);
        folders.MarkMissing(now);

        // The audit dump has to be on disk before the rows it describes are deleted:
        // once they are gone this is the only record of what they held. Same order,
        // and the same guarantee, as a single-file replace.
        if (updatedFileIds.Count > 0)
        {
            foreach (var fileId in updatedFileIds)
            {
                await AuditDump.DocumentsAsync(_db, project.Id, fileId, AuditDump.Replaced, by, ct);
            }
            await _db.SaveChangesAsync(ct);
            await _db.Documents.Where(d => updatedFileIds.Contains(d.TidpFileId)).ExecuteDeleteAsync(ct);
        }

        var batches = QueueImports(project.Id, rootName, toImport, now, by, results);
        var sync = Record(project.Id, rootName, now, by, results);

        _logger.LogInformation(
            "TIDP folder sync {SyncId} for {Project}: {Total} file(s) - "
            + "{Added} added, {Updated} updated, {Skipped} skipped, {Missing} missing, {Failed} failed",
            sync.Id, project.Code, sync.TotalFiles,
            sync.Added, sync.Updated, sync.Skipped, sync.Missing, sync.Failed);

        foreach (var warning in results.Where(r => r.Error is not null))
        {
            _logger.LogWarning(
                "TIDP folder sync {SyncId}: {Path} - {Action}: {Error}",
                sync.Id, warning.RelativePath, warning.Action, warning.Error);
        }

        EnqueueAfterSave(project.Id, batches);

        return ToResult(sync, results);
    }

    // ------------------------------------------------------------- classification

    private TidpFolderSyncFile Classify(
        string path,
        UploadedTidpFile upload,
        IReadOnlyDictionary<string, TidpFile> byPath,
        FolderUpserter folders,
        DisciplineResolver disciplines,
        Project project,
        DateTime now,
        string by,
        List<(TidpFile, byte[])> toImport,
        List<Guid> updatedFileIds)
    {
        var parsed = _parser.ParsePath(path);

        // Not a TIDP workbook at all (`~$...`, a stray PDF): reported, not imported,
        // and above all not a reason to stop.
        if (parsed.IsSkipped)
        {
            return new TidpFolderSyncFile(
                path, TidpSyncAction.Skipped, null, null, null, null, null, null, null,
                Error: parsed.Error, Warnings: parsed.Warnings);
        }

        if (!parsed.IsImportable)
        {
            return Describe(parsed, path, TidpSyncAction.Failed, parsed.Error);
        }

        var owner = folders.UpsertOwner(path, parsed.Owner!);
        var folderDiscipline = folders.UpsertDiscipline(path, parsed.Discipline, owner);

        byPath.TryGetValue(path, out var existing);
        var action = Decide(existing, upload, out var hash);

        if (action == TidpSyncAction.Skipped)
        {
            // A touch with no edit behind it. The stamp is refreshed so the next sync
            // does not hash the file again, but nothing is re-parsed — a re-parse would
            // destroy every hand edit made since, for no change at all.
            existing!.LastModifiedUtc = upload.LastModifiedUtc ?? existing.LastModifiedUtc;
            existing.SizeBytes = upload.SizeBytes;
            existing.ContentHash ??= hash;
            Touch(existing, owner, folderDiscipline, parsed, now, by);
            return Describe(parsed, path, action, null, existing.Id);
        }

        var file = existing;
        if (file is null)
        {
            var disciplineId = disciplines.Resolve(
                parsed.File!.DisciplineTag, parsed.Discipline?.DisciplineName);

            file = new TidpFile
            {
                ProjectId = project.Id,
                DisciplineId = disciplineId,
                FileName = parsed.FileName,
                UploadedBy = by,
                UploadedAt = now,
                Status = TidpFileStatus.Importing,
                CreatedAt = now,
                CreatedBy = by,
                UpdatedAt = now,
                UpdatedBy = by,
            };
            _db.TidpFiles.Add(file);
        }
        else
        {
            file.Status = TidpFileStatus.Importing;
            file.Error = null;
            file.UploadedBy = by;
            file.UploadedAt = now;
            updatedFileIds.Add(file.Id);
        }

        file.ContentHash = hash;
        file.LastModifiedUtc = upload.LastModifiedUtc;
        file.SizeBytes = upload.SizeBytes;
        Touch(file, owner, folderDiscipline, parsed, now, by);

        toImport.Add((file, upload.Content));
        return Describe(parsed, path, action, null, file.Id);
    }

    // Timestamp, then size, then hash — cheapest first, and the hash is only reached
    // when one of the other two already disagrees.
    private static TidpSyncAction Decide(TidpFile? existing, UploadedTidpFile upload, out string hash)
    {
        if (existing is null)
        {
            hash = Hash(upload.Content);
            return TidpSyncAction.Added;
        }

        // A file whose last import failed is re-imported even when it has not changed:
        // otherwise a workbook that broke once could never be retried without an edit.
        if (existing.Status == TidpFileStatus.Failed)
        {
            hash = Hash(upload.Content);
            return TidpSyncAction.Updated;
        }

        var stampMatches =
            existing.LastModifiedUtc is not null
            && upload.LastModifiedUtc is not null
            && existing.LastModifiedUtc == upload.LastModifiedUtc
            && existing.SizeBytes == upload.SizeBytes;

        if (stampMatches && existing.ContentHash is not null)
        {
            hash = existing.ContentHash;
            return TidpSyncAction.Skipped;
        }

        hash = Hash(upload.Content);
        return string.Equals(hash, existing.ContentHash, StringComparison.OrdinalIgnoreCase)
            ? TidpSyncAction.Skipped
            : TidpSyncAction.Updated;
    }

    private static string Hash(byte[] content) => Convert.ToHexString(SHA256.HashData(content));

    // Everything the folder says about a file, refreshed on every sync — a file that
    // moved between owners keeps its row and changes its attribution.
    private static void Touch(
        TidpFile file, TidpFolderOwner owner, TidpFolderDiscipline? discipline,
        ParsedTidpPath parsed, DateTime now, string by)
    {
        file.RelativePath = parsed.RelativePath;
        file.FileName = parsed.FileName;
        file.OwnerId = owner.Id;
        file.FolderDisciplineId = discipline?.Id;
        file.LastSeenAt = now;
        file.FolderStatus = TidpFolderStatus.Present;
        file.MissingSince = null;

        var name = parsed.File!;
        file.NameProjectCode = name.ProjectCode;
        file.NameOriginator = name.Originator;
        file.NameContract = name.Contract;
        file.NameDocType = name.DocType;
        file.DisciplineTag = name.DisciplineTag;
        file.NameZone = name.Zone;
        file.NameLevel = name.Level;
        file.Sequence = name.Sequence;

        file.UpdatedAt = now;
        file.UpdatedBy = by;
    }

    // ------------------------------------------------------------------ missing

    private static IEnumerable<TidpFolderSyncFile> MarkMissing(
        IEnumerable<TidpFile> existing, IReadOnlySet<string> seen, DateTime now)
    {
        foreach (var file in existing.Where(f => !seen.Contains(f.RelativePath!)))
        {
            // MissingSince is set once and kept: it dates the disappearance, not the
            // sync that noticed it again.
            if (file.FolderStatus != TidpFolderStatus.Missing)
            {
                file.FolderStatus = TidpFolderStatus.Missing;
                file.MissingSince = now;
            }

            yield return new TidpFolderSyncFile(
                file.RelativePath!, TidpSyncAction.Missing,
                null, null, null, null, null, file.DisciplineTag, file.Sequence,
                Error: "not present in this upload",
                TidpFileId: file.Id);
        }
    }

    // A name that vanished from one folder and appeared in another with identical
    // content is a move. Reported on both halves, acted on by neither: only a person
    // knows whether the file was moved or copied.
    private static void SuggestMoves(List<TidpFolderSyncFile> results)
    {
        var added = results
            .Where(r => r.Action == TidpSyncAction.Added)
            .ToLookup(r => FileNameOf(r.RelativePath), StringComparer.OrdinalIgnoreCase);
        if (added.Count == 0) return;

        for (var i = 0; i < results.Count; i++)
        {
            var missing = results[i];
            if (missing.Action != TidpSyncAction.Missing) continue;

            var candidate = added[FileNameOf(missing.RelativePath)].FirstOrDefault();
            if (candidate is null) continue;

            results[i] = missing with { MovedTo = candidate.RelativePath };
            var at = results.IndexOf(candidate);
            results[at] = candidate with { MovedFrom = missing.RelativePath };
        }
    }

    private static string FileNameOf(string path) =>
        path.Split('/') is { Length: > 0 } parts ? parts[^1] : path;

    // ------------------------------------------------------------------ queueing

    private List<(Guid BatchId, byte[] Content, string FileName)> QueueImports(
        Guid projectId, string rootName, IReadOnlyList<(TidpFile File, byte[] Content)> toImport,
        DateTime now, string by, List<TidpFolderSyncFile> results)
    {
        var queued = new List<(Guid, byte[], string)>(toImport.Count);

        foreach (var (file, content) in toImport)
        {
            var label = Truncate(
                string.IsNullOrEmpty(rootName) ? file.RelativePath! : $"{rootName}/{file.RelativePath}",
                500);

            var batch = new ImportBatch
            {
                ProjectId = projectId,
                Kind = ImportKind.Tidp,
                TidpFileId = file.Id,
                FileName = label,
                ImportedAt = now,
                UploadedBy = by,
                Status = ImportBatchStatus.Queued,
            };
            _db.ImportBatches.Add(batch);
            queued.Add((batch.Id, content, label));

            var at = results.FindIndex(r =>
                string.Equals(r.RelativePath, file.RelativePath, StringComparison.OrdinalIgnoreCase)
                && r.Action is TidpSyncAction.Added or TidpSyncAction.Updated);
            if (at >= 0) results[at] = results[at] with { BatchId = batch.Id };
        }

        return queued;
    }

    // The queue holds the only copy of the bytes, so nothing may reach it before the
    // rows that explain it are committed — a worker that started early would look up a
    // batch that does not exist yet.
    private void EnqueueAfterSave(Guid projectId, List<(Guid BatchId, byte[] Content, string FileName)> batches)
    {
        if (batches.Count == 0) return;

        _db.SavedChanges += Enqueue;

        void Enqueue(object? sender, SavedChangesEventArgs args)
        {
            _db.SavedChanges -= Enqueue;
            foreach (var (batchId, content, fileName) in batches)
            {
                _queue.EnqueueImport(batchId, content);
                _ = _notifier.ImportQueuedAsync(projectId, batchId, fileName);
            }
        }
    }

    // ------------------------------------------------------------------ plumbing

    // Two manifest entries for one path is the client's bug, not ours: the first wins
    // and the rest are reported rather than silently overwriting each other.
    private static List<(string Path, UploadedTidpFile Upload)> Deduplicate(
        IReadOnlyList<UploadedTidpFile> files, string rootName, out List<TidpFolderSyncFile> duplicates)
    {
        var kept = new List<(string, UploadedTidpFile)>(files.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        duplicates = [];

        foreach (var file in files)
        {
            var path = TidpPathParser.StripRoot(file.RelativePath, rootName);
            if (seen.Add(path))
            {
                kept.Add((path, file));
            }
            else
            {
                duplicates.Add(new TidpFolderSyncFile(
                    path, TidpSyncAction.Failed, null, null, null, null, null, null, null,
                    Error: "the upload carries this path more than once"));
            }
        }

        return kept;
    }

    private static TidpFolderSyncFile Describe(
        ParsedTidpPath parsed, string path, TidpSyncAction action, string? error, Guid? fileId = null) =>
        new(path,
            action,
            parsed.Owner?.OwnerName,
            parsed.Owner?.OwnerType,
            parsed.Owner?.SortOrder,
            parsed.Discipline?.DisciplineCode,
            parsed.Discipline?.DisciplineName,
            parsed.File?.DisciplineTag,
            parsed.File?.Sequence,
            error,
            TidpFileId: fileId,
            Warnings: parsed.Warnings.Count > 0 ? parsed.Warnings : null);

    private TidpFolderSync Record(
        Guid projectId, string rootName, DateTime now, string by, List<TidpFolderSyncFile> results)
    {
        var sync = new TidpFolderSync
        {
            ProjectId = projectId,
            RootName = rootName,
            StartedAt = now,
            StartedBy = by,
            TotalFiles = results.Count,
            Added = results.Count(r => r.Action == TidpSyncAction.Added),
            Updated = results.Count(r => r.Action == TidpSyncAction.Updated),
            Skipped = results.Count(r => r.Action == TidpSyncAction.Skipped),
            Missing = results.Count(r => r.Action == TidpSyncAction.Missing),
            Failed = results.Count(r => r.Action == TidpSyncAction.Failed),
        };
        sync.ResultJson = JsonSerializer.Serialize(results, TidpFolderSyncJson.Options);
        _db.TidpFolderSyncs.Add(sync);
        return sync;
    }

    private static TidpFolderSyncResult ToResult(TidpFolderSync sync, IReadOnlyList<TidpFolderSyncFile> files) =>
        new(sync.Id, sync.ProjectId, sync.RootName, sync.StartedAt,
            sync.TotalFiles, sync.Added, sync.Updated, sync.Skipped, sync.Missing, sync.Failed,
            files);

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}

// One serializer for the stored plan, so what the POST returns and what the GET reads
// back are the same shape.
internal static class TidpFolderSyncJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };
}
