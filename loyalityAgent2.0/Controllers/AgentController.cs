using Microsoft.AspNetCore.Mvc;
using loyalityAgent2._0.Models;
using loyalityAgent2._0.Services;
using loyalityAgent2._0.Data;
using Microsoft.EntityFrameworkCore;

namespace loyalityAgent2._0.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AgentController : ControllerBase
    {
        private readonly IAgentOrchestratorService _orchestratorService;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AgentController> _logger;

        public AgentController(
            IAgentOrchestratorService orchestratorService,
            ApplicationDbContext context,
            ILogger<AgentController> logger)
        {
            _orchestratorService = orchestratorService;
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Process a business loyalty request through the multi-layer agent workflow
        /// </summary>
        /// <param name="request">The business request containing name, category, and address</param>
        /// <returns>Loyalty offer recommendation based on business analysis</returns>
        [HttpPost("loyalty")]
        public async Task<ActionResult<AgentResponse>> ProcessLoyalty([FromBody] AgentRequest request)
        {
            // Validate UserId
            if (request.UserId <= 0)
            {
                return BadRequest(new AgentResponse
                {
                    Success = false,
                    ErrorMessage = "Valid user ID is required"
                });
            }

            // Get user's API key from database
            var user = await _context.AgentUsers.FindAsync(request.UserId);
            if (user == null)
            {
                return Unauthorized(new AgentResponse
                {
                    Success = false,
                    ErrorMessage = "User not found. Please log in again."
                });
            }

            if (string.IsNullOrWhiteSpace(user.GoogleApiKey))
            {
                return BadRequest(new AgentResponse
                {
                    Success = false,
                    ErrorMessage = "Google API key not configured for this user"
                });
            }

            if (string.IsNullOrWhiteSpace(request.BusinessName))
            {
                return BadRequest(new AgentResponse
                {
                    Success = false,
                    ErrorMessage = "Business name cannot be empty"
                });
            }

            if (string.IsNullOrWhiteSpace(request.Category))
            {
                return BadRequest(new AgentResponse
                {
                    Success = false,
                    ErrorMessage = "Category cannot be empty"
                });
            }

            if (string.IsNullOrWhiteSpace(request.FullAddress))
            {
                return BadRequest(new AgentResponse
                {
                    Success = false,
                    ErrorMessage = "Full address cannot be empty"
                });
            }

            _logger.LogInformation("Received loyalty request for user: {UserId}, business: {BusinessName}, Category: {Category}, MinSpent: £{MinimumSpent}",
                request.UserId, request.BusinessName, request.Category, request.MinimumSpent);

            var result = await _orchestratorService.ProcessBusinessLoyaltyAsync(
                user.GoogleApiKey,
                request.BusinessName,
                request.Category,
                request.FullAddress,
                request.MinimumSpent,
                request.ConnectionId);

            if (!result.Success)
            {
                return StatusCode(500, result);
            }

            return Ok(result);
        }

        /// <summary>
        /// Health check endpoint
        /// </summary>
        [HttpGet("health")]
        public IActionResult Health()
        {
            return Ok(new
            {
                Status = "Healthy",
                Service = "Loyalty Agent 2.0",
                Timestamp = DateTime.UtcNow
            });
        }
    }
}
