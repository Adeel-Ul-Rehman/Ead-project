using attendence.Data.Data;
using attendence.Services.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace attendenceProject.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IJwtService _jwtService;
        private readonly IPasswordHasher _passwordHasher;

        public AuthController(
            ApplicationDbContext context,
            IJwtService jwtService,
            IPasswordHasher passwordHasher)
        {
            _context = context;
            _jwtService = jwtService;
            _passwordHasher = passwordHasher;
        }

        /// <summary>
        /// Login endpoint that returns JWT token
        /// POST: api/auth/login
        /// </summary>
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { message = "Invalid request", errors = ModelState });
            }

            // Find user by email
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == request.Email);

            if (user == null)
            {
                return Unauthorized(new { message = "Invalid email or password" });
            }

            // Verify password
            if (!_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
            {
                return Unauthorized(new { message = "Invalid email or password" });
            }

            // Generate JWT token
            var token = _jwtService.GenerateToken(
                user.Id.ToString(),
                user.Email,
                user.Role,
                user.FullName
            );

            return Ok(new LoginResponse
            {
                Token = token,
                UserId = user.Id,
                Email = user.Email,
                FullName = user.FullName,
                Role = user.Role,
                ExpiresIn = 480 * 60 // 8 hours in seconds
            });
        }

        /// <summary>
        /// Validate token endpoint
        /// GET: api/auth/validate
        /// </summary>
        [HttpGet("validate")]
        [Authorize(AuthenticationSchemes = "Bearer")]
        public IActionResult ValidateToken()
        {
            var userId = User.FindFirst("UserId")?.Value;
            var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
            var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            var fullName = User.FindFirst("FullName")?.Value;

            return Ok(new
            {
                valid = true,
                userId,
                email,
                role,
                fullName
            });
        }

        /// <summary>
        /// Refresh token endpoint (placeholder for future implementation)
        /// POST: api/auth/refresh
        /// </summary>
        [HttpPost("refresh")]
        [AllowAnonymous]
        public IActionResult RefreshToken([FromBody] RefreshTokenRequest request)
        {
            // TODO: Implement refresh token logic with refresh token storage
            return StatusCode(501, new { message = "Refresh token not implemented yet" });
        }
    }

    // DTOs for API requests/responses
    public class LoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class LoginResponse
    {
        public string Token { get; set; } = string.Empty;
        public int UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public int ExpiresIn { get; set; }
    }

    public class RefreshTokenRequest
    {
        public string RefreshToken { get; set; } = string.Empty;
    }
}
