using Dip.Application.Abstractions;

namespace Dip.Api.Features.Auth.Logout;

public sealed record LogoutCommand(string RefreshToken, string RequestIp) : ICommand<Unit>;
