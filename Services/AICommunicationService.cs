using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StockMindAI.API.DTOs;
using StockMindAI.API.Models;

namespace StockMindAI.API.Services
{
    public interface IAICommunicationService
    {
        Task<ChatResponseDTO?> GetChatAdvisoryAsync(string question, List<ChatHistory> history, string? activeSymbol, string riskProfile, List<Portfolio> holdings);
        Task<PredictionResponseDTO?> GetStockPredictionAsync(string symbol);
        Task<RecommendationResponseDTO?> GetStockRecommendationAsync(string symbol, string riskProfile);
        Task<string> GetPortfolioRiskReportAsync(List<Portfolio> holdings, string riskProfile);
        Task<string> GetMarketSummaryAsync();
        Task<JsonElement?> GetTechnicalSignalsAsync(string symbol);
        Task<JsonElement?> SearchStockAsync(string query);
    }

    public class AICommunicationService : IAICommunicationService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<AICommunicationService> _logger;
        private readonly string _baseUrl;

        public AICommunicationService(HttpClient httpClient, IConfiguration configuration, ILogger<AICommunicationService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _baseUrl = configuration["AIEngineSettings:BaseUrl"] ?? "http://127.0.0.1:8000";
        }

        public async Task<ChatResponseDTO?> GetChatAdvisoryAsync(string question, List<ChatHistory> history, string? activeSymbol, string riskProfile, List<Portfolio> holdings)
        {
            try
            {
                var chatHistoryPayload = new List<object>();
                foreach (var h in history)
                {
                    chatHistoryPayload.Add(new { sender = h.Sender, message = h.Message });
                }

                var holdingsPayload = new List<object>();
                foreach (var h in holdings)
                {
                    holdingsPayload.Add(new { symbol = h.Symbol, shares = (double)h.Shares, purchase_price = (double)h.PurchasePrice });
                }

                var payload = new
                {
                    question = question,
                    chat_history = chatHistoryPayload,
                    active_symbol = activeSymbol,
                    risk_profile = riskProfile,
                    portfolio = holdingsPayload
                };

                var url = $"{_baseUrl}/api/chat";
                var response = await _httpClient.PostAsJsonAsync(url, payload);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<ChatResponseDTO>();
                }
                
                var errStr = await response.Content.ReadAsStringAsync();
                _logger.LogError("FastAPI chat advisory failed: {Err}", errStr);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed contacting Python AI engine for chat.");
            }
            return new ChatResponseDTO { Answer = "I'm having trouble processing your request right now. Please make sure the Python FastAPI engine is running." };
        }

        public async Task<PredictionResponseDTO?> GetStockPredictionAsync(string symbol)
        {
            try
            {
                var payload = new { symbol = symbol };
                var url = $"{_baseUrl}/api/predict";
                var response = await _httpClient.PostAsJsonAsync(url, payload);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<PredictionResponseDTO>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed fetching stock predictions from FastAPI for {Symbol}", symbol);
            }
            return null;
        }

        public async Task<RecommendationResponseDTO?> GetStockRecommendationAsync(string symbol, string riskProfile)
        {
            try
            {
                var payload = new { symbol = symbol, risk_profile = riskProfile };
                var url = $"{_baseUrl}/api/recommend";
                var response = await _httpClient.PostAsJsonAsync(url, payload);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<RecommendationResponseDTO>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed fetching recommendations from FastAPI for {Symbol}", symbol);
            }
            return null;
        }

        public async Task<string> GetPortfolioRiskReportAsync(List<Portfolio> holdings, string riskProfile)
        {
            try
            {
                var holdingsPayload = new List<object>();
                foreach (var h in holdings)
                {
                    holdingsPayload.Add(new { symbol = h.Symbol, shares = (double)h.Shares, purchase_price = (double)h.PurchasePrice });
                }

                var payload = new
                {
                    holdings = holdingsPayload,
                    risk_profile = riskProfile
                };

                var url = $"{_baseUrl}/api/portfolio/risk";
                var response = await _httpClient.PostAsJsonAsync(url, payload);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsStringAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed fetching portfolio risk report from FastAPI.");
            }
            return "{}";
        }

        public async Task<string> GetMarketSummaryAsync()
        {
            try
            {
                var url = $"{_baseUrl}/api/market/summary";
                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsStringAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed fetching market summary from FastAPI.");
            }
            return "{}";
        }

        public async Task<JsonElement?> GetTechnicalSignalsAsync(string symbol)
        {
            try
            {
                var payload = new { symbol = symbol, period = "1y" };
                var url = $"{_baseUrl}/api/analyze/technical";
                var response = await _httpClient.PostAsJsonAsync(url, payload);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<JsonElement>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed fetching technical indicators from FastAPI for {Symbol}", symbol);
            }
            return null;
        }

        public async Task<JsonElement?> SearchStockAsync(string query)
        {
            try
            {
                var url = $"{_baseUrl}/api/stock/search?q={Uri.EscapeDataString(query)}";
                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<JsonElement>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed fetching stock search suggestions from FastAPI for query: {Query}", query);
            }
            return null;
        }
    }
}
