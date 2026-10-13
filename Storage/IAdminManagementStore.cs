using Sahspn.ArtHistoire.Models;
namespace Sahspn.ArtHistoire.Storage;
public interface IAdminManagementStore
{
    Task<IReadOnlyList<Bulletin>> GetBulletinsAsync(CancellationToken ct);
    Task SaveBulletinAsync(Bulletin item, int originalNumber, CancellationToken ct);
    Task DeleteBulletinAsync(int number, CancellationToken ct);
    Task<IReadOnlyList<Member>> GetMembersAsync(CancellationToken ct);
    Task SaveMemberAsync(Member item, CancellationToken ct);
    Task DeleteMemberAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<Appointment>> GetAppointmentsAsync(CancellationToken ct);
    Task SaveAppointmentAsync(Appointment item, CancellationToken ct);
    Task DeleteAppointmentAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<AdminUser>> GetAdminUsersAsync(CancellationToken ct);
    Task SaveAdminUserAsync(AdminUser item, CancellationToken ct);
    Task DeleteAdminUserAsync(int id, CancellationToken ct);
}
