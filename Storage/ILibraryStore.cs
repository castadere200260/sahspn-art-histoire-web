using Sahspn.ArtHistoire.Models;
namespace Sahspn.ArtHistoire.Storage;

/// <summary>Persistence contract for the searchable public library.</summary>
public interface ILibraryStore
{
    Task<IReadOnlyList<LibraryItem>> GetAllAsync(CancellationToken ct = default);
    Task<(IReadOnlyList<LibraryItem> Items, long Total)> SearchPublishedAsync(string? query, int page, int pageSize, CancellationToken ct = default);
    Task<LibraryItem?> GetByIdAsync(int id, CancellationToken ct = default);
    Task SaveAsync(LibraryItem item, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
