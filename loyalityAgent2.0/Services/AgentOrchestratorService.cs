using loyalityAgent2._0.Models;
using Microsoft.AspNetCore.SignalR;
using loyalityAgent2._0.Hubs;

namespace loyalityAgent2._0.Services
{
    public class AgentOrchestratorService : IAgentOrchestratorService
    {
        private readonly IGeminiService _geminiService;
        private readonly IGeoapifyService _geoapifyService;
        private readonly ISimilarBusinessService _similarBusinessService;
        private readonly ILogger<AgentOrchestratorService> _logger;
        private readonly IHubContext<WorkflowHub> _hubContext;

        public AgentOrchestratorService(
            IGeminiService geminiService,
            IGeoapifyService geoapifyService,
            ISimilarBusinessService similarBusinessService,
            ILogger<AgentOrchestratorService> logger,
            IHubContext<WorkflowHub> hubContext)
        {
            _geminiService = geminiService;
            _geoapifyService = geoapifyService;
            _similarBusinessService = similarBusinessService;
            _logger = logger;
            _hubContext = hubContext;
        }

        public async Task<AgentResponse> ProcessBusinessLoyaltyAsync(string userApiKey, string businessName, string category, string fullAddress, decimal minimumSpent, string? connectionId = null)
        {
            try
            {
                _geminiService.SetApiKey(userApiKey);

                _logger.LogInformation("Starting loyalty agent workflow for business: {BusinessName}, Min Spend: £{MinSpent}", businessName, minimumSpent);
                await BroadcastProgressAsync(connectionId, "START", $"Starting analysis for {businessName} (Min spend: £{minimumSpent})");

                PlaceDetails? placeDetails = null;
                Business? similarBusiness = null;
                BusinessAttributes businessAttributes;
                ProductAnalysisResult productAnalysis;
                ServiceAnalysisResult? serviceAnalysis = null;
                string dataSource = "Unknown";
                string? similarBusinessName = null;
                bool isRealData = false;
                string workflowPath = "";

                // STEP 1: Geoapify Business Search (4s)
                _logger.LogInformation("Step 1: Searching Geoapify for business");
                await BroadcastProgressAsync(connectionId, "GEOAPIFY_SEARCH", "Searching for business in Geoapify...");

                placeDetails = await _geoapifyService.SearchPlaceAsync(businessName, fullAddress);

                if (placeDetails != null && !string.IsNullOrEmpty(placeDetails.Name))
                {
                    // Business found in Geoapify - use real data
                    _logger.LogInformation("Business found in Geoapify: {Name}", placeDetails.Name);
                    dataSource = "Geoapify";
                    isRealData = true;
                    workflowPath = "Geoapify → Real Data";

                    await BroadcastProgressAsync(connectionId, "GEOAPIFY_FOUND", $"Found business: {placeDetails.Name}");

                    // Extract business attributes
                    businessAttributes = await _geminiService.ExtractBusinessAttributesAsync(businessName, category, fullAddress);

                    // Extract menu with Geoapify data
                    await BroadcastProgressAsync(connectionId, "EXTRACT_MENU", "Extracting menu items and prices...");
                    productAnalysis = await _geminiService.ExtractMenuWithGeoapifyDataAsync(placeDetails, businessName, category, fullAddress);
                }
                else
                {
                    // Business NOT found in Geoapify
                    _logger.LogInformation("Business not found in Geoapify, searching database for similar businesses");
                    await BroadcastProgressAsync(connectionId, "GEOAPIFY_NOT_FOUND", "Business not found, searching database for similar businesses...");

                    // STEP 2: Search Database for Similar Business (0.5s)
                    similarBusiness = await _similarBusinessService.FindSimilarBusinessAsync(category, fullAddress);

                    if (similarBusiness != null)
                    {
                        // Similar business found in database
                        _logger.LogInformation("Found similar business in database: {Name}", similarBusiness.BusinessName);
                        dataSource = $"Similar Business: {similarBusiness.BusinessName}";
                        isRealData = true;
                        similarBusinessName = similarBusiness.BusinessName;
                        workflowPath = "Geoapify → Database Similar";

                        await BroadcastProgressAsync(connectionId, "SIMILAR_FOUND", $"Found similar business: {similarBusiness.BusinessName}");

                        // Get complete similar business data
                        var similarBusinessData = await _similarBusinessService.GetCompleteBusinessDataAsync(similarBusiness.BusinessId);

                        // Extract basic attributes
                        businessAttributes = new BusinessAttributes
                        {
                            BusinessModel = similarBusiness.BusinessModel,
                            CoreProductsOrServices = similarBusiness.CoreProductsOrServices,
                            TargetAudience = similarBusiness.TargetAudience,
                            BusinessToneOrStyle = similarBusiness.BusinessToneOrStyle,
                            PopularityOrSize = similarBusiness.PopularityOrSize,
                            SpecializationKeywords = similarBusiness.SpecializationKeywords,
                            BusinessType = similarBusiness.BusinessType
                        };

                        // Generate unique products based on similar business
                        await BroadcastProgressAsync(connectionId, "GENERATE_UNIQUE", "Generating unique suggestions based on similar business...");
                        productAnalysis = await _geminiService.GenerateUniqueFromSimilarBusinessAsync(
                            businessName, category, fullAddress, similarBusinessData, minimumSpent);
                    }
                    else
                    {
                        // No similar business in database - use category-based
                        _logger.LogInformation("No similar business found, using category-based analysis");
                        dataSource = "Category-Based AI";
                        isRealData = false;
                        workflowPath = "Geoapify → Database → Category-Based";

                        await BroadcastProgressAsync(connectionId, "CATEGORY_BASED", "Using category-based analysis...");

                        // Extract basic attributes
                        businessAttributes = await _geminiService.ExtractBusinessAttributesAsync(businessName, category, fullAddress);

                        // Generate category-based products
                        productAnalysis = await _geminiService.GenerateCategoryBasedProductsAsync(businessName, category, fullAddress, minimumSpent);
                    }
                }

                // STEP 3: Analyze Services (if needed)
                if (businessAttributes.BusinessType == BusinessType.Service || businessAttributes.BusinessType == BusinessType.Hybrid)
                {
                    await BroadcastProgressAsync(connectionId, "ANALYZE_SERVICES", "Analyzing services...");
                    serviceAnalysis = await _geminiService.AnalyzeServicesAsync(businessAttributes, minimumSpent);
                }

                // STEP 4: Generate Welcome Gift (always)
                await BroadcastProgressAsync(connectionId, "GENERATE_WELCOME_GIFT", "Generating welcome gift...");
                var welcomeGift = await _geminiService.GenerateWelcomeGiftAsync(productAnalysis, serviceAnalysis, businessAttributes, minimumSpent);
                welcomeGift.DataSource = dataSource;

                // STEP 5: Generate All 3 Tiers Combined (10s)
                await BroadcastProgressAsync(connectionId, "GENERATE_TIERS", "Generating loyalty tiers (Bronze, Silver, Gold)...");
                var tierAnalysis = await _geminiService.GenerateAllTiersCombinedAsync(
                    businessAttributes, productAnalysis, serviceAnalysis, minimumSpent);

                // Ensure all tiers are filled
                EnsureAllTiersFilled(tierAnalysis, minimumSpent);

                // STEP 6: Build Response
                var response = new AgentResponse
                {
                    BusinessAttributes = businessAttributes,
                    LoyaltyTierAnalysis = tierAnalysis,
                    WelcomeGift = welcomeGift,
                    DataSource = dataSource,
                    IsRealData = isRealData,
                    SimilarBusinessName = similarBusinessName,
                    WorkflowPath = workflowPath,
                    Success = true
                };

                // Extract tier rewards explicitly
                response.BronzeTier = tierAnalysis.TierRewards.FirstOrDefault(t => t.Tier == LoyaltyTier.Bronze) 
                    ?? CreateFallbackTier(LoyaltyTier.Bronze, 3, 10m);
                response.SilverTier = tierAnalysis.TierRewards.FirstOrDefault(t => t.Tier == LoyaltyTier.Silver) 
                    ?? CreateFallbackTier(LoyaltyTier.Silver, 5, 20m);
                response.GoldTier = tierAnalysis.TierRewards.FirstOrDefault(t => t.Tier == LoyaltyTier.Gold) 
                    ?? CreateFallbackTier(LoyaltyTier.Gold, 7, 40m);

                // STEP 7: Save to Database
                await BroadcastProgressAsync(connectionId, "SAVING", "Saving business data to database...");
                try
                {
                    await _similarBusinessService.SaveBusinessDataAsync(businessName, category, fullAddress, response, productAnalysis, serviceAnalysis, placeDetails, similarBusiness);
                    _logger.LogInformation("Business data saved to database");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error saving to database, continuing...");
                }

                await BroadcastProgressAsync(connectionId, "COMPLETED", "All analysis completed successfully!", response);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in loyalty agent workflow");
                await BroadcastProgressAsync(connectionId, "ERROR", $"Error: {ex.Message}");

                return new AgentResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    WelcomeGift = new WelcomeGiftResponse { ItemName = "Welcome Gift", ItemPriceGBP = 2.00m, Description = "Welcome gift", IsFree = true },
                    BronzeTier = CreateFallbackTier(LoyaltyTier.Bronze, 3, 10m),
                    SilverTier = CreateFallbackTier(LoyaltyTier.Silver, 5, 20m),
                    GoldTier = CreateFallbackTier(LoyaltyTier.Gold, 7, 40m)
                };
            }
        }

