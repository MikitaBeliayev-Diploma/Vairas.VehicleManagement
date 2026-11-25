using System.Linq.Expressions;
using Vairas.VehicleManagement.Domain;

namespace Vairas.VehicleManagement.Business;

/// <summary>Create an inactive vehicle with an explicit nonempty category set.</summary>
public sealed record CreateVehicleRequest(Guid BranchId, string RegistrationCountryCode, string RegistrationNumber, string Manufacturer, string Model, short ManufactureYear, Transmission Transmission, string[] Categories);
