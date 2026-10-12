using System.ComponentModel.DataAnnotations;
namespace Sahspn.ArtHistoire.Models;
public sealed class Member
{
    public int Id { get; set; }
    [Required, StringLength(120)] public string FullName { get; set; } = "";
    [EmailAddress, StringLength(180)] public string Email { get; set; } = "";
    [StringLength(30)] public string? Phone { get; set; }
    public DateTime? JoinedAt { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsBoardMember { get; set; }
    [StringLength(120)] public string? BoardPosition { get; set; }
    public DateTime? ElectionDate { get; set; }
}
