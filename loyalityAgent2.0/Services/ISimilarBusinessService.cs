using loyalityAgent2._0.Models;

namespace loyalityAgent2._0.Services
{
    public interface ISimilarBusinessService
    {
        Task<Business?> FindSimilarBusinessAsync(string category, string address, int maxResults = 3);
        Task<CompleteBusinessData> GetCompleteBusinessDataAsync(int businessId);
        Task<Business?> GetBusinessByAddressAsync(string businessName, string address);
        Task<Business> SaveBusinessDataAsync(string businessName, string category, string fullAddress, AgentResponse response, ProductAnalysisResult productAnalysis, ServiceAnalysisResult? serviceAnalysis, PlaceDetails? placeDetails, Business? similarBusiness);
    }

    public class CompleteBusinessData
    {
        public Business Business { get; set; } = null!;
        public List<Product> Products { get; set; } = new();
        public List<Service> Services { get; set; } = new();
        public WelcomeGift? WelcomeGift { get; set; }
        public List<TierReward> TierRewards { get; set; } = new();
    }
}
