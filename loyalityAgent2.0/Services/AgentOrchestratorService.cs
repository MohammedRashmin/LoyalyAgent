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
        private readonly IMenuScraperService _menuScraperService;
        private readonly ILogger<AgentOrchestratorService> _logger;
        private readonly IHubContext<WorkflowHub> _hubContext;
        private readonly IConnectionContextService _connectionContext;

        public AgentOrchestratorService(
            IGeminiService geminiService,
            IGeoapifyService geoapifyService,
            ISimilarBusinessService similarBusinessService,
            IMenuScraperService menuScraperService,
            ILogger<AgentOrchestratorService> logger,
            IHubContext<WorkflowHub> hubContext,
            IConnectionContextService connectionContext)
        {
            _geminiService = geminiService;
            _geoapifyService = geoapifyService;
            _similarBusinessService = similarBusinessService;
            _menuScraperService = menuScraperService;
            _logger = logger;
            _hubContext = hubContext;
            _connectionContext = connectionContext;
        }

        public async Task<AgentResponse> ProcessBusinessLoyaltyAsync(string userApiKey, string businessName, string category, string fullAddress, decimal minimumSpent, string? connectionId = null)
        {
            try
            {
                // Set connection ID in context for API call logging
                _connectionContext.SetConnectionId(connectionId);
                _logger.LogInformation("Set connection ID for API logging: {ConnectionId}", connectionId ?? "NULL");
                
                _geminiService.SetApiKey(userApiKey);

                _logger.LogInformation("Starting loyalty agent workflow for business: {BusinessName}, Min Spend: £{MinSpent}", businessName, minimumSpent);
                await BroadcastProgressAsync(connectionId, "START", $"Starting analysis for {businessName} (Min spend: £{minimumSpent})");

                PlaceDetails? placeDetails = null;
                Business? similarBusiness = null;
                CompleteBusinessData? similarBusinessData = null; // Store similar business data for discount tiers
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

                    await BroadcastProgressAsync(connectionId, "GEOAPIFY_FOUND", $"Found business: {placeDetails.Name}");

                    // Extract business attributes and menu combined (1 API call instead of 2)
                    await BroadcastProgressAsync(connectionId, "EXTRACT_ATTRIBUTES_AND_MENU", "Extracting business attributes and menu (combined)...");
                    var combinedResult = await _geminiService.ExtractBusinessAttributesAndMenuCombinedAsync(placeDetails, businessName, category, fullAddress);
                    businessAttributes = combinedResult.BusinessAttributes;
                    productAnalysis = combinedResult.ProductAnalysis;

                    // Note: Combined method already extracted menu, so we skip website scraping for now
                    // (Can add website scraping as optimization later if needed)
                    workflowPath = "Geoapify → Combined Attributes + Menu";
                    dataSource = "Geoapify + Combined AI Extraction";
                }
                else
                {
                    // Business NOT found in Geoapify
                    _logger.LogInformation("Business not found in Geoapify, searching web platforms...");
                    await BroadcastProgressAsync(connectionId, "GEOAPIFY_NOT_FOUND", "Business not found in Geoapify, searching web platforms...");

                    // STEP 2: Combined Web Search + Extract All (reduces 4 POST calls to 1 POST)
                    await BroadcastProgressAsync(connectionId, "WEB_SEARCH_AND_EXTRACT", "Searching web platforms and extracting all data (combined)...");
                    var combinedWebResult = await _geminiService.SearchBusinessAndExtractAllCombinedAsync(
                        businessName, category, fullAddress, minimumSpent);

                    var webSearchResult = combinedWebResult.WebSearchResult;
                    businessAttributes = combinedWebResult.BusinessAttributes;
                    productAnalysis = combinedWebResult.ProductAnalysis;
                    serviceAnalysis = combinedWebResult.ServiceAnalysis; // Already analyzed if service/hybrid

                    if (webSearchResult.Found)
                    {
                        // Business found via web search
                        _logger.LogInformation("Business found via web search on: {Source}", webSearchResult.Source);
                        await BroadcastProgressAsync(connectionId, "WEB_SEARCH_FOUND", $"Found business on {webSearchResult.Source}");

                        // STEP 3: Check Database for Similar Business
                        await BroadcastProgressAsync(connectionId, "CHECK_DATABASE_SIMILAR", "Checking database for similar businesses...");
                        similarBusiness = await _similarBusinessService.FindSimilarBusinessAsync(category, fullAddress);

                        if (similarBusiness != null)
                        {
                            // Similar business found in database
                            _logger.LogInformation("Found similar business in database: {Name}", similarBusiness.BusinessName);
                            dataSource = $"Web Search ({webSearchResult.Source}) → Similar Business: {similarBusiness.BusinessName}";
                            isRealData = true;
                            similarBusinessName = similarBusiness.BusinessName;
                            workflowPath = "Geoapify → Web Search → Database Similar";

                            await BroadcastProgressAsync(connectionId, "SIMILAR_FOUND", $"Found similar business: {similarBusiness.BusinessName}");

                            // Get complete similar business data
                            similarBusinessData = await _similarBusinessService.GetCompleteBusinessDataAsync(similarBusiness.BusinessId);

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
                            
                            // Reset service analysis since we're using similar business data
                            serviceAnalysis = null;
                        }
                        else
                        {
                            // No similar business in database - but check again for discount purposes
                            // Try to find similar business just for discount reference (even if not exact match)
                            await BroadcastProgressAsync(connectionId, "CHECK_SIMILAR_DISCOUNT", "Checking for similar businesses to reference discount rates...");
                            var similarForDiscount = await _similarBusinessService.FindSimilarBusinessAsync(category, fullAddress);
                            
                            if (similarForDiscount != null)
                            {
                                similarBusinessData = await _similarBusinessService.GetCompleteBusinessDataAsync(similarForDiscount.BusinessId);
                                _logger.LogInformation("Found similar business for discount reference: {Name}", similarForDiscount.BusinessName);
                            }
                            
                            // Use Fallback Discount
                            _logger.LogInformation("No similar business found, using fallback discount (real business found)");
                            dataSource = "Web Search → Fallback Discount";
                            isRealData = true;
                            workflowPath = "Geoapify → Web Search → Fallback Discount";

                            await BroadcastProgressAsync(connectionId, "FALLBACK_DISCOUNT", "Using discount-only rewards (real business found but no similar in database)...");
                        }
                    }
                    else
                    {
                        // Web search also failed - use category-based
                        _logger.LogInformation("Business not found on web platforms, using category-based analysis");
                        dataSource = "Category-Based AI";
                        isRealData = false;
                        workflowPath = "Geoapify → Web Search → Category-Based";

                        await BroadcastProgressAsync(connectionId, "WEB_SEARCH_NOT_FOUND", "Business not found on web platforms, using category-based analysis...");

                        // Extract basic attributes
                        businessAttributes = await _geminiService.ExtractBusinessAttributesAsync(businessName, category, fullAddress);

                        // Generate category-based products
                        productAnalysis = await _geminiService.GenerateCategoryBasedProductsAsync(businessName, category, fullAddress, minimumSpent);
                        
                        // Reset service analysis
                        serviceAnalysis = null;
                    }
                }

                // STEP 3: Analyze Services (if needed and not already analyzed in combined call)
                if (serviceAnalysis == null && (businessAttributes.BusinessType == BusinessType.Service || businessAttributes.BusinessType == BusinessType.Hybrid))
                {
                    await BroadcastProgressAsync(connectionId, "ANALYZE_SERVICES", "Analyzing services...");
                    serviceAnalysis = await _geminiService.AnalyzeServicesAsync(businessAttributes, minimumSpent);
                }

                // STEP 4 & 5: Generate Welcome Gift + Tiers Combined (reduced to 1 API call)
                WelcomeGiftResponse welcomeGift;
                LoyaltyTierAnalysis tierAnalysis;
                
                // Use combined method to reduce API calls (2 POST → 1 POST)
                if (dataSource == "Web Search → Fallback Discount")
                {
                    await BroadcastProgressAsync(connectionId, "GENERATE_WELCOME_GIFT_AND_TIERS", "Generating welcome gift and loyalty tiers (combined)...");
                    var combinedResult = await _geminiService.GenerateWelcomeGiftAndDiscountTiersCombinedAsync(
                        productAnalysis, serviceAnalysis, businessAttributes, minimumSpent, similarBusinessData);
                    welcomeGift = combinedResult.WelcomeGift;
                    tierAnalysis = combinedResult.TierAnalysis;
                }
                else
                {
                    // Normal path: Use combined method (2 POST → 1 POST)
                    await BroadcastProgressAsync(connectionId, "GENERATE_WELCOME_GIFT_AND_TIERS", "Generating welcome gift and loyalty tiers (combined)...");
                    var combinedResult = await _geminiService.GenerateWelcomeGiftAndTiersCombinedAsync(
                        productAnalysis, serviceAnalysis, businessAttributes, minimumSpent);
                    welcomeGift = combinedResult.WelcomeGift;
                    tierAnalysis = combinedResult.TierAnalysis;
                }
                
                welcomeGift.DataSource = dataSource;

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

        private async Task<string?> FindWebsiteUrlAsync(string businessName, string address)
        {
            try
            {
                _logger.LogInformation("Searching for website URL for business: {BusinessName}", businessName);

                // Use Gemini with Google Search grounding for better results
                var website = await _geminiService.SearchWebsiteUrlAsync(businessName, address);
                
                if (!string.IsNullOrEmpty(website))
                {
                    return website;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching for website URL");
                return null;
            }
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
