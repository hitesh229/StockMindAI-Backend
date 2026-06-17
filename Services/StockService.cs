using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using StockMindAI.API.Data;
using StockMindAI.API.Models;

namespace StockMindAI.API.Services
{
    public interface IStockService
    {
        Task<List<Watchlist>> GetWatchlistAsync(int userId);
        Task<bool> AddToWatchlistAsync(int userId, string symbol);
        Task<bool> RemoveFromWatchlistAsync(int userId, string symbol);
        Task<JsonElement?> GetStockDetailsAsync(int userId, string symbol);
        Task<List<Alert>> GetActiveAlertsAsync(int userId);
        Task<bool> MarkAlertsAsReadAsync(int userId);
        Task CheckAndGenerateAlertsAsync(int userId, string symbol, JsonElement technicalSignals);
        Task<JsonElement?> SearchStockAsync(string query);
    }

    public class StockService : IStockService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IAICommunicationService _aiService;

        public StockService(ApplicationDbContext dbContext, IAICommunicationService aiService)
        {
            _dbContext = dbContext;
            _aiService = aiService;
        }

        public async Task<List<Watchlist>> GetWatchlistAsync(int userId)
        {
            return await _dbContext.Watchlists
                .Where(w => w.UserId == userId)
                .OrderBy(w => w.Symbol)
                .ToListAsync();
        }

        public async Task<bool> AddToWatchlistAsync(int userId, string symbol)
        {
            symbol = symbol.ToUpper().Trim();
            var exists = await _dbContext.Watchlists.AnyAsync(w => w.UserId == userId && w.Symbol == symbol);
            if (exists) return true;

            var watchlist = new Watchlist
            {
                UserId = userId,
                Symbol = symbol
            };

            _dbContext.Watchlists.Add(watchlist);
            return await _dbContext.SaveChangesAsync() > 0;
        }

        public async Task<bool> RemoveFromWatchlistAsync(int userId, string symbol)
        {
            symbol = symbol.ToUpper().Trim();
            var item = await _dbContext.Watchlists.FirstOrDefaultAsync(w => w.UserId == userId && w.Symbol == symbol);
            if (item == null) return false;

            _dbContext.Watchlists.Remove(item);
            return await _dbContext.SaveChangesAsync() > 0;
        }

        public async Task<JsonElement?> GetStockDetailsAsync(int userId, string symbol)
        {
            symbol = symbol.ToUpper().Trim();
            var data = await _aiService.GetTechnicalSignalsAsync(symbol);
            if (data.HasValue)
            {
                // Trigger alert evaluation in background
                _ = Task.Run(() => CheckAndGenerateAlertsAsync(userId, symbol, data.Value));
                return data.Value;
            }
            return null;
        }

        public async Task<List<Alert>> GetActiveAlertsAsync(int userId)
        {
            return await _dbContext.Alerts
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.CreatedAt)
                .Take(20)
                .ToListAsync();
        }

        public async Task<bool> MarkAlertsAsReadAsync(int userId)
        {
            var unread = await _dbContext.Alerts.Where(a => a.UserId == userId && !a.IsRead).ToListAsync();
            foreach (var a in unread)
            {
                a.IsRead = true;
            }
            return await _dbContext.SaveChangesAsync() > 0;
        }

        public async Task CheckAndGenerateAlertsAsync(int userId, string symbol, JsonElement technicalSignals)
        {
            try
            {
                // Create a separate DbContext instance if running async in background,
                // but for simple sync let's handle with standard context or catch errors.
                if (technicalSignals.TryGetProperty("signals", out var signalsProp))
                {
                    double rsi = 50;
                    if (signalsProp.TryGetProperty("rsi", out var rsiProp)) rsi = rsiProp.GetDouble();

                    double currentPrice = 0;
                    if (technicalSignals.TryGetProperty("current_price", out var priceProp)) currentPrice = priceProp.GetDouble();

                    double bbUpper = 0, bbLower = 0;
                    if (signalsProp.TryGetProperty("bb_upper", out var buProp)) bbUpper = buProp.GetDouble();
                    if (signalsProp.TryGetProperty("bb_lower", out var blProp)) bbLower = blProp.GetDouble();

                    List<Alert> newAlerts = new List<Alert>();

                    // 1. RSI alert
                    if (rsi > 70)
                    {
                        var alertMsg = $"{symbol} RSI is at {rsi:F1} (Overbought). A downward technical correction may occur.";
                        var alertExists = await _dbContext.Alerts.AnyAsync(a => a.UserId == userId && a.Symbol == symbol && a.AlertType == "RSI" && a.Message == alertMsg);
                        if (!alertExists)
                        {
                            newAlerts.Add(new Alert { UserId = userId, Symbol = symbol, AlertType = "RSI", Message = alertMsg });
                        }
                    }
                    else if (rsi < 30)
                    {
                        var alertMsg = $"{symbol} RSI is at {rsi:F1} (Oversold). A potential bullish rebound signal.";
                        var alertExists = await _dbContext.Alerts.AnyAsync(a => a.UserId == userId && a.Symbol == symbol && a.AlertType == "RSI" && a.Message == alertMsg);
                        if (!alertExists)
                        {
                            newAlerts.Add(new Alert { UserId = userId, Symbol = symbol, AlertType = "RSI", Message = alertMsg });
                        }
                    }

                    // 2. Bollinger Band alert
                    if (currentPrice > bbUpper && bbUpper > 0)
                    {
                        var alertMsg = $"{symbol} price broke above upper Bollinger Band (${currentPrice:F2} vs ${bbUpper:F2}), showing high upward momentum or overextension.";
                        var alertExists = await _dbContext.Alerts.AnyAsync(a => a.UserId == userId && a.Symbol == symbol && a.AlertType == "Volatility" && a.Message == alertMsg);
                        if (!alertExists)
                        {
                            newAlerts.Add(new Alert { UserId = userId, Symbol = symbol, AlertType = "Volatility", Message = alertMsg });
                        }
                    }
                    else if (currentPrice < bbLower && bbLower > 0)
                    {
                        var alertMsg = $"{symbol} price fell below lower Bollinger Band (${currentPrice:F2} vs ${bbLower:F2}), representing an oversold breakout.";
                        var alertExists = await _dbContext.Alerts.AnyAsync(a => a.UserId == userId && a.Symbol == symbol && a.AlertType == "Volatility" && a.Message == alertMsg);
                        if (!alertExists)
                        {
                            newAlerts.Add(new Alert { UserId = userId, Symbol = symbol, AlertType = "Volatility", Message = alertMsg });
                        }
                    }

                    if (newAlerts.Count > 0)
                    {
                        _dbContext.Alerts.AddRange(newAlerts);
                        await _dbContext.SaveChangesAsync();
                    }
                }
            }
            catch (Exception)
            {
                // Background task error suppression
            }
        }

        public async Task<JsonElement?> SearchStockAsync(string query)
        {
            return await _aiService.SearchStockAsync(query);
        }
    }
}
