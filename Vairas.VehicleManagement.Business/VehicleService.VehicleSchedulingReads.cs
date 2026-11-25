namespace Vairas.VehicleManagement.Business;

public sealed partial class VehicleService
{
    public async Task<SchedulingVehicleSnapshot> SchedulingEligibilityAsync(Guid school,Guid vehicle,Guid branch,Guid teacher,string category,CancellationToken ct)
    {
        var result=await EligibilityCore(school,vehicle,branch,teacher,category,ct);
        return new(vehicle,school,teacher,branch,category.ToUpperInvariant(),result.Eligibility.VehicleVersion,result.Eligibility.Eligible,gate.Enabled,
            result.ValidUntil<clock.GetUtcNow().AddSeconds(15)?result.ValidUntil:clock.GetUtcNow().AddSeconds(15));
    }
    public async Task<VehicleOwnershipSnapshot> OwnershipAsync(Guid school,Guid vehicle,CancellationToken ct)
    {
        VehicleRules.Id(school);VehicleRules.Id(vehicle);
        await using var session=await store.OpenAsync(school,false,ct);var row=await Find(session,school,vehicle,ct);
        return new(vehicle,school,row.Revision,gate.Enabled,(await session.NowAsync(ct)).AddSeconds(15));
    }
}
