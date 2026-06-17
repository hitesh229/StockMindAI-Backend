using Microsoft.EntityFrameworkCore;
using StockMindAI.API.Models;

namespace StockMindAI.API.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Portfolio> Portfolios { get; set; } = null!;
        public DbSet<Watchlist> Watchlists { get; set; } = null!;
        public DbSet<Alert> Alerts { get; set; } = null!;
        public DbSet<ChatHistory> ChatHistories { get; set; } = null!;
        public DbSet<NewsCache> NewsCaches { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Setup composite key or unique constraints
            modelBuilder.Entity<Watchlist>()
                .HasIndex(w => new { w.UserId, w.Symbol })
                .IsUnique();

            // Set decmials precision
            modelBuilder.Entity<Portfolio>()
                .Property(p => p.Shares)
                .HasPrecision(18, 4);

            modelBuilder.Entity<Portfolio>()
                .Property(p => p.PurchasePrice)
                .HasPrecision(18, 4);

            modelBuilder.Entity<NewsCache>()
                .Property(n => n.SentimentScore)
                .HasPrecision(5, 4);
        }
    }
}
