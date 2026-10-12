using MongoDB.Bson;
using MongoDB.Driver;
using Microsoft.Extensions.Options;
using Sahspn.ArtHistoire.Configuration;
using Sahspn.ArtHistoire.Models;

namespace Sahspn.ArtHistoire.Storage;

public sealed class MongoShopCatalogStore : IShopCatalogStore
{
    private readonly IMongoCollection<BsonDocument> _items;
    public MongoShopCatalogStore(IOptions<MongoDbOptions> options, IMongoClient mongoClient)
    {
        var settings = options.Value;
        var database = mongoClient.GetDatabase(settings.DatabaseName);
        _items = database.GetCollection<BsonDocument>("shopItems");
    }

    public async Task<IReadOnlyList<ShopItem>> GetAllAsync(CancellationToken ct = default) =>
        (await _items.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync(ct)).Select(ToModel).OrderBy(x => x.Type).ThenBy(x => x.Title).ToList();

    public async Task<IReadOnlyList<ShopItem>> GetPublishedAsync(CancellationToken ct = default) =>
        (await _items.Find(Builders<BsonDocument>.Filter.Eq("IsPublished", true)).ToListAsync(ct)).Select(ToModel).OrderBy(x => x.Type).ThenBy(x => x.Title).ToList();

    public async Task<ShopItem?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var doc = await _items.Find(Builders<BsonDocument>.Filter.Eq("Id", id)).FirstOrDefaultAsync(ct);
        return doc is null ? null : ToModel(doc);
    }

    public async Task SaveAsync(ShopItem item, CancellationToken ct = default)
    {
        var isNew = item.Id <= 0;
        if (isNew) item.Id = await NextIdAsync(ct);
        var now = DateTime.UtcNow;
        var doc = new BsonDocument
        {
            ["Id"] = item.Id,
            ["Title"] = item.Title.Trim(),
            ["Type"] = (int)item.Type,
            ["Description"] = item.Description is null ? BsonNull.Value : new BsonString(item.Description),
            ["Price"] = (double)item.Price,
            ["ImageUrl"] = item.ImageUrl is null ? BsonNull.Value : new BsonString(item.ImageUrl),
            ["DocumentUrl"] = item.DocumentUrl is null ? BsonNull.Value : new BsonString(item.DocumentUrl),
            ["IsPublished"] = item.IsPublished,
            ["CreatedAtUtc"] = new BsonDateTime(isNew ? now : (item.CreatedAtUtc == default ? now : item.CreatedAtUtc)),
            ["UpdatedAtUtc"] = new BsonDateTime(now)
        };
        if (isNew) await _items.InsertOneAsync(doc, cancellationToken: ct);
        else
        {
            var existing = await _items.Find(Builders<BsonDocument>.Filter.Eq("Id", item.Id)).FirstOrDefaultAsync(ct)
                ?? throw new InvalidOperationException($"Article de boutique {item.Id} introuvable dans MongoDB, collection 'shopItems'.");
            doc["CreatedAtUtc"] = existing.TryGetValue("CreatedAtUtc", out var created) ? created : new BsonDateTime(now);
            var result = await _items.ReplaceOneAsync(Builders<BsonDocument>.Filter.Eq("Id", item.Id), doc, cancellationToken: ct);
            if (result.MatchedCount == 0) throw new InvalidOperationException($"Article de boutique {item.Id} introuvable dans MongoDB, collection 'shopItems'.");
        }
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var result = await _items.DeleteOneAsync(Builders<BsonDocument>.Filter.Eq("Id", id), ct);
        if (result.DeletedCount == 0) throw new InvalidOperationException($"Article de boutique {id} introuvable dans MongoDB, collection 'shopItems'.");
    }

    private async Task<int> NextIdAsync(CancellationToken ct)
    {
        var last = await _items.Find(FilterDefinition<BsonDocument>.Empty).Sort(Builders<BsonDocument>.Sort.Descending("Id")).FirstOrDefaultAsync(ct);
        return (last is not null && last.TryGetValue("Id", out var id) && id.IsNumeric ? id.ToInt32() : 0) + 1;
    }

    private static ShopItem ToModel(BsonDocument d)
    {
        BsonValue? Get(string name) => d.TryGetValue(name, out var v) ? v : null;
        string? Text(string name) { var v = Get(name); return v is null || v.IsBsonNull ? null : v.ToString(); }
        int Int(string name) { var v = Get(name); return v is not null && v.IsNumeric ? v.ToInt32() : 0; }
        DateTime Date(string name) { var v = Get(name); return v is not null && v.IsBsonDateTime ? v.ToUniversalTime() : DateTime.UtcNow; }
        var price = Get("Price");
        return new ShopItem
        {
            Id = Int("Id"), Title = Text("Title") ?? string.Empty,
            Type = Enum.IsDefined(typeof(ShopItemType), Int("Type")) ? (ShopItemType)Int("Type") : ShopItemType.Document,
            Description = Text("Description"), Price = price is not null && price.IsNumeric ? (decimal)price.ToDouble() : 0m,
            ImageUrl = Text("ImageUrl"), DocumentUrl = Text("DocumentUrl"),
            IsPublished = Get("IsPublished")?.IsBoolean == true && Get("IsPublished")!.AsBoolean,
            CreatedAtUtc = Date("CreatedAtUtc"), UpdatedAtUtc = Date("UpdatedAtUtc")
        };
    }
}
