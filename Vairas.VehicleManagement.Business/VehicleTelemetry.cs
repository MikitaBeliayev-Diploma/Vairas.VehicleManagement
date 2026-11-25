using System.Diagnostics.Metrics;
using System.Diagnostics;
using System.Text.Json;
using Vairas.VehicleManagement.Domain;

namespace Vairas.VehicleManagement.Business;
public static class VehicleTelemetry
{
    public const string Name = "Vairas.VehicleManagement";
    private static readonly Meter Meter = new(Name);
    public static readonly Counter<long> SagaSteps = Meter.CreateCounter<long>("vehicle.saga.steps");
    public static readonly Counter<long> RecoveryAttempts = Meter.CreateCounter<long>("vehicle.saga.recovery");
    public static readonly Counter<long> EventsPublished = Meter.CreateCounter<long>("vehicle.outbox.published");
    private static WorkHealth health = new(0, 0, 0, 0, 0);
    static VehicleTelemetry()
    {
        Meter.CreateObservableGauge("vehicle.saga.pending", () => Volatile.Read(ref health).PendingSagas);
        Meter.CreateObservableGauge("vehicle.saga.needs_recovery", () => Volatile.Read(ref health).RecoverySagas);
        Meter.CreateObservableGauge("vehicle.saga.oldest_seconds", () => Volatile.Read(ref health).OldestSagaSeconds);
        Meter.CreateObservableGauge("vehicle.outbox.pending", () => Volatile.Read(ref health).PendingOutbox);
        Meter.CreateObservableGauge("vehicle.outbox.oldest_seconds", () => Volatile.Read(ref health).OldestOutboxSeconds);
    }

    public static void Update(WorkHealth value) => Volatile.Write(ref health, value);
}
