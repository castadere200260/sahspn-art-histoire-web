using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using Sahspn.ArtHistoire.Configuration;
using Sahspn.ArtHistoire.Models;
using System.Text.Json;

namespace Sahspn.ArtHistoire.Storage;

/// <summary>MongoDB implementation. Separate BSON documents keep persistence concerns out of MVC models.</summary>
public sealed class MongoSiteContentStore : ISiteContentStore
{
    private readonly IMongoDatabase _database;
    private readonly IMongoCollection<PageDocument> _pages;
    private readonly IMongoCollection<BulletinDocument> _bulletins;
    private readonly IMongoCollection<ContactDocument> _contacts;
    private readonly IMongoCollection<GalleryImageDocument> _galleryImages;
    private readonly IMongoCollection<AdminUserDocument> _adminUsers;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<MongoSiteContentStore> _logger;

    public MongoSiteContentStore(IOptions<MongoDbOptions> options, IWebHostEnvironment environment, ILogger<MongoSiteContentStore> logger, IMongoClient mongoClient)
    {
        _environment = environment;
        _logger = logger;
        var settings = options.Value;
        _database = mongoClient.GetDatabase(settings.DatabaseName);
        _pages = _database.GetCollection<PageDocument>("pages");
        _bulletins = _database.GetCollection<BulletinDocument>("bulletins");
        _contacts = _database.GetCollection<ContactDocument>("contactMessages");
        _galleryImages = _database.GetCollection<GalleryImageDocument>("galleryImages");
        _adminUsers = _database.GetCollection<AdminUserDocument>("adminUsers");
    }

    public async Task<IReadOnlyList<Bulletin>> GetLatestBulletinsAsync(int count, CancellationToken cancellationToken = default) =>
        (await _bulletins.Find(FilterDefinition<BulletinDocument>.Empty).SortByDescending(x => x.Number).Limit(count).ToListAsync(cancellationToken)).Select(ToModel).ToList();

