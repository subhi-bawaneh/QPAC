using Dip.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Dip.Application.Behaviors;

// Applied only to commands. Saves changes on success; rolls back implicitly on
// exception (EF Core discards tracked changes when the scope disposes). A real
// TransactionScope isn't used here because SaveChangesAsync is already transactional
// and most command handlers touch a single unit of work.
public sealed class TransactionBehavior<TRequest, TResult> : IPipelineBehavior<TRequest, TResult>
    where TRequest : ICommand<TResult>
{
    private readonly IDipDbContext _db;
    private readonly ILogger<TransactionBehavior<TRequest, TResult>> _logger;

    public TransactionBehavior(IDipDbContext db, ILogger<TransactionBehavior<TRequest, TResult>> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<TResult> Handle(TRequest request, HandlerDelegate<TResult> next, CancellationToken ct)
    {
        var result = await next();
        var affected = await _db.SaveChangesAsync(ct);
        if (affected > 0)
        {
            _logger.LogDebug("Persisted {Count} change(s) for {Request}", affected, typeof(TRequest).Name);
        }
        return result;
    }
}
