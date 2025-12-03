using System.Text;

namespace loyalityAgent2._0.Services
{
    public class ApiCallLoggingHandler : DelegatingHandler
    {
        private readonly IApiCallLoggerService _apiCallLogger;
        private readonly IConnectionContextService _connectionContext;
        private readonly ILogger<ApiCallLoggingHandler> _logger;

        public ApiCallLoggingHandler(
            IApiCallLoggerService apiCallLogger,
            IConnectionContextService connectionContext,
            ILogger<ApiCallLoggingHandler> logger)
        {
            _apiCallLogger = apiCallLogger;
            _connectionContext = connectionContext;
            _logger = logger;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var startTime = DateTime.UtcNow;
            string? requestBody = null;
            string? responseBody = null;
            int? statusCode = null;

            // Capture request body if present
            if (request.Content != null)
            {
                try
                {
                    requestBody = await request.Content.ReadAsStringAsync(cancellationToken);
                    // Recreate content stream for the actual request
                    request.Content = new StringContent(requestBody, Encoding.UTF8, request.Content.Headers.ContentType?.MediaType ?? "application/json");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to read request body");
                }
            }

            // Execute the request
            var response = await base.SendAsync(request, cancellationToken);
            statusCode = (int)response.StatusCode;

            // Capture response body
            if (response.Content != null)
            {
                try
                {
                    responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    // Recreate content stream for the actual response (preserve headers)
                    var contentType = response.Content.Headers.ContentType;
                    response.Content = new StringContent(responseBody, Encoding.UTF8, contentType?.MediaType ?? "application/json");
                    // Copy other headers
                    foreach (var header in response.Content.Headers)
                    {
                        if (header.Key != "Content-Type" && header.Key != "Content-Length")
                        {
                            response.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to read response body");
                }
            }

            var duration = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;

            // Log the API call
            var endpoint = request.RequestUri?.ToString() ?? "Unknown";
            var method = request.Method.Method;

            // Truncate large bodies for display
            if (requestBody != null && requestBody.Length > 5000)
            {
                requestBody = requestBody.Substring(0, 5000) + "... [truncated]";
            }

            if (responseBody != null && responseBody.Length > 10000)
            {
                responseBody = responseBody.Substring(0, 10000) + "... [truncated]";
            }

            // Try to get connection ID from multiple sources
            var connectionId = _connectionContext.GetConnectionId();
            
            // Also check request properties (set by services if needed)
            if (string.IsNullOrEmpty(connectionId) && request.Options.TryGetValue(new HttpRequestOptionsKey<string>("ConnectionId"), out var requestConnectionId))
            {
                connectionId = requestConnectionId;
            }
            
            // Log for debugging
            _logger.LogInformation("API Call intercepted: {Method} {Endpoint}, ConnectionId: {ConnectionId}, Status: {StatusCode}", 
                method, endpoint, connectionId ?? "NULL", statusCode);
            
            // Always log, even if connectionId is null (for debugging)
            if (string.IsNullOrEmpty(connectionId))
            {
                _logger.LogWarning("API call has no connection ID - cannot send to UI. Endpoint: {Endpoint}", endpoint);
            }
            
            await _apiCallLogger.LogApiCallAsync(connectionId, endpoint, method, requestBody, responseBody, statusCode, duration);

            return response;
        }
    }
}

