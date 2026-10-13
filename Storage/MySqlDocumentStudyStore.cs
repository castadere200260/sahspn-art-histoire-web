using Microsoft.EntityFrameworkCore;
using Sahspn.ArtHistoire.Data;
using Sahspn.ArtHistoire.Models;
namespace Sahspn.ArtHistoire.Storage;

public sealed class MySqlDocumentStudyStore(SahspDbContext db) : IDocumentStudyStore
{
    public async Task<IReadOnlyList<DocumentStudy>> GetAllAsync(CancellationToken ct = default) =>
        await db.DocumentStudies.AsNoTracking().OrderBy(x => x.Type).ThenByDescending(x => x.PublishedAt).ThenBy(x => x.Title).ToListAsync(ct);
    public async Task<IReadOnlyList<DocumentStudy>> GetPublishedAsync(CancellationToken ct = default) =>
        await db.DocumentStudies.AsNoTracking().Where(x => x.IsPublished).OrderBy(x => x.Type).ThenByDescending(x => x.PublishedAt).ThenBy(x => x.Title).ToListAsync(ct);
    public async Task<(IReadOnlyList<DocumentStudy> Items, long Total)> SearchPublishedAsync(string? query, int page, int pageSize, CancellationToken ct = default)
    {
        // Bound user-controlled pagination to avoid excessive reads and integer overflow.
        page = Math.Clamp(page, 1, 1_000_000);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var q = db.DocumentStudies.AsNoTracking().Where(x => x.IsPublished);
        if (!string.IsNullOrWhiteSpace(query)) { var term = query.Trim(); q = q.Where(x => x.Title.Contains(term) || (x.Summary != null && x.Summary.Contains(term)) || x.Content.Contains(term)); }
        var total = await q.LongCountAsync(ct);
        var items = await q.OrderByDescending(x => x.PublishedAt).ThenBy(x => x.Title).Skip((Math.Max(1, page) - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }
    public Task<DocumentStudy?> GetByIdAsync(int id, CancellationToken ct = default) => db.DocumentStudies.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
    public async Task SaveAsync(DocumentStudy item, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        if (item.Id <= 0) { item.CreatedAtUtc = now; item.UpdatedAtUtc = now; db.DocumentStudies.Add(item); }
        else { var existing = await db.DocumentStudies.FirstOrDefaultAsync(x => x.Id == item.Id, ct) ?? throw new InvalidOperationException($"Document/étude {item.Id} introuvable."); existing.Title = item.Title.Trim(); existing.Type = item.Type; existing.Summary = item.Summary; existing.Content = item.Content; existing.CoverImage = item.CoverImage; existing.DocumentUrl = item.DocumentUrl; existing.PublishedAt = item.PublishedAt; existing.IsPublished = item.IsPublished; existing.UpdatedAtUtc = now; }
        await db.SaveChangesAsync(ct);
    }
    public async Task DeleteAsync(int id, CancellationToken ct = default) { var x = await db.DocumentStudies.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new InvalidOperationException($"Document/étude {id} introuvable."); db.DocumentStudies.Remove(x); await db.SaveChangesAsync(ct); }
}