    public async Task<(IReadOnlyList<Bulletin> Items, long Total)> SearchBulletinsAsync(string? query, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        // Bound user-controlled pagination to avoid excessive reads and integer overflow.
        page = Math.Clamp(page, 1, 1_000_000);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var filter = FilterDefinition<BulletinDocument>.Empty;
        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim();
            var regex = new BsonRegularExpression(RegexEscape(term), "i");
            var builder = Builders<BulletinDocument>.Filter;
            filter = builder.Or(builder.Regex(x => x.Title, regex), builder.Regex(x => x.Content, regex), builder.Eq(x => x.Number, int.TryParse(term, out var number) ? number : int.MinValue));
        }
        var total = await _bulletins.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var docs = await _bulletins.Find(filter).SortByDescending(x => x.Number).Skip((Math.Max(1, page) - 1) * pageSize).Limit(pageSize).ToListAsync(cancellationToken);
        return (docs.Select(ToModel).ToList(), total);
    }

    public async Task<Bulletin?> GetBulletinAsync(int number, CancellationToken cancellationToken = default)
    {
        var item = await _bulletins.Find(x => x.Number == number).FirstOrDefaultAsync(cancellationToken);
        return item is null ? null : ToModel(item);
    }

    public async Task<SitePage?> GetPageAsync(string slug, CancellationToken cancellationToken = default)
    {
        var item = await _pages.Find(x => x.Slug == slug).FirstOrDefaultAsync(cancellationToken);
        return item is null ? null : new SitePage { Id = 0, Slug = item.Slug, Title = item.Title, Content = item.Content, UpdatedAt = item.UpdatedAt };
    }

    public async Task<IReadOnlyList<SitePage>> GetPagesAsync(CancellationToken cancellationToken = default)
    {
        var pages = await _pages.Find(FilterDefinition<PageDocument>.Empty)
            .SortBy(x => x.Title)
            .ThenBy(x => x.Slug)
            .ToListAsync(cancellationToken);

        return pages.Select(x => new SitePage
        {
            Id = 0,
            Slug = x.Slug,
            Title = x.Title,
            Content = x.Content,
            UpdatedAt = x.UpdatedAt
        }).ToList();
    }

    public async Task SavePageAsync(SitePage page, string? originalSlug, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(originalSlug))
        {
            if (await _pages.Find(x => x.Slug == page.Slug).AnyAsync(cancellationToken))
                throw new InvalidOperationException($"Le slug '{page.Slug}' est déjà utilisé.");

            await _pages.InsertOneAsync(new PageDocument
            {
                Slug = page.Slug,
                Title = page.Title,
                Content = page.Content,
                UpdatedAt = DateTime.UtcNow
            }, cancellationToken: cancellationToken);
            return;
        }

        if (!string.Equals(originalSlug, page.Slug, StringComparison.Ordinal)
            && await _pages.Find(x => x.Slug == page.Slug).AnyAsync(cancellationToken))
            throw new InvalidOperationException($"Le slug '{page.Slug}' est déjà utilisé.");

        var update = Builders<PageDocument>.Update
            .Set(x => x.Slug, page.Slug)
            .Set(x => x.Title, page.Title)
            .Set(x => x.Content, page.Content)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);

        var result = await _pages.UpdateOneAsync(
            x => x.Slug == originalSlug,
            update,
            cancellationToken: cancellationToken);

        if (result.MatchedCount == 0)
            throw new KeyNotFoundException($"La page '{originalSlug}' n'existe plus.");
    }

    public async Task DeletePageAsync(string slug, CancellationToken cancellationToken = default) =>
        await _pages.DeleteOneAsync(x => x.Slug == slug, cancellationToken);

    public async Task AddContactMessageAsync(ContactMessage message, CancellationToken cancellationToken = default) =>
        await _contacts.InsertOneAsync(new ContactDocument { Name = message.Name, Email = message.Email, Subject = message.Subject, Message = message.Message, CreatedAt = message.CreatedAt }, cancellationToken: cancellationToken);

    public async Task<AdminUser?> FindAdminUserAsync(string username, CancellationToken cancellationToken = default)
    {
        var user = await _adminUsers.Find(x => x.Username == username && x.IsActive).FirstOrDefaultAsync(cancellationToken);
        return user is null ? null : new AdminUser { Id = 0, Username = user.Username, PasswordHash = user.PasswordHash, CreatedAtUtc = user.CreatedAtUtc, IsActive = user.IsActive };
    }

    public async Task CreateAdminUserAsync(AdminUser user, CancellationToken cancellationToken = default)
    {
        await _adminUsers.InsertOneAsync(new AdminUserDocument { Username = user.Username, PasswordHash = user.PasswordHash, CreatedAtUtc = user.CreatedAtUtc, IsActive = user.IsActive }, cancellationToken: cancellationToken);
    }


    public async Task<IReadOnlyList<GalleryImage>> GetGalleryImagesAsync(bool publishedOnly = false, CancellationToken cancellationToken = default)
    {
        var filter = publishedOnly
            ? Builders<GalleryImageDocument>.Filter.Eq(x => x.IsPublished, true)
            : FilterDefinition<GalleryImageDocument>.Empty;
        var items = await _galleryImages.Find(filter).SortBy(x => x.DisplayOrder).ThenByDescending(x => x.CreatedAtUtc).ToListAsync(cancellationToken);
        return items.Select(ToGalleryModel).ToList();
    }

    public async Task<GalleryImage?> GetGalleryImageAsync(string id, CancellationToken cancellationToken = default)
    {
        var item = await _galleryImages.Find(x => x.Id == id).FirstOrDefaultAsync(cancellationToken);
        return item is null ? null : ToGalleryModel(item);
    }

    public async Task SaveGalleryImageAsync(GalleryImage image, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(image.Id)) image.Id = Guid.NewGuid().ToString("N");
        var document = new GalleryImageDocument { Id = image.Id, Title = image.Title, Description = image.Description, ImageUrl = image.ImageUrl, AltText = image.AltText, IsPublished = image.IsPublished, DisplayOrder = image.DisplayOrder, CreatedAtUtc = image.CreatedAtUtc };
        await _galleryImages.ReplaceOneAsync(x => x.Id == image.Id, document, new ReplaceOptions { IsUpsert = true }, cancellationToken);
    }

    public async Task DeleteGalleryImageAsync(string id, CancellationToken cancellationToken = default) =>
        await _galleryImages.DeleteOneAsync(x => x.Id == id, cancellationToken);

    private static GalleryImage ToGalleryModel(GalleryImageDocument x) => new() { Id = x.Id, Title = x.Title, Description = x.Description, ImageUrl = x.ImageUrl, AltText = x.AltText, IsPublished = x.IsPublished, DisplayOrder = x.DisplayOrder, CreatedAtUtc = x.CreatedAtUtc };

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        // Fail fast with a provider-specific error if MongoDB is not reachable.
        await _database.Client.GetDatabase("admin")
            .RunCommandAsync<BsonDocument>(new BsonDocumentCommand<BsonDocument>(new BsonDocument("ping", 1)), cancellationToken: cancellationToken);

        // Create indexes idempotently; unique keys prevent duplicate seed records.
        await _adminUsers.Indexes.CreateOneAsync(new CreateIndexModel<AdminUserDocument>(Builders<AdminUserDocument>.IndexKeys.Ascending(x => x.Username), new CreateIndexOptions { Unique = true }), cancellationToken: cancellationToken);
        await _pages.Indexes.CreateOneAsync(new CreateIndexModel<PageDocument>(Builders<PageDocument>.IndexKeys.Ascending(x => x.Slug), new CreateIndexOptions { Unique = true }), cancellationToken: cancellationToken);
        await _bulletins.Indexes.CreateOneAsync(new CreateIndexModel<BulletinDocument>(Builders<BulletinDocument>.IndexKeys.Ascending(x => x.Number), new CreateIndexOptions { Unique = true }), cancellationToken: cancellationToken);
        await _galleryImages.Indexes.CreateOneAsync(new CreateIndexModel<GalleryImageDocument>(Builders<GalleryImageDocument>.IndexKeys.Ascending(x => x.IsPublished).Ascending(x => x.DisplayOrder)), cancellationToken: cancellationToken);
        var path = Path.Combine(_environment.ContentRootPath, "seed-content.json");
        if (!File.Exists(path)) { _logger.LogWarning("Seed file {SeedPath} was not found; MongoDB collections were not seeded.", path); return; }
        if (await _pages.EstimatedDocumentCountAsync(cancellationToken: cancellationToken) == 0 || await _bulletins.EstimatedDocumentCountAsync(cancellationToken: cancellationToken) == 0)
        {
            using var json = JsonDocument.Parse(await File.ReadAllTextAsync(path, cancellationToken));
            var root = json.RootElement;
            var pageMap = new Dictionary<string, (string title, string slug)>
            {
                ["historique.htm"] = ("L’association", "association"),
                ["actualites.htm"] = ("Actualités", "actualites"),
                ["biblio.htm"] = ("Bibliothèque & ressources", "ressources"),
                ["ag.htm"] = ("Assemblée générale", "assemblee-generale"),
                ["contact.htm"] = ("Contact", "contact"),
                ["liens.htm"] = ("Liens utiles", "liens"),
                ["auteur.htm"] = ("Publications", "publications"),
                ["dernews.htm"] = ("Dernières nouvelles", "dernieres-nouvelles")
            };
            if (await _pages.EstimatedDocumentCountAsync(cancellationToken: cancellationToken) == 0 && root.TryGetProperty("pages", out var pages))
            {
                var docs = new List<PageDocument>();
                foreach (var entry in pages.EnumerateObject())
                    if (pageMap.TryGetValue(entry.Name, out var meta) && !string.IsNullOrWhiteSpace(entry.Value.GetString()))
                        docs.Add(new PageDocument { Slug = meta.slug, Title = meta.title, Content = entry.Value.GetString()!, UpdatedAt = DateTime.UtcNow });
                if (docs.Count > 0) await _pages.InsertManyAsync(docs, cancellationToken: cancellationToken);
            }
            if (await _bulletins.EstimatedDocumentCountAsync(cancellationToken: cancellationToken) == 0 && root.TryGetProperty("bulletins", out var bulletins))
            {
                var docs = new List<BulletinDocument>();
                foreach (var entry in bulletins.EnumerateArray())
                {
                    var number = entry.GetProperty("number").GetInt32();
                    string? cover = number switch { 185 => "/heritage/image/Bull 185/Couv 185.png", 184 => "/heritage/image/Bull 184/Couv 184.png", 183 => "/heritage/image/Bull 183/Couverture.png", _ => null };
                    docs.Add(new BulletinDocument { Number = number, Title = entry.GetProperty("title").GetString() ?? $"Bulletin n° {number}", Content = entry.TryGetProperty("content", out var c) ? c.GetString() ?? "" : "", CoverImage = cover });
                }
                if (docs.Count > 0) await _bulletins.InsertManyAsync(docs, cancellationToken: cancellationToken);
            }
        }
    }

    private static string RegexEscape(string value) => System.Text.RegularExpressions.Regex.Escape(value);
    private static Bulletin ToModel(BulletinDocument x) => new() { Id = 0, Number = x.Number, Title = x.Title, Content = x.Content, CoverImage = x.CoverImage, DocumentUrl = x.DocumentUrl, PublishedAt = x.PublishedAt };

    private sealed class GalleryImageDocument
    {
        [BsonId, BsonRepresentation(BsonType.String)] public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public required string Title { get; set; }
        public string? Description { get; set; }
        public required string ImageUrl { get; set; }
        public string? AltText { get; set; }
        public bool IsPublished { get; set; }
        public int DisplayOrder { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }

    private sealed class PageDocument
    {
        [BsonId, BsonRepresentation(BsonType.ObjectId)] public string? Id { get; set; }
        public required string Slug { get; set; }
        public required string Title { get; set; }
        public required string Content { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
    private sealed class BulletinDocument
    {
        [BsonId, BsonRepresentation(BsonType.ObjectId)] public string? Id { get; set; }
        public int Number { get; set; }
        public required string Title { get; set; }
        public string Content { get; set; } = "";
        public string? CoverImage { get; set; }
        public string? DocumentUrl { get; set; }
        public DateTime? PublishedAt { get; set; }
    }
    private sealed class AdminUserDocument
    {
        [BsonId, BsonRepresentation(BsonType.ObjectId)]
        public string? MongoId { get; set; }

        [BsonElement("Id")]
        public int Id { get; set; }

        public required string Username { get; set; }
        public required string PasswordHash { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public bool IsActive { get; set; }
    }
    private sealed class ContactDocument
    {
        [BsonId, BsonRepresentation(BsonType.ObjectId)] public string? Id { get; set; }
        public required string Name { get; set; }
        public required string Email { get; set; }
        public required string Subject { get; set; }
        public required string Message { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
