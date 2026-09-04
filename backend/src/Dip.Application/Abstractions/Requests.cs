namespace Dip.Application.Abstractions;

// Marker interfaces for CQRS requests.
// A command mutates state and returns TResult (use Unit for void).
// A query is read-only and returns TResult.
public interface ICommand<TResult> { }

public interface IQuery<TResult> { }

// Empty value type — same purpose as MediatR.Unit.
public readonly record struct Unit
{
    public static readonly Unit Value = default;
}
