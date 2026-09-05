using Dip.Application.Abstractions;
using Dip.Domain.Entities;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Imports.StartImport;

public sealed class StartImportHandler : ICommandHandler<StartImportCommand, StartImportResult>
{
    private readonly DipDbContext _db;
    private readonly ICurrentUser _currentUser;

    public StartImportHandler(DipDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<StartImportResult> Handle(StartImportCommand command, CancellationToken ct)
    {
        var file = await _db.FolderFiles
            .FirstOrDefaultAsync(f => f.Id == command.FolderFileId, ct)
            ?? throw new KeyNotFoundException($"FolderFile {command.FolderFileId} not found");

        if (string.IsNullOrEmpty(file.StoragePath))
        {
            throw new FluentValidation.ValidationException(
                "The file has no local copy - upload it or sync from Drive first");
        }

        var batch = new ImportBatch
        {
            ProjectId = command.ProjectId,
            Kind = command.Kind,
            Target = command.Target,
            FolderFileId = file.Id,
            FileName = file.Name,
            ImportedAt = DateTime.UtcNow,
            ImportedBy = _currentUser.UserName ?? "system",
            Completed = false,
        };
        _db.ImportBatches.Add(batch);
        // TransactionBehavior commits after we return.

        // Row count is populated by RunImportStep — we don't parse the file here
        // to keep StartImport cheap and predictable.
        return new StartImportResult(batch.Id, TotalRows: 0);
    }
}
