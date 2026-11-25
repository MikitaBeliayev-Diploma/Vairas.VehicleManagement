using System.Linq.Expressions;
using Vairas.VehicleManagement.Domain;

namespace Vairas.VehicleManagement.Business;
public interface IVehicleService
{
    Task<SchedulingVehicleSnapshot> SchedulingEligibilityAsync(Guid school,Guid vehicle,Guid branch,Guid teacher,string category,CancellationToken ct);
    Task<VehicleOwnershipSnapshot> OwnershipAsync(Guid school,Guid vehicle,CancellationToken ct);
    Task<VehicleListResponse> ListAsync(Guid school, Guid? after, int limit, VehicleActor actor, CancellationToken ct);
    Task<VehicleResponse> GetAsync(Guid school, Guid vehicle, VehicleActor actor, CancellationToken ct);
    Task<MutationResponse> CreateAsync(Guid school, string key, CreateVehicleRequest request, VehicleActor actor, CancellationToken ct);
    Task<MutationResponse> UpdateAsync(Guid school, Guid vehicle, string key, UpdateVehicleRequest request, VehicleActor actor, CancellationToken ct);
    Task<MutationResponse> CategoriesAsync(Guid school, Guid vehicle, string key, ReplaceCategoriesRequest request, VehicleActor actor, CancellationToken ct);
    Task<MutationResponse> TeachersAsync(Guid school, Guid vehicle, string key, ReplaceTeacherAssignmentsRequest request, VehicleActor actor, CancellationToken ct);
    Task<StateChangeResponse> RequestStateAsync(Guid school, Guid vehicle, string key, StateChangeRequest request, VehicleActor actor, string correlationId, CancellationToken ct);
    Task<StateChangeResponse> GetStateAsync(Guid school, Guid vehicle, Guid operation, VehicleActor actor, CancellationToken ct);
    Task<VehicleEligibilityResponse> EligibilityAsync(Guid school, Guid vehicle, Guid branch, Guid teacher, string category, CancellationToken ct);
}
