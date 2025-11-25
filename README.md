# Loyalty Agent 2.0 - Multi-Layer Search Agent

A sophisticated multi-layer agent system that combines OpenAI web search and Google Gemini search to provide comprehensive answers to user queries.

## Architecture

The system uses a **3-layer workflow**:

1. **Layer 1 - OpenAI Web Search**: Uses GPT-4o with web search capabilities to find information
2. **Layer 2 - Google Search via Gemini**: Uses Gemini 2.5 Pro with Google Search integration
3. **Layer 3 - Aggregation**: Gemini 2.5 Pro analyzes and synthesizes results from both layers to provide a comprehensive answer

## Project Structure

```
loyalityAgent2.0/
├── Controllers/
│   └── AgentController.cs          # API endpoint for query processing
├── Services/
│   ├── IOpenAIService.cs           # OpenAI service interface
│   ├── OpenAIService.cs            # OpenAI web search implementation
│   ├── IGeminiService.cs           # Gemini service interface
│   ├── GeminiService.cs            # Gemini search and aggregation
│   ├── IAgentOrchestratorService.cs # Orchestrator interface
│   └── AgentOrchestratorService.cs # Workflow coordination
├── Models/
│   ├── AgentRequest.cs             # Request model
│   ├── AgentResponse.cs            # Response model
│   ├── OpenAIModels.cs             # OpenAI API models
│   └── GeminiModels.cs             # Gemini API models
├── appsettings.json                # Configuration with API credentials
└── Program.cs                      # Service registration
```

## Setup

### Prerequisites
- .NET 8.0 SDK
- Valid OpenAI API key
- Valid Google Gemini API key

### Configuration

API credentials are stored in `appsettings.json`:

```json
{
  "OpenAI": {
    "ApiUrl": "https://api.openai.com/v1/responses",
    "BearerToken": "your-openai-token"
  },
  "Gemini": {
    "ApiKey": "your-gemini-api-key"
  }
}
```

### Running the Application

1. Navigate to the project directory:
```bash
cd loyalityAgent2.0
```

2. Restore dependencies:
```bash
dotnet restore
```

3. Run the application:
```bash
dotnet run
```

The API will be available at:
- HTTPS: `https://localhost:7xxx`
- HTTP: `http://localhost:5xxx`
- Swagger UI: `https://localhost:7xxx/swagger`

## API Endpoints

### POST /api/agent/query

Process a query through the multi-layer agent workflow.

**Request Body:**
```json
{
  "query": "What is the height of Burj Khalifa?"
}
```

**Response:**
```json
{
  "finalAnswer": "Aggregated and synthesized answer from both sources",
  "openAIResult": {
    "content": "Results from OpenAI web search",
    "sources": [
      "Source Title - URL"
    ]
  },
  "geminiSearchResult": {
    "content": "Results from Google search via Gemini",
    "sources": [
      "Source Title - URL"
    ]
  },
  "success": true,
  "errorMessage": null
}
```

### GET /api/agent/health

Health check endpoint.

**Response:**
```json
{
  "status": "Healthy",
  "service": "Loyalty Agent 2.0",
  "timestamp": "2025-10-23T12:00:00Z"
}
```

## Usage Examples

### Using cURL

```bash
curl -X POST https://localhost:7xxx/api/agent/query \
  -H "Content-Type: application/json" \
  -d '{"query": "What are the latest developments in AI?"}'
```

### Using PowerShell

```powershell
$body = @{
    query = "What is the capital of France?"
} | ConvertTo-Json

Invoke-RestMethod -Uri "https://localhost:7xxx/api/agent/query" `
  -Method Post `
  -ContentType "application/json" `
  -Body $body
```

### Using the .http file

The project includes `loyalityAgent2.0.http` for testing with VS Code REST Client extension:

```http
POST https://localhost:7xxx/api/agent/query
Content-Type: application/json

{
  "query": "Who won the Nobel Prize in Physics in 2024?"
}
```

## Workflow Details

When you send a query, the system:

1. **Receives the query** via the AgentController
2. **Layer 1**: OpenAI Service performs a web search using GPT-4o
   - Searches the web for relevant information
   - Returns structured results with sources
3. **Layer 2**: Gemini Service performs a Google search
   - Uses Gemini 2.5 Pro with Google Search integration
   - Returns search results with grounding metadata
4. **Layer 3**: Gemini Service aggregates both results
   - Analyzes information from both sources
   - Identifies common themes and contradictions
   - Synthesizes a comprehensive answer
5. **Returns complete response** with all layer results

## Key Features

- **Multi-source intelligence**: Combines OpenAI and Google search capabilities
- **Smart aggregation**: Uses AI to synthesize information from multiple sources
- **Source tracking**: Maintains references to all information sources
- **Error handling**: Comprehensive logging and error management
- **Swagger documentation**: Built-in API documentation
- **CORS enabled**: Ready for frontend integration

## API Rate Limits

Be aware of rate limits for both services:
- **OpenAI**: Check your plan's rate limits
- **Google Gemini**: Check Gemini API quotas

## Security Notes

**IMPORTANT**: The current `appsettings.json` contains API keys for demonstration. For production:

1. **Never commit API keys to version control**
2. Use environment variables or Azure Key Vault
3. Use `appsettings.Development.json` for local development
4. Add `appsettings.json` to `.gitignore`

## Troubleshooting

### Common Issues

1. **401 Unauthorized**: Check your API keys in `appsettings.json`
2. **Timeout errors**: API calls may take time; consider increasing timeout values
3. **Rate limit errors**: You may be hitting API rate limits; implement retry logic if needed

## Future Enhancements

- Add caching to reduce API calls
- Implement retry logic with exponential backoff
- Add authentication to the API
- Store query history in a database
- Add more search providers (Bing, DuckDuckGo, etc.)
- Implement streaming responses
- Add query categorization and routing

## License

This project is for research and development purposes.
