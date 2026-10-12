using Microsoft.EntityFrameworkCore;
using Sahspn.ArtHistoire.Models;
namespace Sahspn.ArtHistoire.Data;
public sealed class SahspDbContext(DbContextOptions<SahspDbContext> options) : DbContext(options)
{
    public DbSet<SitePage> Pages => Set<SitePage>();
    public DbSet<Bulletin> Bulletins => Set<Bulletin>();
    public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<Member> Members => Set<Member>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<ShopItem> ShopItems => Set<ShopItem>();
    public DbSet<Partner> Partners => Set<Partner>();
    public DbSet<DocumentStudy> DocumentStudies => Set<DocumentStudy>();
    public DbSet<LibraryItem> LibraryItems => Set<LibraryItem>();
    public DbSet<GalleryImage> GalleryImages => Set<GalleryImage>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AdminUser>().HasIndex(x => x.Username).IsUnique();
        modelBuilder.Entity<Member>().HasIndex(x => x.Email);
        modelBuilder.Entity<Member>().HasIndex(x => new { x.IsBoardMember, x.IsActive, x.BoardPosition });
        modelBuilder.Entity<Appointment>().HasIndex(x => x.StartsAt);
        modelBuilder.Entity<SitePage>().HasIndex(x => x.Slug).IsUnique();
        modelBuilder.Entity<GalleryImage>().HasKey(x => x.Id);
        modelBuilder.Entity<GalleryImage>().HasIndex(x => new { x.IsPublished, x.DisplayOrder });
        modelBuilder.Entity<Bulletin>().HasIndex(x => x.Number).IsUnique();
        modelBuilder.Entity<ShopItem>().Property(x => x.Price).HasPrecision(10, 2);
        modelBuilder.Entity<Partner>().HasIndex(x => new { x.IsPublished, x.DisplayOrder });
        modelBuilder.Entity<DocumentStudy>().Property(x => x.Content).HasColumnType("longtext");
        modelBuilder.Entity<LibraryItem>().Property(x => x.Content).HasColumnType("longtext");
        modelBuilder.Entity<LibraryItem>().Property(x => x.Summary).HasColumnType("longtext");
        modelBuilder.Entity<LibraryItem>().HasIndex(x => new { x.IsPublished, x.Title });
        modelBuilder.Entity<DocumentStudy>().Property(x => x.Summary).HasColumnType("longtext");
        modelBuilder.Entity<DocumentStudy>().HasIndex(x => new { x.IsPublished, x.Type, x.PublishedAt });
        modelBuilder.Entity<Partner>().Property(x => x.Description).HasColumnType("longtext");
        modelBuilder.Entity<ShopItem>().HasIndex(x => new { x.IsPublished, x.Type });
        modelBuilder.Entity<ShopItem>().Property(x => x.Description).HasColumnType("longtext");
        modelBuilder.Entity<SitePage>().Property(x => x.Content).HasColumnType("longtext");
        modelBuilder.Entity<Bulletin>().Property(x => x.Content).HasColumnType("longtext");
    }
}
