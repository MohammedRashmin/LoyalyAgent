using loyalityAgent2._0.Models;
using System.Text.Json;
using System.Net;

namespace loyalityAgent2._0.Services
{
    public class GeoapifyService : IGeoapifyService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<GeoapifyService> _logger;

        public GeoapifyService(HttpClient httpClient, IConfiguration configuration, ILogger<GeoapifyService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<PlaceDetails?> SearchPlaceAsync(string businessName, string address)
        {
            try
            {
                var apiKey = _configuration["Geoapify:ApiKey"] ?? "";
                if (string.IsNullOrEmpty(apiKey))
                {
                    _logger.LogWarning("Geoapify API key not configured");
                    return null;
                }

                // Build search query
                var query = $"{businessName} {address}";
                var encodedQuery = WebUtility.UrlEncode(query);
                var url = $"https://api.geoapify.com/v1/geocode/search?text={encodedQuery}&apiKey={apiKey}&limit=1";

                _logger.LogInformation("Searching Geoapify for: {Query}", query);

                var response = await _httpClient.GetAsync(url);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Geoapify API Error: {StatusCode} - {Content}", response.StatusCode, content);
                    return null;
                }

                var jsonDoc = JsonDocument.Parse(content);
                var features = jsonDoc.RootElement.GetProperty("features");

                if (features.GetArrayLength() == 0)
                {
                    _logger.LogInformation("No results found in Geoapify for: {Query}", query);
                    return null;
                }

                var feature = features[0];
                var properties = feature.GetProperty("properties");
                var geometry = feature.GetProperty("geometry");
                var coordinates = geometry.GetProperty("coordinates");

                // Debug: Log available properties to diagnose website extraction
                var propertyNames = properties.EnumerateObject().Select(p => p.Name).ToList();
                _logger.LogInformation("Geoapify properties available: {Properties}", string.Join(", ", propertyNames));

                // Extract website - try multiple property names
                string? website = null;
                if (properties.TryGetProperty("website", out var websiteProp))
                {
                    website = websiteProp.GetString();
                }
                else if (properties.TryGetProperty("url", out var urlProp))
                {
                    website = urlProp.GetString();
                }
                else if (properties.TryGetProperty("contact", out var contactProp))
                {
                    if (contactProp.TryGetProperty("website", out var contactWebsite))
                    {
                        website = contactWebsite.GetString();
                    }
                }
                else if (properties.TryGetProperty("datasource", out var datasourceProp))
                {
                    if (datasourceProp.TryGetProperty("raw", out var rawProp))
                    {
                        if (rawProp.TryGetProperty("website", out var rawWebsite))
                        {
                            website = rawWebsite.GetString();
                        }
                        else if (rawProp.TryGetProperty("url", out var rawUrl))
                        {
                            website = rawUrl.GetString();
                        }
                    }
                }

                // Extract phone - try multiple property names
                string? phone = null;
                if (properties.TryGetProperty("phone", out var phoneProp))
                {
                    phone = phoneProp.GetString();
                }
                else if (properties.TryGetProperty("contact", out var contactPhoneProp))
                {
                    if (contactPhoneProp.TryGetProperty("phone", out var contactPhone))
                    {
                        phone = contactPhone.GetString();
                    }
                }
                else if (properties.TryGetProperty("datasource", out var datasourcePhoneProp))
                {
                    if (datasourcePhoneProp.TryGetProperty("raw", out var rawPhoneProp))
                    {
                        if (rawPhoneProp.TryGetProperty("phone", out var rawPhone))
                        {
                            phone = rawPhone.GetString();
                        }
                    }
                }

                var placeDetails = new PlaceDetails
                {
                    Name = properties.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "",
                    FormattedAddress = properties.TryGetProperty("formatted", out var formatted) ? formatted.GetString() ?? "" : "",
                    Latitude = coordinates[1].GetDouble(),
                    Longitude = coordinates[0].GetDouble(),
                    Website = website,
                    PhoneNumber = phone
                };

                _logger.LogInformation("Extracted website: {Website}, phone: {Phone}", website ?? "Not found", phone ?? "Not found");

                // Extract categories
                if (properties.TryGetProperty("categories", out var categories))
                {
                    foreach (var category in categories.EnumerateArray())
                    {
                        placeDetails.Categories.Add(category.GetString() ?? "");
                    }
                }

                _logger.LogInformation("Found place in Geoapify: {Name}", placeDetails.Name);

                return placeDetails;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching Geoapify");
                return null;
            }
        }

