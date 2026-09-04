namespace Dip.Application.Abstractions;

public interface ICurrentUser
{
    Guid? UserId { get; }
    string? UserName { get; }
    bool IsAuthenticated { get; }
    IReadOnlyCollection<string> Permissions { get; }
    IReadOnlyCollection<string> DisciplineCodes { get; }
    bool Has(string permission);
}
