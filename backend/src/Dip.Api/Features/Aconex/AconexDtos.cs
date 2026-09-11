namespace Dip.Api.Features.Aconex;

// One file's share of a multi-file Aconex upload, before anything is written.
//
// This is the only preview left in the system, because it is the only decision the
// operator cannot make without the numbers: they export from Aconex periodically and
// cannot remember what was already loaded, so "this file is 8,000 lines of which 12 are
// new" is the whole answer.
public sealed record AconexFilePreview(
    string FileName,
    int RowsRead,
    int RowsNew,
    int RowsDuplicate,
    string? Error);

public sealed record AconexPreview(
    IReadOnlyList<AconexFilePreview> Files,
    int TotalRowsRead,
    int TotalRowsNew,
    int TotalRowsDuplicate);

public sealed record AconexUploadAccepted(IReadOnlyList<AconexQueuedFile> Files);

public sealed record AconexQueuedFile(Guid BatchId, string FileName);
