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

    // Database Models for Caching
    public class Business
    {
        public int BusinessId { get; set; }
        
        // Search identifiers
        public string BusinessName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string FullAddress { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string AddressHash { get; set; } = string.Empty; // For fast lookup
        
        // Business attributes
        public string BusinessModel { get; set; } = string.Empty;
        public string CoreProductsOrServices { get; set; } = string.Empty;
        public string TargetAudience { get; set; } = string.Empty;
        public string BusinessToneOrStyle { get; set; } = string.Empty;
        public string PopularityOrSize { get; set; } = string.Empty;
        public string SpecializationKeywords { get; set; } = string.Empty;
        public BusinessType BusinessType { get; set; }
        
        // Geoapify data
        public string? GeoapifyPlaceId { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? Website { get; set; }
        public string? PhoneNumber { get; set; }
        
        // Data source tracking
        public string DataSource { get; set; } = "Unknown"; // "Geoapify", "Similar", "Category"
        public string? SimilarBusinessName { get; set; }
        public bool IsRealData { get; set; }
        
        // Timestamps
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastSearchedAt { get; set; }
        public int SearchCount { get; set; } = 0;
        
        // Navigation properties
        public List<Product> Products { get; set; } = new();
        public List<Service> Services { get; set; } = new();
        public WelcomeGift? WelcomeGift { get; set; }
        public List<TierReward> TierRewards { get; set; } = new();
    }

    public class Product
    {
        public int ProductId { get; set; }
        public int BusinessId { get; set; }
        
        public string Name { get; set; } = string.Empty;
        public decimal PriceGBP { get; set; }
        public bool IsPopular { get; set; }
        public bool IsWelcomeGiftEligible { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        // Navigation
        public Business Business { get; set; } = null!;
    }

    public class Service
    {
        public int ServiceId { get; set; }
        public int BusinessId { get; set; }
        
        public string Name { get; set; } = string.Empty;
        public decimal PriceGBP { get; set; }
        public bool IsPopular { get; set; }
        public bool IsDiscountable { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        // Navigation
        public Business Business { get; set; } = null!;
    }

    public class WelcomeGift
    {
        public int WelcomeGiftId { get; set; }
        public int BusinessId { get; set; }
        
        public string ItemName { get; set; } = string.Empty;
        public decimal ItemPriceGBP { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Reasoning { get; set; } = string.Empty;
        public bool IsFree { get; set; } = true;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        // Navigation
        public Business Business { get; set; } = null!;
    }

    public class TierReward
    {
        public int TierRewardId { get; set; }
        public int BusinessId { get; set; }
        
        public LoyaltyTier Tier { get; set; }
        public int RequiredTokens { get; set; }
        public string Reasoning { get; set; } = string.Empty;
        
        // Fallback discount (if no free items)
        public decimal? FallbackDiscountPercentage { get; set; }
        public string? FallbackDescription { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        // Navigation
        public Business Business { get; set; } = null!;
        public List<TierRewardItem> RewardItems { get; set; } = new();
    }

    public class TierRewardItem
    {
        public int TierRewardItemId { get; set; }
        public int TierRewardId { get; set; }
        
        public string ItemName { get; set; } = string.Empty;
        public decimal ItemValueGBP { get; set; }
        public bool CanBeGivenFree { get; set; }
        public string ReasoningForSelection { get; set; } = string.Empty;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        // Navigation
        public TierReward TierReward { get; set; } = null!;
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

    // Response Models
    public class WelcomeGift
    {
        public string ItemName { get; set; } = string.Empty;
        public decimal ItemPriceGBP { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Reasoning { get; set; } = string.Empty;
        public bool IsFree { get; set; } = true;
        public string DataSource { get; set; } = string.Empty;
    }
}
