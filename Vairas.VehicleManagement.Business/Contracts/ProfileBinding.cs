using System.Linq.Expressions;
using Vairas.VehicleManagement.Domain;

namespace Vairas.VehicleManagement.Business;

public sealed record ProfileBinding(string Kind, Guid ProfileId, Guid UserId, bool Active);
