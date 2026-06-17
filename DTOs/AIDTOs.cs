using System.Collections.Generic;

namespace StockMindAI.API.DTOs
{
    public class ChatRequestDTO
    {
        public string Question { get; set; } = string.Empty;
        public string? ActiveSymbol { get; set; }
    }

    public class ChatResponseDTO
    {
        public string Answer { get; set; } = string.Empty;
    }

    public class PredictionResponseDTO
    {
        public string Symbol { get; set; } = string.Empty;
        public List<double> PredictedPrices { get; set; } = new List<double>();
        public double GrowthProbability { get; set; }
        public double RiskProbability { get; set; }
        public string Trend { get; set; } = "SIDEWAYS";
    }

    public class RecommendationResponseDTO
    {
        public string Symbol { get; set; } = string.Empty;
        public double CurrentPrice { get; set; }
        public string Recommendation { get; set; } = "HOLD"; // BUY, HOLD, SELL
        public double ConfidenceScore { get; set; }
        public double TechnicalScore { get; set; }
        public double SentimentScore { get; set; }
        public double PredictionScore { get; set; }
        public string Explanation { get; set; } = string.Empty;
    }
}
