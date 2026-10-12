using Microsoft.AspNetCore.Mvc;
using Sahspn.ArtHistoire.Storage;

namespace Sahspn.ArtHistoire.Controllers;

public sealed class BulletinsController(ISiteContentStore store) : Controller
{
    public async Task<IActionResult> Index(string? q, int page = 1, CancellationToken cancellationToken = default)
    {
        const int pageSize = 12;
        page = Math.Max(1, page);
        var result = await store.SearchBulletinsAsync(q, page, pageSize, cancellationToken);
        ViewBag.Query = q;
        ViewBag.Page = page;
        ViewBag.Total = result.Total;
        return View(result.Items);
    }

    public async Task<IActionResult> Details(int number, CancellationToken cancellationToken)
    {
        var item = await store.GetBulletinAsync(number, cancellationToken);
        return item is null ? NotFound() : View(item);
    }
}
