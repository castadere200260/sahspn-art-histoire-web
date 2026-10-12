using Sahspn.ArtHistoire.Models;
namespace Sahspn.ArtHistoire.Storage;

/// <summary>Persistence contract for searchable documents and studies.</summary>
public interface IDocumentStudyStore
{
    Task<IReadOnlyList<DocumentStudy>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<DocumentStudy>> GetPublishedAsync(CancellationToken ct = default);
    Task<(IReadOnlyList<DocumentStudy> Items, long Total)> SearchPublishedAsync(string? query, int page, int pageSize, CancellationToken ct = default);
    Task<DocumentStudy?> GetByIdAsync(int id, CancellationToken ct = default);
    Task SaveAsync(DocumentStudy item, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
