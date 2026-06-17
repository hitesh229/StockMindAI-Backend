using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using StockMindAI.API.Data;
using StockMindAI.API.DTOs;
using StockMindAI.API.Models;

namespace StockMindAI.API.Services
{
    public interface IPortfolioService
    {
        Task<List<Portfolio>> GetUserHoldingsAsync(int userId);
        Task<Portfolio?> AddHoldingAsync(int userId, HoldingRequest request);
        Task<bool> RemoveHoldingAsync(int userId, int holdingId);
        Task<List<PortfolioItemResponse>> GetPortfolioDetailsAsync(int userId);
        Task<string> GetPortfolioRiskReportAsync(int userId, string riskProfile);
        Task<int> UploadPortfolioCsvAsync(int userId, Stream csvStream);
    }

    public class PortfolioService : IPortfolioService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IAICommunicationService _aiService;

        public PortfolioService(ApplicationDbContext dbContext, IAICommunicationService aiService)
        {
            _dbContext = dbContext;
            _aiService = aiService;
        }

        public async Task<List<Portfolio>> GetUserHoldingsAsync(int userId)
        {
            return await _dbContext.Portfolios
                .Where(p => p.UserId == userId)
                .OrderBy(p => p.Symbol)
                .ToListAsync();
        }

        public async Task<Portfolio?> AddHoldingAsync(int userId, HoldingRequest request)
        {
            var symbol = request.Symbol.ToUpper().Trim();
            var holding = new Portfolio
            {
                UserId = userId,
                Symbol = symbol,
                Shares = request.Shares,
                PurchasePrice = request.PurchasePrice,
                PurchaseDate = request.PurchaseDate.ToUniversalTime()
            };

            _dbContext.Portfolios.Add(holding);
            var success = await _dbContext.SaveChangesAsync() > 0;
            return success ? holding : null;
        }

        public async Task<bool> RemoveHoldingAsync(int userId, int holdingId)
        {
            var item = await _dbContext.Portfolios.FirstOrDefaultAsync(p => p.UserId == userId && p.Id == holdingId);
            if (item == null) return false;

            _dbContext.Portfolios.Remove(item);
            return await _dbContext.SaveChangesAsync() > 0;
        }

        public async Task<List<PortfolioItemResponse>> GetPortfolioDetailsAsync(int userId)
        {
            var holdings = await GetUserHoldingsAsync(userId);
            var responses = new List<PortfolioItemResponse>();

            foreach (var h in holdings)
            {
                decimal currentPrice = h.PurchasePrice; // fallback
                string sector = "Other";
                double beta = 1.0;

                try
                {
                    // Call Python FastAPI to get real-time price & metadata
                    var details = await _aiService.GetTechnicalSignalsAsync(h.Symbol);
                    if (details.HasValue)
                    {
                        if (details.Value.TryGetProperty("current_price", out var priceProp))
                        {
                            currentPrice = (decimal)priceProp.GetDouble();
                        }
                        if (details.Value.TryGetProperty("signals", out var signalsProp))
                        {
                            if (signalsProp.TryGetProperty("sma_50", out _)) // check if populated
                            {
                                // Simple mapping from standard list or default
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    // Fail silently
                }

                // Sector and beta calculations
                var upperSym = h.Symbol.ToUpper();
                if (upperSym == "AAPL" || upperSym == "MSFT" || upperSym == "NVDA" || upperSym == "GOOG" || upperSym == "GOOGL" || upperSym == "TCS" || upperSym == "INFY")
                {
                    sector = "Technology";
                    beta = (upperSym == "TCS" ? 0.75 : (upperSym == "INFY" ? 0.80 : 1.15));
                }
                else if (upperSym == "TSLA" || upperSym == "AMZN")
                {
                    sector = "Consumer Cyclical";
                    beta = 1.40;
                }
                else if (upperSym == "JPM" || upperSym == "BAC" || upperSym == "HDFCBANK")
                {
                    sector = "Financial Services";
                    beta = (upperSym == "HDFCBANK" ? 0.90 : 1.20);
                }
                else if (upperSym == "JNJ" || upperSym == "UNH")
                {
                    sector = "Healthcare";
                    beta = 0.60;
                }
                else if (upperSym == "RELIANCE")
                {
                    sector = "Energy";
                    beta = 0.85;
                }

                var totalValue = h.Shares * currentPrice;
                var totalCost = h.Shares * h.PurchasePrice;
                var pnl = totalValue - totalCost;
                var pnlPercent = totalCost > 0 ? (pnl / totalCost) * 100 : 0;

                responses.Add(new PortfolioItemResponse
                {
                    Id = h.Id,
                    Symbol = h.Symbol,
                    Shares = h.Shares,
                    PurchasePrice = h.PurchasePrice,
                    CurrentPrice = currentPrice,
                    TotalValue = totalValue,
                    PnL = pnl,
                    PnLPercent = pnlPercent,
                    Sector = sector,
                    Beta = beta,
                    PurchaseDate = h.PurchaseDate
                });
            }

            return responses;
        }

        public async Task<string> GetPortfolioRiskReportAsync(int userId, string riskProfile)
        {
            var holdings = await GetUserHoldingsAsync(userId);
            if (holdings.Count == 0) return "{}";
            
            return await _aiService.GetPortfolioRiskReportAsync(holdings, riskProfile);
        }

        public async Task<int> UploadPortfolioCsvAsync(int userId, Stream csvStream)
        {
            var recordsAdded = 0;
            using (var reader = new StreamReader(csvStream))
            {
                var headerLine = await reader.ReadLineAsync();
                if (headerLine == null) return 0;

                var headers = headerLine.Split(',').Select(h => h.Trim().ToLower()).ToList();
                
                int symbolIdx = headers.IndexOf("symbol");
                int sharesIdx = headers.IndexOf("shares");
                int priceIdx = headers.IndexOf("purchaseprice");
                if (priceIdx == -1) priceIdx = headers.IndexOf("price");

                if (symbolIdx == -1 || sharesIdx == -1 || priceIdx == -1)
                {
                    throw new FormatException("CSV header must contain: Symbol, Shares, PurchasePrice");
                }

                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    var columns = line.Split(',').Select(c => c.Trim()).ToList();
                    if (columns.Count <= Math.Max(symbolIdx, Math.Max(sharesIdx, priceIdx))) continue;

                    try
                    {
                        var symbol = columns[symbolIdx].ToUpper();
                        if (string.IsNullOrWhiteSpace(symbol)) continue;

                        var shares = decimal.Parse(columns[sharesIdx]);
                        var price = decimal.Parse(columns[priceIdx]);

                        var holding = new Portfolio
                        {
                            UserId = userId,
                            Symbol = symbol,
                            Shares = shares,
                            PurchasePrice = price,
                            PurchaseDate = DateTime.UtcNow
                        };

                        _dbContext.Portfolios.Add(holding);
                        recordsAdded++;
                    }
                    catch (Exception)
                    {
                        // Skip faulty line
                    }
                }
            }

            if (recordsAdded > 0)
            {
                await _dbContext.SaveChangesAsync();
            }

            return recordsAdded;
        }
    }
}
