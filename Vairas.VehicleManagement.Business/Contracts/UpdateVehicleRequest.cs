using System.Linq.Expressions;
using Vairas.VehicleManagement.Domain;

namespace Vairas.VehicleManagement.Business;

/// <summary>Update descriptive fields; school, branch and operational status cannot be patched.</summary>
public sealed record UpdateVehicleRequest(long ExpectedRevision, string RegistrationCountryCode, string RegistrationNumber, string Manufacturer, string Model, short ManufactureYear, Transmission Transmission);
