using Microsoft.AspNetCore.SignalR;
using loyalityAgent2._0.Hubs;

namespace loyalityAgent2._0.Services
{
    public class ApiCallLoggerService : IApiCallLoggerService
    {
        private readonly IHubContext<WorkflowHub> _hubContext;
        private readonly ILogger<ApiCallLoggerService> _logger;

        public ApiCallLoggerService(
            IHubContext<WorkflowHub> hubContext,
            ILogger<ApiCallLoggerService> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task LogApiCallAsync(string? connectionId, string endpoint, string method, string? requestBody, string? responseBody, int? statusCode, long? durationMs)
        {
            if (string.IsNullOrEmpty(connectionId))
            {
                _logger.LogDebug("API call not logged - no connection ID: {Method} {Endpoint}", method, endpoint);
                return; // No connection to send to
            }

            try
            {
                var apiCallData = new
                {
                    endpoint = endpoint,
                    method = method,
                    requestBody = requestBody,
                    responseBody = responseBody,
                    statusCode = statusCode,
                    durationMs = durationMs,
                    timestamp = DateTime.UtcNow
                };

                _logger.LogInformation("Sending API call to client {ConnectionId}: {Method} {Endpoint}", connectionId, method, endpoint);
                await _hubContext.Clients.Client(connectionId).SendAsync("ReceiveApiCall", apiCallData);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send API call log to client {ConnectionId}", connectionId);
            }
        }
    }
}

