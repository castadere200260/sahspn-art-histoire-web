using MongoDB.Bson;
using MongoDB.Driver;
using Microsoft.Extensions.Options;
using Sahspn.ArtHistoire.Configuration;
using Sahspn.ArtHistoire.Models;

namespace Sahspn.ArtHistoire.Storage;

public sealed class MongoPartnerStore : IPartnerStore
{
    private readonly IMongoCollection<BsonDocument> _partners;

    public MongoPartnerStore(IOptions<MongoDbOptions> options, IMongoClient mongoClient)
    {
        var settings = options.Value;
        var database = mongoClient.GetDatabase(settings.DatabaseName);
        _partners = database.GetCollection<BsonDocument>("partners");
    }

    public async Task<IReadOnlyList<Partner>> GetAllAsync(CancellationToken ct = default) =>
        (await _partners.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync(ct))
            .Select(ToModel).OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name).ToList();

    public async Task<IReadOnlyList<Partner>> GetPublishedAsync(CancellationToken ct = default) =>
        (await _partners.Find(Builders<BsonDocument>.Filter.Eq("IsPublished", true)).ToListAsync(ct))
            .Select(ToModel).OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name).ToList();

    public async Task<Partner?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var doc = await _partners.Find(Builders<BsonDocument>.Filter.Eq("Id", id)).FirstOrDefaultAsync(ct);
        return doc is null ? null : ToModel(doc);
    }

    public async Task SaveAsync(Partner partner, CancellationToken ct = default)
    {
        var isNew = partner.Id <= 0;
        var now = DateTime.UtcNow;
        if (isNew) partner.Id = await NextIdAsync(ct);
        var doc = new BsonDocument
        {
            ["Id"] = partner.Id,
            ["Name"] = partner.Name.Trim(),
            ["Description"] = partner.Description is null ? BsonNull.Value : new BsonString(partner.Description),
            ["WebsiteUrl"] = partner.WebsiteUrl is null ? BsonNull.Value : new BsonString(partner.WebsiteUrl),
            ["LogoUrl"] = partner.LogoUrl is null ? BsonNull.Value : new BsonString(partner.LogoUrl),
            ["IsPublished"] = partner.IsPublished,
            ["DisplayOrder"] = partner.DisplayOrder,
            ["CreatedAtUtc"] = new BsonDateTime(isNew || partner.CreatedAtUtc == default ? now : partner.CreatedAtUtc),
            ["UpdatedAtUtc"] = new BsonDateTime(now)
        };
        if (isNew) await _partners.InsertOneAsync(doc, cancellationToken: ct);
        else
        {
            var filter = Builders<BsonDocument>.Filter.Eq("Id", partner.Id);
            var existing = await _partners.Find(filter).FirstOrDefaultAsync(ct)
                ?? throw new InvalidOperationException($"Partenaire {partner.Id} introuvable dans MongoDB, collection 'partners'.");
            doc["CreatedAtUtc"] = existing.TryGetValue("CreatedAtUtc", out var created) ? created : new BsonDateTime(now);
            var result = await _partners.ReplaceOneAsync(filter, doc, cancellationToken: ct);
            if (result.MatchedCount == 0) throw new InvalidOperationException($"Partenaire {partner.Id} introuvable dans MongoDB, collection 'partners'.");
        }
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var result = await _partners.DeleteOneAsync(Builders<BsonDocument>.Filter.Eq("Id", id), ct);
        if (result.DeletedCount == 0) throw new InvalidOperationException($"Partenaire {id} introuvable dans MongoDB, collection 'partners'.");
    }

    private async Task<int> NextIdAsync(CancellationToken ct)
    {
        var last = await _partners.Find(FilterDefinition<BsonDocument>.Empty)
            .Sort(Builders<BsonDocument>.Sort.Descending("Id")).FirstOrDefaultAsync(ct);
        return (last is not null && last.TryGetValue("Id", out var id) && id.IsNumeric ? id.ToInt32() : 0) + 1;
    }

    private static Partner ToModel(BsonDocument d)
    {
        BsonValue? Get(string name) => d.TryGetValue(name, out var value) ? value : null;
        string? Text(string name) { var value = Get(name); return value is null || value.IsBsonNull ? null : value.ToString(); }
        int Number(string name) { var value = Get(name); return value is not null && value.IsNumeric ? value.ToInt32() : 0; }
        DateTime Date(string name) { var value = Get(name); return value is not null && value.IsBsonDateTime ? value.ToUniversalTime() : DateTime.UtcNow; }
        return new Partner
        {
            Id = Number("Id"), Name = Text("Name") ?? string.Empty, Description = Text("Description"),
            WebsiteUrl = Text("WebsiteUrl"), LogoUrl = Text("LogoUrl"),
            IsPublished = Get("IsPublished")?.IsBoolean == true && Get("IsPublished")!.AsBoolean,
            DisplayOrder = Number("DisplayOrder"), CreatedAtUtc = Date("CreatedAtUtc"), UpdatedAtUtc = Date("UpdatedAtUtc")
        };
    }
}
