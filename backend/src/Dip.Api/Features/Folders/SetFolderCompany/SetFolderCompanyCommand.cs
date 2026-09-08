using Dip.Application.Abstractions;
using Dip.Application.Authorization;

namespace Dip.Api.Features.Folders.SetFolderCompany;

// A company folder holds one delivery partner's work; the author is the picklist
// item the Corporate Summary's author breakdown is named after. Not every folder is
// a company (refactor-plan § 3 R6).
[Permission(Permissions.FoldersAssignTarget)]
public sealed record SetFolderCompanyCommand(Guid Id, bool IsCompany, Guid? AuthorId) : ICommand<FolderNode>;
