using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Sahspn.ArtHistoire.Models;
using Sahspn.ArtHistoire.Storage;
namespace Sahspn.ArtHistoire.Controllers;

[Authorize(Roles = "Administrator")]
[Route("administration")]
public sealed class AdminController(IAdminManagementStore data, ISiteContentStore content, IPasswordHasher<AdminUser> hasher, IWebHostEnvironment environment, ILogger<AdminController> logger, IShopCatalogStore shop, IPartnerStore partners, IDocumentStudyStore documents, ILibraryStore library) : Controller
{
    [HttpGet("")] public async Task<IActionResult> Index(CancellationToken ct) { var b = await data.GetBulletinsAsync(ct); var m = await data.GetMembersAsync(ct); var a = await data.GetAppointmentsAsync(ct); var u = await data.GetAdminUsersAsync(ct); return View(new AdminDashboardViewModel(b.Count, m.Count, a.Count, u.Count)); }

    [HttpGet("bulletins")] public async Task<IActionResult> Bulletins(CancellationToken ct) => View(await data.GetBulletinsAsync(ct));
    [HttpGet("bulletins/nouveau")] public IActionResult NewBulletin() { ViewBag.IsNew = true; ViewBag.OriginalNumber = 0; return View("BulletinForm", new Bulletin { Number = 1, Title = "", Content = "" }); }
    [HttpGet("bulletins/{number:int}/modifier")] public async Task<IActionResult> EditBulletin(int number, CancellationToken ct) { var x = await content.GetBulletinAsync(number, ct); if (x is null) return NotFound(); ViewBag.IsNew = false; ViewBag.OriginalNumber = x.Number; return View("BulletinForm", x); }
    [HttpPost("bulletins/enregistrer"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveBulletin(Bulletin item, int originalNumber, IFormFile? coverFile, IFormFile? documentFile, CancellationToken ct)
    {
        // originalNumber is zero only for create; ignore any client operation flag.
        var isNew = originalNumber <= 0;
        if (item.Number <= 0) ModelState.AddModelError(nameof(item.Number), "Le numéro doit être supérieur à zéro.");
        if (string.IsNullOrWhiteSpace(item.Title)) ModelState.AddModelError(nameof(item.Title), "Le titre est obligatoire.");
        if (!ModelState.IsValid) { ViewBag.IsNew = isNew; ViewBag.OriginalNumber = originalNumber; return View("BulletinForm", item); }

        var existing = await content.GetBulletinAsync(item.Number, ct);
        if (existing is not null && (isNew || item.Number != originalNumber))
        {
            ModelState.AddModelError(nameof(item.Number), "Ce numéro de bulletin existe déjà.");
            ViewBag.IsNew = isNew; ViewBag.OriginalNumber = originalNumber;
            return View("BulletinForm", item);
        }

        try
        {
            if (coverFile is { Length: > 0 }) item.CoverImage = await SaveUploadAsync(coverFile, "covers", new[] { ".jpg", ".jpeg", ".png", ".webp" }, 8 * 1024 * 1024, ct);
            if (documentFile is { Length: > 0 }) item.DocumentUrl = await SaveUploadAsync(documentFile, "bulletins", new[] { ".pdf" }, 25 * 1024 * 1024, ct);
            await data.SaveBulletinAsync(item, originalNumber, ct);
            TempData["AdminSuccess"] = isNew ? "Bulletin créé avec succès." : "Bulletin modifié avec succès.";
            return RedirectToAction(nameof(Bulletins));
        }
        catch (InvalidDataException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            ViewBag.IsNew = isNew; ViewBag.OriginalNumber = originalNumber;
            return View("BulletinForm", item);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur lors de l'enregistrement du bulletin {BulletinNumber}", item.Number);
            ModelState.AddModelError(string.Empty, "L'enregistrement a échoué. Vérifiez la connexion à la base de données et réessayez.");
            ViewBag.IsNew = isNew; ViewBag.OriginalNumber = originalNumber;
            return View("BulletinForm", item);
        }
    }

    [HttpGet("documents-etudes")]
    public async Task<IActionResult> DocumentsStudies(CancellationToken ct)
    {
        var items = await documents.GetAllAsync(ct);

        return View("~/Views/Admin/DocumentStudies.cshtml", items);

        //return View(await documents.GetAllAsync(ct));
    }

    [HttpGet("documents-etudes/nouveau")] public IActionResult NewDocumentStudy() => View("DocumentStudyForm", new DocumentStudy { IsPublished = true, Type = DocumentStudyType.Etude, PublishedAt = DateTime.Today });

    [HttpGet("documents-etudes/{id:int}/modifier")]
    public async Task<IActionResult> EditDocumentStudy(int id, CancellationToken ct)
    {
        var item = await documents.GetByIdAsync(id, ct);
        return item is null ? NotFound() : View("DocumentStudyForm", item);
    }

    [HttpPost("documents-etudes/enregistrer"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveDocumentStudy(DocumentStudy item, IFormFile? coverFile, IFormFile? documentFile, CancellationToken ct)
    {
        if (!Enum.IsDefined(item.Type)) ModelState.AddModelError(nameof(item.Type), "Type invalide.");
        if (!IsSafeCatalogueUrl(item.CoverImage)) ModelState.AddModelError(nameof(item.CoverImage), "La couverture doit être une URL HTTP(S) ou un chemin local commençant par /.");
        if (!IsSafeCatalogueUrl(item.DocumentUrl)) ModelState.AddModelError(nameof(item.DocumentUrl), "Le document doit être une URL HTTP(S) ou un chemin local commençant par /.");
        if (!ModelState.IsValid) return View("DocumentStudyForm", item);
        var isNew = item.Id <= 0;
        try
        {
            if (coverFile is { Length: > 0 }) item.CoverImage = await SaveUploadAsync(coverFile, "documents-etudes/covers", new[] { ".jpg", ".jpeg", ".png", ".webp" }, 8 * 1024 * 1024, ct);
            if (documentFile is { Length: > 0 }) item.DocumentUrl = await SaveUploadAsync(documentFile, "documents-etudes", new[] { ".pdf" }, 25 * 1024 * 1024, ct);
            await documents.SaveAsync(item, ct);
            TempData["AdminSuccess"] = isNew ? "Document/étude créé." : "Document/étude modifié.";
            return RedirectToAction(nameof(DocumentsStudies));
        }
        catch (InvalidDataException ex) { ModelState.AddModelError(string.Empty, ex.Message); return View("DocumentStudyForm", item); }
        catch (Exception ex) { logger.LogError(ex, "Échec de sauvegarde du document/étude {Id}", item.Id); ModelState.AddModelError(string.Empty, "L’enregistrement a échoué. Vérifiez la base de données et réessayez."); return View("DocumentStudyForm", item); }
    }

    [HttpPost("documents-etudes/{id:int}/supprimer"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteDocumentStudy(int id, CancellationToken ct)
    {
        try { await documents.DeleteAsync(id, ct); TempData["AdminSuccess"] = "Document/étude supprimé."; }
        catch (Exception ex) { logger.LogError(ex, "Échec de suppression du document/étude {Id}", id); TempData["AdminError"] = "La suppression a échoué."; }
        return RedirectToAction(nameof(DocumentsStudies));
    }

    [HttpGet("bibliotheque")]
    public async Task<IActionResult> Library(CancellationToken ct) => View(await library.GetAllAsync(ct));

    [HttpGet("bibliotheque/nouveau")]
    public IActionResult NewLibraryItem() => View("LibraryItemForm", new LibraryItem { IsPublished = true });

    [HttpGet("bibliotheque/{id:int}/modifier")]
    public async Task<IActionResult> EditLibraryItem(int id, CancellationToken ct)
    {
        var item = await library.GetByIdAsync(id, ct);
        return item is null ? NotFound() : View("LibraryItemForm", item);
    }

    [HttpPost("bibliotheque/enregistrer"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveLibraryItem(LibraryItem item, IFormFile? coverFile, IFormFile? documentFile, CancellationToken ct)
    {
        if (!IsSafeCatalogueUrl(item.CoverImage)) ModelState.AddModelError(nameof(item.CoverImage), "La couverture doit être une URL HTTP(S) ou un chemin local commençant par /.");
        if (!IsSafeCatalogueUrl(item.DocumentUrl)) ModelState.AddModelError(nameof(item.DocumentUrl), "Le document doit être une URL HTTP(S) ou un chemin local commençant par /.");
        if (!ModelState.IsValid) return View("LibraryItemForm", item);
        var isNew = item.Id <= 0;
        try
        {
            if (coverFile is { Length: > 0 }) item.CoverImage = await SaveUploadAsync(coverFile, "bibliotheque/covers", new[] { ".jpg", ".jpeg", ".png", ".webp" }, 8 * 1024 * 1024, ct);
            if (documentFile is { Length: > 0 }) item.DocumentUrl = await SaveUploadAsync(documentFile, "bibliotheque", new[] { ".pdf" }, 25 * 1024 * 1024, ct);
            await library.SaveAsync(item, ct);
            TempData["AdminSuccess"] = isNew ? "Élément ajouté à la bibliothèque." : "Élément de bibliothèque modifié.";
            return RedirectToAction(nameof(Library));
        }
        catch (InvalidDataException ex) { ModelState.AddModelError(string.Empty, ex.Message); return View("LibraryItemForm", item); }
        catch (Exception ex) { logger.LogError(ex, "Échec de sauvegarde de l’élément de bibliothèque {Id}", item.Id); ModelState.AddModelError(string.Empty, "L’enregistrement a échoué. Vérifiez la base de données et réessayez."); return View("LibraryItemForm", item); }
    }

    [HttpPost("bibliotheque/{id:int}/supprimer"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteLibraryItem(int id, CancellationToken ct)
    {
        try { await library.DeleteAsync(id, ct); TempData["AdminSuccess"] = "Élément supprimé de la bibliothèque."; }
        catch (Exception ex) { logger.LogError(ex, "Échec de suppression de l’élément de bibliothèque {Id}", id); TempData["AdminError"] = "La suppression a échoué."; }
        return RedirectToAction(nameof(Library));
    }

    [HttpGet("partenaires")] public async Task<IActionResult> Partners(CancellationToken ct) => View(await partners.GetAllAsync(ct));

    [HttpGet("partenaires/nouveau")] public IActionResult NewPartner() => View("PartnerForm", new Partner { IsPublished = true });

    [HttpGet("partenaires/{id:int}/modifier")]
    public async Task<IActionResult> EditPartner(int id, CancellationToken ct)
    {
        var partner = await partners.GetByIdAsync(id, ct);
        return partner is null ? NotFound() : View("PartnerForm", partner);
    }

    [HttpPost("partenaires/enregistrer"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SavePartner(Partner item, CancellationToken ct)
    {
        if (!IsSafeCatalogueUrl(item.WebsiteUrl)) ModelState.AddModelError(nameof(item.WebsiteUrl), "Le site doit utiliser une URL HTTP(S).");
        if (!IsSafeCatalogueUrl(item.LogoUrl)) ModelState.AddModelError(nameof(item.LogoUrl), "Le logo doit utiliser une URL HTTP(S) ou un chemin local commençant par /.");
        if (item.DisplayOrder < 0) ModelState.AddModelError(nameof(item.DisplayOrder), "L’ordre d’affichage doit être positif ou nul.");
        if (!ModelState.IsValid) return View("PartnerForm", item);
        try
        {
            var isNew = item.Id <= 0;
            await partners.SaveAsync(item, ct);
            TempData["AdminSuccess"] = isNew ? "Partenaire ajouté." : "Partenaire modifié.";
            return RedirectToAction(nameof(Partners));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Échec de sauvegarde du partenaire {PartnerId}", item.Id);
            ModelState.AddModelError(string.Empty, "Le partenaire n’a pas été enregistré. Vérifiez la base de données et réessayez.");
            return View("PartnerForm", item);
        }
    }

    [HttpPost("partenaires/{id:int}/supprimer"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePartner(int id, CancellationToken ct)
    {
        try { await partners.DeleteAsync(id, ct); TempData["AdminSuccess"] = "Partenaire supprimé."; }
        catch (Exception ex) { logger.LogError(ex, "Échec de suppression du partenaire {PartnerId}", id); TempData["AdminError"] = "Le partenaire n’a pas pu être supprimé."; }
        return RedirectToAction(nameof(Partners));
    }

    [HttpGet("boutique")] public async Task<IActionResult> Shop(CancellationToken ct) => View(await shop.GetAllAsync(ct));

    [HttpGet("boutique/nouveau")] public IActionResult NewShopItem() => View("ShopItemForm", new ShopItem { IsPublished = true, Type = ShopItemType.Bulletin });

    [HttpGet("boutique/{id:int}/modifier")]
    public async Task<IActionResult> EditShopItem(int id, CancellationToken ct)
    {
        var item = await shop.GetByIdAsync(id, ct);
        return item is null ? NotFound() : View("ShopItemForm", item);
    }

    [HttpPost("boutique/enregistrer"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveShopItem(ShopItem item, CancellationToken ct)
    {
        if (!Enum.IsDefined(item.Type)) ModelState.AddModelError(nameof(item.Type), "Type d’article invalide.");
        if (!IsSafeCatalogueUrl(item.ImageUrl))
            ModelState.AddModelError(nameof(item.ImageUrl), "L’image doit utiliser une URL HTTP(S) ou un chemin local commençant par /.");
        if (!IsSafeCatalogueUrl(item.DocumentUrl))
            ModelState.AddModelError(nameof(item.DocumentUrl), "Le document doit utiliser une URL HTTP(S) ou un chemin local commençant par /.");
        if (!ModelState.IsValid) return View("ShopItemForm", item);
        try
        {
            var isNew = item.Id <= 0;
            await shop.SaveAsync(item, ct);
            TempData["AdminSuccess"] = isNew ? "Article ajouté au catalogue." : "Article du catalogue modifié.";
            return RedirectToAction(nameof(Shop));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Échec de sauvegarde de l’article de boutique {ShopItemId}", item.Id);
            ModelState.AddModelError(string.Empty, "L’article n’a pas été enregistré. Vérifiez la base de données et réessayez.");
            return View("ShopItemForm", item);
        }
    }

    [HttpPost("boutique/{id:int}/supprimer"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteShopItem(int id, CancellationToken ct)
    {
        try { await shop.DeleteAsync(id, ct); TempData["AdminSuccess"] = "Article supprimé du catalogue."; }
        catch (Exception ex) { logger.LogError(ex, "Échec de suppression de l’article de boutique {ShopItemId}", id); TempData["AdminError"] = "L’article n’a pas pu être supprimé."; }
        return RedirectToAction(nameof(Shop));
    }


    [HttpGet("galerie")]
    public async Task<IActionResult> Gallery(CancellationToken ct) => View("~/Views/Admin/Gallery.cshtml", await content.GetGalleryImagesAsync(cancellationToken: ct));

    [HttpGet("galerie/nouveau")]
    public IActionResult NewGalleryImage() => View("~/Views/Admin/GalleryImageForm.cshtml", new GalleryImage { IsPublished = true });

    [HttpGet("galerie/{id}/modifier")]
    public async Task<IActionResult> EditGalleryImage(string id, CancellationToken ct)
    {
        var item = await content.GetGalleryImageAsync(id, ct);
        return item is null ? NotFound() : View("~/Views/Admin/GalleryImageForm.cshtml", item);
    }

    [HttpPost("galerie/enregistrer"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveGalleryImage(GalleryImage item, IFormFile? imageFile, CancellationToken ct)
    {
        var isNew = string.IsNullOrWhiteSpace(item.Id) || item.Id.Length > 36;
        if (isNew) item.Id = Guid.NewGuid().ToString("N");
        ViewBag.IsNewGalleryImage = isNew;
        var previousImageUrl = isNew ? null : (await content.GetGalleryImageAsync(item.Id, ct))?.ImageUrl;
        if (string.IsNullOrWhiteSpace(item.Title)) ModelState.AddModelError(nameof(item.Title), "Le titre est obligatoire.");
        if (imageFile is not { Length: > 0 } && string.IsNullOrWhiteSpace(item.ImageUrl))
            ModelState.AddModelError(nameof(item.ImageUrl), "Sélectionnez une image à téléverser.");
        if (!ModelState.IsValid) return View("~/Views/Admin/GalleryImageForm.cshtml", item);

        try
        {
            if (imageFile is { Length: > 0 })
                item.ImageUrl = await SaveUploadAsync(imageFile, "galerie", new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" }, 10 * 1024 * 1024, ct);
            if (string.IsNullOrWhiteSpace(item.AltText)) item.AltText = item.Title;
            if (isNew) item.CreatedAtUtc = DateTime.UtcNow;
            await content.SaveGalleryImageAsync(item, ct);
            if (!string.IsNullOrWhiteSpace(previousImageUrl) && previousImageUrl != item.ImageUrl) TryDeleteGalleryUpload(previousImageUrl);
            TempData["AdminSuccess"] = isNew ? "Image ajoutée à la galerie." : "Image de la galerie modifiée.";
            return RedirectToAction(nameof(Gallery));
        }
        catch (InvalidDataException ex)
        {
            ViewBag.IsNewGalleryImage = isNew;
            ModelState.AddModelError(string.Empty, ex.Message);
            return View("~/Views/Admin/GalleryImageForm.cshtml", item);
        }
        catch (Exception ex)
        {
            ViewBag.IsNewGalleryImage = isNew;
            logger.LogError(ex, "Échec d'enregistrement de l'image de galerie {GalleryImageId}", item.Id);
            ModelState.AddModelError(string.Empty, "L'image n'a pas été enregistrée. Vérifiez la base de données et réessayez.");
            return View("~/Views/Admin/GalleryImageForm.cshtml", item);
        }
    }

    [HttpPost("galerie/{id}/supprimer"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteGalleryImage(string id, CancellationToken ct)
    {
        try
        {
            var item = await content.GetGalleryImageAsync(id, ct);
            await content.DeleteGalleryImageAsync(id, ct);
            if (item is not null) TryDeleteGalleryUpload(item.ImageUrl);
            TempData["AdminSuccess"] = "Image supprimée de la galerie.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Échec de suppression de l'image de galerie {GalleryImageId}", id);
            TempData["AdminError"] = "La suppression de l'image a échoué.";
        }
        return RedirectToAction(nameof(Gallery));
    }

    private void TryDeleteGalleryUpload(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl) || !imageUrl.StartsWith("/uploads/galerie/", StringComparison.OrdinalIgnoreCase)) return;
        var root = Path.GetFullPath(Path.Combine(environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"), "uploads", "galerie"));
        var candidate = Path.GetFullPath(Path.Combine(environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"), imageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));
        if (candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && System.IO.File.Exists(candidate))
            System.IO.File.Delete(candidate);
    }

    private static bool IsSafeCatalogueUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        if (value.StartsWith('/') && !value.StartsWith("//", StringComparison.Ordinal)) return true;
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    private async Task<string> SaveUploadAsync(IFormFile file, string subfolder, string[] allowedExtensions, long maxBytes, CancellationToken ct)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            throw new InvalidDataException($"Format de fichier non autorisé : {extension}. Formats acceptés : {string.Join(", ", allowedExtensions)}.");
        if (file.Length > maxBytes)
            throw new InvalidDataException($"Le fichier dépasse la taille maximale autorisée ({maxBytes / (1024 * 1024)} Mo).");

        var folder = Path.Combine(environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"), "uploads", subfolder);
        Directory.CreateDirectory(folder);
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(folder, fileName);
        await using var stream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await file.CopyToAsync(stream, ct);
        return $"/uploads/{subfolder}/{fileName}";
    }
    [HttpPost("bulletins/{number:int}/supprimer"), ValidateAntiForgeryToken] public async Task<IActionResult> DeleteBulletin(int number, CancellationToken ct) { await data.DeleteBulletinAsync(number, ct); return RedirectToAction(nameof(Bulletins)); }

    [HttpGet("membres")] public async Task<IActionResult> Members(CancellationToken ct) => View(await data.GetMembersAsync(ct));
    [HttpGet("membres/bureau")] public async Task<IActionResult> BoardMembers(CancellationToken ct) => View("BoardMembers", (await data.GetMembersAsync(ct)).Where(x => x.IsBoardMember).OrderBy(x => x.BoardPosition).ThenBy(x => x.FullName).ToList());
    [HttpGet("membres/nouveau")] public IActionResult NewMember() => View("MemberForm", new Member { JoinedAt = DateTime.Today, IsActive = true });
    [HttpGet("membres/{id:int}/modifier")] public async Task<IActionResult> EditMember(int id, CancellationToken ct) { var x = (await data.GetMembersAsync(ct)).FirstOrDefault(x => x.Id == id); return x is null ? NotFound() : View("MemberForm", x); }
    [HttpPost("membres/enregistrer"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveMember(Member item, CancellationToken ct)
    {
        // The operation is determined by the entity key, never by client input.
        var isNew = item.Id <= 0;
        logger.LogDebug(
            "SaveMember atteint : Id={Id}, IsNew={IsNew}, ModelStateValid={Valid}",
            item.Id, isNew, ModelState.IsValid);

        if (!ModelState.IsValid) return View("MemberForm", item);
        try
        {
            await data.SaveMemberAsync(item, ct);
            TempData["AdminSuccess"] = isNew ? "Membre créé avec succès." : "Membre modifié avec succès.";
            return RedirectToAction(nameof(Members));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Échec de persistance du membre {MemberId} avec le fournisseur sélectionné", item.Id);
            ModelState.AddModelError(string.Empty, "Le membre n'a pas été enregistré. Vérifiez la connexion à la base de données et les logs de l'application.");
            return View("MemberForm", item);
        }
    }
    [HttpPost("membres/{id:int}/supprimer"), ValidateAntiForgeryToken] public async Task<IActionResult> DeleteMember(int id, CancellationToken ct) { await data.DeleteMemberAsync(id, ct); return RedirectToAction(nameof(Members)); }

    [HttpGet("rendez-vous")] public async Task<IActionResult> Appointments(CancellationToken ct) => View(await data.GetAppointmentsAsync(ct));
    [HttpGet("rendez-vous/nouveau")]
    public IActionResult NewAppointment()
    {
        return View("AppointmentForm", new Appointment());
    }

    [HttpGet("rendez-vous/{id:int}/modifier")] public async Task<IActionResult> EditAppointment(int id, CancellationToken ct) { var x = (await data.GetAppointmentsAsync(ct)).FirstOrDefault(x => x.Id == id); return x is null ? NotFound() : View("AppointmentForm", x); }
    [HttpPost("rendez-vous/enregistrer"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveAppointment(Appointment item, CancellationToken ct)
    {
        var isNew = item.Id <= 0;
        if (item.EndsAt.HasValue && item.EndsAt < item.StartsAt) ModelState.AddModelError(nameof(item.EndsAt), "La fin doit être postérieure au début.");
        if (!ModelState.IsValid) return View("AppointmentForm", item);
        try
        {
            await data.SaveAppointmentAsync(item, ct);
            TempData["AdminSuccess"] = isNew ? "Rendez-vous créé avec succès." : "Rendez-vous modifié avec succès.";
            return RedirectToAction(nameof(Appointments));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Échec de persistance du rendez-vous {AppointmentId} avec le fournisseur sélectionné", item.Id);
            ModelState.AddModelError(string.Empty, "Le rendez-vous n'a pas été enregistré. Vérifiez la connexion à la base de données et les logs de l'application.");
            return View("AppointmentForm", item);
        }
    }
    [HttpPost("rendez-vous/{id:int}/supprimer"), ValidateAntiForgeryToken] public async Task<IActionResult> DeleteAppointment(int id, CancellationToken ct) { await data.DeleteAppointmentAsync(id, ct); return RedirectToAction(nameof(Appointments)); }

    [HttpGet("utilisateurs")] public async Task<IActionResult> Users(CancellationToken ct) => View(await data.GetAdminUsersAsync(ct));
    [HttpGet("utilisateurs/nouveau")] public IActionResult NewUser() { ViewBag.IsNew = true; ViewBag.UserId = 0; return View("UserForm", new AdminUserInput()); }
    [HttpGet("utilisateurs/{id:int}/modifier")] public async Task<IActionResult> EditUser(int id, CancellationToken ct) { var x = (await data.GetAdminUsersAsync(ct)).FirstOrDefault(x => x.Id == id); if (x is null) return NotFound(); ViewBag.IsNew = false; ViewBag.UserId = id; return View("UserForm", new AdminUserInput { Username = x.Username, IsActive = x.IsActive }); }
    [HttpPost("utilisateurs/enregistrer"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveUser(AdminUserInput input, int id, CancellationToken ct)
    {
        var isNew = id <= 0;
        ViewBag.IsNew = isNew;
        ViewBag.UserId = id;
        if (isNew && string.IsNullOrWhiteSpace(input.Password)) ModelState.AddModelError(nameof(input.Password), "Le mot de passe est obligatoire à la création.");
        if (!ModelState.IsValid) return View("UserForm", input);
        try
        {
            var all = await data.GetAdminUsersAsync(ct);
            if (all.Any(x => x.Username.Equals(input.Username.Trim(), StringComparison.OrdinalIgnoreCase) && x.Id != id))
            {
                ModelState.AddModelError(nameof(input.Username), "Cet identifiant existe déjà.");
                return View("UserForm", input);
            }
            var user = new AdminUser { Id = id, Username = input.Username.Trim(), PasswordHash = "", IsActive = input.IsActive };
            if (!string.IsNullOrWhiteSpace(input.Password)) user.PasswordHash = hasher.HashPassword(user, input.Password);
            if (isNew)
            {
                if (string.IsNullOrWhiteSpace(user.PasswordHash)) return BadRequest();
                await data.SaveAdminUserAsync(user, ct);
            }
            else
            {
                var old = all.FirstOrDefault(x => x.Id == id);
                if (old is null) return NotFound();
                user.CreatedAtUtc = old.CreatedAtUtc;
                if (string.IsNullOrWhiteSpace(user.PasswordHash)) user.PasswordHash = old.PasswordHash;
                if (old.Username == User.Identity?.Name && !user.IsActive) return BadRequest("Vous ne pouvez pas désactiver votre propre compte.");
                await data.SaveAdminUserAsync(user, ct);
            }
            TempData["AdminSuccess"] = isNew ? "Utilisateur créé avec succès." : "Utilisateur modifié avec succès.";
            return RedirectToAction(nameof(Users));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Échec de persistance de l'utilisateur {Username} avec le fournisseur sélectionné", input.Username);
            ModelState.AddModelError(string.Empty, "L'utilisateur n'a pas été enregistré. Vérifiez la connexion à la base de données et les logs de l'application.");
            return View("UserForm", input);
        }
    }
    [HttpPost("utilisateurs/{id:int}/supprimer"), ValidateAntiForgeryToken] public async Task<IActionResult> DeleteUser(int id, CancellationToken ct) { var users = await data.GetAdminUsersAsync(ct); var target = users.FirstOrDefault(x => x.Id == id); if (target is null) return NotFound(); if (target.Username == User.Identity?.Name || target.IsActive && users.Count(x => x.IsActive) <= 1) { TempData["AdminError"] = "Impossible de supprimer votre compte ou le dernier administrateur actif."; return RedirectToAction(nameof(Users)); } await data.DeleteAdminUserAsync(id, ct); return RedirectToAction(nameof(Users)); }
}
public sealed record AdminDashboardViewModel(int TotalBulletins, int TotalMembers, int TotalAppointments, int TotalUsers);
