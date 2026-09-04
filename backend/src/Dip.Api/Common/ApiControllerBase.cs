using Dip.Application.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Common;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    private IDispatcher? _dispatcher;

    protected IDispatcher Dispatcher =>
        _dispatcher ??= HttpContext.RequestServices.GetService(typeof(IDispatcher)) as IDispatcher
        ?? throw new InvalidOperationException("IDispatcher is not registered");
}
