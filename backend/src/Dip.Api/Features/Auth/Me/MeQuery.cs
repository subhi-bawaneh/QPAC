using Dip.Application.Abstractions;

namespace Dip.Api.Features.Auth.Me;

public sealed record MeQuery : IQuery<UserSummary>;
