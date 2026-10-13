using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Sahspn.ArtHistoire.Configuration;
using Sahspn.ArtHistoire.Models;
namespace Sahspn.ArtHistoire.Storage;

public sealed class MongoDocumentStudyStore : IDocumentStudyStore
{
    private readonly IMongoCollection<BsonDocument> _items;
    public MongoDocumentStudyStore(IOptions<MongoDbOptions> options, IMongoClient mongoClient) { var s = options.Value; _items = mongoClient.GetDatabase(s.DatabaseName).GetCollection<BsonDocument>("documentsAndStudies"); }
    public async Task<IReadOnlyList<DocumentStudy>> GetAllAsync(CancellationToken ct = default) => (await _items.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync(ct)).Select(ToModel).OrderBy(x => x.Type).ThenByDescending(x => x.PublishedAt).ThenBy(x => x.Title).ToList();
    public async Task<IReadOnlyList<DocumentStudy>> GetPublishedAsync(CancellationToken ct = default) => (await _items.Find(Builders<BsonDocument>.Filter.Eq("IsPublished", true)).ToListAsync(ct)).Select(ToModel).OrderBy(x => x.Type).ThenByDescending(x => x.PublishedAt).ThenBy(x => x.Title).ToList();
    public async Task<(IReadOnlyList<DocumentStudy> Items, long Total)> SearchPublishedAsync(string? query, int page, int pageSize, CancellationToken ct = default)
    {
        // Bound user-controlled pagination to avoid excessive reads and integer overflow.
        page = Math.Clamp(page, 1, 1_000_000);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var f = Builders<BsonDocument>.Filter.Eq("IsPublished", true);
        if (!string.IsNullOrWhiteSpace(query)) { var rx = new BsonRegularExpression(System.Text.RegularExpressions.Regex.Escape(query.Trim()), "i"); f &= Builders<BsonDocument>.Filter.Or(Builders<BsonDocument>.Filter.Regex("Title", rx), Builders<BsonDocument>.Filter.Regex("Summary", rx), Builders<BsonDocument>.Filter.Regex("Content", rx), Builders<BsonDocument>.Filter.Regex("TypeName", rx)); }
        var total = await _items.CountDocumentsAsync(f, cancellationToken: ct); var docs = await _items.Find(f).Sort(Builders<BsonDocument>.Sort.Descending("PublishedAt").Ascending("Title")).Skip((Math.Max(1, page) - 1) * pageSize).Limit(pageSize).ToListAsync(ct); return (docs.Select(ToModel).ToList(), total);
    }
    public async Task<DocumentStudy?> GetByIdAsync(int id, CancellationToken ct = default) { var d = await _items.Find(Builders<BsonDocument>.Filter.Eq("Id", id)).FirstOrDefaultAsync(ct); return d is null ? null : ToModel(d); }
    public async Task SaveAsync(DocumentStudy item, CancellationToken ct = default)
    {
        var isNew = item.Id <= 0; var now = DateTime.UtcNow; if (isNew) item.Id = await NextIdAsync(ct);
        var doc = new BsonDocument { ["Id"] = item.Id, ["Title"] = item.Title.Trim(), ["Type"] = (int)item.Type, ["TypeName"] = item.Type.ToString(), ["Summary"] = Val(item.Summary), ["Content"] = item.Content ?? string.Empty, ["CoverImage"] = Val(item.CoverImage), ["DocumentUrl"] = Val(item.DocumentUrl), ["PublishedAt"] = item.PublishedAt.HasValue ? new BsonDateTime(item.PublishedAt.Value) : BsonNull.Value, ["IsPublished"] = item.IsPublished, ["CreatedAtUtc"] = new BsonDateTime(isNew ? now : (item.CreatedAtUtc == default ? now : item.CreatedAtUtc)), ["UpdatedAtUtc"] = new BsonDateTime(now) };
        if (isNew) await _items.InsertOneAsync(doc, cancellationToken: ct); else { var old = await _items.Find(Builders<BsonDocument>.Filter.Eq("Id", item.Id)).FirstOrDefaultAsync(ct) ?? throw new InvalidOperationException($"Document/étude {item.Id} introuvable dans MongoDB, collection 'documentsAndStudies'."); if (old.TryGetValue("CreatedAtUtc", out var created)) doc["CreatedAtUtc"] = created; var r = await _items.ReplaceOneAsync(Builders<BsonDocument>.Filter.Eq("Id", item.Id), doc, cancellationToken: ct); if (r.MatchedCount == 0) throw new InvalidOperationException($"Document/étude {item.Id} introuvable dans MongoDB."); }
    }
    public async Task DeleteAsync(int id, CancellationToken ct = default) { var r = await _items.DeleteOneAsync(Builders<BsonDocument>.Filter.Eq("Id", id), ct); if (r.DeletedCount == 0) throw new InvalidOperationException($"Document/étude {id} introuvable dans MongoDB."); }
    private async Task<int> NextIdAsync(CancellationToken ct) { var d = await _items.Find(FilterDefinition<BsonDocument>.Empty).Sort(Builders<BsonDocument>.Sort.Descending("Id")).FirstOrDefaultAsync(ct); return (d is not null && d.TryGetValue("Id", out var v) && v.IsNumeric ? v.ToInt32() : 0) + 1; }
    private static BsonValue Val(string? s) => s is null ? BsonNull.Value : new BsonString(s);
    private static DocumentStudy ToModel(BsonDocument d) { BsonValue? G(string n) => d.TryGetValue(n, out var v) ? v : null; string? S(string n) { var v = G(n); return v is null || v.IsBsonNull ? null : v.ToString(); } int I(string n) { var v = G(n); return v is not null && v.IsNumeric ? v.ToInt32() : 0; } DateTime? D(string n) { var v = G(n); return v is not null && v.IsBsonDateTime ? v.ToUniversalTime() : null; } DateTime Date(string n) => D(n) ?? DateTime.UtcNow; var type = I("Type"); return new DocumentStudy { Id = I("Id"), Title = S("Title") ?? string.Empty, Type = Enum.IsDefined(typeof(DocumentStudyType), type) ? (DocumentStudyType)type : DocumentStudyType.Document, Summary = S("Summary"), Content = S("Content") ?? string.Empty, CoverImage = S("CoverImage"), DocumentUrl = S("DocumentUrl"), PublishedAt = D("PublishedAt"), IsPublished = G("IsPublished")?.IsBoolean == true && G("IsPublished")!.AsBoolean, CreatedAtUtc = Date("CreatedAtUtc"), UpdatedAtUtc = Date("UpdatedAtUtc") }; }
}
