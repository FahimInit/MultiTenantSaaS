using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using SaaS.Application.Common.Abstractions;

namespace SaaS.API.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/system")]
public sealed class SystemController : ControllerBase
{
    private readonly IHostEnvironment _environment;
    private readonly IDateTimeProvider _dateTimeProvider;

    public SystemController(IHostEnvironment environment, IDateTimeProvider dateTimeProvider)
    {
        _environment = environment;
        _dateTimeProvider = dateTimeProvider;
    }

    [HttpGet("info")]
    [ProducesResponseType(typeof(SystemInfoResponse), StatusCodes.Status200OK)]
    public IActionResult GetInfo()
    {
        return Ok(new SystemInfoResponse(
            Service: "Multi-Tenant SaaS Management Platform API",
            Version: "v1",
            Environment: _environment.EnvironmentName,
            UtcNow: _dateTimeProvider.UtcNow));
    }
}

public sealed record SystemInfoResponse(
    string Service,
    string Version,
    string Environment,
    DateTimeOffset UtcNow);
