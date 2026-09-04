namespace Dip.Application.Abstractions;

// Placeholder abstraction so Application handlers can depend on the DbContext
// without referencing EF Core directly. DbSet<T> members are added in Phase 1.2
// once the domain entities exist.
public interface IDipDbContext
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
