using Sahspn.ArtHistoire.Models;

namespace Sahspn.ArtHistoire.Storage;

/// <summary>Persistence contract for partner administration and public read-only display.</summary>
public interface IPartnerStore
{
    Task<IReadOnlyList<Partner>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Partner>> GetPublishedAsync(CancellationToken ct = default);
    Task<Partner?> GetByIdAsync(int id, CancellationToken ct = default);
    Task SaveAsync(Partner partner, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
