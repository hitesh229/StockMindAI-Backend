using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StockMindAI.API.Models
{
    [Table("news_cache")]
    public class NewsCache
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("symbol")]
        [MaxLength(20)]
        public string Symbol { get; set; } = string.Empty;

        [Required]
        [Column("title")]
        [MaxLength(500)]
        public string Title { get; set; } = string.Empty;

        [Column("url")]
        public string? Url { get; set; }

        [Column("published_at")]
        public DateTime? PublishedAt { get; set; }

        [Column("sentiment")]
        [MaxLength(20)]
        public string? Sentiment { get; set; } // Positive, Negative, Neutral

        [Column("sentiment_score")]
        public decimal? SentimentScore { get; set; }

        [Column("summary")]
        public string? Summary { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
