using loyalityAgent2._0.Models;
using System.Text.RegularExpressions;
using System.Net;
using System.Text;

namespace loyalityAgent2._0.Services
{
    public class MenuScraperService : IMenuScraperService
    {
        private readonly HttpClient _httpClient;
        private readonly IGeminiService _geminiService;
        private readonly ILogger<MenuScraperService> _logger;

        public MenuScraperService(
            HttpClient httpClient,
            IGeminiService geminiService,
            ILogger<MenuScraperService> logger)
        {
            _httpClient = httpClient;
            _geminiService = geminiService;
            _logger = logger;
            
            // Set user agent to avoid blocking
            _httpClient.DefaultRequestHeaders.Add("User-Agent", 
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            _httpClient.Timeout = TimeSpan.FromSeconds(10);
        }

        public async Task<ProductAnalysisResult> ScrapeMenuFromWebsiteAsync(string websiteUrl, string businessName, string category)
        {
            try
            {
                _logger.LogInformation("Scraping menu from website: {WebsiteUrl}", websiteUrl);

                // Validate and normalize URL
                var normalizedUrl = NormalizeUrl(websiteUrl);
                if (string.IsNullOrEmpty(normalizedUrl))
                {
                    _logger.LogWarning("Invalid website URL: {WebsiteUrl}", websiteUrl);
                    return new ProductAnalysisResult();
                }

                // Try multiple approaches to get menu
                ProductAnalysisResult? menuItems = null;

                // Approach 1: Try main page
                var htmlContent = await FetchWebsiteHtmlAsync(normalizedUrl);
                if (!string.IsNullOrEmpty(htmlContent))
                {
                    _logger.LogInformation("Fetched {Length} bytes from main page", htmlContent.Length);
                    menuItems = await ExtractMenuItemsFromHtmlAsync(htmlContent, businessName, category, normalizedUrl);
                    
                    if (menuItems.AllProducts.Count >= 3)
                    {
                        _logger.LogInformation("Successfully extracted {Count} items from main page", menuItems.AllProducts.Count);
                        return menuItems;
                    }
                }

                // Approach 2: Try menu page if main page didn't work
                if (menuItems == null || menuItems.AllProducts.Count < 3)
                {
                    _logger.LogInformation("Trying to find and fetch menu page...");
                    var menuUrls = new[]
                    {
                        normalizedUrl.TrimEnd('/') + "/menu",
                        normalizedUrl.TrimEnd('/') + "/Menu",
                        normalizedUrl.TrimEnd('/') + "/MENU",
                        normalizedUrl.TrimEnd('/') + "/food-menu",
                        normalizedUrl.TrimEnd('/') + "/food"
                    };

                    foreach (var menuUrl in menuUrls)
                    {
                        _logger.LogInformation("Trying menu URL: {MenuUrl}", menuUrl);
                        var menuHtml = await FetchWebsiteHtmlAsync(menuUrl);
                        if (!string.IsNullOrEmpty(menuHtml))
                        {
                            var menuResult = await ExtractMenuItemsFromHtmlAsync(menuHtml, businessName, category, menuUrl);
                            if (menuResult.AllProducts.Count > menuItems?.AllProducts.Count)
                            {
                                menuItems = menuResult;
                                if (menuItems.AllProducts.Count >= 3)
                                {
                                    _logger.LogInformation("Successfully extracted {Count} items from menu page", menuItems.AllProducts.Count);
                                    return menuItems;
                                }
                            }
                        }
                    }
                }

                // Approach 3: Use Google Search if HTML scraping failed
                if (menuItems == null || menuItems.AllProducts.Count < 3)
                {
                    _logger.LogInformation("HTML scraping insufficient, using Google Search for menu...");
                    var searchResult = await ExtractMenuFromGoogleSearchAsync(businessName, category, normalizedUrl);
                    if (searchResult.AllProducts.Count > (menuItems?.AllProducts.Count ?? 0))
                    {
                        menuItems = searchResult;
                    }
                }

                return menuItems ?? new ProductAnalysisResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scraping menu from website: {WebsiteUrl}", websiteUrl);
                return new ProductAnalysisResult();
            }
        }

        private string NormalizeUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return string.Empty;

            url = url.Trim();

            // Add protocol if missing
            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
            {
                url = "https://" + url;
            }

            // Validate URL format
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return uri.ToString();
            }

