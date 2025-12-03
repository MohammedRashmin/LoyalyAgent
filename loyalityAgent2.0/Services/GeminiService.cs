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

        public async Task<BusinessAttributes> ExtractBusinessAttributesAsync(string businessName, string category, string fullAddress, PlaceDetails? placeDetails = null)
        {
            try
            {
                _logger.LogInformation("Extracting business attributes for: {BusinessName}", businessName);

                // Build prompt with Geoapify data if available (for faster, more accurate search)
                var geoapifyContext = "";
                if (placeDetails != null)
                {
                    var categoriesList = placeDetails.Categories != null && placeDetails.Categories.Any() 
                        ? string.Join(", ", placeDetails.Categories) 
                        : category;
                    
                    geoapifyContext = $@"
CONFIRMED BUSINESS DATA (from Geoapify):
- Website: {placeDetails.Website ?? "Not available"}
- Phone: {placeDetails.PhoneNumber ?? "Not available"}
- Categories: {categoriesList}
- Location: {placeDetails.FormattedAddress ?? fullAddress}
- Coordinates: {placeDetails.Latitude}, {placeDetails.Longitude}

{(string.IsNullOrEmpty(placeDetails.Website) ? "" : $"IMPORTANT: Search this specific website first: {placeDetails.Website}")}
";
                }

                // Single search query with Google Search grounding
                var searchPrompt = $@"Search the web for information about: {businessName} {category} {fullAddress}
{geoapifyContext}
Tell me about this business:
- What is their business model?
- What products or services do they sell/offer?
- Who are their target customers?
- What is their brand style (casual, luxury, professional, etc.)?
- How big/popular are they (local shop, regional chain, national/international brand)?
- What makes them special or unique?
- Do they primarily sell PRODUCTS, provide SERVICES, or both (HYBRID)?

{(string.IsNullOrEmpty(geoapifyContext) ? "Give me real, specific information about this business based on web search." : "Use the confirmed website and data above to get accurate information. Give me real, specific information about this business.")}";

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
Minimum Spend Per Token: £{minimumSpent}

TASK: Suggest a WELCOME GIFT - a truly FREE item given to new customers with NO purchase required.

IMPORTANT:
- ONLY suggest products that are ACTUALLY on this business's menu/offerings
- DO NOT suggest generic, random, or common items
- If you cannot identify specific products from their actual menu, respond with: NO MENU ITEMS FOUND

WELCOME GIFT CRITERIA:
- CRITICAL: Price MUST be ≤ £{minimumSpent} (the minimum spend per token)
  - This is a truly FREE gift (no purchase required)
  - Business cannot afford items worth more than what a customer typically spends in one visit
  - If minimum spend is £{minimumSpent}, welcome gift MUST be ≤ £{minimumSpent}
- Ideally should be small/affordable (typically £3-5 or less) so business can give it free
- Must be from their ACTUAL menu
- Should be representative of their business (e.g., coffee shop → small coffee/pastry)
- Should create good first impression without significant cost to business
- DO NOT suggest items above £{minimumSpent} - they are not sustainable as free welcome gifts

If you can find specific menu items:
List ALL products with ESTIMATED prices in GBP (£).
Find the SMALLEST/MOST AFFORDABLE items suitable as free welcome gifts (MUST be ≤ £{minimumSpent}).
Recommend ONE small, affordable product from their ACTUAL MENU to give as WELCOME GIFT (no purchase required).

Format:
PRODUCTS: Product1 - £X.XX, Product2 - £Y.YY
POPULAR: [list]
SMALL ITEMS ≤ £{minimumSpent}: Product A - £X.XX, Product B - £Y.YY
RECOMMENDED WELCOME GIFT: ProductName - £Z.ZZ (MUST be ≤ £{minimumSpent})";

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
                
                // Validate API key - don't use placeholder values
                if (string.IsNullOrEmpty(apiKey) || 
                    apiKey.Contains("your-gemini-api-key-here", StringComparison.OrdinalIgnoreCase) ||
                    apiKey.Contains("your_api_key", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogError("Invalid or missing Gemini API key. UserApiKey set: {HasUserKey}, ConfigKey: {ConfigKey}", 
                        !string.IsNullOrEmpty(_userApiKey), 
                        !string.IsNullOrEmpty(_configuration["Gemini:ApiKey"]));
                    throw new InvalidOperationException("Gemini API key is not configured. Please set a valid API key.");
                }
                
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
                
                // Validate API key - don't use placeholder values
                if (string.IsNullOrEmpty(apiKey) || 
                    apiKey.Contains("your-gemini-api-key-here", StringComparison.OrdinalIgnoreCase) ||
                    apiKey.Contains("your_api_key", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogError("Invalid or missing Gemini API key for Google Search. Cannot proceed without valid key.");
                    throw new InvalidOperationException("Gemini API key is not configured. Please set a valid API key.");
                }

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
                            google_search = new { }
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

                // STEP 1: Fast Path - Google Search (5-8s) - Using Geoapify data for targeted search
                var geoapifyInfo = "";
                if (placeDetails != null)
                {
                    var categoriesList = placeDetails.Categories != null && placeDetails.Categories.Any() 
                        ? string.Join(", ", placeDetails.Categories) 
                        : category;
                    
                    geoapifyInfo = $@"
CONFIRMED BUSINESS DATA (from Geoapify):
- Website: {placeDetails.Website ?? "Not available"}
- Phone: {placeDetails.PhoneNumber ?? "Not available"}
- Categories: {categoriesList}
- Location: {placeDetails.FormattedAddress ?? address}

{(string.IsNullOrEmpty(placeDetails.Website) ? "" : $"IMPORTANT: Search this specific website first: {placeDetails.Website}")}
";
                }
                
                var prompt = $@"Business: {businessName}
Category: {category}
Address: {address}
{geoapifyInfo}
TASK: Search Google for menu items and prices for this business.
{(string.IsNullOrEmpty(placeDetails?.Website) ? "- Search web for actual menu items" : $"- Search this specific website: {placeDetails.Website}")}
- Get real prices in GBP (£)
- List ALL products with prices
- Focus on items suitable for welcome gifts (small, affordable)

Format:
PRODUCTS: Product1 - £X.XX, Product2 - £Y.YY
RECOMMENDED FREE: ProductName - £Z.ZZ";

                var response = await PerformGoogleSearchWithPromptAsync(prompt);
                var productAnalysis = ParseProductAnalysisWithPricesFromText(response, 0);

                // Check if we got sufficient menu items (at least 3 items)
                if (productAnalysis.AllProducts.Count >= 3)
                {
                    _logger.LogInformation("Sufficient menu items found via Google Search: {Count}", 
                        productAnalysis.AllProducts.Count);
                    return productAnalysis; // Fast path success ✅
                }

                // STEP 2: Enhanced Path - Multi-platform search (only if needed)
                _logger.LogInformation("Insufficient menu items ({Count}), searching Uber/TripAdvisor/Google...", 
                    productAnalysis.AllProducts.Count);

                var enhancedGeoapifyInfo = "";
                if (placeDetails != null)
                {
                    var categoriesList = placeDetails.Categories != null && placeDetails.Categories.Any() 
                        ? string.Join(", ", placeDetails.Categories) 
                        : category;
                    
                    enhancedGeoapifyInfo = $@"
CONFIRMED BUSINESS DATA (from Geoapify):
- Website: {placeDetails.Website ?? "Not available"}
- Phone: {placeDetails.PhoneNumber ?? "Not available"}
- Categories: {categoriesList}
- Location: {placeDetails.FormattedAddress ?? address}

{(string.IsNullOrEmpty(placeDetails.Website) ? "" : $"IMPORTANT: Start with this website: {placeDetails.Website}")}
";
                }
                
                var enhancedPrompt = $@"Business: {businessName}
Category: {category}
Address: {address}
{enhancedGeoapifyInfo}
TASK: Search Google, Uber Eats, and TripAdvisor for menu items.
{(string.IsNullOrEmpty(placeDetails?.Website) ? "- Search Google Business/Google Maps" : $"- First search this website: {placeDetails.Website}")}
- Search Uber Eats (if food business)
- Search TripAdvisor restaurant page
- Extract ALL products with prices in GBP (£)
- Get comprehensive menu from multiple sources

Format:
PRODUCTS: Product1 - £X.XX, Product2 - £Y.YY
RECOMMENDED FREE: ProductName - £Z.ZZ";

                var enhancedResponse = await PerformGoogleSearchWithPromptAsync(enhancedPrompt);
                var enhancedAnalysis = ParseProductAnalysisWithPricesFromText(enhancedResponse, 0);

                // Use enhanced if better, otherwise use original
                if (enhancedAnalysis.AllProducts.Count > productAnalysis.AllProducts.Count)
                {
                    _logger.LogInformation("Enhanced search found more items: {Count} vs {OriginalCount}", 
                        enhancedAnalysis.AllProducts.Count, productAnalysis.AllProducts.Count);
                    return enhancedAnalysis;
                }

                return productAnalysis;
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

        public async Task<WelcomeGiftResponse> GenerateWelcomeGiftAsync(ProductAnalysisResult productAnalysis, ServiceAnalysisResult? serviceAnalysis, BusinessAttributes businessAttributes, decimal minimumSpent)
        {
            try
            {
                var products = string.Join(", ", productAnalysis.AllProducts.Select(p => $"{p.Name} (£{p.PriceGBP})"));
                
                var prompt = $@"Business: {businessAttributes.BusinessModel}
Available Products: {products}
Minimum Spend Per Token: £{minimumSpent}

CRITICAL CONSTRAINT: Welcome gift price MUST be ≤ £{minimumSpent} (the minimum spend per token).
- This is a truly FREE gift (no purchase required)
- Business cannot afford to give away items worth more than what a customer typically spends in one visit
- If minimum spend is £{minimumSpent}, welcome gift should be ≤ £{minimumSpent} (ideally much less, like £3-5)

TASK: Select the BEST welcome gift (truly free, no purchase required).
- MUST be ≤ £{minimumSpent} (this is REQUIRED, not optional)
- Ideally should be small/affordable (typically £3-5 or less)
- Must be from the available products
- Should create good first impression
- If no products are ≤ £{minimumSpent}, select the cheapest available product

Format:
SELECTED: ProductName - £X.XX (MUST be ≤ £{minimumSpent})
REASONING: [explain why, including why the price is affordable for the business]";

                var response = await GeneratePromptAsync(prompt);
                
                // Parse response
                var selectedMatch = System.Text.RegularExpressions.Regex.Match(response, @"SELECTED:\s*([^-]+)\s*-\s*£?([\d.]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                var reasoningMatch = System.Text.RegularExpressions.Regex.Match(response, @"REASONING:\s*(.+?)(?:\n|$)", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);

                if (selectedMatch.Success && decimal.TryParse(selectedMatch.Groups[2].Value, out var price))
                {
                    // Validate price doesn't exceed minimum spend
                    if (price > minimumSpent)
                    {
                        _logger.LogWarning("AI suggested welcome gift price £{Price} exceeds minimum spend £{MinSpend}. Finding cheaper alternative.", 
                            price, minimumSpent);
                    }
                    else
                    {
                        return new WelcomeGiftResponse
                        {
                            ItemName = selectedMatch.Groups[1].Value.Trim(),
                            ItemPriceGBP = price,
                            Description = $"Welcome! Get one {selectedMatch.Groups[1].Value.Trim()} absolutely FREE - no purchase required!",
                            Reasoning = reasoningMatch.Success ? reasoningMatch.Groups[1].Value.Trim() : "Selected as best welcome gift",
                            IsFree = true
                        };
                    }
                }

                // Fallback: Find affordable product that doesn't exceed minimum spend
                var affordableProduct = productAnalysis.AllProducts
                    .Where(p => p.PriceGBP <= minimumSpent) // Must be ≤ minimum spend
                    .OrderBy(p => p.PriceGBP)
                    .FirstOrDefault();
                
                // If no product is ≤ minimum spend, get the cheapest available (but log warning)
                if (affordableProduct == null)
                {
                    affordableProduct = productAnalysis.AllProducts
                        .OrderBy(p => p.PriceGBP)
                        .FirstOrDefault();
                    
                    if (affordableProduct != null && affordableProduct.PriceGBP > minimumSpent)
                    {
                        _logger.LogWarning("No products found ≤ minimum spend £{MinSpend}. Using cheapest available: £{Price}. This may not be sustainable.", 
                            minimumSpent, affordableProduct.PriceGBP);
                    }
                }

                if (affordableProduct != null)
                {
                    return new WelcomeGiftResponse
                    {
                        ItemName = affordableProduct.Name,
                        ItemPriceGBP = affordableProduct.PriceGBP,
                        Description = $"Welcome! Get one {affordableProduct.Name} absolutely FREE!",
                        Reasoning = "Selected as affordable welcome gift",
                        IsFree = true
                    };
                }

                // Ultimate fallback: Use a default price that's affordable (min of £2 or 20% of minimum spend)
                var defaultPrice = Math.Min(2.00m, minimumSpent * 0.2m);
                if (defaultPrice < 0.50m) defaultPrice = 0.50m; // Minimum £0.50
                
                return new WelcomeGiftResponse
                {
                    ItemName = "Welcome Gift",
                    ItemPriceGBP = defaultPrice,
                    Description = "Welcome! Get a free welcome gift on your first visit!",
                    Reasoning = $"Default welcome gift (affordable at £{defaultPrice:F2}, well below minimum spend of £{minimumSpent})",
                    IsFree = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating welcome gift");
                // Default fallback: Use affordable price (min of £2 or 20% of minimum spend)
                var defaultPrice = Math.Min(2.00m, minimumSpent * 0.2m);
                if (defaultPrice < 0.50m) defaultPrice = 0.50m; // Minimum £0.50
                
                return new WelcomeGiftResponse
                {
                    ItemName = "Welcome Gift",
                    ItemPriceGBP = defaultPrice,
                    Description = "Welcome! Get a free welcome gift!",
                    Reasoning = $"Default welcome gift (affordable at £{defaultPrice:F2}, well below minimum spend of £{minimumSpent})",
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
- CRITICAL PROFIT REQUIREMENT: Business must make minimum 75% profit margin on each tier reward
- Token count calculation: Token Count = Ceiling((Product Price × 4) ÷ £{minimumSpendForToken})
  - This ensures: Total Customer Spend = Tokens × £{minimumSpendForToken}
  - Business Profit = Total Customer Spend - Product Price
  - Profit Margin = (Profit ÷ Total Spend) × 100, must be ≥ 75%
  - Formula explanation: For 75% margin, Total Spend = Product Price × 4 (so profit = 3× product price)

PROFIT CALCULATION EXAMPLES:
- Product £10, Min Spend £25: Tokens = Ceiling(£40 ÷ £25) = 2 tokens → Customer spends £50, Profit = £40 (80% margin) ✓
- Product £15, Min Spend £25: Tokens = Ceiling(£60 ÷ £25) = 3 tokens → Customer spends £75, Profit = £60 (80% margin) ✓
- Product £20, Min Spend £25: Tokens = Ceiling(£80 ÷ £25) = 4 tokens → Customer spends £100, Profit = £80 (80% margin) ✓

TASK: 
1. For each tier, select a product from the available menu
2. Calculate the REQUIRED token count based on: Ceiling((Product Price × 4) ÷ £{minimumSpendForToken})
3. Verify profit margin is ≥ 75% (if not, adjust product selection or token count)
4. Ensure token counts are progressive: Bronze < Silver < Gold (all ≤ 10)

IMPORTANT TOKEN COUNT GUIDELINES:
- DO NOT use the same token counts for every business - vary them based on the business analysis
- Bronze: Should be accessible (typically 2-4 tokens, but can be 1-5) - for customers who visit occasionally
- Silver: Should be moderate (typically 4-7 tokens, but can be 3-8) - for regular customers  
- Gold: Should be premium (typically 7-10 tokens, but can be 5-10) - for loyal, frequent customers
- All token counts MUST be 10 or less
- Token counts should be progressive (Bronze < Silver < Gold)
- Consider: For expensive businesses (high average item price), use lower token counts. For affordable businesses, use higher token counts.
- Consider: For frequent-visit businesses (coffee shops), use lower token counts. For occasional-visit businesses (restaurants), use higher token counts.
- VARY THE TOKEN COUNTS - do not always use 3, 5, 7 or 3, 6, 9. Think about what makes sense for THIS specific business.

CRITICAL: You MUST provide token counts. This is REQUIRED, not optional.

Format for response (YOU MUST FOLLOW THIS EXACT FORMAT):
BRONZE_TIER:
ITEM: ItemName1 - £X.XX
CALCULATED_TOKENS: [calculate: Ceiling((Item Price × 4) ÷ £{minimumSpendForToken})]
TOTAL_CUSTOMER_SPEND: [Tokens × £{minimumSpendForToken}]
PROFIT: [Total Spend - Item Price]
PROFIT_MARGIN: [Profit ÷ Total Spend × 100]% (must be ≥ 75%)
CAN_BE_FREE: YES/NO
REASONING: [explanation including profit calculation and why this works for the business]

SILVER_TIER:
ITEM: ItemName1 - £X.XX
CALCULATED_TOKENS: [calculate: Ceiling((Item Price × 4) ÷ £{minimumSpendForToken}), must be > Bronze tokens]
TOTAL_CUSTOMER_SPEND: [Tokens × £{minimumSpendForToken}]
PROFIT: [Total Spend - Item Price]
PROFIT_MARGIN: [Profit ÷ Total Spend × 100]% (must be ≥ 75%)
CAN_BE_FREE: YES/NO
REASONING: [explanation including profit calculation and why this works for the business]

GOLD_TIER:
ITEM: ItemName1 - £X.XX
CALCULATED_TOKENS: [calculate: Ceiling((Item Price × 4) ÷ £{minimumSpendForToken}), must be > Silver tokens]
TOTAL_CUSTOMER_SPEND: [Tokens × £{minimumSpendForToken}]
PROFIT: [Total Spend - Item Price]
PROFIT_MARGIN: [Profit ÷ Total Spend × 100]% (must be ≥ 75%)
CAN_BE_FREE: YES/NO
REASONING: [explanation including profit calculation and why this works for the business]";

                var response = await GeneratePromptAsync(prompt);
                
                // Log the raw response for debugging
                _logger.LogInformation("Raw AI response for token counts (first 1000 chars): {Response}", 
                    response.Substring(0, Math.Min(1000, response.Length)));
                
                // Parse token counts from response first - prioritize CALCULATED_TOKENS from tier sections
                var bronzeTokensMatch = Regex.Match(response, @"BRONZE_TIER:.*?CALCULATED_TOKENS:\s*(\d+)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                var silverTokensMatch = Regex.Match(response, @"SILVER_TIER:.*?CALCULATED_TOKENS:\s*(\d+)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                var goldTokensMatch = Regex.Match(response, @"GOLD_TIER:.*?CALCULATED_TOKENS:\s*(\d+)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                
                var bronzeTokens = bronzeTokensMatch.Success && int.TryParse(bronzeTokensMatch.Groups[1].Value, out var b) ? b : 0;
                var silverTokens = silverTokensMatch.Success && int.TryParse(silverTokensMatch.Groups[1].Value, out var s) ? s : 0;
                var goldTokens = goldTokensMatch.Success && int.TryParse(goldTokensMatch.Groups[1].Value, out var g) ? g : 0;
                
                // If not found in new format, try old parsing method
                if (bronzeTokens == 0 || silverTokens == 0 || goldTokens == 0)
                {
                    var parsed = ParseTokenCountsFromResponse(response);
                    if (bronzeTokens == 0) bronzeTokens = parsed.bronzeTokens;
                    if (silverTokens == 0) silverTokens = parsed.silverTokens;
                    if (goldTokens == 0) goldTokens = parsed.goldTokens;
                }
                
                // Validate token counts (must be ≤ 10 and progressive)
                // Only validate if we actually got values from AI, otherwise log warning
                if (bronzeTokens == 0 || silverTokens == 0 || goldTokens == 0)
                {
                    _logger.LogWarning("AI did not provide token counts in expected format. Response snippet: {Snippet}", 
                        response.Substring(0, Math.Min(500, response.Length)));
                }
                
                bronzeTokens = Math.Min(Math.Max(bronzeTokens, 1), 10); // Ensure 1-10
                silverTokens = Math.Min(Math.Max(silverTokens, bronzeTokens + 1), 10); // Must be > Bronze, max 10
                goldTokens = Math.Min(Math.Max(goldTokens, silverTokens + 1), 10); // Must be > Silver, max 10
                
                _logger.LogInformation("Final token counts after validation - Bronze: {Bronze}, Silver: {Silver}, Gold: {Gold}", 
                    bronzeTokens, silverTokens, goldTokens);
                
                // Parse all tiers from response with AI-suggested token counts
                // If tokens are still 0 or invalid, calculate based on typical product prices
                if (bronzeTokens == 0 || bronzeTokens > 10) bronzeTokens = 3; // Default for bronze
                if (silverTokens == 0 || silverTokens > 10 || silverTokens <= bronzeTokens) silverTokens = bronzeTokens + 2;
                if (goldTokens == 0 || goldTokens > 10 || goldTokens <= silverTokens) goldTokens = silverTokens + 2;
                
                // Ensure all are within 10
                bronzeTokens = Math.Min(bronzeTokens, 10);
                silverTokens = Math.Min(silverTokens, 10);
                goldTokens = Math.Min(goldTokens, 10);
                
                _logger.LogInformation("Using token counts for parsing - Bronze: {Bronze}, Silver: {Silver}, Gold: {Gold}", 
                    bronzeTokens, silverTokens, goldTokens);
                
                var bronzeReward = ParseTierFromCombinedResponse(response, LoyaltyTier.Bronze, bronzeTokens, 10m, minimumSpendForToken);
                var silverReward = ParseTierFromCombinedResponse(response, LoyaltyTier.Silver, silverTokens, 20m, minimumSpendForToken);
                var goldReward = ParseTierFromCombinedResponse(response, LoyaltyTier.Gold, goldTokens, 40m, minimumSpendForToken);
                
                // After parsing, ALWAYS recalculate tokens based on actual product prices
                // This ensures tokens are correct even if AI didn't provide them
                foreach (var reward in new[] { bronzeReward, silverReward, goldReward })
                {
                    var freeItems = reward.RewardOptions.Where(o => o.CanBeGivenFree).ToList();
                    if (freeItems.Any())
                    {
                        // Use the first free item to calculate tokens (or average if multiple)
                        var avgPrice = freeItems.Average(i => i.ItemValueGBP);
                        
                        // Calculate tokens based on product price: Ceiling((Price × 2) ÷ MinSpend)
                        // This ensures 75% minimum profit margin (Total Spend = Price × 4, Profit = Price × 3)
                        var calculatedTokens = (int)Math.Ceiling((avgPrice * 4) / minimumSpendForToken);
                        calculatedTokens = Math.Min(Math.Max(calculatedTokens, 1), 10);
                        
                        _logger.LogInformation("Recalculating tokens for {Tier} based on product price £{Price}: {OldTokens} -> {NewTokens}", 
                            reward.Tier, avgPrice, reward.RequiredTokens, calculatedTokens);
                        
                        reward.RequiredTokens = calculatedTokens;
                        
                        // Recalculate profit for ALL items in this tier with correct tokens
                        foreach (var item in freeItems)
                        {
                            // Always recalculate based on the tier's token count
                            item.TotalCustomerSpend = calculatedTokens * minimumSpendForToken;
                            item.BusinessProfit = item.TotalCustomerSpend - item.ItemValueGBP;
                            item.ProfitMarginPercentage = item.TotalCustomerSpend > 0 ? (item.BusinessProfit / item.TotalCustomerSpend) * 100 : 0;
                            
                            var visitsText = calculatedTokens == 1 ? "visit" : "visits";
                            var tokensText = calculatedTokens == 1 ? "token" : "tokens";
                            item.ProfitJustification = $"Customer needs {calculatedTokens} {visitsText} (spending £{item.TotalCustomerSpend:F2} total at £{minimumSpendForToken:F2} per visit) to earn {calculatedTokens} {tokensText}, then gets free {item.ItemName} (£{item.ItemValueGBP:F2}). Business profit: £{item.BusinessProfit:F2} ({item.ProfitMarginPercentage:F1}% margin).";
                        }
                    }
                }
                
                // Ensure tiers are progressive (Bronze < Silver < Gold)
                if (bronzeReward.RequiredTokens >= silverReward.RequiredTokens)
                {
                    silverReward.RequiredTokens = Math.Min(bronzeReward.RequiredTokens + 1, 10);
                    _logger.LogInformation("Adjusted Silver tokens to be > Bronze: {Tokens}", silverReward.RequiredTokens);
                }
                if (silverReward.RequiredTokens >= goldReward.RequiredTokens)
                {
                    goldReward.RequiredTokens = Math.Min(silverReward.RequiredTokens + 1, 10);
                    _logger.LogInformation("Adjusted Gold tokens to be > Silver: {Tokens}", goldReward.RequiredTokens);
                }
                
                // Recalculate profits again after ensuring progression
                foreach (var reward in new[] { bronzeReward, silverReward, goldReward })
                {
                    foreach (var item in reward.RewardOptions.Where(o => o.CanBeGivenFree))
                    {
                        item.TotalCustomerSpend = reward.RequiredTokens * minimumSpendForToken;
                        item.BusinessProfit = item.TotalCustomerSpend - item.ItemValueGBP;
                        item.ProfitMarginPercentage = item.TotalCustomerSpend > 0 ? (item.BusinessProfit / item.TotalCustomerSpend) * 100 : 0;
                        
                        var visitsText = reward.RequiredTokens == 1 ? "visit" : "visits";
                        var tokensText = reward.RequiredTokens == 1 ? "token" : "tokens";
                        item.ProfitJustification = $"Customer needs {reward.RequiredTokens} {visitsText} (spending £{item.TotalCustomerSpend:F2} total at £{minimumSpendForToken:F2} per visit) to earn {reward.RequiredTokens} {tokensText}, then gets free {item.ItemName} (£{item.ItemValueGBP:F2}). Business profit: £{item.BusinessProfit:F2} ({item.ProfitMarginPercentage:F1}% margin).";
                    }
                }

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

        private (int bronzeTokens, int silverTokens, int goldTokens) ParseTokenCountsFromResponse(string text)
        {
            _logger.LogInformation("Parsing token counts from AI response. Response length: {Length}", text.Length);
            
            // Try multiple patterns to find token counts
            
            // Pattern 1: TOKEN_COUNTS section with explicit labels
            var tokenCountsMatch = Regex.Match(text, 
                @"TOKEN_COUNTS:.*?BRONZE_TOKENS:\s*(\d+).*?SILVER_TOKENS:\s*(\d+).*?GOLD_TOKENS:\s*(\d+)",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            
            if (tokenCountsMatch.Success && 
                int.TryParse(tokenCountsMatch.Groups[1].Value, out var bronze) &&
                int.TryParse(tokenCountsMatch.Groups[2].Value, out var silver) &&
                int.TryParse(tokenCountsMatch.Groups[3].Value, out var gold))
            {
                _logger.LogInformation("Found token counts in TOKEN_COUNTS section: Bronze={Bronze}, Silver={Silver}, Gold={Gold}", bronze, silver, gold);
                return (bronze, silver, gold);
            }
            
            // Pattern 2: Individual tier sections with CALCULATED_TOKENS or TOKENS field
            var bronzeMatch = Regex.Match(text, @"BRONZE_TIER:.*?(?:CALCULATED_TOKENS|TOKENS):\s*(\d+)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            var silverMatch = Regex.Match(text, @"SILVER_TIER:.*?(?:CALCULATED_TOKENS|TOKENS):\s*(\d+)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            var goldMatch = Regex.Match(text, @"GOLD_TIER:.*?(?:CALCULATED_TOKENS|TOKENS):\s*(\d+)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            
            var bronzeTokens = bronzeMatch.Success && int.TryParse(bronzeMatch.Groups[1].Value, out var b) ? b : 0;
            var silverTokens = silverMatch.Success && int.TryParse(silverMatch.Groups[1].Value, out var s) ? s : 0;
            var goldTokens = goldMatch.Success && int.TryParse(goldMatch.Groups[1].Value, out var g) ? g : 0;
            
            if (bronzeTokens > 0 || silverTokens > 0 || goldTokens > 0)
            {
                _logger.LogInformation("Found token counts in tier sections: Bronze={Bronze}, Silver={Silver}, Gold={Gold}", 
                    bronzeTokens > 0 ? bronzeTokens : 0, silverTokens > 0 ? silverTokens : 0, goldTokens > 0 ? goldTokens : 0);
            }
            
            // Pattern 3: Look for any numbers near "Bronze", "Silver", "Gold" and "token"
            if (bronzeTokens == 0 || silverTokens == 0 || goldTokens == 0)
            {
                var bronzePattern = Regex.Match(text, @"(?:Bronze|BRONZE).*?(?:token|Token).*?(\d+)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                var silverPattern = Regex.Match(text, @"(?:Silver|SILVER).*?(?:token|Token).*?(\d+)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                var goldPattern = Regex.Match(text, @"(?:Gold|GOLD).*?(?:token|Token).*?(\d+)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                
                if (bronzeTokens == 0 && bronzePattern.Success && int.TryParse(bronzePattern.Groups[1].Value, out var b2))
                {
                    bronzeTokens = b2;
                    _logger.LogInformation("Found Bronze tokens via pattern matching: {Tokens}", bronzeTokens);
                }
                if (silverTokens == 0 && silverPattern.Success && int.TryParse(silverPattern.Groups[1].Value, out var s2))
                {
                    silverTokens = s2;
                    _logger.LogInformation("Found Silver tokens via pattern matching: {Tokens}", silverTokens);
                }
                if (goldTokens == 0 && goldPattern.Success && int.TryParse(goldPattern.Groups[1].Value, out var g2))
                {
                    goldTokens = g2;
                    _logger.LogInformation("Found Gold tokens via pattern matching: {Tokens}", goldTokens);
                }
            }
            
            // If we still don't have all tokens, use defaults but log a warning
            if (bronzeTokens == 0) bronzeTokens = 3;
            if (silverTokens == 0) silverTokens = 5;
            if (goldTokens == 0) goldTokens = 7;
            
            if (bronzeTokens == 3 && silverTokens == 5 && goldTokens == 7)
            {
                _logger.LogWarning("Using DEFAULT token counts (3, 5, 7) - AI did not provide token suggestions. Response snippet: {Snippet}", 
                    text.Substring(0, Math.Min(500, text.Length)));
            }
            
            return (bronzeTokens, silverTokens, goldTokens);
        }

        private LoyaltyTierReward ParseTierFromCombinedResponse(string text, LoyaltyTier tier, int tokens, decimal fallbackDiscount, decimal minimumSpendForToken)
        {
            var tierName = tier.ToString().ToUpper() + "_TIER";
            var tierSection = System.Text.RegularExpressions.Regex.Match(text, $@"{tierName}:(.+?)(?=\w+_TIER:|$)", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);

            if (!tierSection.Success)
            {
                return CreateFallbackTierReward(tier, tokens, fallbackDiscount);
            }

            var sectionText = tierSection.Groups[1].Value;
            
            // Try to extract token count from the tier section if present (CALCULATED_TOKENS or TOKENS)
            var tokenMatch = Regex.Match(sectionText, @"(?:CALCULATED_TOKENS|TOKENS):\s*(\d+)", RegexOptions.IgnoreCase);
            if (tokenMatch.Success && int.TryParse(tokenMatch.Groups[1].Value, out var extractedTokens))
            {
                tokens = Math.Min(Math.Max(extractedTokens, 1), 10); // Ensure 1-10
                _logger.LogInformation("Extracted token count for {Tier}: {Tokens}", tier, tokens);
            }
            
            var reward = new LoyaltyTierReward
            {
                Tier = tier,
                RequiredTokens = tokens,
                RewardOptions = new List<TierRewardOption>()
            };

            // Try to extract profit information from the tier section
            var calculatedTokensMatch = Regex.Match(sectionText, @"CALCULATED_TOKENS:\s*(\d+)", RegexOptions.IgnoreCase);
            var totalSpendMatch = Regex.Match(sectionText, @"TOTAL_CUSTOMER_SPEND:\s*£?([\d.]+)", RegexOptions.IgnoreCase);
            var profitMatch = Regex.Match(sectionText, @"PROFIT:\s*£?([\d.]+)", RegexOptions.IgnoreCase);
            var profitMarginMatch = Regex.Match(sectionText, @"PROFIT_MARGIN:\s*([\d.]+)%?", RegexOptions.IgnoreCase);

            var itemMatches = System.Text.RegularExpressions.Regex.Matches(sectionText, 
                @"ITEM:\s*([^-£\n]+)\s*-\s*£?([\d.]+).*?CAN_BE_FREE:\s*(YES|NO).*?REASONING:\s*(.+?)(?=ITEM:|$)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);

            foreach (System.Text.RegularExpressions.Match match in itemMatches)
            {
                if (decimal.TryParse(match.Groups[2].Value, out var price))
                {
                    // Use calculated tokens if available, otherwise use the tokens parameter
                    var itemTokens = tokens;
                    if (calculatedTokensMatch.Success && int.TryParse(calculatedTokensMatch.Groups[1].Value, out var calcTokens))
                    {
                        itemTokens = calcTokens;
                        tokens = itemTokens; // Update the reward's token count
                    }
                    
                    // Calculate profit information - ALWAYS calculate based on actual tokens and min spend
                    // Don't trust AI-provided totalSpend if it seems wrong
                    decimal totalSpend = itemTokens * minimumSpendForToken; // Always calculate from tokens
                    decimal profit = totalSpend - price;
                    decimal profitMargin = totalSpend > 0 ? (profit / totalSpend) * 100 : 0;
                    
                    // Validate AI-provided values - only use if they make sense
                    if (totalSpendMatch.Success && decimal.TryParse(totalSpendMatch.Groups[1].Value, out var aiTotalSpend))
                    {
                        // Only use AI value if it's close to our calculation (within 10%)
                        var expectedSpend = itemTokens * minimumSpendForToken;
                        if (Math.Abs(aiTotalSpend - expectedSpend) < expectedSpend * 0.1m)
                        {
                            totalSpend = aiTotalSpend;
                            profit = totalSpend - price;
                            profitMargin = totalSpend > 0 ? (profit / totalSpend) * 100 : 0;
                        }
                        else
                        {
                            _logger.LogWarning("AI provided totalSpend £{AISpend} but expected £{Expected} for {Tokens} tokens. Using calculated value.", 
                                aiTotalSpend, expectedSpend, itemTokens);
                        }
                    }
                    
                    // Validate profit - ensure it's positive (at least 75% margin)
                    if (profit < 0 || profitMargin < 75)
                    {
                        _logger.LogWarning("Profit calculation shows negative profit or <75% margin for {Tier} tier: Product £{Price}, Tokens {Tokens}, Total Spend £{TotalSpend}, Profit £{Profit}, Margin {Margin}%", 
                            tier, price, itemTokens, totalSpend, profit, profitMargin);
                    }
                    
                    // Build profit justification text
                    var profitJustification = $"Customer needs {itemTokens} visit(s) (spending £{totalSpend:F2} total) to earn {itemTokens} token(s), then gets free {match.Groups[1].Value.Trim()} (£{price:F2}). Business profit: £{profit:F2} ({profitMargin:F1}% margin).";
                    
                    reward.RewardOptions.Add(new TierRewardOption
                    {
                        ItemName = match.Groups[1].Value.Trim(),
                        ItemValueGBP = price,
                        CanBeGivenFree = match.Groups[3].Value.Trim().Equals("YES", StringComparison.OrdinalIgnoreCase),
                        ReasoningForSelection = match.Groups[4].Value.Trim(),
                        TotalCustomerSpend = totalSpend,
                        BusinessProfit = profit,
                        ProfitMarginPercentage = profitMargin,
                        ProfitJustification = profitJustification
                    });
                }
            }
            
            // Update reward's token count if we found calculated tokens in the section
            if (calculatedTokensMatch.Success && int.TryParse(calculatedTokensMatch.Groups[1].Value, out var finalTokens))
            {
                reward.RequiredTokens = Math.Min(Math.Max(finalTokens, 1), 10);
                _logger.LogInformation("Updated {Tier} tier token count from section: {Tokens}", tier, reward.RequiredTokens);
            }
            else
            {
                // Use the tokens parameter passed to this method
                reward.RequiredTokens = tokens;
                _logger.LogInformation("Using token count from parameter for {Tier} tier: {Tokens}", tier, tokens);
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

        public async Task<string> SearchWebsiteUrlAsync(string businessName, string address)
        {
            try
            {
                _logger.LogInformation("Searching for website URL using Google Search: {BusinessName}", businessName);

                var prompt = $@"Search Google for the official website of this business:

Business Name: {businessName}
Address: {address}

Find the official website URL. Look for the business's own website (not Yelp, TripAdvisor, Facebook, or other third-party sites).

Respond with ONLY the website URL in this exact format:
WEBSITE: https://www.example.com

If you cannot find the website, respond with:
WEBSITE: NOT_FOUND";

                // Use Google Search grounding for better results
                var response = await PerformGoogleSearchWithPromptAsync(prompt);
                
                _logger.LogInformation("Website search response: {Response}", response);

                // Extract website URL - try multiple patterns
                string? website = null;

                // Pattern 1: WEBSITE: https://...
                var websiteMatch1 = Regex.Match(
                    response, 
                    @"WEBSITE:\s*(https?://[^\s\n\)]+)", 
                    RegexOptions.IgnoreCase);
                
                if (websiteMatch1.Success)
                {
                    website = websiteMatch1.Groups[1].Value.Trim();
                }
                else
                {
                    // Pattern 2: Direct URL in response (https://www...)
                    var urlMatch = Regex.Match(
                        response,
                        @"(https?://(?:www\.)?[a-zA-Z0-9-]+\.[a-zA-Z]{2,}(?:/[^\s\n\)]*)?)",
                        RegexOptions.IgnoreCase);
                    
                    if (urlMatch.Success)
                    {
                        var foundUrl = urlMatch.Groups[1].Value.Trim();
                        // Filter out common third-party sites
                        if (!foundUrl.Contains("yelp.com") && 
                            !foundUrl.Contains("tripadvisor.com") && 
                            !foundUrl.Contains("facebook.com") &&
                            !foundUrl.Contains("google.com") &&
                            !foundUrl.Contains("zomato.com") &&
                            !foundUrl.Contains("uber.com"))
                        {
                            website = foundUrl;
                        }
                    }
                    
                    if (string.IsNullOrEmpty(website))
                    {
                        // Pattern 3: www.businessname.com format
                        var businessNameLower = businessName.ToLower().Replace(" ", "").Replace("'", "");
                        var wwwMatch = Regex.Match(
                            response,
                            $@"(www\.{Regex.Escape(businessNameLower)}\.[a-zA-Z]{{2,}})",
                            RegexOptions.IgnoreCase);
                        
                        if (wwwMatch.Success)
                        {
                            website = "https://" + wwwMatch.Groups[1].Value.Trim();
                        }
                    }
                }

                // Normalize URL
                if (!string.IsNullOrEmpty(website))
                {
                    website = website.TrimEnd('.', ',', ';', '!', '?', ')', ']');
                    
                    // Add protocol if missing
                    if (!website.StartsWith("http://") && !website.StartsWith("https://"))
                    {
                        website = "https://" + website;
                    }

                    // Validate URL format
                    if (Uri.TryCreate(website, UriKind.Absolute, out var uri) && 
                        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                    {
                        _logger.LogInformation("Found website URL: {Website}", website);
                        return website;
                    }
                }

                _logger.LogWarning("Website URL not found for business: {BusinessName}", businessName);
                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching for website URL");
                return string.Empty;
            }
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

        private string GetTierDescription(Models.TierReward? tierReward)
        {
            if (tierReward == null) return "None";
            if (tierReward.RewardItems.Any())
            {
                return string.Join(", ", tierReward.RewardItems.Select(r => $"{r.ItemName} (£{r.ItemValueGBP})"));
            }
            if (tierReward.FallbackDiscountPercentage.HasValue)
            {
                return $"{tierReward.FallbackDiscountPercentage}% discount";
            }
            return "None";
        }

        // Web Search Methods
        public async Task<WebSearchResult> SearchBusinessOnWebPlatformsAsync(string businessName, string location)
        {
            try
            {
                _logger.LogInformation("Searching web platforms for business: {BusinessName} in {Location}", businessName, location);

                var prompt = $@"Search Google, Uber Eats, and TripAdvisor for: ""{businessName}"" in ""{location}""

Find information about this business:
- Business name, address, website, phone number
- Menu items and prices (if restaurant/food business)
- Services offered (if service business)
- Reviews/ratings if available

Sources to check:
1. Google Business/Google Maps
2. Uber Eats (if food business)
3. TripAdvisor

Respond with:
FOUND: YES or NO
SOURCE: Google/Uber/TripAdvisor/Multiple
BUSINESS_NAME: [name if found]
ADDRESS: [address if found]
WEBSITE: [website if found]
PHONE: [phone if found]
MENU_ITEMS: [list of menu items if found]
RAW_DATA: [full search results]";

                var response = await PerformGoogleSearchWithPromptAsync(prompt);
                return ParseWebSearchResult(response, businessName, location);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching web platforms");
                return new WebSearchResult { Found = false };
            }
        }

        private WebSearchResult ParseWebSearchResult(string text, string businessName, string location)
        {
            var result = new WebSearchResult { Found = false };

            try
            {
                // Check if found
                var foundMatch = Regex.Match(text, @"FOUND:\s*(YES|NO)", RegexOptions.IgnoreCase);
                if (foundMatch.Success && foundMatch.Groups[1].Value.Equals("YES", StringComparison.OrdinalIgnoreCase))
                {
                    result.Found = true;

                    // Extract source
                    var sourceMatch = Regex.Match(text, @"SOURCE:\s*([^\n]+)", RegexOptions.IgnoreCase);
                    if (sourceMatch.Success)
                    {
                        result.Source = sourceMatch.Groups[1].Value.Trim();
                    }

                    // Extract business name
                    var nameMatch = Regex.Match(text, @"BUSINESS_NAME:\s*([^\n]+)", RegexOptions.IgnoreCase);
                    if (nameMatch.Success)
                    {
                        result.BusinessName = nameMatch.Groups[1].Value.Trim();
                    }

                    // Extract address
                    var addressMatch = Regex.Match(text, @"ADDRESS:\s*([^\n]+)", RegexOptions.IgnoreCase);
                    if (addressMatch.Success)
                    {
                        result.Address = addressMatch.Groups[1].Value.Trim();
                    }

                    // Extract website
                    var websiteMatch = Regex.Match(text, @"WEBSITE:\s*([^\n]+)", RegexOptions.IgnoreCase);
                    if (websiteMatch.Success)
                    {
                        result.Website = websiteMatch.Groups[1].Value.Trim();
                    }

                    // Extract phone
                    var phoneMatch = Regex.Match(text, @"PHONE:\s*([^\n]+)", RegexOptions.IgnoreCase);
                    if (phoneMatch.Success)
                    {
                        result.PhoneNumber = phoneMatch.Groups[1].Value.Trim();
                    }

                    // Extract menu items
                    var menuMatch = Regex.Match(text, @"MENU_ITEMS:\s*(.+?)(?=RAW_DATA:|$)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                    if (menuMatch.Success)
                    {
                        var menuText = menuMatch.Groups[1].Value.Trim();
                        result.MenuItems = menuText.Split(new[] { ',', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(item => item.Trim())
                            .Where(item => !string.IsNullOrEmpty(item))
                            .ToList();
                    }

                    // Extract raw data
                    var rawDataMatch = Regex.Match(text, @"RAW_DATA:\s*(.+?)$", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                    if (rawDataMatch.Success)
                    {
                        result.RawSearchData = rawDataMatch.Groups[1].Value.Trim();
                    }
                    else
                    {
                        result.RawSearchData = text; // Use full response if no RAW_DATA section
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing web search result");
            }

            return result;
        }

        public async Task<BusinessAttributes> ExtractBusinessAttributesFromWebSearchAsync(WebSearchResult webSearchResult, string businessName, string category, string fullAddress)
        {
            try
            {
                _logger.LogInformation("Extracting business attributes from web search for: {BusinessName}", businessName);

                var prompt = $@"Business found via web search:
Name: {webSearchResult.BusinessName ?? businessName}
Address: {webSearchResult.Address ?? fullAddress}
Website: {webSearchResult.Website ?? "Not available"}
Phone: {webSearchResult.PhoneNumber ?? "Not available"}
Category: {category}

Web Search Data:
{webSearchResult.RawSearchData ?? "No additional data"}

TASK: Extract business attributes from the web search information.
Determine:
- Business model (B2B, B2C, etc.)
- Core products or services
- Target audience
- Business tone/style
- Popularity/size
- Specialization keywords
- Business type (Product, Service, or Hybrid)

Give me real, specific information based on the web search data.";

                var response = await PerformGoogleSearchWithPromptAsync(prompt);
                return ParseBusinessAttributesFromText(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting business attributes from web search");
                return await ExtractBusinessAttributesAsync(businessName, category, fullAddress); // Fallback
            }
        }

        public async Task<ProductAnalysisResult> ExtractProductsFromWebSearchAsync(WebSearchResult webSearchResult, string businessName, string category, string fullAddress)
        {
            try
            {
                _logger.LogInformation("Extracting products from web search for: {BusinessName}", businessName);

                var prompt = $@"Business found via web search:
Name: {webSearchResult.BusinessName ?? businessName}
Address: {webSearchResult.Address ?? fullAddress}
Website: {webSearchResult.Website ?? "Not available"}
Category: {category}

Web Search Data:
{webSearchResult.RawSearchData ?? "No additional data"}

Menu Items Found: {string.Join(", ", webSearchResult.MenuItems)}

TASK: Extract menu items and prices from the web search data.
- Extract ALL products with prices in GBP (£)
- Use the menu items found in web search
- Get real prices from the search results
- List ALL products with prices

Format:
PRODUCTS: Product1 - £X.XX, Product2 - £Y.YY
RECOMMENDED FREE: ProductName - £Z.ZZ";

                var response = await PerformGoogleSearchWithPromptAsync(prompt);
                return ParseProductAnalysisWithPricesFromText(response, 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting products from web search");
                return new ProductAnalysisResult();
            }
        }

        public async Task<LoyaltyTierAnalysis> GenerateDiscountOnlyTiersAsync(BusinessAttributes businessAttributes, ProductAnalysisResult? productAnalysis, ServiceAnalysisResult? serviceAnalysis, decimal minimumSpendForToken, CompleteBusinessData? similarBusinessData = null)
        {
            try
            {
                _logger.LogInformation("Generating discount-only tiers. Min spend for 1 token: £{MinSpend}", minimumSpendForToken);

                var analysis = new LoyaltyTierAnalysis
                {
                    MinimumSpendForToken = minimumSpendForToken,
                    TierRewards = new List<LoyaltyTierReward>(),
                    HasFreeItemOptions = false,
                    OverallStrategy = "Token-based loyalty program with tier-based discount rewards (real business found but insufficient data for free items)"
                };

                // STEP 1: Extract discount percentages from similar business (if available) - PRIORITY
                decimal? bronzeDiscount = null, silverDiscount = null, goldDiscount = null;
                string? discountSource = null;
                
                if (similarBusinessData != null)
                {
                    _logger.LogInformation("Found similar business: {Name}, extracting discount percentages", similarBusinessData.Business.BusinessName);
                    
                    var similarBronze = similarBusinessData.TierRewards.FirstOrDefault(t => t.Tier == LoyaltyTier.Bronze);
                    var similarSilver = similarBusinessData.TierRewards.FirstOrDefault(t => t.Tier == LoyaltyTier.Silver);
                    var similarGold = similarBusinessData.TierRewards.FirstOrDefault(t => t.Tier == LoyaltyTier.Gold);
                    
                    if (similarBronze?.FallbackDiscountPercentage.HasValue == true)
                    {
                        bronzeDiscount = similarBronze.FallbackDiscountPercentage.Value;
                        _logger.LogInformation("Extracted Bronze discount from similar business: {Discount}%", bronzeDiscount);
                    }
                    if (similarSilver?.FallbackDiscountPercentage.HasValue == true)
                    {
                        silverDiscount = similarSilver.FallbackDiscountPercentage.Value;
                        _logger.LogInformation("Extracted Silver discount from similar business: {Discount}%", silverDiscount);
                    }
                    if (similarGold?.FallbackDiscountPercentage.HasValue == true)
                    {
                        goldDiscount = similarGold.FallbackDiscountPercentage.Value;
                        _logger.LogInformation("Extracted Gold discount from similar business: {Discount}%", goldDiscount);
                    }
                    
                    if (bronzeDiscount.HasValue || silverDiscount.HasValue || goldDiscount.HasValue)
                    {
                        discountSource = $"Based on similar {similarBusinessData.Business.Category} in your area";
                    }
                }

                // STEP 2: Ask AI to suggest token counts for discount-only tiers
                var tokenPrompt = $@"Business: {businessAttributes.BusinessModel}
Business Type: {businessAttributes.BusinessType}
Core Offerings: {businessAttributes.CoreProductsOrServices}
Customer earns 1 token per £{minimumSpendForToken} spent

TASK: Suggest appropriate token counts for a discount-only loyalty program (all tokens must be ≤ 10):
- Bronze: Should be accessible (typically 2-4 tokens) - for occasional customers
- Silver: Should be moderate (typically 4-7 tokens) - for regular customers  
- Gold: Should be premium (typically 7-10 tokens) - for loyal customers

Respond in this format:
BRONZE_TOKENS: [number 1-10]
SILVER_TOKENS: [number 1-10, must be > Bronze]
GOLD_TOKENS: [number 1-10, must be > Silver]";

                var tokenResponse = await GeneratePromptAsync(tokenPrompt);
                var (bronzeTokens, silverTokens, goldTokens) = ParseTokenCountsFromResponse(tokenResponse);
                
                // Validate token counts
                bronzeTokens = Math.Min(Math.Max(bronzeTokens, 2), 10);
                silverTokens = Math.Min(Math.Max(silverTokens, bronzeTokens + 1), 10);
                goldTokens = Math.Min(Math.Max(goldTokens, silverTokens + 1), 10);
                
                _logger.LogInformation("AI suggested discount-only token counts - Bronze: {Bronze}, Silver: {Silver}, Gold: {Gold}", 
                    bronzeTokens, silverTokens, goldTokens);

                // STEP 3: Ask AI to suggest products/services for each tier (NOT discount percentages)
                var productSuggestionPrompt = $@"Business: {businessAttributes.BusinessModel}
Business Type: {businessAttributes.BusinessType}
Category: {businessAttributes.CoreProductsOrServices}
{(similarBusinessData != null ? $"Similar Business Products: {string.Join(", ", similarBusinessData.Products.Select(p => $"{p.Name} (£{p.PriceGBP})"))}" : "")}

TASK: Suggest appropriate products or services that customers would want as rewards for each loyalty tier.
- DO NOT suggest discount percentages
- Suggest actual products/services that make sense for this business type
- Consider typical price ranges for this category

For each tier, suggest:
- Bronze: Small/affordable item (typically £5-15 value)
- Silver: Medium item (typically £15-30 value)
- Gold: Premium item (typically £30-50 value)

Respond in this format:
BRONZE_PRODUCT: [product/service name] - £[estimated price]
SILVER_PRODUCT: [product/service name] - £[estimated price]
GOLD_PRODUCT: [product/service name] - £[estimated price]";

                var productResponse = await GeneratePromptAsync(productSuggestionPrompt);
                _logger.LogInformation("AI product suggestion response: {Response}", productResponse);

                // Parse product suggestions
                var bronzeProductMatch = System.Text.RegularExpressions.Regex.Match(productResponse, @"BRONZE_PRODUCT:\s*([^-]+)\s*-\s*£?([\d.]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                var silverProductMatch = System.Text.RegularExpressions.Regex.Match(productResponse, @"SILVER_PRODUCT:\s*([^-]+)\s*-\s*£?([\d.]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                var goldProductMatch = System.Text.RegularExpressions.Regex.Match(productResponse, @"GOLD_PRODUCT:\s*([^-]+)\s*-\s*£?([\d.]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                decimal? bronzeProductPrice = null, silverProductPrice = null, goldProductPrice = null;
                string? bronzeProduct = null, silverProduct = null, goldProduct = null;

                if (bronzeProductMatch.Success && decimal.TryParse(bronzeProductMatch.Groups[2].Value, out var bp))
                {
                    bronzeProduct = bronzeProductMatch.Groups[1].Value.Trim();
                    bronzeProductPrice = bp;
                }
                if (silverProductMatch.Success && decimal.TryParse(silverProductMatch.Groups[2].Value, out var sp))
                {
                    silverProduct = silverProductMatch.Groups[1].Value.Trim();
                    silverProductPrice = sp;
                }
                if (goldProductMatch.Success && decimal.TryParse(goldProductMatch.Groups[2].Value, out var gp))
                {
                    goldProduct = goldProductMatch.Groups[1].Value.Trim();
                    goldProductPrice = gp;
                }

                // STEP 4: Calculate discount percentages
                // Priority: Similar business discounts > Calculated from product price > AI-suggested > Default
                var tiers = new[]
                {
                    new { 
                        Tier = LoyaltyTier.Bronze, 
                        Tokens = bronzeTokens, 
                        Product = bronzeProduct,
                        ProductPrice = bronzeProductPrice
                    },
                    new { 
                        Tier = LoyaltyTier.Silver, 
                        Tokens = silverTokens, 
                        Product = silverProduct,
                        ProductPrice = silverProductPrice
                    },
                    new { 
                        Tier = LoyaltyTier.Gold, 
                        Tokens = goldTokens, 
                        Product = goldProduct,
                        ProductPrice = goldProductPrice
                    }
                };

                foreach (var tier in tiers)
                {
                    var totalSpendForTier = minimumSpendForToken * tier.Tokens;
                    decimal discount;
                    string dataSource;
                    string reasoning;

                    // Calculate discount based on priority
                    if (tier.Tier == LoyaltyTier.Bronze && bronzeDiscount.HasValue)
                    {
                        discount = bronzeDiscount.Value;
                        dataSource = discountSource ?? "Similar business";
                        reasoning = $"Using discount from similar business. Customer has spent £{totalSpendForTier} total.";
                    }
                    else if (tier.Tier == LoyaltyTier.Silver && silverDiscount.HasValue)
                    {
                        discount = silverDiscount.Value;
                        dataSource = discountSource ?? "Similar business";
                        reasoning = $"Using discount from similar business. Customer has spent £{totalSpendForTier} total.";
                    }
                    else if (tier.Tier == LoyaltyTier.Gold && goldDiscount.HasValue)
                    {
                        discount = goldDiscount.Value;
                        dataSource = discountSource ?? "Similar business";
                        reasoning = $"Using discount from similar business. Customer has spent £{totalSpendForTier} total.";
                    }
                    else if (tier.ProductPrice.HasValue)
                    {
                        // Calculate discount based on product price vs customer spend
                        // Discount % = (Product Price / Total Customer Spend) * 100
                        // But ensure it's reasonable (typically 10-50%)
                        discount = Math.Min(Math.Max((tier.ProductPrice.Value / totalSpendForTier) * 100, 10), 50);
                        discount = Math.Round(discount, 0); // Round to whole number
                        dataSource = "AI-suggested (calculated from product price)";
                        reasoning = $"Calculated discount based on suggested product '{tier.Product}' (£{tier.ProductPrice.Value:F2}) vs customer spend (£{totalSpendForTier}).";
                    }
                    else
                    {
                        // Fallback to default discounts
                        discount = tier.Tier == LoyaltyTier.Bronze ? 10m : tier.Tier == LoyaltyTier.Silver ? 20m : 40m;
                        dataSource = "Default";
                        reasoning = $"Using default discount. Customer has spent £{totalSpendForTier} total.";
                    }

                    var tierReward = new LoyaltyTierReward
                    {
                        Tier = tier.Tier,
                        RequiredTokens = tier.Tokens,
                        RewardOptions = new List<TierRewardOption>(), // No free items
                        FallbackDiscount = new TierFallbackDiscount
                        {
                            DiscountPercentage = discount,
                            Description = $"Get {discount}% off your next purchase",
                            Reasoning = reasoning,
                            DataSource = dataSource,
                            SuggestedProduct = tier.Product,
                            SuggestedProductPrice = tier.ProductPrice
                        },
                        Reasoning = $"{tier.Tier} tier: {tier.Tokens} tokens for {discount}% discount (discount-only due to limited business data)"
                    };

                    analysis.TierRewards.Add(tierReward);
                }

                _logger.LogInformation("Discount-only tier analysis complete");
                return analysis;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating discount-only tiers");
                return CreateFallbackTierAnalysis(minimumSpendForToken);
            }
        }
    }
}
