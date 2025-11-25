using loyalityAgent2._0.Models;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace loyalityAgent2._0.Services
{
    public partial class GeminiService : IGeminiService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<GeminiService> _logger;
        private string? _userApiKey;

        public GeminiService(HttpClient httpClient, IConfiguration configuration, ILogger<GeminiService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        public void SetApiKey(string apiKey)
        {
            _userApiKey = apiKey;
            _logger.LogInformation("API key set for user request");
        }

        public async Task<BusinessAttributes> ExtractBusinessAttributesAsync(string businessName, string category, string fullAddress)
        {
            try
            {
                _logger.LogInformation("Extracting business attributes for: {BusinessName}", businessName);

                // Single search query with Google Search grounding
                var searchPrompt = $@"Search the web for information about: {businessName} {category} {fullAddress}

Tell me about this business:
- What is their business model?
- What products or services do they sell/offer?
- Who are their target customers?
- What is their brand style (casual, luxury, professional, etc.)?
- How big/popular are they (local shop, regional chain, national/international brand)?
- What makes them special or unique?
- Do they primarily sell PRODUCTS, provide SERVICES, or both (HYBRID)?

Give me real, specific information about this business based on web search.";

                var response = await PerformGoogleSearchWithPromptAsync(searchPrompt);

                _logger.LogInformation("Google Search Response Length: {Length} chars", response.Length);
                _logger.LogInformation("Full Response: {Response}", response);

                var businessAttributes = ParseBusinessAttributesFromText(response);

                _logger.LogInformation("Parsed BusinessModel: {BusinessModel}", businessAttributes.BusinessModel);
                _logger.LogInformation("Parsed Products/Services: {Products}", businessAttributes.CoreProductsOrServices);
                _logger.LogInformation("Parsed BusinessType: {BusinessType}", businessAttributes.BusinessType);

                return businessAttributes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting business attributes");
                throw;
            }
        }

        public async Task<ProductAnalysisResult> AnalyzeProductsAsync(BusinessAttributes businessAttributes, decimal minimumSpent)
        {
            try
            {
                _logger.LogInformation("Analyzing products for WELCOME GIFT (truly free, no spending required)");

                if (string.IsNullOrEmpty(businessAttributes.CoreProductsOrServices))
                {
                    _logger.LogWarning("No products/services information available");
                    return new ProductAnalysisResult();
                }

                var prompt = $@"Business sells: {businessAttributes.CoreProductsOrServices}
Target customers: {businessAttributes.TargetAudience}
Business type: {businessAttributes.BusinessModel}
Specialization: {businessAttributes.SpecializationKeywords}
Location: UK (prices in British Pounds £)

TASK: Suggest a WELCOME GIFT - a truly FREE item given to new customers with NO purchase required.

IMPORTANT:
- ONLY suggest products that are ACTUALLY on this business's menu/offerings
- DO NOT suggest generic, random, or common items
- If you cannot identify specific products from their actual menu, respond with: NO MENU ITEMS FOUND

WELCOME GIFT CRITERIA:
- Must be small/affordable (typically under £3-5) so business can give it free
- Must be from their ACTUAL menu
- Should be representative of their business (e.g., coffee shop → small coffee/pastry)
- Should create good first impression without significant cost to business

If you can find specific menu items:
List ALL products with ESTIMATED prices in GBP (£).
Find the SMALLEST/MOST AFFORDABLE items suitable as free welcome gifts.
Recommend ONE small, affordable product from their ACTUAL MENU to give as WELCOME GIFT (no purchase required).

Format:
PRODUCTS: Product1 - £X.XX, Product2 - £Y.YY
POPULAR: [list]
SMALL ITEMS UNDER £5: Product A - £X.XX, Product B - £Y.YY
RECOMMENDED WELCOME GIFT: ProductName - £Z.ZZ";

                var response = await GeneratePromptAsync(prompt);
                _logger.LogInformation("Welcome Gift Analysis Response: {Response}", response);

                return ParseProductAnalysisWithPricesFromText(response, minimumSpent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing products for welcome gift");
                return new ProductAnalysisResult();
            }
        }

        public async Task<ServiceAnalysisResult> AnalyzeServicesAsync(BusinessAttributes businessAttributes, decimal minimumSpent)
        {
            try
            {
                _logger.LogInformation("Analyzing services for WELCOME OFFER (discount for first-time customers)");

                if (string.IsNullOrEmpty(businessAttributes.CoreProductsOrServices))
                {
                    _logger.LogWarning("No products/services information available");
                    return new ServiceAnalysisResult();
                }

                var prompt = $@"Business offers: {businessAttributes.CoreProductsOrServices}
Target customers: {businessAttributes.TargetAudience}
Business type: {businessAttributes.BusinessModel}
Specialization: {businessAttributes.SpecializationKeywords}
Location: UK (prices in British Pounds £)

TASK: Suggest a WELCOME OFFER for new customers - a discount on their first service visit.

IMPORTANT:
- ONLY suggest services that are ACTUALLY on this business's menu/offerings
- DO NOT suggest generic, random, or common services
- If you cannot identify specific services from their actual menu, respond with: NO MENU ITEMS FOUND

WELCOME OFFER CRITERIA:
- Should be a popular entry-level service that attracts new customers
- Discount should be meaningful but sustainable (typically 15-30% off)
- Must be from their ACTUAL service menu
- Should showcase what the business does best

If you can find specific menu services:
List ALL services with ESTIMATED prices in GBP (£).
Find ENTRY-LEVEL or POPULAR services suitable for first-time customers.
Recommend ONE service from their ACTUAL MENU to offer a welcome discount on.

Format:
SERVICES: Service1 - £X.XX, Service2 - £Y.YY
POPULAR: [list]
ENTRY-LEVEL SERVICES: Service A - £X.XX, Service B - £Y.YY
RECOMMENDED FOR DISCOUNT: ServiceName - £Z.ZZ";

                var response = await GeneratePromptAsync(prompt);
                _logger.LogInformation("Welcome Offer Service Analysis Response: {Response}", response);

                return ParseServiceAnalysisWithPricesFromText(response, minimumSpent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing services for welcome offer");
                return new ServiceAnalysisResult();
            }
        }

        public async Task<ReasoningResult> ReasonProductOfferAsync(BusinessAttributes businessAttributes, string product, decimal productPrice, decimal minimumSpent)
        {
            try
            {
                _logger.LogInformation("Reasoning product offer for: {Product} (£{Price}) when customer spends £{MinSpent}", product, productPrice, minimumSpent);

                var prompt = $@"Business: {businessAttributes.BusinessModel}
Target: {businessAttributes.TargetAudience}
Size: {businessAttributes.PopularityOrSize}
Product to give FREE: {product} (worth £{productPrice})
Customer spending: £{minimumSpent}

Offer: When customer spends £{minimumSpent}, give '{product}' (worth £{productPrice}) FREE.

Should this business approve this offer?

Consider:
- Product value (£{productPrice}) vs minimum spend (£{minimumSpent}) = {productPrice / minimumSpent * 100:F1}% value
- Cost impact on business
- Customer appeal (good deal?)
- If product price > 25% of minimum spend, suggest discount instead

Respond with:
DECISION: YES or NO
REASONING: [explain why]
If NO: ALTERNATIVE: [either 'PercentageDiscount' with suggested % or 'FreeToken']
PERCENTAGE: [if suggesting discount, give number like 10, 15, 20]";

                var response = await GeneratePromptAsync(prompt);
                _logger.LogInformation("Product Reasoning Response: {Response}", response);

                return ParseReasoningFromText(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reasoning product offer");
                return new ReasoningResult
                {
                    IsApproved = false,
                    Reasoning = "Error in reasoning process",
                    FallbackOption = "FreeToken"
                };
            }
        }

        public async Task<ReasoningResult> ReasonServiceDiscountAsync(BusinessAttributes businessAttributes, string service, decimal servicePrice, decimal minimumSpent)
        {
            try
            {
                _logger.LogInformation("Reasoning service discount for: {Service} (£{Price}) when customer spends £{MinSpent}", service, servicePrice, minimumSpent);

                var prompt = $@"Business: {businessAttributes.BusinessModel}
Target: {businessAttributes.TargetAudience}
Size: {businessAttributes.PopularityOrSize}
Service to discount: {service} (normally £{servicePrice})
Customer typical spend: £{minimumSpent}

Offer: Discount on '{service}' (£{servicePrice}) for first visit.

What discount percentage should be offered?

Consider:
- Service is around the customer's spending level (£{servicePrice} vs £{minimumSpent})
- Business profitability
- First-time customer acquisition value
- Suggest 10-25% range

Respond with:
DECISION: APPROVED or NOT APPROVED
PERCENTAGE: [number only, e.g., 15, 20, 25]
REASONING: [explain why this percentage]";

                var response = await GeneratePromptAsync(prompt);
                _logger.LogInformation("Service Reasoning Response: {Response}", response);

                return ParseServiceReasoningFromText(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reasoning service discount");
                return new ReasoningResult
                {
                    IsApproved = false,
                    Reasoning = "Error in reasoning process",
                    FallbackOption = "FreeToken"
                };
            }
        }

        public async Task<string> GeneratePromptAsync(string prompt)
        {
            try
            {
                var apiKey = _userApiKey ?? _configuration["Gemini:ApiKey"] ?? "";
                var apiUrl = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={apiKey}";

                var requestBody = new GeminiRequest
                {
                    Contents = new List<GeminiContent>
                    {
                        new GeminiContent
                        {
                            Parts = new List<GeminiPart>
                            {
                                new GeminiPart { Text = prompt }
                            }
                        }
                    }
                };

                var jsonContent = JsonSerializer.Serialize(requestBody);
                var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(apiUrl, httpContent);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Gemini API Error: {StatusCode} - {Content}", response.StatusCode, responseContent);
                    throw new Exception($"Gemini API Error: {response.StatusCode} - {responseContent}");
                }

                var geminiResponse = JsonSerializer.Deserialize<GeminiResponse>(responseContent);
                return ExtractContent(geminiResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating prompt response");
                throw;
            }
        }

        private async Task<string> PerformGoogleSearchWithPromptAsync(string prompt)
        {
            try
            {
                var apiKey = _userApiKey ?? _configuration["Gemini:ApiKey"] ?? "";

                // Try with Google Search grounding first
                _logger.LogInformation("Attempting search with Google Search grounding");
                var apiUrl = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={apiKey}";

                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new { text = prompt }
                            }
                        }
                    },
                    tools = new[]
                    {
                        new
                        {
                            google_search_retrieval = new { }
                        }
                    }
                };

                var jsonContent = JsonSerializer.Serialize(requestBody);
                var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                _logger.LogInformation("Request body: {Body}", jsonContent);

                var response = await _httpClient.PostAsync(apiUrl, httpContent);
                var responseContent = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("Response status: {Status}", response.StatusCode);
                _logger.LogInformation("Response body: {Body}", responseContent.Substring(0, Math.Min(1000, responseContent.Length)));

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Google Search grounding failed, falling back to regular generation");
                    // Fall back to regular generation without search
                    return await GeneratePromptAsync(prompt);
                }

                var geminiResponse = JsonSerializer.Deserialize<GeminiResponse>(responseContent);
                var content = ExtractContent(geminiResponse);

                if (string.IsNullOrEmpty(content) || content == "No content found")
                {
                    _logger.LogWarning("No content from grounding, using fallback");
                    return await GeneratePromptAsync(prompt);
                }

                return content;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing Google search, using fallback");
                // Fallback to regular generation
                return await GeneratePromptAsync(prompt);
            }
        }

        private BusinessAttributes ParseBusinessAttributesFromText(string text)
        {
            try
            {
                var attrs = new BusinessAttributes();

                // Try multiple parsing strategies

                // Strategy 1: Look for "Key: Value" format
                attrs.BusinessModel = ExtractValueAfterKey(text, new[] { "Business Model:", "business model:" })
                    ?? ExtractSection(text, new[] { "business model", "1." })
                    ?? "Unknown";

                attrs.CoreProductsOrServices = ExtractValueAfterKey(text, new[] { "Products/Services:", "products/services:", "Products:", "Services:" })
                    ?? ExtractSection(text, new[] { "products", "services", "2.", "offers", "sells" })
                    ?? "Unknown";

                attrs.TargetAudience = ExtractValueAfterKey(text, new[] { "Target Audience:", "target audience:", "Customers:" })
                    ?? ExtractSection(text, new[] { "target", "audience", "customers", "3." })
                    ?? "General customers";

                attrs.BusinessToneOrStyle = ExtractValueAfterKey(text, new[] { "Brand Style:", "brand style:", "Tone:" })
                    ?? ExtractSection(text, new[] { "tone", "style", "4.", "brand" })
                    ?? "Professional";

                attrs.PopularityOrSize = ExtractValueAfterKey(text, new[] { "Size/Popularity:", "size/popularity:", "Size:" })
                    ?? ExtractSection(text, new[] { "size", "popularity", "5.", "scale" })
                    ?? "Local";

                attrs.SpecializationKeywords = ExtractValueAfterKey(text, new[] { "Unique Features:", "unique features:", "Special:" })
                    ?? ExtractSection(text, new[] { "unique", "special", "6.", "specialization" })
                    ?? "";

                // Determine business type
                attrs.BusinessType = DetermineBusinessTypeFromText(text);

                // Validate that we got real data, not instructions
                if (attrs.BusinessModel.Contains("analyze") || attrs.BusinessModel.Contains("extract") ||
                    attrs.CoreProductsOrServices.Contains("analyze") || attrs.CoreProductsOrServices.Contains("extract"))
                {
                    _logger.LogWarning("Parsed data contains instructions, not actual information");

                    // Try to extract any useful information from the text
                    var cleanText = text.Replace("**", "").Replace("*", "");
                    var sentences = cleanText.Split(new[] { '.', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => s.Trim())
                        .Where(s => s.Length > 10 && !s.Contains("search") && !s.Contains("analyze") && !s.Contains("extract"))
                        .ToList();

                    if (sentences.Any())
                    {
                        // Use sentences as fallback
                        attrs.CoreProductsOrServices = string.Join(". ", sentences.Take(3));
                    }
                }

                var productsPreview = string.IsNullOrEmpty(attrs.CoreProductsOrServices)
                    ? "N/A"
                    : attrs.CoreProductsOrServices.Substring(0, Math.Min(50, attrs.CoreProductsOrServices.Length));

                _logger.LogInformation("Parsed attributes - Model: {Model}, Type: {Type}, Products: {Products}",
                    attrs.BusinessModel, attrs.BusinessType, productsPreview);

                return attrs;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing business attributes");
                return new BusinessAttributes
                {
                    BusinessModel = "Unknown",
                    CoreProductsOrServices = "Unknown",
                    TargetAudience = "General customers",
                    BusinessToneOrStyle = "Professional",
                    PopularityOrSize = "Local",
                    BusinessType = BusinessType.Hybrid
                };
            }
        }

        private string? ExtractValueAfterKey(string text, string[] keys)
        {
            foreach (var key in keys)
            {
                var index = text.IndexOf(key, StringComparison.OrdinalIgnoreCase);
                if (index >= 0)
                {
                    var startIndex = index + key.Length;
                    var remainingText = text.Substring(startIndex);

                    // Extract until newline or next key pattern
                    var endIndex = remainingText.IndexOfAny(new[] { '\n', '\r' });
                    if (endIndex < 0)
                        endIndex = Math.Min(200, remainingText.Length); // Max 200 chars

                    var value = remainingText.Substring(0, endIndex).Trim();

                    // Clean up the value
                    value = value.TrimStart('[').TrimEnd(']').Trim();

                    if (!string.IsNullOrEmpty(value) && value.Length > 3 &&
                        !value.Contains("[") && !value.Contains("their") && !value.Contains("what"))
                    {
                        return value;
                    }
                }
            }
            return null;
        }

        private string? ExtractSection(string text, string[] keywords)
        {
            var lines = text.Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var lowerLine = line.ToLower();

                foreach (var keyword in keywords)
                {
                    if (lowerLine.Contains(keyword))
                    {
                        // Extract from current line
                        var colonIndex = line.IndexOf(':');
                        if (colonIndex > 0 && colonIndex < line.Length - 1)
                        {
                            var content = line.Substring(colonIndex + 1).Trim();
                            if (!string.IsNullOrEmpty(content) && content.Length > 3)
                            {
                                return content;
                            }
                        }

                        // Try next line
                        if (i + 1 < lines.Length)
                        {
                            var nextLine = lines[i + 1].Trim();
                            if (!string.IsNullOrEmpty(nextLine) && nextLine.Length > 3 && !nextLine.Contains(':'))
                            {
                                return nextLine;
                            }
                        }
                    }
                }
            }

            return null;
        }

        private BusinessType DetermineBusinessTypeFromText(string text)
        {
            var lowerText = text.ToLower();

            // Check for explicit service-oriented classification first
            if (lowerText.Contains("primarily service") ||
                lowerText.Contains("service-oriented") ||
                lowerText.Contains("service-based") ||
                lowerText.Contains("mainly service") ||
                lowerText.Contains("purely service"))
            {
                _logger.LogInformation("Detected explicit service-oriented business");
                return BusinessType.Service;
            }

            // Check for explicit product-oriented classification
            if (lowerText.Contains("primarily product") ||
                lowerText.Contains("product-based") ||
                lowerText.Contains("mainly product") ||
                lowerText.Contains("purely product"))
            {
                _logger.LogInformation("Detected explicit product-oriented business");
                return BusinessType.Product;
            }

            // Look for explicit classification in structured format
            if (lowerText.Contains("business type") || lowerText.Contains("7.") || lowerText.Contains("type:"))
            {
                if (Regex.IsMatch(lowerText, @"\bproduct\b(?!\s*and\s*service)") && !lowerText.Contains("service"))
                    return BusinessType.Product;
                if (Regex.IsMatch(lowerText, @"\bservice\b(?!\s*and\s*product)") && !lowerText.Contains("product"))
                    return BusinessType.Service;
                if (lowerText.Contains("hybrid") || lowerText.Contains("both"))
                    return BusinessType.Hybrid;
            }

            // Heuristic analysis with improved keywords
            int productScore = 0;
            int serviceScore = 0;

            // Product-focused keywords
            string[] productKeywords = {
                "sells", "retail", "store", "shop", "merchandise", "goods",
                "inventory", "e-commerce", "products for sale", "selling products",
                "online store", "physical products"
            };

            // Service-focused keywords
            string[] serviceKeywords = {
                "provides", "offers services", "consultation", "repair", "maintenance",
                "professional services", "treatments", "spa", "massage", "salon",
                "therapy", "clinic", "appointment", "sessions", "bookings",
                "therapeutic", "wellness", "healthcare", "personal care"
            };

            foreach (var keyword in productKeywords)
                if (lowerText.Contains(keyword)) productScore++;

            foreach (var keyword in serviceKeywords)
                if (lowerText.Contains(keyword)) serviceScore++;

            _logger.LogInformation("Business type scoring - Product: {ProductScore}, Service: {ServiceScore}",
                productScore, serviceScore);

            // More lenient thresholds and clearer classification
            if (serviceScore > productScore && serviceScore >= 1)
                return BusinessType.Service;
            if (productScore > serviceScore && productScore >= 1)
                return BusinessType.Product;

            // If scores are equal and both > 0, it's genuinely hybrid
            if (productScore > 0 && serviceScore > 0 && productScore == serviceScore)
                return BusinessType.Hybrid;

            // Default to hybrid only if we really can't determine
            return BusinessType.Hybrid;
        }

        // NOTE: Replaced by ParseProductAnalysisWithPricesFromText in GeminiServicePriceParsing.cs

        // NOTE: Replaced by ParseServiceAnalysisWithPricesFromText in GeminiServicePriceParsing.cs

        private ReasoningResult ParseReasoningFromText(string text)
        {
            var result = new ReasoningResult();

            try
            {
                // Extract DECISION
                var decisionMatch = Regex.Match(text, @"DECISION:\s*(YES|NO|APPROVED)", RegexOptions.IgnoreCase);
                result.IsApproved = decisionMatch.Success && (decisionMatch.Groups[1].Value.ToUpper() == "YES" || decisionMatch.Groups[1].Value.ToUpper() == "APPROVED");

                // Extract REASONING
                var reasoningMatch = Regex.Match(text, @"REASONING:\s*(.+?)(?:\n(?:ALTERNATIVE|$))", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (reasoningMatch.Success)
                {
                    result.Reasoning = reasoningMatch.Groups[1].Value.Trim();
                }

                // Extract ALTERNATIVE
                if (!result.IsApproved)
                {
                    var altMatch = Regex.Match(text, @"ALTERNATIVE:\s*(.+?)(?:\n|$)", RegexOptions.IgnoreCase);
                    if (altMatch.Success)
                    {
                        var alternative = altMatch.Groups[1].Value.ToLower();
                        if (alternative.Contains("discount") || alternative.Contains("%"))
                        {
                            result.FallbackOption = "PercentageDiscount";
                            var percentMatch = Regex.Match(alternative, @"(\d+)\s*%?");
                            if (percentMatch.Success)
                            {
                                result.SuggestedPercentage = decimal.Parse(percentMatch.Groups[1].Value);
                            }
                        }
                        else
                        {
                            result.FallbackOption = "FreeToken";
                        }
                    }
                    else
                    {
                        result.FallbackOption = "FreeToken";
                    }
                }

                _logger.LogInformation("Reasoning result: Approved={Approved}, Fallback={Fallback}",
                    result.IsApproved, result.FallbackOption);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing reasoning");
                result.IsApproved = false;
                result.FallbackOption = "FreeToken";
            }

            return result;
        }

        private ReasoningResult ParseServiceReasoningFromText(string text)
        {
            var result = new ReasoningResult();

            try
            {
                // Extract DECISION
                var decisionMatch = Regex.Match(text, @"DECISION:\s*(APPROVED|NOT APPROVED)", RegexOptions.IgnoreCase);
                result.IsApproved = decisionMatch.Success && decisionMatch.Groups[1].Value.ToUpper() == "APPROVED";

                // Extract PERCENTAGE
                var percentMatch = Regex.Match(text, @"PERCENTAGE:\s*(\d+)", RegexOptions.IgnoreCase);
                if (percentMatch.Success)
                {
                    result.SuggestedPercentage = decimal.Parse(percentMatch.Groups[1].Value);
                }

                // Extract REASONING
                var reasoningMatch = Regex.Match(text, @"REASONING:\s*(.+?)$", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (reasoningMatch.Success)
                {
                    result.Reasoning = reasoningMatch.Groups[1].Value.Trim();
                }

                if (!result.IsApproved)
                {
                    result.FallbackOption = "FreeToken";
                }

                _logger.LogInformation("Service reasoning: Approved={Approved}, Percentage={Percentage}",
                    result.IsApproved, result.SuggestedPercentage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing service reasoning");
                result.IsApproved = false;
                result.FallbackOption = "FreeToken";
            }

            return result;
        }

        public async Task<LoyaltyTierAnalysis> AnalyzeLoyaltyTiersAsync(
            BusinessAttributes businessAttributes,
            ProductAnalysisResult? productAnalysis,
            ServiceAnalysisResult? serviceAnalysis,
            decimal minimumSpendForToken)
        {
            try
            {
                _logger.LogInformation("Analyzing loyalty tiers. Min spend for 1 token: £{MinSpend}", minimumSpendForToken);

                var analysis = new LoyaltyTierAnalysis
                {
                    MinimumSpendForToken = minimumSpendForToken,
                    TierRewards = new List<LoyaltyTierReward>()
                };

                // Build context from available products/services
                var availableItems = new StringBuilder();
                if (productAnalysis?.AllProducts?.Any() == true)
                {
                    availableItems.AppendLine("AVAILABLE PRODUCTS:");
                    foreach (var product in productAnalysis.AllProducts)
                    {
                        availableItems.AppendLine($"- {product.Name}: £{product.PriceGBP}");
                    }
                }

                if (serviceAnalysis?.AllServices?.Any() == true)
                {
                    availableItems.AppendLine("\nAVAILABLE SERVICES:");
                    foreach (var service in serviceAnalysis.AllServices)
                    {
                        availableItems.AppendLine($"- {service.Name}: £{service.PriceGBP}");
                    }
                }

                var hasItems = availableItems.Length > 0;
                analysis.HasFreeItemOptions = hasItems;

                // Define tier requirements
                var tiers = new[]
                {
                    new { Tier = LoyaltyTier.Bronze, Tokens = 3, FallbackDiscount = 10m },
                    new { Tier = LoyaltyTier.Silver, Tokens = 5, FallbackDiscount = 20m },
                    new { Tier = LoyaltyTier.Gold, Tokens = 7, FallbackDiscount = 40m }
                };

                foreach (var tier in tiers)
                {
                    var totalSpendForTier = minimumSpendForToken * tier.Tokens;

                    // Define value ranges for progressive tiers
                    string valueGuidance = tier.Tier switch
                    {
                        LoyaltyTier.Bronze => $"Lower-value items (typically £3-£8 range) - customer has spent £{totalSpendForTier} total",
                        LoyaltyTier.Silver => $"Medium-value items (typically £8-£15 range) - customer has spent £{totalSpendForTier} total. Should be MORE VALUABLE than Bronze tier rewards.",
                        LoyaltyTier.Gold => $"Higher-value items (typically £15-£25 range) - customer has spent £{totalSpendForTier} total. Should be the MOST VALUABLE rewards, better than Bronze and Silver.",
                        _ => $"Items worth approximately 10-15% of total spend (£{totalSpendForTier})"
                    };

                    var prompt = $@"Business: {businessAttributes.BusinessModel}
Business Type: {businessAttributes.BusinessType}
Core Offerings: {businessAttributes.CoreProductsOrServices}
Specialization: {businessAttributes.SpecializationKeywords}

LOYALTY TIER: {tier.Tier} ({tier.Tokens} tokens)
- Customer earns 1 token per £{minimumSpendForToken} spent
- Total customer spend to reach {tier.Tier}: £{totalSpendForTier}
- VALUE TIER: {valueGuidance}

{(hasItems ? availableItems.ToString() : "NO SPECIFIC MENU ITEMS AVAILABLE")}

TASK:
{(hasItems ?
    $@"1. Suggest 2-3 items from the ACTUAL MENU as FREE rewards for {tier.Tier} tier
2. PROGRESSIVE VALUE - {tier.Tier} tier requirements:
   {(tier.Tier == LoyaltyTier.Bronze ? "- Suggest ENTRY-LEVEL or SMALLER items from the menu" : "")}
   {(tier.Tier == LoyaltyTier.Silver ? "- Suggest BETTER/MORE VALUABLE items than Bronze tier - mid-range menu items" : "")}
   {(tier.Tier == LoyaltyTier.Gold ? "- Suggest PREMIUM items - the BEST rewards on the menu, significantly better than Bronze/Silver" : "")}
   - Item value must fit the business model and be from ACTUAL menu
   - Business must be able to afford giving it free (consider profit margins)
   - Customer should feel rewarded for loyalty (spent £{totalSpendForTier})
   - Items must be appropriate for this tier level
3. Determine if giving these items FREE is viable

Format:
ITEM: ItemName1 - £X.XX
CAN_BE_FREE: YES/NO
REASONING: Brief explanation

ITEM: ItemName2 - £Y.YY
CAN_BE_FREE: YES/NO
REASONING: Brief explanation

OVERALL_VIABILITY: YES/NO" :
    $@"Since no specific menu items are available:
OVERALL_VIABILITY: NO
REASONING: No specific products/services identified, must use discount fallback")}

FALLBACK: If items cannot be given free, suggest {tier.FallbackDiscount}% discount on next purchase as alternative.";

                    var response = await GeneratePromptAsync(prompt);
                    _logger.LogInformation("{Tier} tier analysis response: {Response}", tier.Tier, response);

                    var tierReward = ParseLoyaltyTierResponse(response, tier.Tier, tier.Tokens, tier.FallbackDiscount);
                    analysis.TierRewards.Add(tierReward);
                }

                // Determine overall strategy
                var hasAnyFreeItems = analysis.TierRewards.Any(t => t.RewardOptions.Any(r => r.CanBeGivenFree));
                analysis.OverallStrategy = hasAnyFreeItems
                    ? "Token-based loyalty program with tier rewards (free items and discount fallbacks)"
                    : "Token-based loyalty program with tier-based discount rewards";

                _logger.LogInformation("Loyalty tier analysis complete. Strategy: {Strategy}", analysis.OverallStrategy);

                return analysis;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing loyalty tiers");

                // Return fallback structure with discount-only tiers
                return new LoyaltyTierAnalysis
                {
                    MinimumSpendForToken = minimumSpendForToken,
                    HasFreeItemOptions = false,
                    OverallStrategy = "Discount-based loyalty program (fallback)",
                    TierRewards = new List<LoyaltyTierReward>
                    {
                        CreateFallbackTierReward(LoyaltyTier.Bronze, 3, 10m),
                        CreateFallbackTierReward(LoyaltyTier.Silver, 5, 20m),
                        CreateFallbackTierReward(LoyaltyTier.Gold, 7, 40m)
                    }
                };
            }
        }

        private LoyaltyTierReward ParseLoyaltyTierResponse(string text, LoyaltyTier tier, int tokens, decimal fallbackDiscount)
        {
            var reward = new LoyaltyTierReward
            {
                Tier = tier,
                RequiredTokens = tokens,
                RewardOptions = new List<TierRewardOption>()
            };

            try
            {
                // Extract items and their viability
                var itemMatches = Regex.Matches(text, @"ITEM:\s*(.+?)\s*-\s*£?([\d.]+).*?CAN_BE_FREE:\s*(YES|NO).*?REASONING:\s*(.+?)(?=ITEM:|OVERALL_VIABILITY:|FALLBACK:|$)",
                    RegexOptions.IgnoreCase | RegexOptions.Singleline);

                foreach (Match match in itemMatches)
                {
                    if (match.Success)
                    {
                        var option = new TierRewardOption
                        {
                            ItemName = match.Groups[1].Value.Trim(),
                            ItemValueGBP = decimal.TryParse(match.Groups[2].Value, out var price) ? price : 0m,
                            CanBeGivenFree = match.Groups[3].Value.Trim().Equals("YES", StringComparison.OrdinalIgnoreCase),
                            ReasoningForSelection = match.Groups[4].Value.Trim()
                        };
                        reward.RewardOptions.Add(option);
                    }
                }

                // Extract overall viability
                var viabilityMatch = Regex.Match(text, @"OVERALL_VIABILITY:\s*(YES|NO)", RegexOptions.IgnoreCase);
                var hasViableItems = viabilityMatch.Success && viabilityMatch.Groups[1].Value.Equals("YES", StringComparison.OrdinalIgnoreCase);

                // If no viable free items or no items found, set fallback discount
                if (!hasViableItems || !reward.RewardOptions.Any(r => r.CanBeGivenFree))
                {
                    reward.FallbackDiscount = new TierFallbackDiscount
                    {
                        DiscountPercentage = fallbackDiscount,
                        Description = $"Get {fallbackDiscount}% off your next purchase",
                        Reasoning = reward.RewardOptions.Any()
                            ? "Free items not viable for business profitability at this tier"
                            : "No specific menu items identified for free rewards"
                    };
                }

                // Extract reasoning
                var reasoningMatch = Regex.Match(text, @"REASONING:\s*(.+?)(?=\n|$)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (reasoningMatch.Success)
                {
                    reward.Reasoning = reasoningMatch.Groups[1].Value.Trim();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing tier response for {Tier}", tier);
                reward.FallbackDiscount = new TierFallbackDiscount
                {
                    DiscountPercentage = fallbackDiscount,
                    Description = $"Get {fallbackDiscount}% off your next purchase",
                    Reasoning = "Parsing error, using discount fallback"
                };
            }

            return reward;
        }

        private LoyaltyTierReward CreateFallbackTierReward(LoyaltyTier tier, int tokens, decimal discountPercentage)
        {
            return new LoyaltyTierReward
            {
                Tier = tier,
                RequiredTokens = tokens,
                RewardOptions = new List<TierRewardOption>(),
                FallbackDiscount = new TierFallbackDiscount
                {
                    DiscountPercentage = discountPercentage,
                    Description = $"Get {discountPercentage}% off your next purchase",
                    Reasoning = "Default tier discount (no specific items available)"
                },
                Reasoning = $"{tier} tier: {tokens} tokens for {discountPercentage}% discount"
            };
        }

        private string ExtractContent(GeminiResponse? response)
        {
            if (response?.Candidates == null || !response.Candidates.Any())
            {
                _logger.LogWarning("No candidates in Gemini response");
                return "No content found";
            }

            var textParts = response.Candidates
                .Where(c => c.Content?.Parts != null)
                .SelectMany(c => c.Content!.Parts)
                .Where(p => !string.IsNullOrEmpty(p.Text))
                .Select(p => p.Text)
                .ToList();

            if (!textParts.Any())
            {
                _logger.LogWarning("No text parts found in response");
                return "No content found";
            }

            return string.Join("\n", textParts);
        }

        // New optimized methods
        public async Task<ProductAnalysisResult> ExtractMenuWithGeoapifyDataAsync(PlaceDetails? placeDetails, string businessName, string category, string address)
        {
            try
            {
                _logger.LogInformation("Extracting menu with Geoapify data for: {BusinessName}", businessName);

                var prompt = $@"Business: {businessName}
Category: {category}
Address: {address}
{(placeDetails != null ? $"Website: {placeDetails.Website ?? "Not available"}\nPhone: {placeDetails.PhoneNumber ?? "Not available"}" : "")}

TASK: Extract menu items and prices for this business.
- Search web for actual menu items
- Get real prices in GBP (£)
- List ALL products with prices
- Focus on items suitable for welcome gifts (small, affordable)

Format:
PRODUCTS: Product1 - £X.XX, Product2 - £Y.YY
RECOMMENDED FREE: ProductName - £Z.ZZ";

                var response = await PerformGoogleSearchWithPromptAsync(prompt);
                return ParseProductAnalysisWithPricesFromText(response, 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting menu with Geoapify data");
                return new ProductAnalysisResult();
            }
        }

        public async Task<ProductAnalysisResult> GenerateUniqueFromSimilarBusinessAsync(string businessName, string category, string address, CompleteBusinessData similarBusiness, decimal minimumSpent)
        {
            try
            {
                _logger.LogInformation("Generating unique suggestions for {BusinessName} based on similar business {SimilarName}", 
                    businessName, similarBusiness.Business.BusinessName);

                var similarProducts = string.Join(", ", similarBusiness.Products.Select(p => $"{p.Name} (£{p.PriceGBP})"));
                var similarWelcomeGift = similarBusiness.WelcomeGift != null 
                    ? $"{similarBusiness.WelcomeGift.ItemName} (£{similarBusiness.WelcomeGift.ItemPriceGBP})" 
                    : "None";

                var bronzeDesc = GetTierDescription(similarBusiness.TierRewards.FirstOrDefault(t => t.Tier == LoyaltyTier.Bronze));
                var silverDesc = GetTierDescription(similarBusiness.TierRewards.FirstOrDefault(t => t.Tier == LoyaltyTier.Silver));
                var goldDesc = GetTierDescription(similarBusiness.TierRewards.FirstOrDefault(t => t.Tier == LoyaltyTier.Gold));

                var prompt = $@"You are creating a UNIQUE loyalty program for a NEW business.

NEW BUSINESS:
- Name: {businessName}
- Category: {category}
- Location: {address}
- Minimum Spend per Token: £{minimumSpent}

SIMILAR BUSINESS IN AREA (for reference):
- Name: {similarBusiness.Business.BusinessName}
- Location: {similarBusiness.Business.City}
- Products: {similarProducts}
- Welcome Gift: {similarWelcomeGift}
- Bronze Tier: {bronzeDesc}
- Silver Tier: {silverDesc}
- Gold Tier: {goldDesc}

TASK:
Based on the similar business data, create a UNIQUE loyalty program for ""{businessName}"" that:
1. Is DIFFERENT from {similarBusiness.Business.BusinessName} (don't copy exactly)
2. Is REALISTIC for a {category} in {address}
3. Has similar price ranges (appropriate for location)
4. Is appropriate for the category

Generate menu items and prices:
PRODUCTS: Product1 - £X.XX, Product2 - £Y.YY
RECOMMENDED FREE: ProductName - £Z.ZZ

Make it unique but realistic!";

                var response = await GeneratePromptAsync(prompt);
                return ParseProductAnalysisWithPricesFromText(response, minimumSpent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating unique from similar business");
                return new ProductAnalysisResult();
            }
        }

        public async Task<ProductAnalysisResult> GenerateCategoryBasedProductsAsync(string businessName, string category, string address, decimal minimumSpent)
        {
            try
            {
                _logger.LogInformation("Generating category-based products for: {BusinessName}", businessName);

                var prompt = $@"Create loyalty program for a NEW business.

Business: {businessName}
Category: {category}
Location: {address}
Minimum Spend per Token: £{minimumSpent}

TASK:
Based on typical {category} businesses in {address}, suggest:
- Typical menu items and prices (realistic for location)
- Welcome gift item
- Appropriate pricing for the area

Generate:
PRODUCTS: Product1 - £X.XX, Product2 - £Y.YY
RECOMMENDED FREE: ProductName - £Z.ZZ

Use realistic pricing for {address} area.";

                var response = await GeneratePromptAsync(prompt);
                return ParseProductAnalysisWithPricesFromText(response, minimumSpent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating category-based products");
                return new ProductAnalysisResult();
            }
        }

        public async Task<WelcomeGift> GenerateWelcomeGiftAsync(ProductAnalysisResult productAnalysis, ServiceAnalysisResult? serviceAnalysis, BusinessAttributes businessAttributes, decimal minimumSpent)
        {
            try
            {
                var products = string.Join(", ", productAnalysis.AllProducts.Select(p => $"{p.Name} (£{p.PriceGBP})"));
                
                var prompt = $@"Business: {businessAttributes.BusinessModel}
Available Products: {products}
Minimum Spend: £{minimumSpent}

TASK: Select the BEST welcome gift (truly free, no purchase required).
- Must be small/affordable (typically under £3-5)
- Must be from the available products
- Should create good first impression

Format:
SELECTED: ProductName - £X.XX
REASONING: [explain why]";

                var response = await GeneratePromptAsync(prompt);
                
                // Parse response
                var selectedMatch = System.Text.RegularExpressions.Regex.Match(response, @"SELECTED:\s*([^-]+)\s*-\s*£?([\d.]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                var reasoningMatch = System.Text.RegularExpressions.Regex.Match(response, @"REASONING:\s*(.+?)(?:\n|$)", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);

                if (selectedMatch.Success && decimal.TryParse(selectedMatch.Groups[2].Value, out var price))
                {
                    return new WelcomeGift
                    {
                        ItemName = selectedMatch.Groups[1].Value.Trim(),
                        ItemPriceGBP = price,
                        Description = $"Welcome! Get one {selectedMatch.Groups[1].Value.Trim()} absolutely FREE - no purchase required!",
                        Reasoning = reasoningMatch.Success ? reasoningMatch.Groups[1].Value.Trim() : "Selected as best welcome gift",
                        IsFree = true
                    };
                }

                // Fallback to first affordable product
                var affordableProduct = productAnalysis.AllProducts
                    .Where(p => p.PriceGBP <= 5)
                    .OrderBy(p => p.PriceGBP)
                    .FirstOrDefault();

                if (affordableProduct != null)
                {
                    return new WelcomeGift
                    {
                        ItemName = affordableProduct.Name,
                        ItemPriceGBP = affordableProduct.PriceGBP,
                        Description = $"Welcome! Get one {affordableProduct.Name} absolutely FREE!",
                        Reasoning = "Selected as affordable welcome gift",
                        IsFree = true
                    };
                }

                // Ultimate fallback
                return new WelcomeGift
                {
                    ItemName = "Welcome Gift",
                    ItemPriceGBP = 2.00m,
                    Description = "Welcome! Get a free welcome gift on your first visit!",
                    Reasoning = "Default welcome gift",
                    IsFree = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating welcome gift");
                return new WelcomeGift
                {
                    ItemName = "Welcome Gift",
                    ItemPriceGBP = 2.00m,
                    Description = "Welcome! Get a free welcome gift!",
                    Reasoning = "Default welcome gift",
                    IsFree = true
                };
            }
        }

        public async Task<LoyaltyTierAnalysis> GenerateAllTiersCombinedAsync(BusinessAttributes businessAttributes, ProductAnalysisResult? productAnalysis, ServiceAnalysisResult? serviceAnalysis, decimal minimumSpendForToken)
        {
            try
            {
                _logger.LogInformation("Generating all tiers combined. Min spend for 1 token: £{MinSpend}", minimumSpendForToken);

                var analysis = new LoyaltyTierAnalysis
                {
                    MinimumSpendForToken = minimumSpendForToken,
                    TierRewards = new List<LoyaltyTierReward>()
                };

                // Build available items
                var availableItems = new System.Text.StringBuilder();
                if (productAnalysis?.AllProducts?.Any() == true)
                {
                    availableItems.AppendLine("AVAILABLE PRODUCTS:");
                    foreach (var product in productAnalysis.AllProducts)
                    {
                        availableItems.AppendLine($"- {product.Name}: £{product.PriceGBP}");
                    }
                }

                if (serviceAnalysis?.AllServices?.Any() == true)
                {
                    availableItems.AppendLine("\nAVAILABLE SERVICES:");
                    foreach (var service in serviceAnalysis.AllServices)
                    {
                        availableItems.AppendLine($"- {service.Name}: £{service.PriceGBP}");
                    }
                }

                var hasItems = availableItems.Length > 0;
                analysis.HasFreeItemOptions = hasItems;

                // Combined prompt for all 3 tiers
                var prompt = $@"Business: {businessAttributes.BusinessModel}
Business Type: {businessAttributes.BusinessType}
Core Offerings: {businessAttributes.CoreProductsOrServices}
{(hasItems ? availableItems.ToString() : "NO SPECIFIC MENU ITEMS AVAILABLE")}

LOYALTY TIER SYSTEM:
- Customer earns 1 token per £{minimumSpendForToken} spent
- Bronze: 3 tokens (total spend: £{minimumSpendForToken * 3}) - Lower-value items (£3-£8)
- Silver: 5 tokens (total spend: £{minimumSpendForToken * 5}) - Medium-value items (£8-£15)
- Gold: 7 tokens (total spend: £{minimumSpendForToken * 7}) - Higher-value items (£15-£25)

TASK: Analyze ALL 3 tiers together and suggest rewards for each.

Format for EACH tier:
BRONZE_TIER:
ITEM: ItemName1 - £X.XX
CAN_BE_FREE: YES/NO
REASONING: [explanation]

SILVER_TIER:
ITEM: ItemName1 - £X.XX
CAN_BE_FREE: YES/NO
REASONING: [explanation]

GOLD_TIER:
ITEM: ItemName1 - £X.XX
CAN_BE_FREE: YES/NO
REASONING: [explanation]";

                var response = await GeneratePromptAsync(prompt);
                
                // Parse all tiers from response
                var bronzeReward = ParseTierFromCombinedResponse(response, LoyaltyTier.Bronze, 3, 10m);
                var silverReward = ParseTierFromCombinedResponse(response, LoyaltyTier.Silver, 5, 20m);
                var goldReward = ParseTierFromCombinedResponse(response, LoyaltyTier.Gold, 7, 40m);

                analysis.TierRewards.Add(bronzeReward);
                analysis.TierRewards.Add(silverReward);
                analysis.TierRewards.Add(goldReward);

                analysis.OverallStrategy = hasItems
                    ? "Token-based loyalty program with tier rewards (free items and discount fallbacks)"
                    : "Token-based loyalty program with tier-based discount rewards";

                return analysis;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating combined tiers");
                return CreateFallbackTierAnalysis(minimumSpendForToken);
            }
        }

        private LoyaltyTierReward ParseTierFromCombinedResponse(string text, LoyaltyTier tier, int tokens, decimal fallbackDiscount)
        {
            var tierName = tier.ToString().ToUpper() + "_TIER";
            var tierSection = System.Text.RegularExpressions.Regex.Match(text, $@"{tierName}:(.+?)(?=\w+_TIER:|$)", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);

            if (!tierSection.Success)
            {
                return CreateFallbackTierReward(tier, tokens, fallbackDiscount);
            }

            var sectionText = tierSection.Groups[1].Value;
            var reward = new LoyaltyTierReward
            {
                Tier = tier,
                RequiredTokens = tokens,
                RewardOptions = new List<TierRewardOption>()
            };

            var itemMatches = System.Text.RegularExpressions.Regex.Matches(sectionText, 
                @"ITEM:\s*([^-£\n]+)\s*-\s*£?([\d.]+).*?CAN_BE_FREE:\s*(YES|NO).*?REASONING:\s*(.+?)(?=ITEM:|$)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);

            foreach (System.Text.RegularExpressions.Match match in itemMatches)
            {
                if (decimal.TryParse(match.Groups[2].Value, out var price))
                {
                    reward.RewardOptions.Add(new TierRewardOption
                    {
                        ItemName = match.Groups[1].Value.Trim(),
                        ItemValueGBP = price,
                        CanBeGivenFree = match.Groups[3].Value.Trim().Equals("YES", StringComparison.OrdinalIgnoreCase),
                        ReasoningForSelection = match.Groups[4].Value.Trim()
                    });
                }
            }

            if (!reward.RewardOptions.Any(r => r.CanBeGivenFree))
            {
                reward.FallbackDiscount = new TierFallbackDiscount
                {
                    DiscountPercentage = fallbackDiscount,
                    Description = $"Get {fallbackDiscount}% off your next purchase",
                    Reasoning = "Free items not viable for business profitability"
                };
            }

            return reward;
        }

        private LoyaltyTierAnalysis CreateFallbackTierAnalysis(decimal minimumSpendForToken)
        {
            return new LoyaltyTierAnalysis
            {
                MinimumSpendForToken = minimumSpendForToken,
                HasFreeItemOptions = false,
                OverallStrategy = "Discount-based loyalty program (fallback)",
                TierRewards = new List<LoyaltyTierReward>
                {
                    CreateFallbackTierReward(LoyaltyTier.Bronze, 3, 10m),
                    CreateFallbackTierReward(LoyaltyTier.Silver, 5, 20m),
                    CreateFallbackTierReward(LoyaltyTier.Gold, 7, 40m)
                }
            };
        }

        private LoyaltyTierReward CreateFallbackTierReward(LoyaltyTier tier, int tokens, decimal discountPercentage)
        {
            return new LoyaltyTierReward
            {
                Tier = tier,
                RequiredTokens = tokens,
                RewardOptions = new List<TierRewardOption>(),
                FallbackDiscount = new TierFallbackDiscount
                {
                    DiscountPercentage = discountPercentage,
                    Description = $"Get {discountPercentage}% off your next purchase",
                    Reasoning = "Default tier discount"
                },
                Reasoning = $"{tier} tier: {tokens} tokens for {discountPercentage}% discount"
            };
        }

        private string GetTierDescription(TierReward? tierReward)
        {
            if (tierReward == null) return "None";
            if (tierReward.RewardOptions.Any())
            {
                return string.Join(", ", tierReward.RewardOptions.Select(r => $"{r.ItemName} (£{r.ItemValueGBP})"));
            }
            if (tierReward.FallbackDiscount != null)
            {
                return $"{tierReward.FallbackDiscount.DiscountPercentage}% discount";
            }
            return "None";
        }
    }
}
