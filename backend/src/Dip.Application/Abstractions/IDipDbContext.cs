using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dip.Application.Abstractions;

// Ports for the application layer. Handlers depend on this interface, not the concrete
// DipDbContext, so unit tests can swap in an in-memory or fake context.
public interface IDipDbContext
{
    DbSet<Project> Projects { get; }
    DbSet<Discipline> Disciplines { get; }
    DbSet<PicklistItem> PicklistItems { get; }
    DbSet<StatusMapping> StatusMappings { get; }
    DbSet<BaselineActivity> BaselineActivities { get; }
    DbSet<Folder> Folders { get; }
    DbSet<FolderFile> FolderFiles { get; }
    DbSet<Tidp> Tidps { get; }
    DbSet<Document> Documents { get; }
    DbSet<DataExchange> DataExchanges { get; }
    DbSet<AconexRevision> AconexRevisions { get; }
    DbSet<TidpDraft> TidpDrafts { get; }
    DbSet<DocumentDraft> DocumentDrafts { get; }
    DbSet<DataExchangeDraft> DataExchangeDrafts { get; }
    DbSet<PromoteBatch> PromoteBatches { get; }
    DbSet<DocumentSnapshot> DocumentSnapshots { get; }
    DbSet<ImportBatch> ImportBatches { get; }
    DbSet<ImportStagingRow> ImportStagingRows { get; }
    DbSet<AuditLog> AuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
