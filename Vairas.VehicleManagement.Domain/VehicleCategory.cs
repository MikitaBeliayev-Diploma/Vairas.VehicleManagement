namespace Vairas.VehicleManagement.Domain;
public sealed class VehicleCategory
{
    public Guid VehicleId { get; set; }
    public Guid SchoolId { get; set; }
    public string CategoryCode { get; set; } = "";
}
