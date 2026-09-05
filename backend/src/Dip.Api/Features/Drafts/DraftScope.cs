using Dip.Application.Abstractions;
using Dip.Application.Behaviors;

namespace Dip.Api.Features.Drafts;

// Editors carrying `discipline:` claims may only touch rows in those disciplines.
// The AuthorizationBehavior checks the discipline arriving on the command; this
// checks the one already stored on the row being edited.
internal static class DraftScope
{
    public static void EnsureCanEdit(ICurrentUser user, string disciplineCode)
    {
        if (user.DisciplineCodes.Count == 0 || string.IsNullOrEmpty(disciplineCode)) return;
        if (!user.DisciplineCodes.Contains(disciplineCode, StringComparer.OrdinalIgnoreCase))
        {
            throw new ForbiddenException(
                $"Discipline '{disciplineCode}' is not in the user's allowed disciplines");
        }
    }
}
