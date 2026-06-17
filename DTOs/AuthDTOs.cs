using System.ComponentModel.DataAnnotations;

namespace StockMindAI.API.DTOs
{
    public class RegisterRequest
    {
        [Required]
        [MinLength(3)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MinLength(6)]
        public string Password { get; set; } = string.Empty;

        public string RiskAppetite { get; set; } = "Medium";
        public string? InvestmentGoals { get; set; }
    }

    public class LoginRequest
    {
        [Required]
        public string UsernameOrEmail { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }

    public class AuthResponse
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public string RiskAppetite { get; set; } = "Medium";
        public string? InvestmentGoals { get; set; }
    }

    public class ProfileUpdateRequest
    {
        public string RiskAppetite { get; set; } = "Medium";
        public string? InvestmentGoals { get; set; }
    }
}
