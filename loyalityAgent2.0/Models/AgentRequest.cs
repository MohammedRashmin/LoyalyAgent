namespace loyalityAgent2._0.Models
{
    public class AgentRequest
    {
        public int UserId { get; set; } // User ID to get API key
        public string BusinessName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string FullAddress { get; set; } = string.Empty;
        public decimal MinimumSpent { get; set; } = 0; // Minimum spending amount in GBP (£)
        public string? ConnectionId { get; set; }
    }
}
