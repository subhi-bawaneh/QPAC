using System.Text.Json;
using Dip.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.Tidps.SyncTidpFolder;

// The whole TIDP folder in one multipart request.
//
// Multipart rather than a zip, for three reasons. A browser's `webkitdirectory` input
// already yields each file's `webkitRelativePath` and `lastModified`, so nothing has to
// be re-derived; a zip would need a packing library on the client (a new dependency)
// and its DOS timestamps are rounded to two seconds, which loses the precision
// PLAN.md § 1 asks for; and every other upload in this API is already an IFormFile.
//
// The parts are described by a `manifest` field rather than by position, because
// position is exactly the kind of coupling that silently attaches one file's bytes to
// another file's path:
//
//   manifest = {
//     "rootName": "02.TIDPs",
//     "folders":  ["01.NAP", "01.NAP/AR-Architectural", "01.NAP/ID-Interior Design", ...],
//     "files": [
//       { "field": "f0",
//         "relativePath": "02.TIDPs/01.NAP/AR-Architectural/QF01012-...-000001.xlsx",
//         "lastModifiedUtc": "2026-09-01T10:22:31.4170000Z",
//         "sizeBytes": 364959 }, ... ] }
//   f0 = <the bytes of that file>
//   f1 = ...
//
// `folders` is optional and exists for one reason: an empty discipline folder has no
// files to carry it, and `01.NAP/ID-Interior Design` is a fact about the project worth
// keeping. A client that cannot enumerate folders leaves it out.
[Route("api/projects/{projectId:guid}/tidp-folder")]
[Authorize]
public sealed class SyncTidpFolderController : ApiControllerBase
{
    // The sample folder is 7 MB across 36 workbooks; the cap leaves room for a project
    // several times that without letting one request take the process down with it.
    private const long MaxBytes = 500L * 1024 * 1024;

    private static readonly JsonSerializerOptions ManifestJson = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    [HttpPost("sync")]
    [RequestSizeLimit(MaxBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxBytes, ValueCountLimit = 8192)]
    [ProducesResponseType(typeof(TidpFolderSyncResult), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TidpFolderSyncResult>> Sync(
        Guid projectId, [FromForm] string manifest, CancellationToken ct = default)
    {
        TidpFolderManifest? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<TidpFolderManifest>(manifest, ManifestJson);
        }
        catch (JsonException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "The manifest is not valid JSON",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest,
            });
        }

        if (parsed is null)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "The manifest is empty",
                Status = StatusCodes.Status400BadRequest,
            });
        }

        var form = await Request.ReadFormAsync(ct);
        var files = new List<UploadedTidpFile>(parsed.Files.Count);
        var missing = new List<string>();

        foreach (var entry in parsed.Files)
        {
            var part = form.Files[entry.Field];
            if (part is null)
            {
                // Named in the manifest but absent from the body — a truncated upload,
                // and the one thing here worth refusing outright: importing what did
                // arrive would mark everything else Missing.
                missing.Add(entry.RelativePath);
                continue;
            }

            using var buffer = new MemoryStream();
            await part.CopyToAsync(buffer, ct);
            files.Add(new UploadedTidpFile(
                entry.RelativePath,
                entry.LastModifiedUtc,
                // The manifest's size is the client's claim; the part is the fact.
                buffer.Length,
                buffer.ToArray()));
        }

        if (missing.Count > 0)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "The upload is incomplete",
                Detail =
                    $"{missing.Count} file(s) named in the manifest carry no bytes, "
                    + $"starting with '{missing[0]}'. Nothing was imported.",
                Status = StatusCodes.Status400BadRequest,
            });
        }

        var result = await Dispatcher.Send(
            new SyncTidpFolderCommand(projectId, parsed.RootName, parsed.Folders, files), ct);

        // 202, not 200: what comes back is the plan. Added and Updated files are queued
        // for the worker, and their outcome is read from the GET or watched on the hub.
        return Accepted(result);
    }
}

public sealed record TidpFolderManifest(
    string? RootName,
    IReadOnlyList<string> Folders,
    IReadOnlyList<TidpFolderManifestFile> Files)
{
    public string? RootName { get; init; } = RootName;
    public IReadOnlyList<string> Folders { get; init; } = Folders ?? [];
    public IReadOnlyList<TidpFolderManifestFile> Files { get; init; } = Files ?? [];
}

public sealed record TidpFolderManifestFile(
    string Field,
    string RelativePath,
    DateTime? LastModifiedUtc,
    long SizeBytes);
