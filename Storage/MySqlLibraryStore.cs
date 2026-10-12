using Microsoft.EntityFrameworkCore;
using Sahspn.ArtHistoire.Data;
using Sahspn.ArtHistoire.Models;
namespace Sahspn.ArtHistoire.Storage;

public sealed class MySqlLibraryStore(SahspDbContext db) : ILibraryStore
{
    public async Task<IReadOnlyList<LibraryItem>> GetAllAsync(CancellationToken ct = default) =>
        await db.LibraryItems.AsNoTracking().OrderBy(x => x.Title).ToListAsync(ct);

    public async Task<(IReadOnlyList<LibraryItem> Items, long Total)> SearchPublishedAsync(string? query, int page, int pageSize, CancellationToken ct = default)
    {
        // Bound user-controlled pagination to avoid excessive reads and integer overflow.
        page = Math.Clamp(page, 1, 1_000_000);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var q = db.LibraryItems.AsNoTracking().Where(x => x.IsPublished);
        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim();
            var hasYear = int.TryParse(term, out var year);
            q = q.Where(x => x.Title.Contains(term) || (x.Author != null && x.Author.Contains(term)) || (x.Category != null && x.Category.Contains(term)) || (x.Summary != null && x.Summary.Contains(term)) || x.Content.Contains(term) || (hasYear && x.PublicationYear == year));
        }
        var total = await q.LongCountAsync(ct);
        var items = await q.OrderBy(x => x.Title).Skip((Math.Max(1, page) - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }

    public Task<LibraryItem?> GetByIdAsync(int id, CancellationToken ct = default) => db.LibraryItems.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task SaveAsync(LibraryItem item, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        if (item.Id <= 0) { item.CreatedAtUtc = now; item.UpdatedAtUtc = now; db.LibraryItems.Add(item); }
        else
        {
            var existing = await db.LibraryItems.FirstOrDefaultAsync(x => x.Id == item.Id, ct) ?? throw new InvalidOperationException($"Élément de bibliothèque {item.Id} introuvable.");
            existing.Title = item.Title.Trim(); existing.Author = item.Author; existing.Category = item.Category; existing.PublicationYear = item.PublicationYear; existing.Summary = item.Summary; existing.Content = item.Content; existing.CoverImage = item.CoverImage; existing.DocumentUrl = item.DocumentUrl; existing.IsPublished = item.IsPublished; existing.UpdatedAtUtc = now;
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var item = await db.LibraryItems.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new InvalidOperationException($"Élément de bibliothèque {id} introuvable.");
        db.LibraryItems.Remove(item); await db.SaveChangesAsync(ct);
    }
}
