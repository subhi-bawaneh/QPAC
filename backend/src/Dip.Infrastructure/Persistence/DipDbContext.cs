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
    public DipDbContext(DbContextOptions<DipDbContext> options) : base(options) { }

    // Live entities
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Discipline> Disciplines => Set<Discipline>();
    public DbSet<PicklistItem> PicklistItems => Set<PicklistItem>();
    public DbSet<StatusMapping> StatusMappings => Set<StatusMapping>();
    public DbSet<DocumentTypeSerial> DocumentTypeSerials => Set<DocumentTypeSerial>();
    public DbSet<BaselineActivity> BaselineActivities => Set<BaselineActivity>();
    public DbSet<TidpFile> TidpFiles => Set<TidpFile>();
    public DbSet<TidpFolderOwner> TidpFolderOwners => Set<TidpFolderOwner>();
    public DbSet<TidpFolderDiscipline> TidpFolderDisciplines => Set<TidpFolderDiscipline>();
    public DbSet<TidpFolderSync> TidpFolderSyncs => Set<TidpFolderSync>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DataExchange> DataExchanges => Set<DataExchange>();
    public DbSet<AconexRevision> AconexRevisions => Set<AconexRevision>();

    // Materialized / operational
    public DbSet<DocumentSnapshot> DocumentSnapshots => Set<DocumentSnapshot>();
    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();
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

        // Local development can run the whole system on a SQLite file instead of SQL
        // Server. EF keys its model cache by provider, so this branch never touches the
        // SQL Server model. See SqliteModelTweaks and docs/local-dev.md.
        if (Database.IsSqlite())
        {
            SqliteModelTweaks.Apply(builder);
        }
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        // Force every DateTime and DateTime? property to Kind=Unspecified so the naive
        // timestamps required by PLAN.md § 1 don't drift with the CLR Kind of the value
        // handed in. See DateTimeUnspecifiedConverter.
        configurationBuilder.Properties<DateTime>().HaveConversion<DateTimeUnspecifiedConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<NullableDateTimeUnspecifiedConverter>();
    }
}
