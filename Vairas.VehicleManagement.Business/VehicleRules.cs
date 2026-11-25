using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Vairas.VehicleManagement.Domain;

namespace Vairas.VehicleManagement.Business;

public static class VehicleRules
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { Converters = { new JsonStringEnumConverter(allowIntegerValues: false) } };
    public static void Id(Guid id) { if (id == Guid.Empty) throw VehicleErrors.Invalid(); }
    public static void Revision(long value) { if (value < 1) throw VehicleErrors.Invalid(); }
    public static void Match(long expected, long actual) { Revision(expected); if (expected != actual) throw VehicleErrors.Conflict(); }
    public static string Text(string? value, int max)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text) || text.Length > max || text.Any(char.IsControl)) throw VehicleErrors.Invalid();
        return text;
    }
    public static string Country(string value)
    {
        var code = Text(value, 2).ToUpperInvariant();
        if (code.Length != 2 || code.Any(c => c is < 'A' or > 'Z')) throw VehicleErrors.Invalid();
        return code;
    }
    public static string Key(string value)
    {
        if (value is null || value.Length is < 8 or > 128 || value.Any(c => c < 33 || c > 126)) throw VehicleErrors.Invalid();
        return value;
    }
    public static void Fresh(VehicleActor actor, DateTimeOffset now)
    {
        Id(actor.UserId);
        if (actor.ValidUntil <= now) throw new UnauthorizedException("SESSION_INVALID");
    }
    public static DateTimeOffset Authority(VehicleAuthorityRequest request, VehicleAuthorityResponse response, DateTimeOffset now)
    {
        if (response.SchoolId != request.SchoolId || response.BranchId != request.BranchId || response.ActorUserId != request.ActorUserId ||
            response.TeacherId != request.TeacherId || response.ValidUntil <= now || response.SchoolRevision < 1 ||
            request.BranchId.HasValue && response.BranchRevision is not > 0 ||
            request.SchoolMembershipId.HasValue && response.Allowed && response.SchoolMembershipId != request.SchoolMembershipId)
            throw VehicleErrors.Dependency();
        if (!response.Allowed) throw VehicleErrors.Denied();
        if (request.TeacherId.HasValue && (response.SchoolMembershipId is null || response.SchoolMembershipId == Guid.Empty || response.TeacherUserId is null || response.TeacherUserId == Guid.Empty || response.MembershipRevision is not > 0))
            throw VehicleErrors.Dependency();
        return response.ValidUntil;
    }
    public static string[] Categories(string[]? values, ICategoryCatalogue catalogue)
    {
        if (values is null || values.Length is < 1 or > 32) throw VehicleErrors.Invalid();
        var codes = values.Select(x => Text(x, 16).ToUpperInvariant()).Order(StringComparer.Ordinal).ToArray();
        if (codes.Distinct(StringComparer.Ordinal).Count() != codes.Length) throw VehicleErrors.Invalid();
        if (codes.Any(x => !catalogue.Contains(x))) throw new UnprocessableEntityException("CATEGORY_NOT_SUPPORTED");
        return codes;
    }
    public static void Details(short year, Transmission transmission, DateTimeOffset now)
    {
        if (year < 1886 || year > now.Year + 1 || !Enum.IsDefined(transmission)) throw VehicleErrors.Invalid();
    }
    public static string Fingerprint(string command, Guid school, Guid? vehicle, object request) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { version = 1, command, school, vehicle, request }, Json))));
    public static bool Terminal(SagaStatus status) => status is SagaStatus.Completed or SagaStatus.Rejected;
    public static StateChangeResponse Response(StateChangeOperation row) => new(row.OperationId, row.VehicleId, row.OperationType, row.Status, row.BlockId, row.ResultVehicleStatus, row.ErrorCode, row.CreatedAt, row.UpdatedAt);
}
