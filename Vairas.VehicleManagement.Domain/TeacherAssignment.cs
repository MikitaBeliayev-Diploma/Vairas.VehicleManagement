namespace Vairas.VehicleManagement.Domain;
public sealed class TeacherAssignment
{
    public Guid VehicleId { get; set; }
    public Guid SchoolId { get; set; }
    public Guid TeacherId { get; set; }
    public Guid SchoolMembershipId { get; set; }
    public DateTimeOffset AssignedAt { get; set; }
}
