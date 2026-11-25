using System.Linq.Expressions;
using Vairas.VehicleManagement.Domain;

namespace Vairas.VehicleManagement.Business;

/// <summary>Replace supported categories while the vehicle is safely outside operation.</summary>
public sealed record ReplaceCategoriesRequest(long ExpectedRevision, string[] Categories);
