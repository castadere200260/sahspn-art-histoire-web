using Sahspn.ArtHistoire.Models;

namespace Sahspn.ArtHistoire.Storage;

/// <summary>Persistence contract for the read-only public shop catalogue and its admin management.</summary>
public interface IShopCatalogStore
{
    Task<IReadOnlyList<ShopItem>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ShopItem>> GetPublishedAsync(CancellationToken ct = default);
    Task<ShopItem?> GetByIdAsync(int id, CancellationToken ct = default);
    Task SaveAsync(ShopItem item, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
