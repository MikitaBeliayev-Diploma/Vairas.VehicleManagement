using System.Text.Json;
using Vairas.VehicleManagement.Domain;

namespace Vairas.VehicleManagement.Business;
public sealed partial class VehicleService
{
    public async Task<MutationResponse> CreateAsync(Guid school, string key, CreateVehicleRequest request, VehicleActor actor, CancellationToken ct)
    {
        key = VehicleRules.Key(key);
        VehicleRules.Id(request.BranchId);
        var categories = VehicleRules.Categories(request.Categories, catalogue);
        VehicleRules.Details(request.ManufactureYear, request.Transmission, clock.GetUtcNow());
        request = request with
        {
            RegistrationCountryCode = VehicleRules.Country(request.RegistrationCountryCode),
            RegistrationNumber = VehicleRules.Text(request.RegistrationNumber, 32).ToUpperInvariant(),
            Manufacturer = VehicleRules.Text(request.Manufacturer, 100),
            Model = VehicleRules.Text(request.Model, 100),
            Categories = categories
        };
        var fingerprint = VehicleRules.Fingerprint("CreateVehicle", school, null, request);
        actor = await Authorize(school, request.BranchId, actor, ct);
        await using var s = await store.OpenAsync(school, true, ct);
        var prior = await Replay(s, school, actor.UserId, key, "CreateVehicle", fingerprint, ct);
        if (prior is not null)
            return Saved(prior);
        var now = await s.NowAsync(ct);
        var v = new Vehicle
        {
            VehicleId = Guid.NewGuid(),
            SchoolId = school,
            BranchId = request.BranchId,
            RegistrationCountryCode = request.RegistrationCountryCode,
            RegistrationNumber = request.RegistrationNumber,
            Manufacturer = request.Manufacturer,
            Model = request.Model,
            ManufactureYear = request.ManufactureYear,
            Transmission = request.Transmission,
            CreatedAt = now,
            UpdatedAt = now
        };
        s.Add(v);
        foreach (var code in categories)
            s.Add(new VehicleCategory { SchoolId = school, VehicleId = v.VehicleId, CategoryCode = code });
        return await Complete(s, v, actor, key, "CreateVehicle", fingerprint, null, ct);
    }

    public async Task<MutationResponse> UpdateAsync(Guid school, Guid vehicle, string key, UpdateVehicleRequest request, VehicleActor actor, CancellationToken ct)
    {
        key = VehicleRules.Key(key);
        VehicleRules.Id(vehicle);
        VehicleRules.Revision(request.ExpectedRevision);
        VehicleRules.Details(request.ManufactureYear, request.Transmission, clock.GetUtcNow());
        request = request with
        {
            RegistrationCountryCode = VehicleRules.Country(request.RegistrationCountryCode),
            RegistrationNumber = VehicleRules.Text(request.RegistrationNumber, 32).ToUpperInvariant(),
            Manufacturer = VehicleRules.Text(request.Manufacturer, 100),
            Model = VehicleRules.Text(request.Model, 100)
        };
        var fingerprint = VehicleRules.Fingerprint("UpdateVehicle", school, vehicle, request);
        actor = await Authorize(school, null, actor, ct);
        await using var s = await store.OpenAsync(school, true, ct);
        var v = await Find(s, school, vehicle, ct);
        var prior = await Replay(s, school, actor.UserId, key, "UpdateVehicle", fingerprint, ct);
        if (prior is not null)
            return Saved(prior);
        await EnsureNoSaga(s, v, ct);
        VehicleRules.Match(request.ExpectedRevision, v.Revision);
        v.RegistrationCountryCode = request.RegistrationCountryCode;
        v.RegistrationNumber = request.RegistrationNumber;
        v.Manufacturer = request.Manufacturer;
        v.Model = request.Model;
        v.ManufactureYear = request.ManufactureYear;
        v.Transmission = request.Transmission;
        v.Revision++;
        v.UpdatedAt = await s.NowAsync(ct);
        return await Complete(s, v, actor, key, "UpdateVehicle", fingerprint, request.ExpectedRevision, ct);
    }
}
