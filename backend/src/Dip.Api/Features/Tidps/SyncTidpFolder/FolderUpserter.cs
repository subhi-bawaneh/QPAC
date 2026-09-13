using Dip.Application.Documents;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Tidps.SyncTidpFolder;

// Owner and discipline folders, upserted by their path relative to the root exactly as
// files are. Held for the life of one sync: the same owner folder is reached once per
// file inside it, and each must return the same row rather than a second insert.
internal sealed class FolderUpserter
{
    private readonly DipDbContext _db;
    private readonly Guid _projectId;
    private readonly DateTime _now;
    private readonly string _by;
    private readonly TidpPathParser _parser;

    private readonly Dictionary<string, TidpFolderOwner> _owners;
    private readonly Dictionary<string, TidpFolderDiscipline> _disciplines;
    private readonly HashSet<string> _seen = new(StringComparer.OrdinalIgnoreCase);

    public FolderUpserter(
        DipDbContext db, Guid projectId, DateTime now, string by, TidpPathParser parser,
        IEnumerable<TidpFolderOwner> existingOwners,
        IEnumerable<TidpFolderDiscipline> existingDisciplines)
    {
        _db = db;
        _projectId = projectId;
        _now = now;
        _by = by;
        _parser = parser;
        _owners = existingOwners.ToDictionary(o => o.RelativePath, StringComparer.OrdinalIgnoreCase);
        _disciplines = existingDisciplines.ToDictionary(d => d.RelativePath, StringComparer.OrdinalIgnoreCase);
    }

    // The folders the client listed, empty ones included. Anything two levels deep that
    // reads as `XX-Name` becomes a discipline row under its owner; anything deeper is
    // left to the files inside it, which carry their own full path.
    public void Declare(IEnumerable<string>? folderPaths)
    {
        foreach (var declared in folderPaths ?? [])
        {
            var path = TidpPathParser.Normalize(declared);
            if (path.Length == 0) continue;

            var segments = path.Split('/');
            if (segments.Length > 2) continue;

            var owner = UpsertOwnerFolder(segments[0], _parser.ParseOwnerFolder(segments[0]));
            if (segments.Length == 2)
            {
                var parsed = _parser.ParseDisciplineFolder(segments[1]);
                if (parsed is not null) UpsertDisciplineFolder(path, parsed, owner);
            }
        }
    }

    public TidpFolderOwner UpsertOwner(string filePath, ParsedOwnerFolder parsed) =>
        UpsertOwnerFolder(TidpPathParser.OwnerPath(filePath), parsed);

    private TidpFolderOwner UpsertOwnerFolder(string ownerPath, ParsedOwnerFolder parsed)
    {
        var row = Get(_owners, ownerPath) ?? Create(ownerPath, parsed.FolderName);

        row.FolderName = parsed.FolderName;
        row.SortOrder = parsed.SortOrder;
        row.OwnerName = parsed.OwnerName;
        row.OwnerType = parsed.OwnerType;
        MarkPresent(row.RelativePath, () => { row.FolderStatus = TidpFolderStatus.Present; row.MissingSince = null; });
        row.LastSeenAt = _now;
        row.UpdatedAt = _now;
        row.UpdatedBy = _by;
        return row;
    }

    public TidpFolderDiscipline? UpsertDiscipline(
        string filePath, ParsedDisciplineFolder? parsed, TidpFolderOwner owner)
    {
        var path = TidpPathParser.DisciplinePath(filePath);
        // Either the file sits straight under its owner, or the level-2 folder is not a
        // discipline folder at all. Both are valid; neither invents a discipline row.
        if (path is null || parsed is null) return null;

        return UpsertDisciplineFolder(path, parsed, owner);
    }

