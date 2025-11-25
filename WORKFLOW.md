# Loyalty Agent 2.0 - Workflow Documentation

## Overview
The Loyalty Agent 2.0 is an intelligent system that analyzes businesses and recommends personalized loyalty offers based on their business type, products, services, and market position.

## API Endpoint

**POST** `/api/agent/loyalty`

### Request Body
```json
{
  "businessName": "Example Coffee Shop",
  "category": "Restaurant/Cafe",
  "fullAddress": "123 Main St, New York, NY 10001"
}
```

### Response
```json
{
  "businessAttributes": {
    "businessModel": "B2C Retail",
    "coreProductsOrServices": "Coffee, pastries, sandwiches",
    "targetAudience": "Urban professionals, students",
    "businessToneOrStyle": "Casual, modern",
    "popularityOrSize": "Local neighborhood cafe",
    "specializationKeywords": "Artisan coffee, organic ingredients",
    "businessType": "Hybrid"
  },
  "loyaltyOffer": {
    "type": "FreeProduct",
    "description": "Get one small coffee absolutely free!",
    "productOrServiceName": "Small Coffee",
    "discountPercentage": null,
    "isFreeToken": false,
    "reasoning": "Offering a free small coffee is an excellent loyalty incentive..."
  },
  "workflowPath": "Hybrid -> Product (Free) approved",
  "success": true,
  "errorMessage": null
}
```

## Workflow Architecture

### Step 1: Business Attribute Extraction
Using Google Search via Gemini API, the system extracts 7 key business attributes:

1. **BusinessModel** - B2B, B2C, subscription, retail, etc.
2. **CoreProductsOrServices** - Main offerings
3. **TargetAudience** - Customer demographics
4. **BusinessToneOrStyle** - Brand personality
5. **PopularityOrSize** - Market reach (local, regional, national, international)
6. **SpecializationKeywords** - Unique selling points
7. **BusinessType** - Classified as Product, Service, or Hybrid

### Step 2: Three-Layer Business Classification

#### Layer 1: Product-Based Businesses
For businesses that primarily sell products:

1. **Product Analysis**
   - Gemini searches and identifies all products
   - Filters for popular products
   - Selects ONE product that is:
     - Easy to give away free
     - Low cost impact
     - Attractive to customers
     - Suitable as loyalty incentive

2. **Reasoning Model**
   - Evaluates if the selected product can be given for free
   - Analyzes impact on business revenue
   - Assesses customer attraction value

3. **Decision Path**
   - **If Approved**: Offer the product for free
   - **If Not Approved**:
     - Option 1: Offer percentage discount (based on business capability)
     - Option 2: Fallback to 1 free loyalty token

#### Layer 2: Service-Based Businesses
For businesses that primarily provide services:

1. **Service Analysis**
   - Gemini identifies all services offered
   - Filters for popular services
   - Selects ONE discountable service that:
     - Is popular enough to attract customers
     - Is not the core/premium service
     - Is suitable for discounting

2. **Discount Reasoning Model**
   - Determines appropriate discount percentage (10%, 15%, 20%, 25%, etc.)
   - Considers business size and capability
   - Balances customer attraction with profitability

3. **Decision Path**
   - **If Approved**: Return discount percentage for the service
   - **If Not Approved**: Fallback to 1 free loyalty token

#### Layer 3: Hybrid Businesses
For businesses offering both products and services:

1. **Priority: Product Workflow First**
   - Attempts the complete product-based workflow
   - If a free product is approved → Return product offer

2. **Fallback: Service Workflow**
   - If product workflow fails, try service workflow
   - If service discount is approved → Return service offer

3. **Ultimate Fallback**
   - If both workflows fail → Return 1 free loyalty token

## Offer Types

1. **FreeProduct**: One specific product for free
2. **DiscountedService**: Percentage discount on a service
3. **PercentageDiscount**: Percentage discount on a product
4. **FreeToken**: One loyalty token (universal fallback)

## Key Features

### Single Web Search
- Only ONE Google search is performed (at the beginning)
- Eliminates duplicate searches
- All subsequent analysis uses Gemini's reasoning without additional searches

### Intelligent Reasoning
- Each decision is backed by AI reasoning
- Considers business capability and market position
- Provides explanations for all recommendations

### Automatic Fallbacks
- Always provides a valid offer
- Gracefully handles edge cases
- Ensures businesses always get a recommendation

## Example Workflows

### Example 1: Local Coffee Shop (Hybrid → Product)
```
Input: "Joe's Coffee", "Cafe", "123 Main St, Seattle, WA"

Step 1: Extract attributes → BusinessType = Hybrid
Step 2: Try product workflow → Found "Small Coffee"
Step 3: Reason → Approved (low cost, high appeal)
Result: Free Small Coffee

Workflow Path: "Hybrid -> Product (Free) approved"
```

### Example 2: Hair Salon (Service)
```
Input: "Bella Hair Salon", "Beauty Salon", "456 Oak Ave, Miami, FL"

Step 1: Extract attributes → BusinessType = Service
Step 2: Analyze services → Found "Basic Haircut"
Step 3: Reason discount → 15% approved
Result: 15% off Basic Haircut

Workflow Path: "Service -> Discount approved (15%)"
```

### Example 3: Luxury Watch Store (Product → Fallback)
```
Input: "Premium Timepieces", "Jewelry", "789 5th Ave, New York, NY"

Step 1: Extract attributes → BusinessType = Product
Step 2: Analyze products → Found "Watch Cleaning Kit"
Step 3: Reason → Not approved (even small items too valuable)
Step 4: Reason suggests 10% discount instead
Result: 10% off Watch Cleaning Kit

Workflow Path: "Product -> Discount fallback (10%)"
```

## Configuration

Ensure your `appsettings.json` contains:

```json
{
  "Gemini": {
    "ApiKey": "your-gemini-api-key-here"
  }
}
```

## Dependencies

- .NET 8.0
- Google Gemini API 2.5 Flash
- ASP.NET Core Web API

## Benefits of This Workflow

1. **No Double Searching**: Single Google search at the start
2. **Intelligent Classification**: Automatic business type detection
3. **Context-Aware Offers**: Recommendations based on business capability
4. **Always Succeeds**: Fallback mechanisms ensure valid output
5. **Transparent Reasoning**: All decisions include explanations
6. **Scalable**: Works for businesses of any size or type
