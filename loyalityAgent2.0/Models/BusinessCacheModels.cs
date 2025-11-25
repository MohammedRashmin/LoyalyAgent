using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace loyalityAgent2._0.Models
{
    // Database model for caching business data
    public class Business
    {
        [Key]
        public int BusinessId { get; set; }

        // Search identifiers
        [Required]
        [StringLength(255)]
        public string BusinessName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Category { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        public string FullAddress { get; set; } = string.Empty;

        [StringLength(100)]
        public string City { get; set; } = string.Empty;

        [StringLength(100)]
        public string Country { get; set; } = string.Empty;

        [StringLength(64)]
        public string AddressHash { get; set; } = string.Empty; // For fast lookup

        // Business attributes
        [StringLength(200)]
        public string BusinessModel { get; set; } = string.Empty;

        [Column(TypeName = "nvarchar(max)")]
        public string CoreProductsOrServices { get; set; } = string.Empty;

        [StringLength(200)]
        public string TargetAudience { get; set; } = string.Empty;

        [StringLength(100)]
        public string BusinessToneOrStyle { get; set; } = string.Empty;

        [StringLength(100)]
        public string PopularityOrSize { get; set; } = string.Empty;

        [StringLength(200)]
        public string SpecializationKeywords { get; set; } = string.Empty;

        public BusinessType BusinessType { get; set; }

        // Geoapify data
        [StringLength(100)]
        public string? GeoapifyPlaceId { get; set; }

        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        [StringLength(500)]
        public string? Website { get; set; }

        [StringLength(50)]
        public string? PhoneNumber { get; set; }

        // Data source tracking
        [StringLength(50)]
        public string DataSource { get; set; } = "Unknown"; // "Geoapify", "Similar", "Category"

        [StringLength(255)]
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
        [Key]
        public int ProductId { get; set; }

        [Required]
        public int BusinessId { get; set; }

        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [Column(TypeName = "decimal(10,2)")]
        public decimal PriceGBP { get; set; }

        public bool IsPopular { get; set; }
        public bool IsWelcomeGiftEligible { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        [ForeignKey("BusinessId")]
        public Business Business { get; set; } = null!;
    }

    public class Service
    {
        [Key]
        public int ServiceId { get; set; }

        [Required]
        public int BusinessId { get; set; }

        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [Column(TypeName = "decimal(10,2)")]
        public decimal PriceGBP { get; set; }

        public bool IsPopular { get; set; }
        public bool IsDiscountable { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        [ForeignKey("BusinessId")]
        public Business Business { get; set; } = null!;
    }

    public class WelcomeGift
    {
        [Key]
        public int WelcomeGiftId { get; set; }

        [Required]
        public int BusinessId { get; set; }

        [Required]
        [StringLength(200)]
        public string ItemName { get; set; } = string.Empty;

        [Column(TypeName = "decimal(10,2)")]
        public decimal ItemPriceGBP { get; set; }

        [Column(TypeName = "nvarchar(max)")]
        public string Description { get; set; } = string.Empty;

        [Column(TypeName = "nvarchar(max)")]
        public string Reasoning { get; set; } = string.Empty;

        public bool IsFree { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        [ForeignKey("BusinessId")]
        public Business Business { get; set; } = null!;
    }

    public class TierReward
    {
        [Key]
        public int TierRewardId { get; set; }

        [Required]
        public int BusinessId { get; set; }

        public LoyaltyTier Tier { get; set; }
        public int RequiredTokens { get; set; }

        [Column(TypeName = "nvarchar(max)")]
        public string Reasoning { get; set; } = string.Empty;

        // Fallback discount (if no free items)
        [Column(TypeName = "decimal(5,2)")]
        public decimal? FallbackDiscountPercentage { get; set; }

        [StringLength(500)]
        public string? FallbackDescription { get; set; }

        [Column(TypeName = "nvarchar(max)")]
        public string? FallbackReasoning { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        [ForeignKey("BusinessId")]
        public Business Business { get; set; } = null!;
        public List<TierRewardItem> RewardItems { get; set; } = new();
    }

    public class TierRewardItem
    {
        [Key]
        public int TierRewardItemId { get; set; }

        [Required]
        public int TierRewardId { get; set; }

        [Required]
        [StringLength(200)]
        public string ItemName { get; set; } = string.Empty;

        [Column(TypeName = "decimal(10,2)")]
        public decimal ItemValueGBP { get; set; }

        public bool CanBeGivenFree { get; set; }

        [Column(TypeName = "nvarchar(max)")]
        public string ReasoningForSelection { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        [ForeignKey("TierRewardId")]
        public TierReward TierReward { get; set; } = null!;
    }
}