    // Paths the sync did not meet this time. Like files, folders are marked rather than
    // removed: an emptied discipline folder is still part of the project's shape.
    public void MarkMissing(DateTime now)
    {
        foreach (var owner in _owners.Values.Where(o => !_seen.Contains(o.RelativePath)))
        {
            if (owner.FolderStatus == TidpFolderStatus.Missing) continue;
            owner.FolderStatus = TidpFolderStatus.Missing;
            owner.MissingSince = now;
        }

        foreach (var discipline in _disciplines.Values.Where(d => !_seen.Contains(d.RelativePath)))
        {
            if (discipline.FolderStatus == TidpFolderStatus.Missing) continue;
            discipline.FolderStatus = TidpFolderStatus.Missing;
            discipline.MissingSince = now;
        }
    }

    // ------------------------------------------------------------------ internals

    private TidpFolderDiscipline UpsertDisciplineFolder(
        string path, ParsedDisciplineFolder parsed, TidpFolderOwner owner)
    {
        if (!_disciplines.TryGetValue(path, out var row))
        {
            row = new TidpFolderDiscipline
            {
                ProjectId = _projectId,
                RelativePath = path,
                CreatedAt = _now,
                CreatedBy = _by,
            };
            _db.TidpFolderDisciplines.Add(row);
            _disciplines[path] = row;
        }

        row.OwnerId = owner.Id;
        row.FolderName = parsed.FolderName;
        row.DisciplineCode = parsed.DisciplineCode;
        row.DisciplineName = parsed.DisciplineName;
        MarkPresent(path, () => { row.FolderStatus = TidpFolderStatus.Present; row.MissingSince = null; });
        row.LastSeenAt = _now;
        row.UpdatedAt = _now;
        row.UpdatedBy = _by;
        return row;
    }

    private TidpFolderOwner Create(string path, string folderName)
    {
        var row = new TidpFolderOwner
        {
            ProjectId = _projectId,
            RelativePath = path,
            FolderName = folderName,
            CreatedAt = _now,
            CreatedBy = _by,
        };
        _db.TidpFolderOwners.Add(row);
        _owners[path] = row;
        return row;
    }

    private void MarkPresent(string path, Action apply)
    {
        _seen.Add(path);
        apply();
    }

    private static T? Get<T>(Dictionary<string, T> map, string key) where T : class =>
        map.TryGetValue(key, out var value) ? value : null;
}

// The corporate Discipline a new TidpFile row points at before its workbook has been
// opened. Best effort on purpose: the importer reads the sheet's own DISCIPLINE header
// and overwrites this the moment the file is parsed. What it buys is a row that is
// attributable the instant it is created rather than only after the worker reaches it.
internal sealed class DisciplineResolver
{
    private readonly DipDbContext _db;
    private readonly Guid _projectId;
    private readonly List<Discipline> _known;

    public DisciplineResolver(DipDbContext db, Guid projectId, List<Discipline> known)
    {
        _db = db;
        _projectId = projectId;
        _known = known;
    }

    public Guid Resolve(string disciplineTag, string? folderName)
    {
        // The file name's own tag first (ARC, ELE, STL …), then the folder's spelt-out
        // name (`AR-Architectural` -> "Architectural"), which is what the corporate
        // list is keyed by. Tags with neither — FWR, KNL, VTR, sitting straight under an
        // owner folder — get a row of their own, exactly as the importer would make one.
        var match =
            Find(d => string.Equals(d.Code, disciplineTag, StringComparison.OrdinalIgnoreCase))
            ?? (folderName is null
                ? null
                : Find(d => string.Equals(d.CorporateName, folderName, StringComparison.OrdinalIgnoreCase)))
            ?? Find(d => string.Equals(d.CorporateName, disciplineTag, StringComparison.OrdinalIgnoreCase));

        if (match is not null) return match.Id;

        var created = new Discipline
        {
            ProjectId = _projectId,
            Code = Truncate(disciplineTag, 20),
            CorporateName = Truncate(folderName ?? disciplineTag, 100),
        };
        // Added to the in-memory list as well as the context: the same tag is reached
        // once per file that carries it, and the project's unique index on
        // (ProjectId, Code) would refuse the second insert.
        _db.Disciplines.Add(created);
        _known.Add(created);
        return created.Id;
    }

    private Discipline? Find(Func<Discipline, bool> predicate) => _known.FirstOrDefault(predicate);

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
