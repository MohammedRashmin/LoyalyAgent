using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using loyalityAgent2._0.Data;
using loyalityAgent2._0.Models;
using System.Security.Cryptography;
using System.Text;

namespace loyalityAgent2._0.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AuthController> _logger;

        public AuthController(ApplicationDbContext context, ILogger<AuthController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpPost("register")]
        public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request)
        {
            try
            {
                // Check if user already exists
                var existingUser = await _context.AgentUsers
                    .FirstOrDefaultAsync(u => u.Email.ToLower() == request.Email.ToLower());

                if (existingUser != null)
                {
                    return BadRequest(new AuthResponse
                    {
                        Success = false,
                        Message = "User with this email already exists"
                    });
                }

                // Hash password
                var passwordHash = HashPassword(request.Password);

                // Create new user
                var user = new User
                {
                    Email = request.Email.ToLower(),
                    PasswordHash = passwordHash,
                    GoogleApiKey = request.GoogleApiKey,
                    CreatedAt = DateTime.UtcNow
                };

                _context.AgentUsers.Add(user);
                await _context.SaveChangesAsync();

                _logger.LogInformation("New user registered: {Email}", user.Email);

                return Ok(new AuthResponse
                {
                    Success = true,
                    Message = "Registration successful",
                    UserId = user.UserId,
                    Email = user.Email
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during registration");
                return StatusCode(500, new AuthResponse
                {
                    Success = false,
                    Message = "An error occurred during registration"
                });
            }
        }

        [HttpPost("login")]
        public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
        {
            try
            {
                var user = await _context.AgentUsers
                    .FirstOrDefaultAsync(u => u.Email.ToLower() == request.Email.ToLower());

                if (user == null)
                {
                    return Unauthorized(new AuthResponse
                    {
                        Success = false,
                        Message = "Invalid email or password"
                    });
                }

                // Verify password
                if (!VerifyPassword(request.Password, user.PasswordHash))
                {
                    return Unauthorized(new AuthResponse
                    {
                        Success = false,
                        Message = "Invalid email or password"
                    });
                }

                // Update last login
                user.LastLoginAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                _logger.LogInformation("User logged in: {Email}", user.Email);

                return Ok(new AuthResponse
                {
                    Success = true,
                    Message = "Login successful",
                    UserId = user.UserId,
                    Email = user.Email
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login");
                return StatusCode(500, new AuthResponse
                {
                    Success = false,
                    Message = "An error occurred during login"
                });
            }
        }

        [HttpGet("user/{userId}")]
        public async Task<ActionResult<User>> GetUser(int userId)
        {
            try
            {
                var user = await _context.AgentUsers.FindAsync(userId);

                if (user == null)
                {
                    return NotFound(new { Message = "User not found" });
                }

                // Return user without password hash
                return Ok(new
                {
                    userId = user.UserId,
                    email = user.Email,
                    createdAt = user.CreatedAt,
                    lastLoginAt = user.LastLoginAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user");
                return StatusCode(500, new { Message = "Error retrieving user" });
            }
        }

        private string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(hashedBytes);
            }
        }

        private bool VerifyPassword(string password, string hashedPassword)
        {
            var hashOfInput = HashPassword(password);
            return hashOfInput == hashedPassword;
        }
    }
}
