using Microsoft.EntityFrameworkCore;
using Sahspn.ArtHistoire.Data;
using Sahspn.ArtHistoire.Models;

namespace Sahspn.ArtHistoire.Storage;

public sealed class MySqlShopCatalogStore(SahspDbContext db) : IShopCatalogStore
{
    public async Task<IReadOnlyList<ShopItem>> GetAllAsync(CancellationToken ct = default) =>
        await db.ShopItems.AsNoTracking().OrderBy(x => x.Type).ThenBy(x => x.Title).ToListAsync(ct);

    public async Task<IReadOnlyList<ShopItem>> GetPublishedAsync(CancellationToken ct = default) =>
        await db.ShopItems.AsNoTracking().Where(x => x.IsPublished).OrderBy(x => x.Type).ThenBy(x => x.Title).ToListAsync(ct);

    public Task<ShopItem?> GetByIdAsync(int id, CancellationToken ct = default) =>
        db.ShopItems.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task SaveAsync(ShopItem item, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        if (item.Id <= 0)
        {
            item.CreatedAtUtc = now;
            item.UpdatedAtUtc = now;
            db.ShopItems.Add(item);
        }
        else
        {
            var existing = await db.ShopItems.FirstOrDefaultAsync(x => x.Id == item.Id, ct)
                ?? throw new InvalidOperationException($"Article de boutique {item.Id} introuvable.");
            existing.Title = item.Title.Trim();
            existing.Type = item.Type;
            existing.Description = item.Description;
            existing.Price = item.Price;
            existing.ImageUrl = item.ImageUrl;
            existing.DocumentUrl = item.DocumentUrl;
            existing.IsPublished = item.IsPublished;
            existing.UpdatedAtUtc = now;
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var item = await db.ShopItems.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException($"Article de boutique {id} introuvable.");
        db.ShopItems.Remove(item);
        await db.SaveChangesAsync(ct);
    }
}