            return string.Empty;
        }

        private async Task<string> FetchWebsiteHtmlAsync(string url)
        {
            try
            {
                _logger.LogInformation("Fetching HTML from: {Url}", url);

                var response = await _httpClient.GetAsync(url);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("HTTP {StatusCode} when fetching {Url}", response.StatusCode, url);
                    return string.Empty;
                }

                var content = await response.Content.ReadAsStringAsync();
                
                // Limit content size to avoid memory issues (first 100KB should be enough for menu)
                if (content.Length > 100000)
                {
                    content = content.Substring(0, 100000);
                    _logger.LogInformation("Truncated HTML content to 100KB");
                }

                return content;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "HTTP error fetching {Url}", url);
                return string.Empty;
            }
            catch (TaskCanceledException)
            {
                _logger.LogWarning("Timeout fetching {Url}", url);
                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching {Url}", url);
                return string.Empty;
            }
        }

        private async Task<ProductAnalysisResult> ExtractMenuItemsFromHtmlAsync(string htmlContent, string businessName, string category, string websiteUrl)
        {
            try
            {
                _logger.LogInformation("Extracting menu items from HTML using AI");

                // Clean HTML - remove scripts, styles, but keep structure
                var cleanedHtml = CleanHtmlContent(htmlContent);

                // Check if HTML seems to have menu content
                var hasMenuKeywords = cleanedHtml.Contains("menu", StringComparison.OrdinalIgnoreCase) ||
                                     cleanedHtml.Contains("price", StringComparison.OrdinalIgnoreCase) ||
                                     cleanedHtml.Contains("item", StringComparison.OrdinalIgnoreCase) ||
                                     cleanedHtml.Contains("dish", StringComparison.OrdinalIgnoreCase);

                if (!hasMenuKeywords)
                {
                    _logger.LogWarning("HTML doesn't seem to contain menu keywords, might be JavaScript-rendered");
                    
                    // Try to find menu page URL
                    var menuUrl = FindMenuPageUrl(htmlContent, websiteUrl);
                    if (!string.IsNullOrEmpty(menuUrl) && menuUrl != websiteUrl)
                    {
                        _logger.LogInformation("Found menu page URL: {MenuUrl}, fetching...", menuUrl);
                        var menuHtml = await FetchWebsiteHtmlAsync(menuUrl);
                        if (!string.IsNullOrEmpty(menuHtml))
                        {
                            cleanedHtml = CleanHtmlContent(menuHtml);
                            websiteUrl = menuUrl; // Update URL for context
                        }
                    }
                }

                // Use Gemini with Google Search grounding for better extraction
                var prompt = $@"Extract COMPLETE menu items and prices from this restaurant website.

Business: {businessName}
Category: {category}
Website: {websiteUrl}

HTML Content (first 100KB):
{cleanedHtml.Substring(0, Math.Min(100000, cleanedHtml.Length))}

TASK: Extract EVERY SINGLE menu item with prices from the HTML.
- Look for ALL menu sections (CHAATS, INDIAN BREAD, LUNCH, RICE, NOODLES, STARTERS, SOUPS, SOUTH INDIAN, SNACKS, SIDE DISHES, DESSERTS, DRINKS, APPETIZERS, MAIN COURSES, etc.)
- Extract item names EXACTLY as they appear in HTML
- Extract prices EXACTLY as they appear, then convert to GBP (£)
- Look for price patterns: Rs., $, £, €, numbers with currency symbols, price tags
- Search through ALL HTML content systematically - don't miss any items
- Include items from EVERY category/section you find

CURRENCY CONVERSION:
- LKR (Sri Lankan Rupees): 1 GBP ≈ 400 LKR (e.g., Rs. 800 = £2.00)
- USD: 1 GBP ≈ 1.25 USD (e.g., $10 = £8.00)
- EUR: 1 GBP ≈ 1.15 EUR (e.g., €10 = £8.70)
- If no currency symbol, assume GBP if UK restaurant, LKR if Sri Lankan

CRITICAL REQUIREMENTS:
- Extract items that are ACTUALLY in the HTML (not invented)
- Include items from ALL menu categories/sections
- List EVERY item you find - be thorough and comprehensive
- If you see menu sections but no prices, still list the items (estimate reasonable prices based on category)
- If no menu items found at all, respond with: NO_MENU_FOUND

Format (list ALL items, be comprehensive):
PRODUCTS: Item1 - £X.XX, Item2 - £Y.YY, Item3 - £Z.ZZ, Item4 - £A.AA, Item5 - £B.BB, [continue listing ALL items from ALL sections]
POPULAR: [list popular items if mentioned in HTML]
RECOMMENDED_FREE: ItemName - £Z.ZZ (smallest/cheapest item, typically under £5)";

                // Try to use Google Search to get menu information if HTML doesn't have enough
                string response;
                if (cleanedHtml.Length < 10000 || !hasMenuKeywords)
                {
                    _logger.LogInformation("HTML seems minimal or JavaScript-rendered, using Google Search for menu");
                    // Use Google Search grounding to find menu from web
                    var searchPrompt = $@"Search Google for the menu of this restaurant:

Business: {businessName}
Website: {websiteUrl}
Category: {category}

Find the complete menu with prices. Look for:
- Menu items from all sections (CHAATS, INDIAN BREAD, LUNCH, RICE, NOODLES, STARTERS, SOUPS, etc.)
- Prices in any currency (convert to GBP: 1 GBP ≈ 400 LKR, 1 GBP ≈ 1.25 USD)
- All available dishes and their prices

Extract ALL menu items with prices.

Format:
PRODUCTS: Product1 - £X.XX, Product2 - £Y.YY, Product3 - £Z.ZZ
POPULAR: [list popular items]
RECOMMENDED_FREE: ProductName - £Z.ZZ";

                    response = await _geminiService.GeneratePromptAsync(searchPrompt);
                }
                else
                {
                    // Use regular prompt for HTML extraction
                    response = await _geminiService.GeneratePromptAsync(prompt);
                }
                
                _logger.LogInformation("AI extraction response length: {Length} chars", response.Length);
                _logger.LogDebug("AI extraction response: {Response}", response);

                // Parse the response
                var result = ParseProductAnalysisFromText(response);

                // Validate extracted items
                result = ValidateAndFilterMenuItems(result, businessName, category, cleanedHtml);

                if (result.AllProducts.Count > 0)
                {
                    _logger.LogInformation("Successfully extracted {Count} validated menu items from website", result.AllProducts.Count);
                }
                else
                {
                    _logger.LogWarning("No menu items extracted from website HTML. Response: {Response}", 
                        response.Substring(0, Math.Min(500, response.Length)));
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting menu items from HTML");
                return new ProductAnalysisResult();
            }
        }

        private string FindMenuPageUrl(string html, string baseUrl)
        {
            try
            {
                // Look for menu links in HTML
                var menuLinkPatterns = new[]
                {
                    @"href=[""']([^""']*menu[^""']*)[""']",
                    @"href=[""']([^""']*/menu[^""']*)[""']",
                    @"href=[""']([^""']*Menu[^""']*)[""']"
                };

                foreach (var pattern in menuLinkPatterns)
                {
                    var matches = Regex.Matches(html, pattern, RegexOptions.IgnoreCase);
                    foreach (Match match in matches)
                    {
                        if (match.Groups.Count > 1)
                        {
                            var menuPath = match.Groups[1].Value;
                            
                            // Convert relative URL to absolute
                            if (menuPath.StartsWith("/"))
                            {
                                var uri = new Uri(baseUrl);
                                return $"{uri.Scheme}://{uri.Host}{menuPath}";
                            }
                            else if (menuPath.StartsWith("http"))
                            {
                                return menuPath;
                            }
                            else if (!menuPath.Contains("://"))
                            {
                                var uri = new Uri(baseUrl);
                                return $"{uri.Scheme}://{uri.Host}/{menuPath.TrimStart('/')}";
                            }
                        }
                    }
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error finding menu page URL");
                return string.Empty;
            }
        }

        private async Task<ProductAnalysisResult> ExtractMenuFromGoogleSearchAsync(string businessName, string category, string websiteUrl)
        {
            try
            {
                _logger.LogInformation("Extracting menu using Google Search for: {BusinessName}", businessName);

                var prompt = $@"Search Google for the COMPLETE menu of this restaurant:

Business: {businessName}
Website: {websiteUrl}
Category: {category}

Find the COMPLETE menu with EVERY item and price. Search:
- The website {websiteUrl} directly
- Google Business listing
- Menu pages, PDFs, or images
- Review sites that mention menu items

Extract:
- Menu items from ALL sections (CHAATS, INDIAN BREAD, LUNCH, RICE, NOODLES, STARTERS, SOUPS, SOUTH INDIAN, SNACKS, SIDE DISHES, DESSERTS, DRINKS, APPETIZERS, MAIN COURSES, etc.)
- Prices in any currency - convert to GBP (£)
  - LKR (Sri Lankan Rupees): 1 GBP ≈ 400 LKR
  - USD: 1 GBP ≈ 1.25 USD
  - EUR: 1 GBP ≈ 1.15 EUR
- Extract EVERY available dish with prices
- Be thorough - don't miss any items

CRITICAL:
- Extract items that are ACTUALLY on the menu (from real sources)
- Include items from ALL menu categories/sections
- Convert all prices to GBP (£)
- List as MANY items as possible - be comprehensive

Format (list ALL items found):
PRODUCTS: Item1 - £X.XX, Item2 - £Y.YY, Item3 - £Z.ZZ, Item4 - £A.AA, Item5 - £B.BB, [continue for ALL items]
POPULAR: [list popular items if mentioned]
RECOMMENDED_FREE: ItemName - £Z.ZZ (smallest/cheapest item, typically under £5)";

                var response = await _geminiService.GeneratePromptAsync(prompt);
                
                _logger.LogInformation("Google Search menu extraction response length: {Length}", response.Length);
                
                var result = ParseProductAnalysisFromText(response);
                
                _logger.LogInformation("Extracted {Count} menu items from Google Search", result.AllProducts.Count);
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting menu from Google Search");
                return new ProductAnalysisResult();
            }
        }

        private string CleanHtmlContent(string html)
        {
            try
            {
                // Remove script tags
                html = Regex.Replace(html, @"<script[^>]*>[\s\S]*?</script>", "", RegexOptions.IgnoreCase);
                
                // Remove style tags
                html = Regex.Replace(html, @"<style[^>]*>[\s\S]*?</style>", "", RegexOptions.IgnoreCase);
                
                // Remove comments
                html = Regex.Replace(html, @"<!--[\s\S]*?-->", "");
                
                // Remove excessive whitespace
                html = Regex.Replace(html, @"\s+", " ");
                
                // Decode HTML entities
                html = WebUtility.HtmlDecode(html);
                
                return html;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error cleaning HTML content");
                return html; // Return original if cleaning fails
            }
        }

        private ProductAnalysisResult ParseProductAnalysisFromText(string text)
        {
            var result = new ProductAnalysisResult();

            try
            {
                // Check if menu was found
                if (text.Contains("NO_MENU_FOUND", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("AI reported no menu found in HTML");
                    return result;
                }

                // Extract products from "PRODUCTS:" line - try multiple patterns
                var productsMatch = Regex.Match(text, @"PRODUCTS:\s*(.+?)(?:\n(?:POPULAR|RECOMMENDED_FREE|$))", 
                    RegexOptions.IgnoreCase | RegexOptions.Singleline);
                
                if (productsMatch.Success)
                {
                    var productsText = productsMatch.Groups[1].Value;
                    
                    // Parse individual products - try multiple formats
                    // Format 1: "Product Name - £X.XX"
                    var productMatches1 = Regex.Matches(productsText, 
                        @"([^-£€$Rs]+?)\s*-\s*£?([\d.]+)", RegexOptions.IgnoreCase);
                    
                    // Format 2: "Product Name £X.XX" (no dash)
                    var productMatches2 = Regex.Matches(productsText, 
                        @"([^£€$Rs]+?)\s+£([\d.]+)", RegexOptions.IgnoreCase);
                    
                    // Format 3: "Product Name: £X.XX"
                    var productMatches3 = Regex.Matches(productsText, 
                        @"([^:£€$Rs]+?):\s*£?([\d.]+)", RegexOptions.IgnoreCase);
                    
                    var allMatches = new List<Match>();
                    allMatches.AddRange(productMatches1.Cast<Match>());
                    allMatches.AddRange(productMatches2.Cast<Match>());
                    allMatches.AddRange(productMatches3.Cast<Match>());
                    
                    // Remove duplicates and parse
                    var seenProducts = new HashSet<string>();
                    foreach (var match in allMatches)
                    {
                        if (match.Groups.Count >= 3)
                        {
                            var productName = match.Groups[1].Value.Trim()
                                .TrimEnd(',', ';', '.', ':', '-');
                            
                            // Skip if empty or too short
                            if (string.IsNullOrWhiteSpace(productName) || productName.Length < 2)
                                continue;
                            
                            // Skip duplicates
                            var productKey = productName.ToLower();
                            if (seenProducts.Contains(productKey))
                                continue;
                            
                            if (decimal.TryParse(match.Groups[2].Value, out var price) && price > 0)
                            {
                                result.AllProducts.Add(new ProductItem
                                {
                                    Name = productName,
                                    PriceGBP = price
                                });
                                seenProducts.Add(productKey);
                            }
                        }
                    }
                    
                    _logger.LogInformation("Extracted {Count} unique products from PRODUCTS line", result.AllProducts.Count);
                }
                
                // Also try to extract products from the entire text if PRODUCTS line didn't work
                if (result.AllProducts.Count == 0)
                {
                    _logger.LogInformation("No products found in PRODUCTS line, trying to extract from full text");
                    
                    // Look for product-price patterns throughout the text
                    var fallbackMatches = Regex.Matches(text, 
                        @"([A-Z][a-zA-Z\s]{3,40}?)\s*[-:]?\s*£([\d.]+)", 
                        RegexOptions.IgnoreCase | RegexOptions.Multiline);
                    
                    var seenFallback = new HashSet<string>();
                    foreach (Match match in fallbackMatches)
                    {
                        if (match.Groups.Count >= 3)
                        {
                            var productName = match.Groups[1].Value.Trim();
                            var productKey = productName.ToLower();
                            
                            if (!seenFallback.Contains(productKey) && 
                                productName.Length >= 3 && 
                                productName.Length <= 100 &&
                                !productName.Contains("PRODUCTS") &&
                                !productName.Contains("POPULAR") &&
                                !productName.Contains("RECOMMENDED"))
                            {
                                if (decimal.TryParse(match.Groups[2].Value, out var price) && price > 0 && price <= 500)
                                {
                                    result.AllProducts.Add(new ProductItem
                                    {
                                        Name = productName,
                                        PriceGBP = price
                                    });
                                    seenFallback.Add(productKey);
                                }
                            }
                        }
                    }
                    
                    if (result.AllProducts.Count > 0)
                    {
                        _logger.LogInformation("Extracted {Count} products from fallback pattern matching", result.AllProducts.Count);
                    }
                }

                // Extract popular products
                var popularMatch = Regex.Match(text, @"POPULAR:\s*(.+?)(?:\n|RECOMMENDED_FREE:|$)", 
                    RegexOptions.IgnoreCase | RegexOptions.Singleline);
                
                if (popularMatch.Success)
                {
                    var popularText = popularMatch.Groups[1].Value;
                    var popularItems = popularText.Split(new[] { ',', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(item => item.Trim())
                        .Where(item => !string.IsNullOrEmpty(item))
                        .ToList();
                    
                    result.PopularProducts = popularItems;
                }

                // Extract recommended free product
                var freeMatch = Regex.Match(text, @"RECOMMENDED_FREE:\s*([^-£]+?)\s*-\s*£?([\d.]+)", 
                    RegexOptions.IgnoreCase);
                
                if (freeMatch.Success)
                {
                    result.SelectedFreeProduct = freeMatch.Groups[1].Value.Trim();
                    if (decimal.TryParse(freeMatch.Groups[2].Value, out var freePrice))
                    {
                        result.SelectedFreeProductPrice = freePrice;
                    }
                }
                else if (result.AllProducts.Any())
                {
                    // Auto-select cheapest item as welcome gift
                    var cheapest = result.AllProducts.OrderBy(p => p.PriceGBP).First();
                    result.SelectedFreeProduct = cheapest.Name;
                    result.SelectedFreeProductPrice = cheapest.PriceGBP;
                }

                _logger.LogInformation("Parsed {Count} products from AI response", result.AllProducts.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing product analysis from text");
            }

            return result;
        }

        private ProductAnalysisResult ValidateAndFilterMenuItems(ProductAnalysisResult result, string businessName, string category, string htmlContent)
        {
            try
            {
                var validatedResult = new ProductAnalysisResult();
                var htmlLower = htmlContent.ToLower();
                var businessNameLower = businessName.ToLower();

                foreach (var product in result.AllProducts)
                {
                    var productNameLower = product.Name.ToLower();
                    var isValid = true;

                    // Validation 1: Check if product name appears in HTML (if HTML is substantial)
                    if (htmlContent.Length > 1000)
                    {
                        // Check if product name or similar appears in HTML
                        var productWords = productNameLower.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                            .Where(w => w.Length > 3) // Ignore short words like "the", "and"
                            .ToList();
                        
                        var matchesInHtml = productWords.Count(word => 
                            htmlLower.Contains(word, StringComparison.OrdinalIgnoreCase));
                        
                        // At least 50% of significant words should appear in HTML
                        if (productWords.Count > 0 && matchesInHtml < (productWords.Count * 0.5))
                        {
                            _logger.LogDebug("Product '{Product}' may not be in HTML (only {Matches}/{Total} words match)", 
                                product.Name, matchesInHtml, productWords.Count);
                            // Don't reject, but flag for review
                        }
                    }

                    // Validation 2: Price reasonableness check
                    if (product.PriceGBP <= 0 || product.PriceGBP > 500)
                    {
                        _logger.LogWarning("Product '{Product}' has unreasonable price: £{Price}", 
                            product.Name, product.PriceGBP);
                        isValid = false;
                    }

                    // Validation 3: Product name sanity check
                    if (string.IsNullOrWhiteSpace(product.Name) || 
                        product.Name.Length < 2 || 
                        product.Name.Length > 200)
                    {
                        _logger.LogWarning("Product name invalid: '{Product}'", product.Name);
                        isValid = false;
                    }

                    // Validation 4: Check for common AI hallucination patterns
                    var suspiciousPatterns = new[]
                    {
                        "example", "sample", "test", "placeholder", "lorem ipsum",
                        "product name", "item name", "menu item"
                    };
                    
                    if (suspiciousPatterns.Any(pattern => productNameLower.Contains(pattern)))
                    {
                        _logger.LogWarning("Product '{Product}' matches suspicious pattern", product.Name);
                        isValid = false;
                    }

                    if (isValid)
                    {
                        validatedResult.AllProducts.Add(product);
                    }
                    else
                    {
                        _logger.LogInformation("Filtered out invalid product: {Product} - £{Price}", 
                            product.Name, product.PriceGBP);
                    }
                }

                // Copy other properties
                validatedResult.PopularProducts = result.PopularProducts;
                validatedResult.SelectedFreeProduct = result.SelectedFreeProduct;
                validatedResult.SelectedFreeProductPrice = result.SelectedFreeProductPrice;

                // Re-select cheapest if needed
                if (string.IsNullOrEmpty(validatedResult.SelectedFreeProduct) && validatedResult.AllProducts.Any())
                {
                    var cheapest = validatedResult.AllProducts
                        .Where(p => p.PriceGBP <= 5) // Only items under £5 for welcome gift
                        .OrderBy(p => p.PriceGBP)
                        .FirstOrDefault() 
                        ?? validatedResult.AllProducts.OrderBy(p => p.PriceGBP).First();
                    
                    validatedResult.SelectedFreeProduct = cheapest.Name;
                    validatedResult.SelectedFreeProductPrice = cheapest.PriceGBP;
                }

                _logger.LogInformation("Validation: {Original} items → {Validated} valid items", 
                    result.AllProducts.Count, validatedResult.AllProducts.Count);

                return validatedResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating menu items");
                return result; // Return original if validation fails
            }
        }
    }
}

