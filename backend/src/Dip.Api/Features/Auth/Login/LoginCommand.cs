using Dip.Application.Abstractions;

namespace Dip.Api.Features.Auth.Login;

public sealed record LoginCommand(string Email, string Password, string RequestIp) : ICommand<AuthResult>;
