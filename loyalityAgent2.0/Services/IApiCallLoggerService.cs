namespace loyalityAgent2._0.Services
{
    public interface IApiCallLoggerService
    {
        Task LogApiCallAsync(string? connectionId, string endpoint, string method, string? requestBody, string? responseBody, int? statusCode, long? durationMs);
    }
}



