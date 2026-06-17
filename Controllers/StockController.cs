using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockMindAI.API.Services;

namespace StockMindAI.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class StockController : ControllerBase
    {
        private readonly IStockService _stockService;

        public StockController(IStockService stockService)
        {
            _stockService = stockService;
        }

        [HttpGet("details")]
        public async Task<IActionResult> GetStockDetails([FromQuery] string symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol)) return BadRequest("Symbol parameter is required.");
            var userId = GetUserIdFromToken();
            var data = await _stockService.GetStockDetailsAsync(userId, symbol);
            if (data == null) return NotFound($"Could not retrieve stock details for {symbol}. Verify symbol is valid.");
            return Ok(data);
        }

        [HttpGet("search")]
        public async Task<IActionResult> SearchStock([FromQuery] string q)
        {
            if (string.IsNullOrWhiteSpace(q)) 
                return Ok(new { quotes = new object[0] });

            var data = await _stockService.SearchStockAsync(q);
            if (data == null) 
                return Ok(new { quotes = new object[0] });

            return Ok(data);
        }

        [HttpGet("watchlist")]
        public async Task<IActionResult> GetWatchlist()
        {
            var userId = GetUserIdFromToken();
            var watchlist = await _stockService.GetWatchlistAsync(userId);
            return Ok(watchlist);
        }

        [HttpPost("watchlist/{symbol}")]
        public async Task<IActionResult> AddToWatchlist(string symbol)
        {
            var userId = GetUserIdFromToken();
            var success = await _stockService.AddToWatchlistAsync(userId, symbol);
            if (!success) return BadRequest("Could not add to watchlist.");
            return Ok(new { Message = "Added to watchlist successfully." });
        }

        [HttpDelete("watchlist/{symbol}")]
        public async Task<IActionResult> RemoveFromWatchlist(string symbol)
        {
            var userId = GetUserIdFromToken();
            var success = await _stockService.RemoveFromWatchlistAsync(userId, symbol);
            if (!success) return NotFound("Symbol not found on watchlist.");
            return Ok(new { Message = "Removed from watchlist successfully." });
        }

        [HttpGet("alerts")]
        public async Task<IActionResult> GetAlerts()
        {
            var userId = GetUserIdFromToken();
            var alerts = await _stockService.GetActiveAlertsAsync(userId);
            return Ok(alerts);
        }

        [HttpPost("alerts/read")]
        public async Task<IActionResult> MarkAlertsAsRead()
        {
            var userId = GetUserIdFromToken();
            await _stockService.MarkAlertsAsReadAsync(userId);
            return Ok(new { Message = "Alerts marked as read." });
        }

        private int GetUserIdFromToken()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null ? int.Parse(claim.Value) : 0;
        }
    }
}
