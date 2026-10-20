using Trail.Application.Common.Abstractions;

namespace Trail.Api.Infrastructure;

internal sealed class RequestContext(IHttpContextAccessor accessor) : IRequestContext
{
    public string? IpAddress => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent
    {
        get
        {
            var agent = accessor.HttpContext?.Request.Headers.UserAgent.ToString();
            return string.IsNullOrEmpty(agent) ? null : agent.Length > 512 ? agent[..512] : agent;
        }
    }
}
