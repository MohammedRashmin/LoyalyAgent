using loyalityAgent2._0.Models;

namespace loyalityAgent2._0.Services
{
    public interface IGeminiService
    {
        void SetApiKey(string apiKey);
        Task<BusinessAttributes> ExtractBusinessAttributesAsync(string businessName, string category, string fullAddress);
        Task<ProductAnalysisResult> AnalyzeProductsAsync(BusinessAttributes businessAttributes, decimal minimumSpent);
        Task<ServiceAnalysisResult> AnalyzeServicesAsync(BusinessAttributes businessAttributes, decimal minimumSpent);
        Task<ReasoningResult> ReasonProductOfferAsync(BusinessAttributes businessAttributes, string product, decimal productPrice, decimal minimumSpent);
        Task<ReasoningResult> ReasonServiceDiscountAsync(BusinessAttributes businessAttributes, string service, decimal servicePrice, decimal minimumSpent);
        Task<LoyaltyTierAnalysis> AnalyzeLoyaltyTiersAsync(BusinessAttributes businessAttributes, ProductAnalysisResult? productAnalysis, ServiceAnalysisResult? serviceAnalysis, decimal minimumSpendForToken);
        Task<string> GeneratePromptAsync(string prompt);
        Task<string> SearchWebsiteUrlAsync(string businessName, string address);
        
        // New methods for optimized workflow
        Task<ProductAnalysisResult> ExtractMenuWithGeoapifyDataAsync(PlaceDetails? placeDetails, string businessName, string category, string address);
        Task<ProductAnalysisResult> GenerateUniqueFromSimilarBusinessAsync(string businessName, string category, string address, CompleteBusinessData similarBusiness, decimal minimumSpent);
        Task<ProductAnalysisResult> GenerateCategoryBasedProductsAsync(string businessName, string category, string address, decimal minimumSpent);
        Task<WelcomeGiftResponse> GenerateWelcomeGiftAsync(ProductAnalysisResult productAnalysis, ServiceAnalysisResult? serviceAnalysis, BusinessAttributes businessAttributes, decimal minimumSpent);
        Task<LoyaltyTierAnalysis> GenerateAllTiersCombinedAsync(BusinessAttributes businessAttributes, ProductAnalysisResult? productAnalysis, ServiceAnalysisResult? serviceAnalysis, decimal minimumSpendForToken);
        
        // Web search methods
        Task<WebSearchResult> SearchBusinessOnWebPlatformsAsync(string businessName, string location);
        Task<BusinessAttributes> ExtractBusinessAttributesFromWebSearchAsync(WebSearchResult webSearchResult, string businessName, string category, string fullAddress);
        Task<ProductAnalysisResult> ExtractProductsFromWebSearchAsync(WebSearchResult webSearchResult, string businessName, string category, string fullAddress);
        Task<LoyaltyTierAnalysis> GenerateDiscountOnlyTiersAsync(BusinessAttributes businessAttributes, ProductAnalysisResult? productAnalysis, ServiceAnalysisResult? serviceAnalysis, decimal minimumSpendForToken);
    }
}
