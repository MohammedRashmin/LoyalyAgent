using loyalityAgent2._0.Models;

namespace loyalityAgent2._0.Services
{
    public interface IGeoapifyService
    {
        Task<PlaceDetails?> SearchPlaceAsync(string businessName, string address);
        Task<List<PlaceDetails>> FindSimilarPlacesAsync(string category, string address, int radiusMeters = 1000);
        Task<PlaceDetails?> GeocodeAddressAsync(string address);
    }
}
