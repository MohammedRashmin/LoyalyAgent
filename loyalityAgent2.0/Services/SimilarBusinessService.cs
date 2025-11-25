using loyalityAgent2._0.Data;
using loyalityAgent2._0.Models;
using Microsoft.EntityFrameworkCore;

namespace loyalityAgent2._0.Services
{
    public class SimilarBusinessService : ISimilarBusinessService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SimilarBusinessService> _logger;
        private readonly IGeoapifyService _geoapifyService;

        public SimilarBusinessService(
            ApplicationDbContext context,
            ILogger<SimilarBusinessService> logger,
            IGeoapifyService geoapifyService)
        {
            _context = context;
            _logger = logger;
            _geoapifyService = geoapifyService;
        }

        public async Task<Business?> FindSimilarBusinessAsync(string category, string address, int maxResults = 3)
        {
            try
            {
                // Extract city from address (simple extraction)
                var city = ExtractCityFromAddress(address);
                
                _logger.LogInformation("Searching for similar businesses: Category={Category}, City={City}", category, city);

                // First try to find by category and city
                var similarBusinesses = await _context.Businesses
                    .Where(b => b.Category.ToLower() == category.ToLower() && 
                               b.City.ToLower() == city.ToLower())
                    .OrderByDescending(b => b.CreatedAt)
                    .Take(maxResults)
                    .ToListAsync();

                if (similarBusinesses.Any())
                {
                    _logger.LogInformation("Found {Count} similar businesses in database", similarBusinesses.Count);
                    return similarBusinesses.First(); // Return most recent
                }

                // If no exact match, try just category
                similarBusinesses = await _context.Businesses
                    .Where(b => b.Category.ToLower() == category.ToLower())
                    .OrderByDescending(b => b.CreatedAt)
                    .Take(maxResults)
                    .ToListAsync();

                if (similarBusinesses.Any())
                {
                    _logger.LogInformation("Found {Count} businesses with same category", similarBusinesses.Count);
                    return similarBusinesses.First();
                }

                _logger.LogInformation("No similar businesses found in database");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding similar business");
                return null;
            }
        }

        public async Task<CompleteBusinessData> GetCompleteBusinessDataAsync(int businessId)
        {
            var business = await _context.Businesses
                .Include(b => b.Products)
                .Include(b => b.Services)
                .Include(b => b.WelcomeGift)
                .Include(b => b.TierRewards)
                    .ThenInclude(tr => tr.RewardItems)
                .FirstOrDefaultAsync(b => b.BusinessId == businessId);

            if (business == null)
            {
                throw new Exception($"Business with ID {businessId} not found");
            }

            return new CompleteBusinessData
            {
                Business = business,
                Products = business.Products,
                Services = business.Services,
                WelcomeGift = business.WelcomeGift,
                TierRewards = business.TierRewards
            };
        }

        public async Task<Business?> GetBusinessByAddressAsync(string businessName, string address)
        {
            try
            {
                var addressHash = GenerateAddressHash(address);
                
                var business = await _context.Businesses
                    .FirstOrDefaultAsync(b => 
                        b.BusinessName.ToLower() == businessName.ToLower() &&
                        b.AddressHash == addressHash);

                return business;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting business by address");
                return null;
            }
        }

        private string ExtractCityFromAddress(string address)
        {
            // Simple extraction - look for common city patterns
            // For "53/3 Ramakrishna Rd, Colombo 00600" -> "Colombo"
            var parts = address.Split(',');
            if (parts.Length > 1)
            {
                var cityPart = parts[parts.Length - 1].Trim();
                // Remove postal code if present (e.g., "Colombo 00600" -> "Colombo")
                var city = cityPart.Split(' ')[0];
                return city;
            }
            return address;
        }

        private string GenerateAddressHash(string address)
        {
            // Simple hash for address lookup
            return address.ToLower().Replace(" ", "").GetHashCode().ToString();
        }

