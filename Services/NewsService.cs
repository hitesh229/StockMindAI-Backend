using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StockMindAI.API.Data;
using StockMindAI.API.Models;

namespace StockMindAI.API.Services
{
    public interface INewsService
    {
        Task<List<NewsCache>> GetStockNewsAsync(string symbol);
        Task<string> ExplainStockMovementAsync(string symbol);
    }

    public class NewsService : INewsService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IAICommunicationService _aiService;
        private readonly HttpClient _httpClient;
        private readonly string _fastApiUrl;

        public NewsService(ApplicationDbContext dbContext, IAICommunicationService aiService, HttpClient httpClient, IConfiguration configuration)
        {
            _dbContext = dbContext;
            _aiService = aiService;
            _httpClient = httpClient;
            _fastApiUrl = configuration["AIEngineSettings:BaseUrl"] ?? "http://127.0.0.1:8000";
        }

        public async Task<List<NewsCache>> GetStockNewsAsync(string symbol)
        {
            symbol = symbol.ToUpper().Trim();
            
            // Check cache first (recent news within 12 hours)
            var expiry = DateTime.UtcNow.AddHours(-12);
            var cachedNews = await _dbContext.NewsCaches
                .Where(n => n.Symbol == symbol && n.CreatedAt > expiry)
                .OrderByDescending(n => n.PublishedAt ?? n.CreatedAt)
                .ToListAsync();

            if (cachedNews.Count > 0) return cachedNews;

            // Otherwise, fetch mock/real items from FastAPI or default
            var freshNews = new List<NewsCache>();
            
            // Generate some typical structural market headlines for demo
            var templates = new List<(string Title, string Summary)>
            {
                ( $"{symbol} Rebounds Strongly as Institutional Buying Gains Pace", "Increased block orders and strong technical support indicators suggest positive investor accumulation patterns." ),
                ( $"{symbol} Faces Volatility Ahead of Competitive Sector Pressures", "Broader index contractions and moving average resistance levels suggest short-term trading compression." ),
                ( $"AI Tech Boom Drives Increased Interest in Equities Like {symbol}", "Enthusiasm around generative workflows and enterprise integrations drives retail and commercial capital inflows." )
            };

            foreach (var t in templates)
            {
                var newsItem = new NewsCache
                {
                    Symbol = symbol,
                    Title = t.Title,
                    Summary = t.Summary,
                    PublishedAt = DateTime.UtcNow.AddHours(-new Random().Next(1, 10)),
                    Url = "https://finance.yahoo.com/quote/" + symbol,
                    Sentiment = "Neutral",
                    SentimentScore = 0.0m
                };

                try
                {
                    // Hit the Python sentiment analyzer for each headline
                    var payload = new { symbol = symbol, headline = t.Title };
                    var res = await _httpClient.PostAsJsonAsync($"{_fastApiUrl}/api/analyze/sentiment", payload);
                    if (res.IsSuccessStatusCode)
                    {
                        var data = await res.Content.ReadFromJsonAsync<JsonElement>();
                        if (data.TryGetProperty("sentiment", out var sentProp))
                            newsItem.Sentiment = sentProp.GetString();
                        if (data.TryGetProperty("score", out var scoreProp))
                            newsItem.SentimentScore = (decimal)scoreProp.GetDouble();
                        if (data.TryGetProperty("reason", out var reasonProp))
                            newsItem.Summary = reasonProp.GetString();
                    }
                }
                catch (Exception)
                {
                    // Fail over to Neutral defaults
                }

                freshNews.Add(newsItem);
            }

            // Cache in database
            _dbContext.NewsCaches.AddRange(freshNews);
            await _dbContext.SaveChangesAsync();

            return freshNews;
        }

        public async Task<string> ExplainStockMovementAsync(string symbol)
        {
            symbol = symbol.ToUpper().Trim();
            try
            {
                // Call Python engine recommendation explanation which combines signals
                var rec = await _aiService.GetStockRecommendationAsync(symbol, "Medium");
                if (rec != null && !string.IsNullOrEmpty(rec.Explanation))
                {
                    return rec.Explanation;
                }
            }
            catch (Exception)
            {
                // Fallback
            }
            return $"{symbol} is experiencing typical trading range variations with technical consolidation near moving average bounds.";
        }
    }
}
