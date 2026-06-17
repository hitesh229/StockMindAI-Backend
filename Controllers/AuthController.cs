using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using StockMindAI.API.Data;
using StockMindAI.API.DTOs;
using StockMindAI.API.Models;

namespace StockMindAI.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IConfiguration _configuration;

        public AuthController(ApplicationDbContext dbContext, IConfiguration configuration)
        {
            _dbContext = dbContext;
            _configuration = configuration;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var usernameExists = await _dbContext.Users.AnyAsync(u => u.Username.ToLower() == request.Username.ToLower());
            if (usernameExists) return BadRequest("Username is already taken.");

            var emailExists = await _dbContext.Users.AnyAsync(u => u.Email.ToLower() == request.Email.ToLower());
            if (emailExists) return BadRequest("Email is already registered.");

            var hash = ComputeHash(request.Password);
            var user = new User
            {
                Username = request.Username,
                Email = request.Email,
                PasswordHash = hash,
                RiskAppetite = request.RiskAppetite,
                InvestmentGoals = request.InvestmentGoals
            };

            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            var token = GenerateJwtToken(user);
            return Ok(new AuthResponse
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Token = token,
                RiskAppetite = user.RiskAppetite,
                InvestmentGoals = user.InvestmentGoals
            });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var input = request.UsernameOrEmail.ToLower();
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == input || u.Email.ToLower() == input);
            if (user == null) return Unauthorized("Invalid username or password.");

            var hash = ComputeHash(request.Password);
            if (user.PasswordHash != hash) return Unauthorized("Invalid username or password.");

            var token = GenerateJwtToken(user);
            return Ok(new AuthResponse
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Token = token,
                RiskAppetite = user.RiskAppetite,
                InvestmentGoals = user.InvestmentGoals
            });
        }

        [Authorize]
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            var userId = GetUserIdFromToken();
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null) return NotFound("User not found.");

            return Ok(new
            {
                user.Id,
                user.Username,
                user.Email,
                user.RiskAppetite,
                user.InvestmentGoals,
                user.CreatedAt
            });
        }

        [Authorize]
        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] ProfileUpdateRequest request)
        {
            var userId = GetUserIdFromToken();
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null) return NotFound("User not found.");

            user.RiskAppetite = request.RiskAppetite;
            user.InvestmentGoals = request.InvestmentGoals;
            user.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();
            return Ok(new
            {
                user.Id,
                user.Username,
                user.RiskAppetite,
                user.InvestmentGoals,
                Message = "Profile updated successfully."
            });
        }

        private string ComputeHash(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(password);
                var hashBytes = sha256.ComputeHash(bytes);
                return Convert.ToBase64String(hashBytes);
            }
        }

        private string GenerateJwtToken(User user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var keyStr = _configuration["JwtSettings:Secret"] ?? "SuperSecretKeyStockMindAI_2026_AdvancedSecured_KeyRequirement_MustBe32Chars!";
            var key = Encoding.ASCII.GetBytes(keyStr);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Name, user.Username),
                    new Claim(ClaimTypes.Email, user.Email)
                }),
                Expires = DateTime.UtcNow.AddMinutes(double.Parse(_configuration["JwtSettings:ExpiryInMinutes"] ?? "1440")),
                Issuer = _configuration["JwtSettings:Issuer"],
                Audience = _configuration["JwtSettings:Audience"],
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        private int GetUserIdFromToken()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null ? int.Parse(claim.Value) : 0;
        }
    }
}
