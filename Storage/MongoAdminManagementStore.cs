using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Sahspn.ArtHistoire.Configuration;
using Sahspn.ArtHistoire.Models;

namespace Sahspn.ArtHistoire.Storage;

/// <summary>
/// MongoDB persistence for administration. Reads both PascalCase documents created by
/// MongoSiteContentStore and legacy camelCase documents; writes canonical PascalCase fields.
/// </summary>
public sealed class MongoAdminManagementStore : IAdminManagementStore
{
    private readonly IMongoCollection<BsonDocument> _bulletins;
    private readonly IMongoCollection<BsonDocument> _members;
    private readonly IMongoCollection<BsonDocument> _appointments;
    private readonly IMongoCollection<BsonDocument> _users;

    public MongoAdminManagementStore(IOptions<MongoDbOptions> options, IMongoClient mongoClient)
    {
        var settings = options.Value;
        var database = mongoClient.GetDatabase(settings.DatabaseName);
        _bulletins = database.GetCollection<BsonDocument>("bulletins");
        _members = database.GetCollection<BsonDocument>("members");
        _appointments = database.GetCollection<BsonDocument>("appointments");
        _users = database.GetCollection<BsonDocument>("adminUsers");
    }

    public async Task<IReadOnlyList<Bulletin>> GetBulletinsAsync(CancellationToken ct)
    {
        var docs = await _bulletins.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync(ct);
        return docs.Select(d => new Bulletin
        {
            Number = Int(d, "Number"), Title = String(d, "Title"), Content = String(d, "Content"),
            CoverImage = NullableString(d, "CoverImage"), DocumentUrl = NullableString(d, "DocumentUrl"),
            PublishedAt = Date(d, "PublishedAt")
        }).OrderByDescending(x => x.Number).ToList();
    }

    public async Task SaveBulletinAsync(Bulletin item, int originalNumber, CancellationToken ct)
    {
        var isNew = originalNumber <= 0;
        var fields = new BsonDocument
        {
            ["Number"] = item.Number,
            ["Title"] = item.Title ?? string.Empty,
            ["Content"] = item.Content ?? string.Empty,
            ["CoverImage"] = item.CoverImage is null ? BsonNull.Value : new BsonString(item.CoverImage),
            ["DocumentUrl"] = item.DocumentUrl is null ? BsonNull.Value : new BsonString(item.DocumentUrl),
            ["PublishedAt"] = item.PublishedAt.HasValue ? new BsonDateTime(item.PublishedAt.Value) : BsonNull.Value
        };

        if (isNew)
        {
            await _bulletins.InsertOneAsync(fields, cancellationToken: ct);
            return;
        }

        var filter = AnyFieldEquals("Number", "number", originalNumber);
        var result = await _bulletins.UpdateOneAsync(filter, new BsonDocument("$set", fields), cancellationToken: ct);
        if (result.MatchedCount == 0)
            throw new InvalidOperationException($"Bulletin {originalNumber} introuvable dans MongoDB, collection 'bulletins'.");
    }

    public async Task DeleteBulletinAsync(int number, CancellationToken ct)
    {
        var result = await _bulletins.DeleteOneAsync(AnyFieldEquals("Number", "number", number), ct);
        if (result.DeletedCount == 0)
            throw new InvalidOperationException($"Bulletin {number} introuvable dans MongoDB, collection 'bulletins'.");
    }

    public async Task<IReadOnlyList<Member>> GetMembersAsync(CancellationToken ct)
    {
        var docs = await _members.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync(ct);
        return docs.Select(d => new Member
        {
            Id = Int(d, "Id"), FullName = String(d, "FullName"), Email = String(d, "Email"),
            Phone = NullableString(d, "Phone"), JoinedAt = Date(d, "JoinedAt"), IsActive = Bool(d, "IsActive", true)
        }).OrderBy(x => x.FullName).ToList();
    }

