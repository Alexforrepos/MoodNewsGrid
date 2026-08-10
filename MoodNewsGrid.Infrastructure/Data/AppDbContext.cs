using Microsoft.EntityFrameworkCore;
using MoodNewsGrid.Core.Models;

namespace MoodNewsGrid.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<NewsItem> NewsItems => Set<NewsItem>();
    public DbSet<NewsRewrite> NewsRewrites => Set<NewsRewrite>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<NewsItem>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).IsRequired().HasMaxLength(1000);
            e.Property(x => x.OriginalText).IsRequired();
            e.Property(x => x.SourceUrl).IsRequired().HasMaxLength(2000);
            e.Property(x => x.SourceName).IsRequired().HasMaxLength(200);
            // Не дублируем одну и ту же новость при повторном refresh
            e.HasIndex(x => x.SourceUrl).IsUnique();

            e.HasMany(x => x.Rewrites)
                .WithOne(x => x.NewsItem)
                .HasForeignKey(x => x.NewsItemId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<NewsRewrite>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.RewrittenText).IsRequired();
            // Кэш: один рерайт на пару (новость, настроение)
            e.HasIndex(x => new { x.NewsItemId, x.Mood }).IsUnique();
        });
    }
}
