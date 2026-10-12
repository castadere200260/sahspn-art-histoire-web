using Microsoft.EntityFrameworkCore;
using Sahspn.ArtHistoire.Data;
using Sahspn.ArtHistoire.Models;

namespace Sahspn.ArtHistoire.Storage;

public sealed class MySqlPartnerStore(SahspDbContext db) : IPartnerStore
{
    public async Task<IReadOnlyList<Partner>> GetAllAsync(CancellationToken ct = default) =>
        await db.Partners.AsNoTracking().OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name).ToListAsync(ct);

    public async Task<IReadOnlyList<Partner>> GetPublishedAsync(CancellationToken ct = default) =>
        await db.Partners.AsNoTracking().Where(x => x.IsPublished).OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name).ToListAsync(ct);

    public Task<Partner?> GetByIdAsync(int id, CancellationToken ct = default) =>
        db.Partners.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task SaveAsync(Partner partner, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        if (partner.Id <= 0)
        {
            partner.Name = partner.Name.Trim();
            partner.CreatedAtUtc = now;
            partner.UpdatedAtUtc = now;
            db.Partners.Add(partner);
        }
        else
        {
            var existing = await db.Partners.FirstOrDefaultAsync(x => x.Id == partner.Id, ct)
                ?? throw new InvalidOperationException($"Partenaire {partner.Id} introuvable.");
            existing.Name = partner.Name.Trim();
            existing.Description = partner.Description;
            existing.WebsiteUrl = partner.WebsiteUrl;
            existing.LogoUrl = partner.LogoUrl;
            existing.IsPublished = partner.IsPublished;
            existing.DisplayOrder = partner.DisplayOrder;
            existing.UpdatedAtUtc = now;
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var existing = await db.Partners.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException($"Partenaire {id} introuvable.");
        db.Partners.Remove(existing);
        await db.SaveChangesAsync(ct);
    }
}
