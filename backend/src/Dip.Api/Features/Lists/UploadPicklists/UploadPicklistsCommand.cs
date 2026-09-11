using Dip.Application.Abstractions;
using Dip.Application.Authorization;
using Dip.Api.Features.Tidps.UploadTidpFile;

namespace Dip.Api.Features.Lists.UploadPicklists;

// The picklists workbook. This one upserts rather than replaces: a code an operator
// soft-deleted stays deleted, counted and reported, because a re-import must not
// resurrect a decision somebody made.
[Permission(Permissions.FilesManage)]
public sealed record UploadPicklistsCommand(
    Guid ProjectId,
    string FileName,
    byte[] Content) : ICommand<UploadAccepted>;
