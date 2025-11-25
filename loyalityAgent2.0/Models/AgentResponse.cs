namespace loyalityAgent2._0.Models
{
    public class AgentResponse
    {
        public BusinessAttributes? BusinessAttributes { get; set; }
        public LoyaltyOffer? LoyaltyOffer { get; set; }
        public LoyaltyTierAnalysis? LoyaltyTierAnalysis { get; set; }
        
        // Explicit 4 Tiers (ALWAYS filled)
        public WelcomeGiftResponse WelcomeGift { get; set; } = new();
        public LoyaltyTierReward BronzeTier { get; set; } = new();
        public LoyaltyTierReward SilverTier { get; set; } = new();
        public LoyaltyTierReward GoldTier { get; set; } = new();
        
        // Metadata
        public string DataSource { get; set; } = string.Empty; // "Geoapify", "Similar", "Category"
        public bool IsRealData { get; set; }
        public string? SimilarBusinessName { get; set; }
        
        public string WorkflowPath { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class GeminiSearchResult
    {
        public string Content { get; set; } = string.Empty;
        public List<string> Sources { get; set; } = new();
    }
}
