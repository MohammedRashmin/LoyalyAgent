using Microsoft.EntityFrameworkCore;
using loyalityAgent2._0.Models;

namespace loyalityAgent2._0.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<User> AgentUsers { get; set; }
        public DbSet<Business> Businesses { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Service> Services { get; set; }
        public DbSet<WelcomeGift> WelcomeGifts { get; set; }
        public DbSet<TierReward> TierRewards { get; set; }
        public DbSet<TierRewardItem> TierRewardItems { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.UserId);
                entity.HasIndex(e => e.Email).IsUnique();
                entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
                entity.Property(e => e.PasswordHash).IsRequired().HasMaxLength(255);
                entity.Property(e => e.GoogleApiKey).IsRequired().HasMaxLength(500);
            });

            // Business entity configuration
            modelBuilder.Entity<Business>(entity =>
            {
                entity.HasKey(e => e.BusinessId);
                entity.HasIndex(e => e.AddressHash);
                entity.HasIndex(e => new { e.Category, e.City });
                entity.HasMany(e => e.Products).WithOne(p => p.Business).OnDelete(DeleteBehavior.Cascade);
                entity.HasMany(e => e.Services).WithOne(s => s.Business).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.WelcomeGift).WithOne(wg => wg.Business).OnDelete(DeleteBehavior.Cascade);
                entity.HasMany(e => e.TierRewards).WithOne(tr => tr.Business).OnDelete(DeleteBehavior.Cascade);
            });

            // Product entity configuration
            modelBuilder.Entity<Product>(entity =>
            {
                entity.HasKey(e => e.ProductId);
                entity.HasIndex(e => e.BusinessId);
            });

            // Service entity configuration
            modelBuilder.Entity<Service>(entity =>
            {
                entity.HasKey(e => e.ServiceId);
                entity.HasIndex(e => e.BusinessId);
            });

            // WelcomeGift entity configuration
            modelBuilder.Entity<WelcomeGift>(entity =>
            {
                entity.HasKey(e => e.WelcomeGiftId);
                entity.HasIndex(e => e.BusinessId).IsUnique();
            });

            // TierReward entity configuration
            modelBuilder.Entity<TierReward>(entity =>
            {
                entity.HasKey(e => e.TierRewardId);
                entity.HasIndex(e => e.BusinessId);
                entity.HasMany(e => e.RewardItems).WithOne(tri => tri.TierReward).OnDelete(DeleteBehavior.Cascade);
            });

            // TierRewardItem entity configuration
            modelBuilder.Entity<TierRewardItem>(entity =>
            {
                entity.HasKey(e => e.TierRewardItemId);
                entity.HasIndex(e => e.TierRewardId);
            });
        }
    }
}
