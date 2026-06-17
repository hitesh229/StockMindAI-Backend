using System;
using System.ComponentModel.DataAnnotations;

namespace StockMindAI.API.DTOs
{
    public class HoldingRequest
    {
        [Required]
        [MaxLength(20)]
        public string Symbol { get; set; } = string.Empty;

        [Required]
        [Range(0.0001, 100000000.0)]
        public decimal Shares { get; set; }

        [Required]
        [Range(0.0001, 100000000.0)]
        public decimal PurchasePrice { get; set; }

        public DateTime PurchaseDate { get; set; } = DateTime.UtcNow;
    }

    public class PortfolioItemResponse
    {
        public int Id { get; set; }
        public string Symbol { get; set; } = string.Empty;
        public decimal Shares { get; set; }
        public decimal PurchasePrice { get; set; }
        public decimal CurrentPrice { get; set; }
        public decimal TotalValue { get; set; }
        public decimal PnL { get; set; }
        public decimal PnLPercent { get; set; }
        public string Sector { get; set; } = "Other";
        public double Beta { get; set; } = 1.0;
        public DateTime PurchaseDate { get; set; }
    }

    public class BrokerSyncRequest
    {
        [Required]
        public string Broker { get; set; } = string.Empty;

        [Required]
        public string AuthCode { get; set; } = string.Empty;
    }
}
