namespace Dip.Application.Authorization;

// Applied to a Command or Query type to require a permission for its execution.
// The AuthorizationBehavior in the dispatcher pipeline reads it via reflection.
// Multiple attributes = user must have ALL listed permissions.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
public sealed class PermissionAttribute : Attribute
{
    public string Permission { get; }
    public PermissionAttribute(string permission) => Permission = permission;
}

// Marks a request as requiring the current user's Editor discipline claim
// to match a property on the command. Used together with PermissionAttribute.
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class DisciplineScopeAttribute : Attribute
{
}
