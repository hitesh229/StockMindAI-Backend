using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockMindAI.API.Services;

namespace StockMindAI.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class NewsController : ControllerBase
    {
        private readonly INewsService _newsService;

        public NewsController(INewsService newsService)
        {
            _newsService = newsService;
        }

        [HttpGet("{symbol}")]
        public async Task<IActionResult> GetNews(string symbol)
        {
            var news = await _newsService.GetStockNewsAsync(symbol);
            return Ok(news);
        }

        [HttpGet("explain/{symbol}")]
        public async Task<IActionResult> ExplainMovement(string symbol)
        {
            var explanation = await _newsService.ExplainStockMovementAsync(symbol);
            return Ok(new { Symbol = symbol, Explanation = explanation });
        }
    }
}
