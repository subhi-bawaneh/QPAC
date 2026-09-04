namespace Dip.Application.Abstractions;

public delegate Task<TResult> HandlerDelegate<TResult>();

// A pipeline behavior wraps a request; it can short-circuit or delegate to next().
public interface IPipelineBehavior<in TRequest, TResult>
{
    Task<TResult> Handle(TRequest request, HandlerDelegate<TResult> next, CancellationToken ct);
}
