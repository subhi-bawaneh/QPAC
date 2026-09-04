using System.Reflection;
using Dip.Application.Abstractions;
using Dip.Domain.Entities;
using Dip.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Dip.Infrastructure.Persistence;

public class DipDbContext
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>, IDipDbContext
{
    static DipDbContext()
    {
        // PLAN.md § 1: all timestamps are 'timestamp without time zone' (naive, matching Excel).
        // Enable the legacy switch so DateTime.Kind = Utc/Local both round-trip.
        // Decision documented in docs/decisions/001-datetime.md.
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
    }

    public DipDbContext(DbContextOptions<DipDbContext> options) : base(options) { }

    // Live entities
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Discipline> Disciplines => Set<Discipline>();
    public DbSet<PicklistItem> PicklistItems => Set<PicklistItem>();
    public DbSet<StatusMapping> StatusMappings => Set<StatusMapping>();
    public DbSet<BaselineActivity> BaselineActivities => Set<BaselineActivity>();
    public DbSet<Folder> Folders => Set<Folder>();
    public DbSet<FolderFile> FolderFiles => Set<FolderFile>();
    public DbSet<Tidp> Tidps => Set<Tidp>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DataExchange> DataExchanges => Set<DataExchange>();
    public DbSet<AconexRevision> AconexRevisions => Set<AconexRevision>();

    // Draft entities
    public DbSet<TidpDraft> TidpDrafts => Set<TidpDraft>();
    public DbSet<DocumentDraft> DocumentDrafts => Set<DocumentDraft>();
    public DbSet<DataExchangeDraft> DataExchangeDrafts => Set<DataExchangeDraft>();
    public DbSet<PromoteBatch> PromoteBatches => Set<PromoteBatch>();

    // Materialized / operational
    public DbSet<DocumentSnapshot> DocumentSnapshots => Set<DocumentSnapshot>();
    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();
    public DbSet<ImportStagingRow> ImportStagingRows => Set<ImportStagingRow>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    // Infrastructure-only entity — kept off IDipDbContext.
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Rename Identity tables so they don't clash with the domain naming convention.
        builder.Entity<ApplicationUser>().ToTable("Users");
        builder.Entity<ApplicationRole>().ToTable("Roles");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserRole<Guid>>().ToTable("UserRoles");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserClaim<Guid>>().ToTable("UserClaims");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityRoleClaim<Guid>>().ToTable("RoleClaims");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserLogin<Guid>>().ToTable("UserLogins");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserToken<Guid>>().ToTable("UserTokens");

        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
