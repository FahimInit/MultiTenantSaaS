using Microsoft.AspNetCore.Http;
using SaaS.Application.Common.Abstractions;

namespace SaaS.Infrastructure.Identity;

public sealed class HttpRequestContextInfo : IRequestContextInfo
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpRequestContextInfo(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string IpAddress =>
        _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    public string UserAgent =>
        _httpContextAccessor.HttpContext?.Request.Headers.UserAgent.FirstOrDefault() ?? "unknown";

    public string CorrelationId =>
        _httpContextAccessor.HttpContext?.TraceIdentifier ?? string.Empty;
}
