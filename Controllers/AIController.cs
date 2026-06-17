using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StockMindAI.API.Data;
using StockMindAI.API.DTOs;
using StockMindAI.API.Models;
using StockMindAI.API.Services;

namespace StockMindAI.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class AIController : ControllerBase
    {
        private readonly IAICommunicationService _aiService;
        private readonly IPortfolioService _portfolioService;
        private readonly ApplicationDbContext _dbContext;

        public AIController(IAICommunicationService aiService, IPortfolioService portfolioService, ApplicationDbContext dbContext)
        {
            _aiService = aiService;
            _portfolioService = portfolioService;
            _dbContext = dbContext;
        }

        [HttpPost("chat")]
        public async Task<IActionResult> Chat([FromBody] ChatRequestDTO request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userId = GetUserIdFromToken();
            var user = await _dbContext.Users.FindAsync(userId);
            var riskProfile = user?.RiskAppetite ?? "Medium";

            // 1. Fetch user chat history (last 10 messages to keep context window clean)
            var history = await _dbContext.ChatHistories
                .Where(c => c.UserId == userId)
                .OrderBy(c => c.CreatedAt)
                .SuffixHistory(10) // fetch last 10
                .ToListAsync();

            // 2. Fetch holdings for portfolio risk context
            var holdings = await _portfolioService.GetUserHoldingsAsync(userId);

            // 3. Save User message
            var userMsg = new ChatHistory { UserId = userId, Sender = "User", Message = request.Question };
            _dbContext.ChatHistories.Add(userMsg);
            await _dbContext.SaveChangesAsync();

            // 4. Contact AI engine for response
            var response = await _aiService.GetChatAdvisoryAsync(request.Question, history, request.ActiveSymbol, riskProfile, holdings);
            
            if (response != null)
            {
                // 5. Save AI response
                var aiMsg = new ChatHistory { UserId = userId, Sender = "AI", Message = response.Answer };
                _dbContext.ChatHistories.Add(aiMsg);
                await _dbContext.SaveChangesAsync();

                return Ok(response);
            }

            return StatusCode(500, "Could not contact AI advisor service.");
        }

        [HttpGet("chat/history")]
        public async Task<IActionResult> GetChatHistory()
        {
            var userId = GetUserIdFromToken();
            var history = await _dbContext.ChatHistories
                .Where(c => c.UserId == userId)
                .OrderBy(c => c.CreatedAt)
                .Take(50)
                .ToListAsync();
            return Ok(history);
        }

        [HttpPost("chat/clear")]
        public async Task<IActionResult> ClearChatHistory()
        {
            var userId = GetUserIdFromToken();
            var items = await _dbContext.ChatHistories.Where(c => c.UserId == userId).ToListAsync();
            _dbContext.ChatHistories.RemoveRange(items);
            await _dbContext.SaveChangesAsync();
            return Ok(new { Message = "Chat history cleared successfully." });
        }

        [HttpGet("predict")]
        public async Task<IActionResult> GetPredictions([FromQuery] string symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol)) return BadRequest("Symbol parameter is required.");
            var data = await _aiService.GetStockPredictionAsync(symbol);
            if (data == null) return NotFound($"Could not generate predictions for {symbol}.");
            return Ok(data);
        }

        [HttpGet("recommend")]
        public async Task<IActionResult> GetRecommendation([FromQuery] string symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol)) return BadRequest("Symbol parameter is required.");
            var userId = GetUserIdFromToken();
            var user = await _dbContext.Users.FindAsync(userId);
            var riskProfile = user?.RiskAppetite ?? "Medium";

            var rec = await _aiService.GetStockRecommendationAsync(symbol, riskProfile);
            if (rec == null) return NotFound($"Could not generate recommendation for {symbol}.");
            return Ok(rec);
        }

        [HttpGet("market-summary")]
        public async Task<IActionResult> GetMarketSummary()
        {
            var summary = await _aiService.GetMarketSummaryAsync();
            return Content(summary, "application/json");
        }

        private int GetUserIdFromToken()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null ? int.Parse(claim.Value) : 0;
        }
    }

    public static class ChatHistoryExtensions
    {
        public static IQueryable<ChatHistory> SuffixHistory(this IQueryable<ChatHistory> query, int count)
        {
            return query.OrderByDescending(c => c.CreatedAt).Take(count).OrderBy(c => c.CreatedAt);
        }
    }
}
