using System.Threading;
using Microsoft.AspNetCore.Http;

namespace loyalityAgent2._0.Services
{
    public class ConnectionContextService : IConnectionContextService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly AsyncLocal<string?> _connectionId = new AsyncLocal<string?>();

        public ConnectionContextService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public string? GetConnectionId()
        {
            // Try HttpContext first (for request-scoped)
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext != null && httpContext.Items.TryGetValue("ConnectionId", out var contextId))
            {
                return contextId as string;
            }
            
            // Fallback to AsyncLocal
            return _connectionId.Value;
        }

        public void SetConnectionId(string? connectionId)
        {
            // Store in HttpContext if available
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext != null)
            {
                httpContext.Items["ConnectionId"] = connectionId;
            }
            
            // Also store in AsyncLocal as fallback
            _connectionId.Value = connectionId;
        }
    }
}

