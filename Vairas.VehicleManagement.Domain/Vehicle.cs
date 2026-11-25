namespace Vairas.VehicleManagement.Domain;
public sealed class Vehicle
{
    public Guid VehicleId { get; set; }
    public Guid SchoolId { get; set; }
    public Guid BranchId { get; set; }
    public string RegistrationCountryCode { get; set; } = "";
    public string RegistrationNumber { get; set; } = "";
    public string Manufacturer { get; set; } = "";
    public string Model { get; set; } = "";
    public short ManufactureYear { get; set; }
    public Transmission Transmission { get; set; }
    public VehicleStatus Status { get; set; } = VehicleStatus.Inactive;
    public long Revision { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
