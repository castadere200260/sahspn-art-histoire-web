using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sahspn.ArtHistoire.Storage;

namespace Sahspn.ArtHistoire.Controllers;

/// <summary>Public, read-only directory pages. No write operation is exposed here.</summary>
[AllowAnonymous]
[Route("")]
public sealed class PublicDirectoryController(IAdminManagementStore data, IShopCatalogStore shop, ISiteContentStore content, IPartnerStore partners, IDocumentStudyStore documents, ILibraryStore library) : Controller
{
    [HttpGet("galerie")]
    public async Task<IActionResult> Gallery(CancellationToken ct) => View("~/Views/Public/Gallery.cshtml", await content.GetGalleryImagesAsync(publishedOnly: true, cancellationToken: ct));

    [HttpGet("membres")]
    public async Task<IActionResult> Members(CancellationToken ct)
    {
        var members = await data.GetMembersAsync(ct);
        return View("~/Views/Public/Members.cshtml", members.OrderBy(x => x.FullName).ToList());
    }

    [HttpGet("bureau")]
    public async Task<IActionResult> BoardMembers(CancellationToken ct)
    {
        var members = await data.GetMembersAsync(ct);
        var boardMembers = members.Where(x => x.IsBoardMember && x.IsActive)
            .OrderBy(x => x.BoardPosition).ThenBy(x => x.FullName).ToList();
        return View("~/Views/Public/BoardMembers.cshtml", boardMembers);
    }

    [HttpGet("rendez-vous")]
    public async Task<IActionResult> Appointments(CancellationToken ct)
    {
        var appointments = await data.GetAppointmentsAsync(ct);
        return View("~/Views/Public/Appointments.cshtml", appointments.OrderBy(x => x.StartsAt).ToList());
    }
    [HttpGet("boutique")]
    public async Task<IActionResult> Shop(CancellationToken ct)
    {
        var items = await shop.GetPublishedAsync(ct);
        return View("~/Views/Public/Shop.cshtml", items);
    }

    [HttpGet("documents-etudes")]
    public async Task<IActionResult> DocumentsStudies(string? q, int page = 1, CancellationToken ct = default)
    {
        const int pageSize = 12;
        page = Math.Max(1, page);
        var result = await documents.SearchPublishedAsync(q, page, pageSize, ct);
        ViewBag.Query = q; ViewBag.Page = page; ViewBag.Total = result.Total;
        return View("~/Views/Public/DocumentsStudies.cshtml", result.Items);
    }

    [HttpGet("documents-etudes/{id:int}")]
    public async Task<IActionResult> DocumentStudyDetails(int id, CancellationToken ct)
    {
        var item = await documents.GetByIdAsync(id, ct);
        return item is null || !item.IsPublished ? NotFound() : View("~/Views/Public/DocumentStudyDetails.cshtml", item);
    }

    [HttpGet("bibliotheque")]
    public async Task<IActionResult> Library(string? q, int page = 1, CancellationToken ct = default)
    {
        const int pageSize = 12;
        page = Math.Max(1, page);
        var result = await library.SearchPublishedAsync(q, page, pageSize, ct);
        ViewBag.Query = q; ViewBag.Page = page; ViewBag.Total = result.Total;
        return View("~/Views/Public/Library.cshtml", result.Items);
    }

    [HttpGet("bibliotheque/{id:int}")]
    public async Task<IActionResult> LibraryDetails(int id, CancellationToken ct)
    {
        var item = await library.GetByIdAsync(id, ct);
        return item is null || !item.IsPublished ? NotFound() : View("~/Views/Public/LibraryDetails.cshtml", item);
    }

    [HttpGet("partenaires")]
    public async Task<IActionResult> Partners(CancellationToken ct)
    {
        var items = await partners.GetPublishedAsync(ct);
        return View("~/Views/Public/Partners.cshtml", items);
    }

}
