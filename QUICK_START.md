# Quick Start Guide - Loyalty Agent 2.0

## What You Just Built

A **3-layer intelligent search agent** that:
1. Searches using OpenAI's GPT-4o with web search
2. Searches using Google Gemini with Google Search
3. Aggregates both results using Gemini for a comprehensive answer

## Running the Application

```bash
cd "C:\Users\User\Desktop\Poddle JTN\Research\loyality agent 2.0\loyalityAgent2.0\loyalityAgent2.0"
dotnet run
```

The application will start and show you the URLs (typically https://localhost:7xxx)

## Testing the API

### Option 1: Use Swagger UI
1. Open your browser
2. Go to `https://localhost:7xxx/swagger`
3. Try the `/api/agent/query` endpoint
4. Enter a query like: `{"query": "What is the height of Burj Khalifa?"}`

### Option 2: Use PowerShell
```powershell
# Replace 7xxx with your actual port number
$uri = "https://localhost:7xxx/api/agent/query"
$body = @{
    query = "What is the height of Burj Khalifa?"
} | ConvertTo-Json

Invoke-RestMethod -Uri $uri -Method Post -ContentType "application/json" -Body $body -SkipCertificateCheck
```

### Option 3: Use the test-requests.http file
1. Open `test-requests.http` in VS Code
2. Install REST Client extension if needed
3. Click "Send Request" above any request

## How It Works

```
User Query
    ↓
┌─────────────────────────────────────┐
│  AgentController                    │
│  POST /api/agent/query              │
└──────────────┬──────────────────────┘
               ↓
┌─────────────────────────────────────┐
│  AgentOrchestratorService           │
│  Coordinates the 3-layer workflow   │
└──────────────┬──────────────────────┘
               ↓
    ┌──────────┴──────────┐
    ↓                     ↓
┌─────────┐          ┌─────────┐
│ Layer 1 │          │ Layer 2 │
│ OpenAI  │          │ Gemini  │
│ Search  │          │ Search  │
└────┬────┘          └────┬────┘
     │                    │
     └──────────┬─────────┘
                ↓
         ┌─────────────┐
         │   Layer 3   │
         │   Gemini    │
         │ Aggregation │
         └──────┬──────┘
                ↓
         Final Answer
```

## Workflow Explained

1. **User sends query**: "What is the height of Burj Khalifa?"

2. **Layer 1 - OpenAI Search**:
   - Uses GPT-4o with web_search tool
   - Searches the web for information
   - Returns results with sources

3. **Layer 2 - Gemini Google Search**:
   - Uses Gemini 2.5 Pro with Google Search
   - Performs a Google search
   - Returns results with grounding metadata

4. **Layer 3 - Gemini Aggregation**:
   - Takes both search results
   - Analyzes and synthesizes information
   - Identifies common themes and contradictions
   - Returns comprehensive final answer

5. **Response includes**:
   - Final aggregated answer
   - OpenAI search results with sources
   - Gemini search results with sources

## Response Structure

```json
{
  "finalAnswer": "The Burj Khalifa is 828 meters (2,717 feet) tall...",
  "openAIResult": {
    "content": "Information from OpenAI search...",
    "sources": [
      "Wikipedia - https://en.wikipedia.org/...",
      "Official Site - https://..."
    ]
  },
  "geminiSearchResult": {
    "content": "Information from Google search...",
    "sources": [
      "Source Title - URL"
    ]
  },
  "success": true,
  "errorMessage": null
}
```

## File Structure Overview

```
Controllers/
  AgentController.cs       ← API endpoint (you send requests here)

Services/
  AgentOrchestratorService.cs  ← Coordinates the workflow
  OpenAIService.cs             ← Layer 1: OpenAI web search
  GeminiService.cs             ← Layer 2 & 3: Google search + aggregation

Models/
  AgentRequest.cs          ← { "query": "your question" }
  AgentResponse.cs         ← Response with all layer results
  OpenAIModels.cs          ← OpenAI API data structures
  GeminiModels.cs          ← Gemini API data structures

appsettings.json           ← Your API keys (keep secret!)
```

## Configuration

Your API credentials are in `appsettings.json`:

```json
{
  "OpenAI": {
    "ApiUrl": "https://api.openai.com/v1/responses",
    "BearerToken": "sk-proj-..."
  },
  "Gemini": {
    "ApiKey": "AIzaSy..."
  }
}
```

## Example Queries to Try

1. **General Knowledge**: "What is the height of Burj Khalifa?"
2. **Recent Events**: "Who won the Nobel Prize in 2024?"
3. **Business Info**: "Find details about Neil Moodie Studio at E1 6EA"
4. **Technology**: "What are the latest AI developments?"
5. **Travel**: "Best restaurants in Paris"

## Troubleshooting

**Port already in use?**
```bash
dotnet run --urls "https://localhost:5001;http://localhost:5000"
```

**Can't access Swagger?**
- Make sure you're using HTTPS: `https://localhost:7xxx/swagger`
- Check the console output for the correct port number

**API errors?**
- Verify API keys in `appsettings.json`
- Check API quotas for OpenAI and Gemini
- Review logs in the console output

## Next Steps

1. **Test the endpoints** using Swagger or PowerShell
2. **Review the code** in each service to understand the workflow
3. **Customize** the system prompt in OpenAIService.cs or GeminiService.cs
4. **Add features** like caching, retry logic, or additional search providers
5. **Secure the API** with authentication before deploying

## Important Notes

- Build Status: ✅ **SUCCESS** (0 warnings, 0 errors)
- The API keys in `appsettings.json` are sensitive - don't commit them to Git
- Rate limits apply to both OpenAI and Gemini APIs
- First requests may be slower as services initialize

Enjoy your multi-layer search agent! 🚀
