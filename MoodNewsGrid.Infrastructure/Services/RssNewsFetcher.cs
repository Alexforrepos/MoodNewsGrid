using System.ServiceModel.Syndication;
using System.Xml;
using MoodNewsGrid.Core.Interfaces;
using MoodNewsGrid.Core.Models;

namespace MoodNewsGrid.Infrastructure.Services;

public class RssFeedSource
{
    public required string Name { get; init; }
    public required string Url { get; init; }
}

/// <summary>
/// Тянет новости из списка публичных RSS-фидов (без парсинга полной статьи —
/// берём заголовок + summary/description, которые сам источник даёт как факт-базу).
/// </summary>
public class RssNewsFetcher(HttpClient httpClient, IEnumerable<RssFeedSource> feeds) : INewsFetcher
{
    public async Task<IReadOnlyList<NewsItem>> FetchLatestAsync(CancellationToken ct = default)
    {
        var result = new List<NewsItem>();

        foreach (var feed in feeds)
        {
            try
            {
                await using var stream = await httpClient.GetStreamAsync(feed.Url, ct);
                using var reader = XmlReader.Create(stream);
                var syndicationFeed = SyndicationFeed.Load(reader);
                if (syndicationFeed is null) continue;

                foreach (var item in syndicationFeed.Items.Take(10))
                {
                    var summary = item.Summary?.Text ?? item.Title?.Text ?? string.Empty;
                    var link = item.Links.FirstOrDefault()?.Uri.ToString() ?? string.Empty;

                    result.Add(new NewsItem
                    {
                        Title = item.Title?.Text ?? "(без заголовка)",
                        OriginalText = StripHtml(summary),
                        SourceUrl = link,
                        SourceName = feed.Name,
                        PublishedAt = item.PublishDate != default
                            ? item.PublishDate.ToUniversalTime()
                            : DateTimeOffset.UtcNow.ToUniversalTime()
                    });
                }
            }
            catch
            {
                // Один упавший источник не должен ронять весь refresh — просто пропускаем.
                // В проде тут был бы ILogger, для тестового опускаем ради скорости.
            }
        }

        return result;
    }

    private static string StripHtml(string input) =>
        System.Text.RegularExpressions.Regex.Replace(input, "<.*?>", string.Empty).Trim();
}
