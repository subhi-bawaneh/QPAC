using System.Reflection;
using Dip.Application.Abstractions;
using Dip.Application.Authorization;

namespace Dip.Application.Behaviors;

public sealed class UnauthorizedException : Exception
{
    public UnauthorizedException(string message) : base(message) { }
}

public sealed class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message) { }
}

// A write that would break a uniqueness rule the caller can resolve — a document
// number already taken, a picklist code already in use. Surfaces as 409.
public sealed class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
}

public sealed class AuthorizationBehavior<TRequest, TResult> : IPipelineBehavior<TRequest, TResult>
{
    private readonly ICurrentUser _currentUser;

    public AuthorizationBehavior(ICurrentUser currentUser)
    {
        _currentUser = currentUser;
    }

    public Task<TResult> Handle(TRequest request, HandlerDelegate<TResult> next, CancellationToken ct)
    {
        var type = typeof(TRequest);
        var attributes = type.GetCustomAttributes<PermissionAttribute>(inherit: false).ToArray();

        if (attributes.Length > 0)
        {
            if (!_currentUser.IsAuthenticated)
            {
                throw new UnauthorizedException($"Authentication required for {type.Name}");
            }

            foreach (var attribute in attributes)
            {
                if (!_currentUser.Has(attribute.Permission))
                {
                    throw new ForbiddenException(
                        $"Missing permission '{attribute.Permission}' for {type.Name}");
                }
            }

            // Enforce discipline scoping for Editor-like users.
            var scopedProperty = type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(p => p.GetCustomAttribute<DisciplineScopeAttribute>() is not null);

            if (scopedProperty is not null && _currentUser.DisciplineCodes.Count > 0)
            {
                var value = scopedProperty.GetValue(request)?.ToString();
                if (!string.IsNullOrEmpty(value)
                    && !_currentUser.DisciplineCodes.Contains(value, StringComparer.OrdinalIgnoreCase))
                {
                    throw new ForbiddenException(
                        $"Discipline '{value}' is not in the user's allowed disciplines");
                }
            }
        }

        return next();
    }
}
