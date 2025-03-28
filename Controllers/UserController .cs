// Controllers/UserAuthController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using Google.Apis.Auth.OAuth2.Requests;

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
        user.RefreshToken = null;
        user.RefreshTokenExpiryTime = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new { message = "Logout successful" });
    }

    private string GenerateJwtToken(User user)
    {
        var jwtKey = _configuration["Jwt:Key"];
        var jwtIssuer = _configuration["Jwt:Issuer"];
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email)
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
    public string Email { get; set; }
    public string Password { get; set; }
}

public class RefreshTokenRequest
{
    public string RefreshToken { get; set; }
}