using System.Linq.Expressions;
using Vairas.VehicleManagement.Domain;

namespace Vairas.VehicleManagement.Business;

public interface ISchoolAuthority
{
    Task<VehicleAuthorityResponse> ValidateAsync(VehicleAuthorityRequest request, CancellationToken ct);
}