        private void EnsureAllTiersFilled(LoyaltyTierAnalysis tierAnalysis, decimal minimumSpent)
        {
            var requiredTiers = new[] { LoyaltyTier.Bronze, LoyaltyTier.Silver, LoyaltyTier.Gold };
            
            foreach (var tier in requiredTiers)
            {
                var existingTier = tierAnalysis.TierRewards.FirstOrDefault(t => t.Tier == tier);
                if (existingTier == null)
                {
                    tierAnalysis.TierRewards.Add(CreateFallbackTier(tier, 
                        tier == LoyaltyTier.Bronze ? 3 : tier == LoyaltyTier.Silver ? 5 : 7,
                        tier == LoyaltyTier.Bronze ? 10m : tier == LoyaltyTier.Silver ? 20m : 40m));
                }
                else if (!existingTier.RewardOptions.Any() && existingTier.FallbackDiscount == null)
                {
                    existingTier.FallbackDiscount = new TierFallbackDiscount
                    {
                        DiscountPercentage = tier == LoyaltyTier.Bronze ? 10m : tier == LoyaltyTier.Silver ? 20m : 40m,
                        Description = $"Get {(tier == LoyaltyTier.Bronze ? 10 : tier == LoyaltyTier.Silver ? 20 : 40)}% off your next purchase",
                        Reasoning = "Default tier discount"
                    };
                }
            }
        }

        private LoyaltyTierReward CreateFallbackTier(LoyaltyTier tier, int tokens, decimal discount)
        {
            return new LoyaltyTierReward
            {
                Tier = tier,
                RequiredTokens = tokens,
                RewardOptions = new List<TierRewardOption>(),
                FallbackDiscount = new TierFallbackDiscount
                {
                    DiscountPercentage = discount,
                    Description = $"Get {discount}% off your next purchase",
                    Reasoning = "Default tier discount"
                },
                Reasoning = $"{tier} tier: {tokens} tokens for {discount}% discount"
            };
        }

        private async Task BroadcastProgressAsync(string? connectionId, string step, string message, object? data = null)
        {
            if (!string.IsNullOrEmpty(connectionId))
            {
                await _hubContext.Clients.Client(connectionId).SendAsync("ReceiveWorkflowUpdate", new WorkflowProgress
                {
                    Step = step,
                    Message = message,
                    Data = data,
                    Status = step == "ERROR" ? "error" : (step == "COMPLETED" ? "completed" : "processing")
                });
            }
        }
    }
}
