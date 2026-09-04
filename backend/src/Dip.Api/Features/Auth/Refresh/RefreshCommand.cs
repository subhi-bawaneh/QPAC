using Dip.Application.Abstractions;

namespace Dip.Api.Features.Auth.Refresh;

public sealed record RefreshCommand(string RefreshToken, string RequestIp) : ICommand<AuthResult>;
