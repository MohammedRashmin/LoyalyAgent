namespace loyalityAgent2._0.Models
{
    public class BusinessAttributes
    {
        public string BusinessModel { get; set; } = string.Empty;
        public string CoreProductsOrServices { get; set; } = string.Empty;
        public string TargetAudience { get; set; } = string.Empty;
        public string BusinessToneOrStyle { get; set; } = string.Empty;
        public string PopularityOrSize { get; set; } = string.Empty;
        public string SpecializationKeywords { get; set; } = string.Empty;
        public BusinessType BusinessType { get; set; }
    }

    public enum BusinessType
    {
        Product,
        Service,
        Hybrid
    }

    public class LoyaltyOffer
    {
        public OfferType Type { get; set; }
        public string Description { get; set; } = string.Empty;
        public string ProductOrServiceName { get; set; } = string.Empty;
        public decimal? DiscountPercentage { get; set; }
        public bool IsFreeToken { get; set; }
        public string Reasoning { get; set; } = string.Empty;
        public decimal MinimumSpendGBP { get; set; } // Minimum spend required for this offer
        public decimal FreeItemValueGBP { get; set; } // Value of the free item
    }

    public enum OfferType
    {
        FreeProduct,
        DiscountedService,
        FreeToken,
        PercentageDiscount
    }

    public class ProductAnalysisResult
    {
        public List<ProductItem> AllProducts { get; set; } = new();
        public List<string> PopularProducts { get; set; } = new();
        public string? SelectedFreeProduct { get; set; }
        public decimal SelectedFreeProductPrice { get; set; }
        public List<ProductItem> ProductsMeetingThreshold { get; set; } = new();
    }

    public class ServiceAnalysisResult
    {
        public List<ServiceItem> AllServices { get; set; } = new();
        public List<string> PopularServices { get; set; } = new();
        public string? SelectedDiscountableService { get; set; }
        public decimal SelectedServicePrice { get; set; }
        public List<ServiceItem> ServicesMeetingThreshold { get; set; } = new();
    }

    public class ProductItem
    {
        public string Name { get; set; } = string.Empty;
        public decimal PriceGBP { get; set; }
    }

    public class ServiceItem
    {
        public string Name { get; set; } = string.Empty;
        public decimal PriceGBP { get; set; }
    }

    public class ReasoningResult
    {
        public bool IsApproved { get; set; }
        public string Reasoning { get; set; } = string.Empty;
        public decimal? SuggestedPercentage { get; set; }
        public string FallbackOption { get; set; } = string.Empty;
    }

    // Loyalty Tier Models
    public class LoyaltyTierReward
    {
        public LoyaltyTier Tier { get; set; }
        public int RequiredTokens { get; set; }
        public List<TierRewardOption> RewardOptions { get; set; } = new();
        public TierFallbackDiscount? FallbackDiscount { get; set; }
        public string Reasoning { get; set; } = string.Empty;
    }

    public enum LoyaltyTier
    {
        Bronze,
        Silver,
        Gold
    }

    public class TierRewardOption
    {
        public string ItemName { get; set; } = string.Empty;
        public decimal ItemValueGBP { get; set; }
        public bool CanBeGivenFree { get; set; }
        public string ReasoningForSelection { get; set; } = string.Empty;
    }

    public class TierFallbackDiscount
    {
        public decimal DiscountPercentage { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Reasoning { get; set; } = string.Empty;
    }

    public class LoyaltyTierAnalysis
    {
        public decimal MinimumSpendForToken { get; set; }
        public List<LoyaltyTierReward> TierRewards { get; set; } = new();
        public bool HasFreeItemOptions { get; set; }
        public string OverallStrategy { get; set; } = string.Empty;
    }

    // Geoapify Models
    public class PlaceDetails
    {
        public string? PlaceId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string FormattedAddress { get; set; } = string.Empty;
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? Website { get; set; }
        public string? PhoneNumber { get; set; }
        public List<string> Categories { get; set; } = new();
        public int? PriceLevel { get; set; }
        public double? Rating { get; set; }
    }

    // Response Models (non-database)
    public class WelcomeGiftResponse
    {
        public string ItemName { get; set; } = string.Empty;
        public decimal ItemPriceGBP { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Reasoning { get; set; } = string.Empty;
        public bool IsFree { get; set; } = true;
        public string DataSource { get; set; } = string.Empty;
    }
}
