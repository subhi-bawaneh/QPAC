using Dip.Application.Abstractions;
using Dip.Application.Authorization;
using Dip.Api.Features.Tidps.UploadTidpFile;

namespace Dip.Api.Features.Baseline.UploadBaseline;

// The P6 export. A re-upload replaces the baseline wholesale, so every outgoing
// activity is dumped to AuditLogs first, exactly as a TIDP replace is.
[Permission(Permissions.FilesManage)]
public sealed record UploadBaselineCommand(
    Guid ProjectId,
    string FileName,
    byte[] Content) : ICommand<UploadAccepted>;
