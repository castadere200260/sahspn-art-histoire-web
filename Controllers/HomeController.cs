using Microsoft.AspNetCore.Mvc;
using Sahspn.ArtHistoire.Storage;

namespace Sahspn.ArtHistoire.Controllers;

public sealed class HomeController(ISiteContentStore store) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(await store.GetLatestBulletinsAsync(3, cancellationToken));

    public IActionResult Error() => View();
}
