using loyalityAgent2._0.Models;

namespace loyalityAgent2._0.Services
{
    public interface IMenuScraperService
    {
        Task<ProductAnalysisResult> ScrapeMenuFromWebsiteAsync(string websiteUrl, string businessName, string category);
    }
}





