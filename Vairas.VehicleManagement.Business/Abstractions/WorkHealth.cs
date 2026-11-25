using System.Linq.Expressions;
using Vairas.VehicleManagement.Domain;

namespace Vairas.VehicleManagement.Business;

public sealed record WorkHealth(long PendingSagas, long RecoverySagas, double OldestSagaSeconds, long PendingOutbox, double OldestOutboxSeconds);
