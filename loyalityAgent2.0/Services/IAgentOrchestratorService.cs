using loyalityAgent2._0.Models;

namespace loyalityAgent2._0.Services
{
    public interface IAgentOrchestratorService
    {
        Task<AgentResponse> ProcessBusinessLoyaltyAsync(string userApiKey, string businessName, string category, string fullAddress, decimal minimumSpent, string? connectionId = null);
    }
}