    public async Task SaveMemberAsync(Member item, CancellationToken ct)
    {
        // Id <= 0 is always a create; never trust a client-supplied operation flag.
        var isNew = item.Id <= 0;
        if (isNew) item.Id = await NextIdAsync(_members, "Id", "id", ct);
        var fields = new BsonDocument
        {
            ["Id"] = item.Id, ["FullName"] = item.FullName, ["Email"] = item.Email,
            ["Phone"] = item.Phone is null ? BsonNull.Value : new BsonString(item.Phone),
            ["JoinedAt"] = item.JoinedAt.HasValue ? new BsonDateTime(item.JoinedAt.Value) : BsonNull.Value,
            ["IsActive"] = item.IsActive
        };
        if (isNew) { await _members.InsertOneAsync(fields, cancellationToken: ct); return; }
        var result = await _members.UpdateOneAsync(AnyFieldEquals("Id", "id", item.Id), new BsonDocument("$set", fields), cancellationToken: ct);
        if (result.MatchedCount == 0) throw new InvalidOperationException($"Membre {item.Id} introuvable dans MongoDB, collection 'members'.");
    }

    public async Task DeleteMemberAsync(int id, CancellationToken ct)
    {
        var result = await _members.DeleteOneAsync(AnyFieldEquals("Id", "id", id), ct);
        if (result.DeletedCount == 0) throw new InvalidOperationException($"Membre {id} introuvable dans MongoDB, collection 'members'.");
    }

    public async Task<IReadOnlyList<Appointment>> GetAppointmentsAsync(CancellationToken ct)
    {
        var docs = await _appointments.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync(ct);
        return docs.Select(d => new Appointment
        {
            Id = Int(d, "Id"), Title = String(d, "Title"), Description = NullableString(d, "Description"),
            StartsAt = Date(d, "StartsAt") ?? DateTime.MinValue, EndsAt = Date(d, "EndsAt"), Location = NullableString(d, "Location")
        }).OrderBy(x => x.StartsAt).ToList();
    }

    public async Task SaveAppointmentAsync(Appointment item, CancellationToken ct)
    {
        var isNew = item.Id <= 0;
        if (isNew) item.Id = await NextIdAsync(_appointments, "Id", "id", ct);
        var fields = new BsonDocument
        {
            ["Id"] = item.Id, ["Title"] = item.Title,
            ["Description"] = item.Description is null ? BsonNull.Value : new BsonString(item.Description),
            ["StartsAt"] = new BsonDateTime(item.StartsAt),
            ["EndsAt"] = item.EndsAt.HasValue ? new BsonDateTime(item.EndsAt.Value) : BsonNull.Value,
            ["Location"] = item.Location is null ? BsonNull.Value : new BsonString(item.Location)
        };
        if (isNew) { await _appointments.InsertOneAsync(fields, cancellationToken: ct); return; }
        var result = await _appointments.UpdateOneAsync(AnyFieldEquals("Id", "id", item.Id), new BsonDocument("$set", fields), cancellationToken: ct);
        if (result.MatchedCount == 0) throw new InvalidOperationException($"Rendez-vous {item.Id} introuvable dans MongoDB, collection 'appointments'.");
    }

    public async Task DeleteAppointmentAsync(int id, CancellationToken ct)
    {
        var result = await _appointments.DeleteOneAsync(AnyFieldEquals("Id", "id", id), ct);
        if (result.DeletedCount == 0) throw new InvalidOperationException($"Rendez-vous {id} introuvable dans MongoDB, collection 'appointments'.");
    }

