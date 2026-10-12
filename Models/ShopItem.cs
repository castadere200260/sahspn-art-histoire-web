using System.ComponentModel.DataAnnotations;

namespace Sahspn.ArtHistoire.Models;

/// <summary>A public catalogue entry. The catalogue deliberately has no order or payment workflow.</summary>
public sealed class ShopItem
{
    public int Id { get; set; }

    [Required, StringLength(180)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public ShopItemType Type { get; set; } = ShopItemType.Bulletin;

    [StringLength(2000)]
    public string? Description { get; set; }

    [Range(typeof(decimal), "0", "999.99", ParseLimitsInInvariantCulture = true)]
    public decimal Price { get; set; }

    [StringLength(500)]
    public string? ImageUrl { get; set; }

    [StringLength(500)]
    public string? DocumentUrl { get; set; }

    public bool IsPublished { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public enum ShopItemType
{
    Bulletin = 0,
    Document = 1
}
