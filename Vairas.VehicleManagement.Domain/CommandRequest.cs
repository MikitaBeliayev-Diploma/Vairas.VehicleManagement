namespace Vairas.VehicleManagement.Domain;
public sealed class CommandRequest
{
    public Guid RequestId { get; set; }
    public Guid SchoolId { get; set; }
    public Guid ActorUserId { get; set; }
    public string IdempotencyKey { get; set; } = "";
    public string CommandName { get; set; } = "";
    public string Fingerprint { get; set; } = "";
    public Guid VehicleId { get; set; }
    public string? ResponseJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
