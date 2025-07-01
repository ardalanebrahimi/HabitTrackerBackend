// Controllers/UserAuthController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using Google.Apis.Auth.OAuth2.Requests;
using Microsoft.AspNetCore.Authorization;

[ApiController]
[Route("api/[controller]")]
public class UserController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;

    public UserController(AppDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
        {
            throw new UnauthorizedAccessException("Invalid or missing User ID.");
        }
        return userId;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterUserRequest request)
    {
        // Check if the email already exists
        if (await _context.Users.AnyAsync(c => c.Email == request.Email))
        {
            return BadRequest(new { message = "Email already exists." });
        }

        // Hash the password
        string passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

        // Generate a refresh token
        var refreshToken = GenerateRefreshToken();

        // Create and save the new user
        var newUser = new User
        {
            UserName = request.UserName,
            Email = request.Email,
            PasswordHash = passwordHash,
            RefreshToken = refreshToken,
            RefreshTokenExpiryTime = DateTime.UtcNow.AddMonths(6) // Set the refresh token expiry time
        };

        _context.Users.Add(newUser);
        await _context.SaveChangesAsync();

        return Ok(new { message = "User registration successful" });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] UserLoginRequest   loginRequest)
    {
        var user = await _context.Users.FirstOrDefaultAsync(c => c.Email == loginRequest.Email);
        if (user == null || !BCrypt.Net.BCrypt.Verify(loginRequest.Password, user.PasswordHash))
        {
            return Unauthorized(new { message = "Invalid email or password" });
        }

        // Generate both tokens
        var accessToken = GenerateJwtToken(user);
        var refreshToken = GenerateRefreshToken();

        // Save the refresh token in the database
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddMonths(6); // Extend to 6 months
        await _context.SaveChangesAsync();

        return Ok(new { 
            AccessToken = accessToken, 
            RefreshToken = refreshToken,
            UserName = user.UserName
        });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest model)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.RefreshToken == model.RefreshToken);

        if (user == null || user.RefreshTokenExpiryTime < DateTime.UtcNow)
        {
            return Unauthorized(new { message = "Invalid or expired refresh token." });
        }

        // Generate new access token and refresh token
        var newAccessToken = GenerateJwtToken(user);
        var newRefreshToken = GenerateRefreshToken();

        // Update the refresh token in the database
        user.RefreshToken = newRefreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddMonths(6); // Extend refresh token validity
        await _context.SaveChangesAsync();

        return Ok(new { 
            AccessToken = newAccessToken, 
            RefreshToken = newRefreshToken,
            UserName = user.UserName
        });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Unauthorized();

        var user = await _context.Users.FindAsync(Guid.Parse(userId));
        if (user == null) return Unauthorized();

        // Clear refresh token on logout
        user.RefreshToken = string.Empty;
        user.RefreshTokenExpiryTime = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new { message = "Logout successful" });
    }

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Unauthorized();

        var user = await _context.Users.FindAsync(Guid.Parse(userId));
        if (user == null) return NotFound();

        return Ok(new { 
            Id = user.Id,
            UserName = user.UserName,
            Email = user.Email
        });
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Unauthorized();

        var user = await _context.Users.FindAsync(Guid.Parse(userId));
        if (user == null) return NotFound();

        // Check if the new email is already taken by another user
        if (!string.IsNullOrEmpty(request.Email) && request.Email != user.Email)
        {
            var emailExists = await _context.Users.AnyAsync(u => u.Email == request.Email && u.Id != user.Id);
            if (emailExists)
            {
                return BadRequest(new { message = "Email already exists." });
            }
            user.Email = request.Email;
        }

        // Check if the new username is already taken by another user
        if (!string.IsNullOrEmpty(request.UserName) && request.UserName != user.UserName)
        {
            var usernameExists = await _context.Users.AnyAsync(u => u.UserName == request.UserName && u.Id != user.Id);
            if (usernameExists)
            {
                return BadRequest(new { message = "Username already exists." });
            }
            user.UserName = request.UserName;
        }

        await _context.SaveChangesAsync();

        return Ok(new { 
            message = "Profile updated successfully",
            user = new {
                Id = user.Id,
                UserName = user.UserName,
                Email = user.Email
            }
        });
    }

    [HttpGet("token-balance")]
    [Authorize]
    public async Task<ActionResult<TokenBalanceDTO>> GetTokenBalance()
    {
        try
        {
            var userId = GetUserId();
            var subscriptionService = HttpContext.RequestServices.GetRequiredService<SubscriptionService>();
            var tokenBalance = await subscriptionService.GetTokenBalanceAsync(userId);
            return Ok(tokenBalance);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving token balance: {ex.Message}");
        }
    }

    [HttpPost("spend-token")]
    [Authorize]
    public async Task<ActionResult<TokenBalanceDTO>> SpendToken([FromBody] SpendTokenRequest request)
    {
        try
        {
            var userId = GetUserId();
            var subscriptionService = HttpContext.RequestServices.GetRequiredService<SubscriptionService>();
            var tokenBalance = await subscriptionService.SpendTokensAsync(userId, request);
            return Ok(tokenBalance);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error spending tokens: {ex.Message}");
        }
    }

    [HttpPost("earn-token")]
    [Authorize]
    public async Task<ActionResult<TokenBalanceDTO>> EarnToken([FromBody] EarnTokenRequest request)
    {
        try
        {
            var userId = GetUserId();
            var subscriptionService = HttpContext.RequestServices.GetRequiredService<SubscriptionService>();
            var tokenBalance = await subscriptionService.EarnTokensAsync(userId, request);
            return Ok(tokenBalance);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error earning tokens: {ex.Message}");
        }
    }

    [HttpGet("subscription/status")]
    [Authorize]
    public async Task<ActionResult<SubscriptionStatusDTO>> GetSubscriptionStatus()
    {
        try
        {
            var userId = GetUserId();
            var subscriptionService = HttpContext.RequestServices.GetRequiredService<SubscriptionService>();
            var status = await subscriptionService.GetSubscriptionStatusAsync(userId);
            return Ok(status);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving subscription status: {ex.Message}");
        }
    }

    [HttpGet("referral-code")]
    [Authorize]
    public async Task<ActionResult<object>> GetReferralCode()
    {
        try
        {
            var userId = GetUserId();
            var subscriptionService = HttpContext.RequestServices.GetRequiredService<SubscriptionService>();
            var referralCode = await subscriptionService.GenerateReferralCodeAsync(userId);
            return Ok(new { referralCode });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error generating referral code: {ex.Message}");
        }
    }

    [HttpPost("apply-referral")]
    [Authorize]
    public async Task<ActionResult<TokenBalanceDTO>> ApplyReferralCode([FromBody] ReferralCodeRequest request)
    {
        try
        {
            var userId = GetUserId();
            var subscriptionService = HttpContext.RequestServices.GetRequiredService<SubscriptionService>();
            
            var success = await subscriptionService.ProcessReferralAsync(userId, request.ReferralCode);
            if (!success)
            {
                return BadRequest("Invalid referral code or already used");
            }
            
            var tokenBalance = await subscriptionService.GetTokenBalanceAsync(userId);
            return Ok(tokenBalance);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error applying referral code: {ex.Message}");
        }
    }

    [HttpGet("token-history")]
    [Authorize]
    public async Task<ActionResult<List<TokenTransactionDTO>>> GetTokenHistory([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        try
        {
            var userId = GetUserId();
            
            var transactions = await _context.TokenTransactions
                .Where(tt => tt.UserId == userId)
                .OrderByDescending(tt => tt.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(tt => new TokenTransactionDTO
                {
                    Id = tt.Id,
                    Amount = tt.Amount,
                    TransactionType = tt.TransactionType,
                    Description = tt.Description,
                    CreatedAt = tt.CreatedAt
                })
                .ToListAsync();
                
            return Ok(transactions);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving token history: {ex.Message}");
        }
    }

    private string GenerateJwtToken(User user)
    {
        var jwtKey = _configuration["Jwt:Key"];
        var jwtIssuer = _configuration["Jwt:Issuer"];
        
        if (string.IsNullOrEmpty(jwtKey) || string.IsNullOrEmpty(jwtIssuer))
            throw new InvalidOperationException("JWT configuration is missing");

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email ?? "")
        };

        var token = new JwtSecurityToken(
            issuer: jwtIssuer,
            audience: jwtIssuer,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1), // Short-lived access token
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private string GenerateRefreshToken()
    {
        var randomNumber = new byte[64]; // More secure refresh token
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomNumber);
        }
        return Convert.ToBase64String(randomNumber);
    }
}
public class UserLoginRequest
{
    public required string Email { get; set; }
    public required string Password { get; set; }
}

public class RefreshTokenRequest
{
    public required string RefreshToken { get; set; }
}