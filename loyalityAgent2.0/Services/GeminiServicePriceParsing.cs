using loyalityAgent2._0.Models;
using System.Text.RegularExpressions;

namespace loyalityAgent2._0.Services
{
    public partial class GeminiService
    {
        private ProductAnalysisResult ParseProductAnalysisWithPricesFromText(string text, decimal minimumSpent)
        {
            var result = new ProductAnalysisResult();

            try
            {
                // Check if no menu items were found
                if (text.Contains("NO MENU ITEMS FOUND", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("No menu items found in product analysis");
                    return result; // Return empty result to trigger fallback
                }

                // Extract products with prices using regex: "ProductName - £XX.XX"
                var pricePattern = @"([^-£\n]+)\s*-\s*£(\d+\.?\d*)";
                var matches = Regex.Matches(text, pricePattern);

                foreach (Match match in matches)
                {
                    if (match.Groups.Count >= 3)
                    {
                        var productName = match.Groups[1].Value.Trim();
                        if (decimal.TryParse(match.Groups[2].Value, out decimal price))
                        {
                            result.AllProducts.Add(new ProductItem
                            {
                                Name = productName,
                                PriceGBP = price
                            });

                            // Add to threshold list if under 25% of minimum spend
                            if (price <= minimumSpent / 4)
                            {
                                result.ProductsMeetingThreshold.Add(new ProductItem
                                {
                                    Name = productName,
                                    PriceGBP = price
                                });
                            }
                        }
                    }
                }

                // Extract recommended free product
                var recommendedMatch = Regex.Match(text, @"RECOMMENDED FREE:\s*([^-\n]+)\s*-\s*£(\d+\.?\d*)", RegexOptions.IgnoreCase);
                if (recommendedMatch.Success && recommendedMatch.Groups.Count >= 3)
                {
                    result.SelectedFreeProduct = recommendedMatch.Groups[1].Value.Trim();
                    decimal.TryParse(recommendedMatch.Groups[2].Value, out decimal price);
                    result.SelectedFreeProductPrice = price;
                }
                else if (result.ProductsMeetingThreshold.Any())
                {
                    // Fallback: use cheapest product meeting threshold
                    var cheapest = result.ProductsMeetingThreshold.OrderBy(p => p.PriceGBP).First();
                    result.SelectedFreeProduct = cheapest.Name;
                    result.SelectedFreeProductPrice = cheapest.PriceGBP;
                }

                _logger.LogInformation("Parsed {Count} products, {ThresholdCount} meeting threshold, Selected: {Selected} at £{Price}",
                    result.AllProducts.Count, result.ProductsMeetingThreshold.Count,
                    result.SelectedFreeProduct, result.SelectedFreeProductPrice);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing product analysis with prices");
            }

            return result;
        }

        private ServiceAnalysisResult ParseServiceAnalysisWithPricesFromText(string text, decimal minimumSpent)
        {
            var result = new ServiceAnalysisResult();

            try
            {
                // Check if no menu items were found
                if (text.Contains("NO MENU ITEMS FOUND", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("No menu items found in service analysis");
                    return result; // Return empty result to trigger fallback
                }

                // Extract services with prices using regex: "ServiceName - £XX.XX"
                var pricePattern = @"([^-£\n]+)\s*-\s*£(\d+\.?\d*)";
                var matches = Regex.Matches(text, pricePattern);

                foreach (Match match in matches)
                {
                    if (match.Groups.Count >= 3)
                    {
                        var serviceName = match.Groups[1].Value.Trim();
                        if (decimal.TryParse(match.Groups[2].Value, out decimal price))
                        {
                            result.AllServices.Add(new ServiceItem
                            {
                                Name = serviceName,
                                PriceGBP = price
                            });

                            // Add to threshold list if near minimum spend (50%-150% range)
                            if (price >= minimumSpent * 0.5m && price <= minimumSpent * 1.5m)
                            {
                                result.ServicesMeetingThreshold.Add(new ServiceItem
                                {
                                    Name = serviceName,
                                    PriceGBP = price
                                });
                            }
                        }
                    }
                }

                // Extract recommended discount service
                var recommendedMatch = Regex.Match(text, @"RECOMMENDED DISCOUNT:\s*([^-\n]+)\s*-\s*£(\d+\.?\d*)", RegexOptions.IgnoreCase);
                if (recommendedMatch.Success && recommendedMatch.Groups.Count >= 3)
                {
                    result.SelectedDiscountableService = recommendedMatch.Groups[1].Value.Trim();
                    decimal.TryParse(recommendedMatch.Groups[2].Value, out decimal price);
                    result.SelectedServicePrice = price;
                }
                else if (result.ServicesMeetingThreshold.Any())
                {
                    // Fallback: use service closest to minimum spend
                    var closest = result.ServicesMeetingThreshold.OrderBy(s => Math.Abs(s.PriceGBP - minimumSpent)).First();
                    result.SelectedDiscountableService = closest.Name;
                    result.SelectedServicePrice = closest.PriceGBP;
                }

                _logger.LogInformation("Parsed {Count} services, {ThresholdCount} meeting threshold, Selected: {Selected} at £{Price}",
                    result.AllServices.Count, result.ServicesMeetingThreshold.Count,
                    result.SelectedDiscountableService, result.SelectedServicePrice);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing service analysis with prices");
            }

            return result;
        }
    }
}
