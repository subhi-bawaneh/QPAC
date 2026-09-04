using System.Diagnostics;
using Dip.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Dip.Application.Behaviors;

public sealed class LoggingBehavior<TRequest, TResult> : IPipelineBehavior<TRequest, TResult>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResult>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResult>> logger)
    {
        _logger = logger;
    }

    public async Task<TResult> Handle(TRequest request, HandlerDelegate<TResult> next, CancellationToken ct)
    {
        var name = typeof(TRequest).Name;
        var sw = Stopwatch.StartNew();
        _logger.LogInformation("Handling {Request}", name);
        try
        {
            var result = await next();
            sw.Stop();
            _logger.LogInformation("Handled {Request} in {Elapsed}ms", name, sw.ElapsedMilliseconds);
            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Failed {Request} after {Elapsed}ms", name, sw.ElapsedMilliseconds);
            throw;
        }
    }
}
