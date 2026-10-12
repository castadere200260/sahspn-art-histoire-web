using Sahspn.ArtHistoire.Models;

namespace Sahspn.ArtHistoire.Storage;

/// <summary>Provider-neutral persistence contract for the public website.</summary>
public interface ISiteContentStore
{
    Task<IReadOnlyList<Bulletin>> GetLatestBulletinsAsync(int count, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Bulletin> Items, long Total)> SearchBulletinsAsync(string? query, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<Bulletin?> GetBulletinAsync(int number, CancellationToken cancellationToken = default);
    Task<SitePage?> GetPageAsync(string slug, CancellationToken cancellationToken = default);
    Task AddContactMessageAsync(ContactMessage message, CancellationToken cancellationToken = default);
    Task<AdminUser?> FindAdminUserAsync(string username, CancellationToken cancellationToken = default);
    Task CreateAdminUserAsync(AdminUser user, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GalleryImage>> GetGalleryImagesAsync(bool publishedOnly = false, CancellationToken cancellationToken = default);
    Task<GalleryImage?> GetGalleryImageAsync(string id, CancellationToken cancellationToken = default);
    Task SaveGalleryImageAsync(GalleryImage image, CancellationToken cancellationToken = default);
    Task DeleteGalleryImageAsync(string id, CancellationToken cancellationToken = default);
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
