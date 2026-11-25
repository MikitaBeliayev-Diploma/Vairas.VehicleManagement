using System.Linq.Expressions;
using Vairas.VehicleManagement.Domain;

namespace Vairas.VehicleManagement.Business;

public interface IUserProfiles
{
    Task RequireActiveAsync(Guid userId, CancellationToken ct);
    Task<ProfileBinding> TeacherAsync(Guid teacherId, CancellationToken ct);
}
