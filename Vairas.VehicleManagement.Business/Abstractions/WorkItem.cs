using System.Linq.Expressions;
using Vairas.VehicleManagement.Domain;

namespace Vairas.VehicleManagement.Business;

public sealed record WorkItem(Guid SchoolId, Guid ItemId);
