using Microsoft.EntityFrameworkCore;
using Sahspn.ArtHistoire.Data;
using Sahspn.ArtHistoire.Models;

namespace Sahspn.ArtHistoire.Storage;

public sealed class MySqlSiteContentStore(SahspDbContext db, ContentSeeder seeder) : ISiteContentStore
{
    public async Task<IReadOnlyList<Bulletin>> GetLatestBulletinsAsync(int count, CancellationToken cancellationToken = default) =>
        await db.Bulletins.AsNoTracking().OrderByDescending(x => x.Number).Take(count).ToListAsync(cancellationToken);

    public async Task<(IReadOnlyList<Bulletin> Items, long Total)> SearchBulletinsAsync(string? query, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        // Bound user-controlled pagination to avoid excessive reads and integer overflow.
        page = Math.Clamp(page, 1, 1_000_000);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var itemsQuery = db.Bulletins.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim();
            itemsQuery = itemsQuery.Where(x => x.Title.Contains(term) || x.Content.Contains(term) || x.Number.ToString().Contains(term));
        }
        var total = await itemsQuery.LongCountAsync(cancellationToken);
        var items = await itemsQuery.OrderByDescending(x => x.Number).Skip((Math.Max(1, page) - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return (items, total);
    }

    public Task<Bulletin?> GetBulletinAsync(int number, CancellationToken cancellationToken = default) =>
        db.Bulletins.AsNoTracking().FirstOrDefaultAsync(x => x.Number == number, cancellationToken);

    public Task<SitePage?> GetPageAsync(string slug, CancellationToken cancellationToken = default) =>
        db.Pages.AsNoTracking().FirstOrDefaultAsync(x => x.Slug == slug, cancellationToken);

    public async Task<IReadOnlyList<SitePage>> GetPagesAsync(CancellationToken cancellationToken = default) =>
        await db.Pages.AsNoTracking()
            .OrderBy(x => x.Title)
            .ThenBy(x => x.Slug)
            .ToListAsync(cancellationToken);

    public async Task SavePageAsync(SitePage page, string? originalSlug, CancellationToken cancellationToken = default)
    {
        var isNew = string.IsNullOrWhiteSpace(originalSlug);
        if (isNew)
        {
            if (await db.Pages.AnyAsync(x => x.Slug == page.Slug, cancellationToken))
                throw new InvalidOperationException($"Le slug '{page.Slug}' est déjà utilisé.");

            db.Pages.Add(new SitePage
            {
                Slug = page.Slug,
                Title = page.Title,
                Content = page.Content,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            var entity = await db.Pages.FirstOrDefaultAsync(x => x.Slug == originalSlug, cancellationToken)
                ?? throw new KeyNotFoundException($"La page '{originalSlug}' n'existe plus.");

            if (!string.Equals(originalSlug, page.Slug, StringComparison.Ordinal)
                && await db.Pages.AnyAsync(x => x.Slug == page.Slug, cancellationToken))
                throw new InvalidOperationException($"Le slug '{page.Slug}' est déjà utilisé.");

            entity.Slug = page.Slug;
            entity.Title = page.Title;
            entity.Content = page.Content;
            entity.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeletePageAsync(string slug, CancellationToken cancellationToken = default)
    {
        var page = await db.Pages.FirstOrDefaultAsync(x => x.Slug == slug, cancellationToken);
        if (page is null) return;
        db.Pages.Remove(page);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task AddContactMessageAsync(ContactMessage message, CancellationToken cancellationToken = default)
    {
        db.ContactMessages.Add(message);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<AdminUser?> FindAdminUserAsync(string username, CancellationToken cancellationToken = default) =>
        db.AdminUsers.AsNoTracking().FirstOrDefaultAsync(x => x.Username == username && x.IsActive, cancellationToken);

    public async Task CreateAdminUserAsync(AdminUser user, CancellationToken cancellationToken = default)
    {
        db.AdminUsers.Add(user);
        await db.SaveChangesAsync(cancellationToken);
    }


    public async Task<IReadOnlyList<GalleryImage>> GetGalleryImagesAsync(bool publishedOnly = false, CancellationToken cancellationToken = default)
    {
        var query = db.GalleryImages.AsNoTracking();
        if (publishedOnly) query = query.Where(x => x.IsPublished);
        return await query.OrderBy(x => x.DisplayOrder).ThenByDescending(x => x.CreatedAtUtc).ToListAsync(cancellationToken);
    }

    public Task<GalleryImage?> GetGalleryImageAsync(string id, CancellationToken cancellationToken = default) =>
        db.GalleryImages.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task SaveGalleryImageAsync(GalleryImage image, CancellationToken cancellationToken = default)
    {
        var existing = await db.GalleryImages.FirstOrDefaultAsync(x => x.Id == image.Id, cancellationToken);
        if (existing is null) db.GalleryImages.Add(image);
        else
        {
            existing.Title = image.Title;
            existing.Description = image.Description;
            existing.ImageUrl = image.ImageUrl;
            existing.AltText = image.AltText;
            existing.IsPublished = image.IsPublished;
            existing.DisplayOrder = image.DisplayOrder;
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteGalleryImageAsync(string id, CancellationToken cancellationToken = default)
    {
        var image = await db.GalleryImages.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (image is null) return;
        db.GalleryImages.Remove(image);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await db.Database.EnsureCreatedAsync(cancellationToken);
        // EnsureCreated does not evolve an already-existing database. Create the new
        // administrator table explicitly so upgrades from the previous site version work.
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS `AdminUsers` (
                `Id` int NOT NULL AUTO_INCREMENT,
                `Username` varchar(100) NOT NULL,
                `PasswordHash` varchar(500) NOT NULL,
                `CreatedAtUtc` datetime(6) NOT NULL,
                `IsActive` tinyint(1) NOT NULL,
                PRIMARY KEY (`Id`),
                UNIQUE KEY `IX_AdminUsers_Username` (`Username`)
            ) CHARACTER SET=utf8mb4;
            """, cancellationToken);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS `Members` (
                `Id` int NOT NULL AUTO_INCREMENT,
                `FullName` varchar(120) NOT NULL,
                `Email` varchar(180) NOT NULL,
                `Phone` varchar(30) NULL,
                `JoinedAt` datetime(6) NULL,
                `IsActive` tinyint(1) NOT NULL,
                PRIMARY KEY (`Id`), KEY `IX_Members_Email` (`Email`)
            ) CHARACTER SET=utf8mb4;
            CREATE TABLE IF NOT EXISTS `Appointments` (
                `Id` int NOT NULL AUTO_INCREMENT,
                `Title` varchar(180) NOT NULL,
                `Description` varchar(2000) NULL,
                `StartsAt` datetime(6) NOT NULL,
                `EndsAt` datetime(6) NULL,
                `Location` varchar(180) NULL,
                PRIMARY KEY (`Id`), KEY `IX_Appointments_StartsAt` (`StartsAt`)
            ) CHARACTER SET=utf8mb4;
            """, cancellationToken);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS `LibraryItems` (
                `Id` int NOT NULL AUTO_INCREMENT,
                `Title` varchar(180) NOT NULL,
                `Author` varchar(180) NULL,
                `Category` varchar(120) NULL,
                `PublicationYear` int NULL,
                `Summary` longtext NULL,
                `Content` longtext NOT NULL,
                `CoverImage` varchar(500) NULL,
                `DocumentUrl` varchar(500) NULL,
                `IsPublished` tinyint(1) NOT NULL,
                `CreatedAtUtc` datetime(6) NOT NULL,
                `UpdatedAtUtc` datetime(6) NOT NULL,
                PRIMARY KEY (`Id`), KEY `IX_LibraryItems_IsPublished_Title` (`IsPublished`, `Title`)
            ) CHARACTER SET=utf8mb4;
            """, cancellationToken);

        // Create the catalogue table explicitly for existing installations; EnsureCreated does not migrate schemas.
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS `Partners` (
                `Id` int NOT NULL AUTO_INCREMENT,
                `Name` varchar(160) NOT NULL,
                `Description` longtext NULL,
                `WebsiteUrl` varchar(500) NULL,
                `LogoUrl` varchar(500) NULL,
                `IsPublished` tinyint(1) NOT NULL,
                `DisplayOrder` int NOT NULL,
                `CreatedAtUtc` datetime(6) NOT NULL,
                `UpdatedAtUtc` datetime(6) NOT NULL,
                PRIMARY KEY (`Id`), KEY `IX_Partners_IsPublished_DisplayOrder` (`IsPublished`, `DisplayOrder`)
            ) CHARACTER SET=utf8mb4;
            """, cancellationToken);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS `DocumentStudies` (
                `Id` int NOT NULL AUTO_INCREMENT,
                `Title` varchar(180) NOT NULL,
                `Type` int NOT NULL,
                `Summary` longtext NULL,
                `Content` longtext NOT NULL,
                `CoverImage` varchar(500) NULL,
                `DocumentUrl` varchar(500) NULL,
                `PublishedAt` datetime(6) NULL,
                `IsPublished` tinyint(1) NOT NULL,
                `CreatedAtUtc` datetime(6) NOT NULL,
                `UpdatedAtUtc` datetime(6) NOT NULL,
                PRIMARY KEY (`Id`), KEY `IX_DocumentStudies_IsPublished_Type_PublishedAt` (`IsPublished`, `Type`, `PublishedAt`)
            ) CHARACTER SET=utf8mb4;
            """, cancellationToken);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS `GalleryImages` (
                `Id` varchar(36) NOT NULL,
                `Title` varchar(160) NOT NULL,
                `Description` varchar(500) NULL,
                `ImageUrl` varchar(500) NOT NULL,
                `AltText` varchar(180) NULL,
                `IsPublished` tinyint(1) NOT NULL,
                `DisplayOrder` int NOT NULL,
                `CreatedAtUtc` datetime(6) NOT NULL,
                PRIMARY KEY (`Id`), KEY `IX_GalleryImages_IsPublished_DisplayOrder` (`IsPublished`, `DisplayOrder`)
            ) CHARACTER SET=utf8mb4;
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS `ShopItems` (
                `Id` int NOT NULL AUTO_INCREMENT,
                `Title` varchar(180) NOT NULL,
                `Type` int NOT NULL,
                `Description` longtext NULL,
                `Price` decimal(10,2) NOT NULL,
                `ImageUrl` varchar(500) NULL,
                `DocumentUrl` varchar(500) NULL,
                `IsPublished` tinyint(1) NOT NULL,
                `CreatedAtUtc` datetime(6) NOT NULL,
                `UpdatedAtUtc` datetime(6) NOT NULL,
                PRIMARY KEY (`Id`), KEY `IX_ShopItems_IsPublished_Type` (`IsPublished`, `Type`)
            ) CHARACTER SET=utf8mb4;
            """, cancellationToken);
        await seeder.SeedAsync();
    }
}
