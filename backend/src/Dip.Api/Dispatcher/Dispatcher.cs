using Dip.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Dip.Api.Dispatcher;

// Reflection-based dispatcher: resolves the concrete handler for TRequest/TResult,
// wraps it in the registered pipeline behaviors, and invokes the chain.
// Command pipeline: Logging -> Authorization -> Validation -> Transaction -> Handler.
// Query pipeline:   Logging -> Authorization -> Validation -> Handler.
public sealed class Dispatcher : IDispatcher
{
    private readonly IServiceProvider _services;

    public Dispatcher(IServiceProvider services)
    {
        _services = services;
    }

    public Task<TResult> Send<TResult>(ICommand<TResult> command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var requestType = command.GetType();
        var handlerType = typeof(ICommandHandler<,>).MakeGenericType(requestType, typeof(TResult));
        var behaviorType = typeof(IPipelineBehavior<,>).MakeGenericType(requestType, typeof(TResult));
        return InvokePipeline<TResult>(command, requestType, handlerType, behaviorType, isCommand: true, ct);
    }

    public Task<TResult> Query<TResult>(IQuery<TResult> query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var requestType = query.GetType();
        var handlerType = typeof(IQueryHandler<,>).MakeGenericType(requestType, typeof(TResult));
        var behaviorType = typeof(IPipelineBehavior<,>).MakeGenericType(requestType, typeof(TResult));
        return InvokePipeline<TResult>(query, requestType, handlerType, behaviorType, isCommand: false, ct);
    }

    private Task<TResult> InvokePipeline<TResult>(
        object request,
        Type requestType,
        Type handlerType,
        Type behaviorType,
        bool isCommand,
        CancellationToken ct)
    {
        var handler = _services.GetService(handlerType)
            ?? throw new InvalidOperationException($"No handler registered for {requestType.Name}");

        // Terminal delegate calls the actual handler.
        HandlerDelegate<TResult> terminal = () =>
        {
            var method = handlerType.GetMethod("Handle")
                ?? throw new InvalidOperationException($"{handlerType.Name} has no Handle method");
            var task = (Task<TResult>?)method.Invoke(handler, new[] { request, (object)ct })
                ?? throw new InvalidOperationException($"{handlerType.Name}.Handle returned null");
            return task;
        };

        // Compose behaviors in reverse so the first registered runs first.
        var behaviors = _services.GetServices(behaviorType).OfType<object>().ToArray();
        HandlerDelegate<TResult> next = terminal;
        for (var i = behaviors.Length - 1; i >= 0; i--)
        {
            var behavior = behaviors[i];
            var captured = next;
            next = () =>
            {
                var method = behaviorType.GetMethod("Handle")
                    ?? throw new InvalidOperationException($"{behaviorType.Name} has no Handle method");
                var task = (Task<TResult>?)method.Invoke(behavior, new[] { request, captured, (object)ct })
                    ?? throw new InvalidOperationException($"{behavior.GetType().Name}.Handle returned null");
                return task;
            };
        }

        _ = isCommand;
        return next();
    }
}
