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
        
        // New methods for optimized workflow
        Task<ProductAnalysisResult> ExtractMenuWithGeoapifyDataAsync(PlaceDetails? placeDetails, string businessName, string category, string address);
        Task<ProductAnalysisResult> GenerateUniqueFromSimilarBusinessAsync(string businessName, string category, string address, CompleteBusinessData similarBusiness, decimal minimumSpent);
        Task<ProductAnalysisResult> GenerateCategoryBasedProductsAsync(string businessName, string category, string address, decimal minimumSpent);
        Task<WelcomeGift> GenerateWelcomeGiftAsync(ProductAnalysisResult productAnalysis, ServiceAnalysisResult? serviceAnalysis, BusinessAttributes businessAttributes, decimal minimumSpent);
        Task<LoyaltyTierAnalysis> GenerateAllTiersCombinedAsync(BusinessAttributes businessAttributes, ProductAnalysisResult? productAnalysis, ServiceAnalysisResult? serviceAnalysis, decimal minimumSpendForToken);
    }
}
