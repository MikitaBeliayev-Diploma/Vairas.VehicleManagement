using System.Linq.Expressions;
using Vairas.VehicleManagement.Domain;

namespace Vairas.VehicleManagement.Business;

/// <summary>Authorized vehicle representation; no scheduling reservations are owned here.</summary>
public sealed record VehicleResponse(Guid VehicleId, Guid SchoolId, Guid BranchId, string RegistrationCountryCode, string RegistrationNumber, string Manufacturer, string Model, short ManufactureYear, Transmission Transmission, VehicleStatus Status, long Revision, string[] Categories, TeacherAssignmentResponse[] TeacherAssignments);