    public async Task<IReadOnlyList<AdminUser>> GetAdminUsersAsync(CancellationToken ct)
    {
        var docs = await _users.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync(ct);
        // Older records did not persist an integer ID. Migrate those records once so edit/delete
        // operations use a stable key instead of a list position that changes when usernames change.
        var nextId = docs.Select(d => Int(d, "Id")).DefaultIfEmpty(0).Max() + 1;
        foreach (var doc in docs.Where(d => Value(d, "Id") is null || Int(d, "Id") <= 0))
        {
            var assignedId = nextId++;
            await _users.UpdateOneAsync(
                Builders<BsonDocument>.Filter.Eq("_id", doc["_id"]),
                new BsonDocument("$set", new BsonDocument("Id", assignedId)),
                cancellationToken: ct);
            doc["Id"] = assignedId;
        }

        return docs.Select(d => new AdminUser
        {
            Id = Int(d, "Id"),
            Username = String(d, "Username"),
            PasswordHash = String(d, "PasswordHash"),
            CreatedAtUtc = Date(d, "CreatedAtUtc") ?? DateTime.UtcNow,
            IsActive = Bool(d, "IsActive", true)
        }).OrderBy(x => x.Username, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task SaveAdminUserAsync(AdminUser item, CancellationToken ct)
    {
        var isNew = item.Id <= 0;
        if (isNew)
        {
            item.Id = await NextIdAsync(_users, "Id", "id", ct);
            await _users.InsertOneAsync(new BsonDocument
            {
                ["Id"] = item.Id,
                ["Username"] = item.Username,
                ["PasswordHash"] = item.PasswordHash,
                ["CreatedAtUtc"] = new BsonDateTime(item.CreatedAtUtc),
                ["IsActive"] = item.IsActive
            }, cancellationToken: ct);
            return;
        }

        var update = new BsonDocument
        {
            ["Username"] = item.Username,
            ["IsActive"] = item.IsActive
        };
        if (!string.IsNullOrWhiteSpace(item.PasswordHash)) update["PasswordHash"] = item.PasswordHash;
        var result = await _users.UpdateOneAsync(
            AnyFieldEquals("Id", "id", item.Id),
            new BsonDocument("$set", update),
            cancellationToken: ct);
        if (result.MatchedCount == 0)
            throw new InvalidOperationException($"Utilisateur {item.Id} introuvable dans MongoDB, collection 'adminUsers'.");
    }

    public async Task DeleteAdminUserAsync(int id, CancellationToken ct)
    {
        var result = await _users.DeleteOneAsync(AnyFieldEquals("Id", "id", id), ct);
        if (result.DeletedCount == 0)
            throw new InvalidOperationException($"Utilisateur {id} introuvable dans MongoDB, collection 'adminUsers'.");
    }

    private static FilterDefinition<BsonDocument> AnyFieldEquals(string canonical, string legacy, BsonValue value) =>
        Builders<BsonDocument>.Filter.Or(
            Builders<BsonDocument>.Filter.Eq(canonical, value),
            Builders<BsonDocument>.Filter.Eq(legacy, value));

    private static async Task<int> NextIdAsync(IMongoCollection<BsonDocument> collection, string canonical, string legacy, CancellationToken ct)
    {
        var docs = await collection.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync(ct);
        return docs.Select(d => Int(d, canonical)).DefaultIfEmpty(0).Max() + 1;
    }

    private static BsonValue? Value(BsonDocument document, string name)
    {
        if (document.TryGetValue(name, out var exact)) return exact;
        var legacy = char.ToLowerInvariant(name[0]) + name[1..];
        if (document.TryGetValue(legacy, out var camel)) return camel;
        foreach (var element in document.Elements)
            if (string.Equals(element.Name, name, StringComparison.OrdinalIgnoreCase))
                return element.Value;
        return null;
    }

    private static int Int(BsonDocument d, string name) { var v = Value(d, name); return v is not null && !v.IsBsonNull && v.IsNumeric ? v.ToInt32() : 0; }
    private static string String(BsonDocument d, string name) { var v = Value(d, name); return v is not null && !v.IsBsonNull ? v.ToString() : string.Empty; }
    private static string? NullableString(BsonDocument d, string name) { var v = Value(d, name); return v is null || v.IsBsonNull ? null : v.ToString(); }
    private static DateTime? Date(BsonDocument d, string name) { var v = Value(d, name); return v is not null && !v.IsBsonNull && v.IsBsonDateTime ? v.ToUniversalTime() : null; }
    private static bool Bool(BsonDocument d, string name, bool fallback) { var v = Value(d, name); return v is not null && v.IsBoolean ? v.AsBoolean : fallback; }
}