        public async Task<List<PlaceDetails>> FindSimilarPlacesAsync(string category, string address, int radiusMeters = 1000)
        {
            try
            {
                var apiKey = _configuration["Geoapify:ApiKey"] ?? "";
                if (string.IsNullOrEmpty(apiKey))
                {
                    return new List<PlaceDetails>();
                }

                // First geocode the address to get coordinates
                var geocoded = await GeocodeAddressAsync(address);
                if (geocoded == null || !geocoded.Latitude.HasValue || !geocoded.Longitude.HasValue)
                {
                    _logger.LogWarning("Could not geocode address for similar places search");
                    return new List<PlaceDetails>();
                }

                // Search nearby places by category
                var categoryFilter = MapCategoryToGeoapifyCategory(category);
                var url = $"https://api.geoapify.com/v2/places?categories={categoryFilter}&filter=circle:{geocoded.Longitude},{geocoded.Latitude},{radiusMeters}&limit=5&apiKey={apiKey}";

                _logger.LogInformation("Searching for similar places: {Category} near {Address}", category, address);

                var response = await _httpClient.GetAsync(url);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Geoapify Nearby Search Error: {StatusCode}", response.StatusCode);
                    return new List<PlaceDetails>();
                }

                var jsonDoc = JsonDocument.Parse(content);
                var features = jsonDoc.RootElement.GetProperty("features");

                var places = new List<PlaceDetails>();
                foreach (var feature in features.EnumerateArray())
                {
                    var properties = feature.GetProperty("properties");
                    var geometry = feature.GetProperty("geometry");
                    var coordinates = geometry.GetProperty("coordinates");

                    places.Add(new PlaceDetails
                    {
                        PlaceId = properties.TryGetProperty("place_id", out var placeId) ? placeId.GetString() : null,
                        Name = properties.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "",
                        FormattedAddress = properties.TryGetProperty("formatted", out var formatted) ? formatted.GetString() ?? "" : "",
                        Latitude = coordinates[1].GetDouble(),
                        Longitude = coordinates[0].GetDouble(),
                        Website = properties.TryGetProperty("website", out var website) ? website.GetString() : null,
                        PhoneNumber = properties.TryGetProperty("phone", out var phone) ? phone.GetString() : null
                    });
                }

                _logger.LogInformation("Found {Count} similar places", places.Count);
                return places;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding similar places");
                return new List<PlaceDetails>();
            }
        }

        public async Task<PlaceDetails?> GeocodeAddressAsync(string address)
        {
            try
            {
                var apiKey = _configuration["Geoapify:ApiKey"] ?? "";
                if (string.IsNullOrEmpty(apiKey))
                {
                    return null;
                }

                var encodedAddress = WebUtility.UrlEncode(address);
                var url = $"https://api.geoapify.com/v1/geocode/search?text={encodedAddress}&apiKey={apiKey}&limit=1";

                var response = await _httpClient.GetAsync(url);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var jsonDoc = JsonDocument.Parse(content);
                var features = jsonDoc.RootElement.GetProperty("features");

                if (features.GetArrayLength() == 0)
                {
                    return null;
                }

                var feature = features[0];
                var properties = feature.GetProperty("properties");
                var geometry = feature.GetProperty("geometry");
                var coordinates = geometry.GetProperty("coordinates");

                return new PlaceDetails
                {
                    Latitude = coordinates[1].GetDouble(),
                    Longitude = coordinates[0].GetDouble(),
                    FormattedAddress = properties.TryGetProperty("formatted", out var formatted) ? formatted.GetString() ?? "" : ""
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error geocoding address");
                return null;
            }
        }

        private string MapCategoryToGeoapifyCategory(string category)
        {
            // Map common categories to Geoapify categories
            var lowerCategory = category.ToLower();
            
            if (lowerCategory.Contains("restaurant") || lowerCategory.Contains("cafe") || lowerCategory.Contains("food"))
                return "catering.restaurant";
            
            if (lowerCategory.Contains("salon") || lowerCategory.Contains("beauty") || lowerCategory.Contains("spa"))
                return "commercial.beauty_salon";
            
            if (lowerCategory.Contains("shop") || lowerCategory.Contains("store") || lowerCategory.Contains("retail"))
                return "commercial.shopping_mall";
            
            // Default to restaurant category
            return "catering.restaurant";
        }
    }
}
