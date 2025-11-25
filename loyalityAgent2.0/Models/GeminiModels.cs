using System.Text.Json.Serialization;

namespace loyalityAgent2._0.Models
{
    public class GeminiRequest
    {
        [JsonPropertyName("contents")]
        public List<GeminiContent> Contents { get; set; } = new();

        [JsonPropertyName("tools")]
        public List<GeminiTool>? Tools { get; set; }
    }

    public class GeminiContent
    {
        [JsonPropertyName("parts")]
        public List<GeminiPart> Parts { get; set; } = new();
    }

    public class GeminiPart
    {
        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;
    }

    public class GeminiTool
    {
        [JsonPropertyName("google_search")]
        public object GoogleSearch { get; set; } = new { };
    }

    public class GeminiResponse
    {
        [JsonPropertyName("candidates")]
        public List<GeminiCandidate>? Candidates { get; set; }
    }

    public class GeminiCandidate
    {
        [JsonPropertyName("content")]
        public GeminiContent? Content { get; set; }

        [JsonPropertyName("groundingMetadata")]
        public GroundingMetadata? GroundingMetadata { get; set; }
    }

    public class GroundingMetadata
    {
        [JsonPropertyName("searchEntryPoint")]
        public SearchEntryPoint? SearchEntryPoint { get; set; }

        [JsonPropertyName("groundingChunks")]
        public List<GroundingChunk>? GroundingChunks { get; set; }

        [JsonPropertyName("webSearchQueries")]
        public List<string>? WebSearchQueries { get; set; }
    }

    public class SearchEntryPoint
    {
        [JsonPropertyName("renderedContent")]
        public string? RenderedContent { get; set; }
    }

    public class GroundingChunk
    {
        [JsonPropertyName("web")]
        public WebChunk? Web { get; set; }
    }

    public class WebChunk
    {
        [JsonPropertyName("uri")]
        public string? Uri { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }
    }
}
