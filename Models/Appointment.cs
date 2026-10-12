using System.ComponentModel.DataAnnotations;
namespace Sahspn.ArtHistoire.Models;
public sealed class Appointment
{
    public int Id { get; set; }
    [Required, StringLength(180)] public string Title { get; set; } = "";
    [StringLength(2000)] public string? Description { get; set; }
    [Required] public DateTime StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    [StringLength(180)] public string? Location { get; set; }
}
