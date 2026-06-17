using System;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StockMindAI.API.Data;
using StockMindAI.API.DTOs;
using StockMindAI.API.Services;

namespace StockMindAI.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class PortfolioController : ControllerBase
    {
        private readonly IPortfolioService _portfolioService;
        private readonly ApplicationDbContext _dbContext;

        public PortfolioController(IPortfolioService portfolioService, ApplicationDbContext dbContext)
        {
            _portfolioService = portfolioService;
            _dbContext = dbContext;
        }

        [HttpGet]
        public async Task<IActionResult> GetPortfolio()
        {
            var userId = GetUserIdFromToken();
            var details = await _portfolioService.GetPortfolioDetailsAsync(userId);
            return Ok(details);
        }

        [HttpPost]
        public async Task<IActionResult> AddHolding([FromBody] HoldingRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userId = GetUserIdFromToken();
            var holding = await _portfolioService.AddHoldingAsync(userId, request);
            if (holding == null) return BadRequest("Could not add stock holding.");
            return Ok(holding);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteHolding(int id)
        {
            var userId = GetUserIdFromToken();
            var success = await _portfolioService.RemoveHoldingAsync(userId, id);
            if (!success) return NotFound("Holding not found.");
            return Ok(new { Message = "Holding deleted successfully." });
        }

        [HttpGet("risk")]
        public async Task<IActionResult> GetPortfolioRiskReport()
        {
            var userId = GetUserIdFromToken();
            var user = await _dbContext.Users.FindAsync(userId);
            var riskProfile = user?.RiskAppetite ?? "Medium";

            var report = await _portfolioService.GetPortfolioRiskReportAsync(userId, riskProfile);
            return Content(report, "application/json");
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadPortfolioCsv(IFormFile file)
        {
            if (file == null || file.Length == 0) return BadRequest("No file was uploaded.");
            if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)) return BadRequest("Invalid file type. Only CSV files are supported.");

            var userId = GetUserIdFromToken();
            try
            {
                using (var stream = file.OpenReadStream())
                {
                    var count = await _portfolioService.UploadPortfolioCsvAsync(userId, stream);
                    return Ok(new { Message = $"Portfolio imported successfully. {count} holdings added.", Count = count });
                }
            }
            catch (FormatException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred during file parsing: {ex.Message}");
            }
        }

        private int GetUserIdFromToken()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null ? int.Parse(claim.Value) : 0;
        }
    }
}
