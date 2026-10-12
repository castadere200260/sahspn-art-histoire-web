using Microsoft.AspNetCore.Mvc;
using Sahspn.ArtHistoire.Models;
using Sahspn.ArtHistoire.Storage;

namespace Sahspn.ArtHistoire.Controllers;

public sealed class PagesController(ISiteContentStore store) : Controller
{
    public async Task<IActionResult> Details(string slug, CancellationToken cancellationToken)
    {
        var page = await store.GetPageAsync(slug, cancellationToken);
        return page is null ? NotFound() : View(page);
    }

    [HttpGet]
    public IActionResult Contact()
    {
        ViewBag.Sent = false;
        return View(new ContactMessage { Name = "", Email = "", Subject = "", Message = "" });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Contact(ContactMessage model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View(model);
        await store.AddContactMessageAsync(model, cancellationToken);
        ViewBag.Sent = true;
        return View(new ContactMessage { Name = "", Email = "", Subject = "", Message = "" });
    }
}
