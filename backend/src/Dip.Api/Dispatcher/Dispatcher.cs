using System.Reflection;
using System.Runtime.ExceptionServices;
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
        var handleMethod = handlerType.GetMethod("Handle")
            ?? throw new InvalidOperationException($"{handlerType.Name} has no Handle method");
        HandlerDelegate<TResult> terminal = () => InvokeAsync<TResult>(handleMethod, handler, new[] { request, (object)ct });

        // Compose behaviors in reverse so the first registered runs first.
        var behaviors = _services.GetServices(behaviorType).OfType<object>().ToArray();
        var behaviorHandleMethod = behaviorType.GetMethod("Handle")
            ?? throw new InvalidOperationException($"{behaviorType.Name} has no Handle method");

        HandlerDelegate<TResult> next = terminal;
        for (var i = behaviors.Length - 1; i >= 0; i--)
        {
            var behavior = behaviors[i];
            var captured = next;
            next = () => InvokeAsync<TResult>(behaviorHandleMethod, behavior, new[] { request, captured, (object)ct });
        }

        _ = isCommand;
        return next();
    }

    // Reflection.Invoke wraps thrown exceptions in TargetInvocationException.
    // Unwrap it (preserving the original stack) so downstream middleware
    // (ProblemDetailsExceptionHandler, logging) sees the actual domain type.
    private static async Task<TResult> InvokeAsync<TResult>(MethodInfo method, object target, object[] args)
    {
        try
        {
            var task = (Task<TResult>?)method.Invoke(target, args)
                ?? throw new InvalidOperationException($"{target.GetType().Name}.{method.Name} returned null");
            return await task;
        }
        catch (TargetInvocationException tex) when (tex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(tex.InnerException).Throw();
            throw; // unreachable, keeps the compiler happy
        }
    }
}