        public async Task<Business> SaveBusinessDataAsync(string businessName, string category, string fullAddress, AgentResponse response, ProductAnalysisResult productAnalysis, ServiceAnalysisResult? serviceAnalysis, PlaceDetails? placeDetails, Business? similarBusiness)
        {
            try
            {
                var city = ExtractCityFromAddress(fullAddress);
                var addressHash = GenerateAddressHash(fullAddress);

                var business = new Business
                {
                    BusinessName = businessName,
                    Category = category,
                    FullAddress = fullAddress,
                    City = city,
                    Country = "Sri Lanka", // Default, can be extracted from address
                    AddressHash = addressHash,
                    BusinessModel = response.BusinessAttributes?.BusinessModel ?? "",
                    CoreProductsOrServices = response.BusinessAttributes?.CoreProductsOrServices ?? "",
                    TargetAudience = response.BusinessAttributes?.TargetAudience ?? "",
                    BusinessToneOrStyle = response.BusinessAttributes?.BusinessToneOrStyle ?? "",
                    PopularityOrSize = response.BusinessAttributes?.PopularityOrSize ?? "",
                    SpecializationKeywords = response.BusinessAttributes?.SpecializationKeywords ?? "",
                    BusinessType = response.BusinessAttributes?.BusinessType ?? BusinessType.Hybrid,
                    GeoapifyPlaceId = placeDetails?.PlaceId,
                    Latitude = placeDetails?.Latitude,
                    Longitude = placeDetails?.Longitude,
                    Website = placeDetails?.Website,
                    PhoneNumber = placeDetails?.PhoneNumber,
                    DataSource = response.DataSource,
                    SimilarBusinessName = response.SimilarBusinessName,
                    IsRealData = response.IsRealData,
                    UpdatedAt = DateTime.UtcNow,
                    LastSearchedAt = DateTime.UtcNow,
                    SearchCount = 1
                };

                // Save products
                foreach (var product in productAnalysis.AllProducts)
                {
                    business.Products.Add(new Product
                    {
                        Name = product.Name,
                        PriceGBP = product.PriceGBP,
                        IsPopular = productAnalysis.PopularProducts.Contains(product.Name),
                        IsWelcomeGiftEligible = product.Name == productAnalysis.SelectedFreeProduct
                    });
                }

                // Save services
                if (serviceAnalysis != null)
                {
                    foreach (var service in serviceAnalysis.AllServices)
                    {
                        business.Services.Add(new Service
                        {
                            Name = service.Name,
                            PriceGBP = service.PriceGBP,
                            IsPopular = serviceAnalysis.PopularServices.Contains(service.Name),
                            IsDiscountable = service.Name == serviceAnalysis.SelectedDiscountableService
                        });
                    }
                }

                // Save welcome gift
                if (response.WelcomeGift != null)
                {
                    business.WelcomeGift = new WelcomeGift
                    {
                        ItemName = response.WelcomeGift.ItemName,
                        ItemPriceGBP = response.WelcomeGift.ItemPriceGBP,
                        Description = response.WelcomeGift.Description,
                        Reasoning = response.WelcomeGift.Reasoning,
                        IsFree = response.WelcomeGift.IsFree
                    };
                }

                // Save tier rewards
                if (response.LoyaltyTierAnalysis != null)
                {
                    foreach (var tierReward in response.LoyaltyTierAnalysis.TierRewards)
                    {
                        var dbTierReward = new TierReward
                        {
                            Tier = tierReward.Tier,
                            RequiredTokens = tierReward.RequiredTokens,
                            Reasoning = tierReward.Reasoning,
                            FallbackDiscountPercentage = tierReward.FallbackDiscount?.DiscountPercentage,
                            FallbackDescription = tierReward.FallbackDiscount?.Description
                        };

                        foreach (var item in tierReward.RewardOptions)
                        {
                            dbTierReward.RewardItems.Add(new TierRewardItem
                            {
                                ItemName = item.ItemName,
                                ItemValueGBP = item.ItemValueGBP,
                                CanBeGivenFree = item.CanBeGivenFree,
                                ReasoningForSelection = item.ReasoningForSelection
                            });
                        }

                        business.TierRewards.Add(dbTierReward);
                    }
                }

                _context.Businesses.Add(business);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Saved business data to database: {BusinessId}", business.BusinessId);
                return business;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving business data");
                throw;
            }
        }
    }
}
