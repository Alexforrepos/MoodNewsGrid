using Microsoft.AspNetCore.Mvc;
using MoodNewsGrid.Core.Interfaces;
using MoodNewsGrid.Core.Models;

namespace MoodNewsGrid.Web.Controllers;

public class NewsController(INewsService newsService) : Controller
{
    // GET /News?mood=Joyful
    public async Task<IActionResult> Index(
        Mood mood = Mood.Neutral,
        CancellationToken ct = default)
    {
        var news = await newsService.GetAllAsync(ct);

        ViewBag.SelectedMood = mood;
        ViewBag.AllMoods = Enum.GetValues<Mood>();

        // Для каждой новости получаем рерайт выбранного настроения.
        // Если его ещё нет в БД — он будет создан через LLM.
        var rewrites = new Dictionary<Guid, NewsRewrite>();

        foreach (var item in news)
        {
            rewrites[item.Id] = await newsService.GetOrCreateRewriteAsync(
                item.Id,
                mood,
                ct);
        }

        ViewBag.Rewrites = rewrites;

        return View(news);
    }

    // GET /News/Details/{id}?mood=Ironic
    public async Task<IActionResult> Details(
        Guid id,
        Mood mood = Mood.Neutral,
        CancellationToken ct = default)
    {
        var item = await newsService.GetByIdAsync(id, ct);

        if (item is null)
            return NotFound();

        var rewrite = await newsService.GetOrCreateRewriteAsync(
            id,
            mood,
            ct);

        ViewBag.SelectedMood = mood;
        ViewBag.AllMoods = Enum.GetValues<Mood>();
        ViewBag.Rewrite = rewrite;

        return View(item);
    }

    // POST /News/Refresh
    [HttpPost]
    public async Task<IActionResult> Refresh(
        CancellationToken ct)
    {
        await newsService.RefreshNewsAsync(ct);

        return RedirectToAction(nameof(Index));
    }
}

