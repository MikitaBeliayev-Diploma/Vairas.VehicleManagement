using System.Linq.Expressions;
using Vairas.VehicleManagement.Domain;

namespace Vairas.VehicleManagement.Business;

public interface IEventPublisher
{
    Task PublishAsync(OutboxMessage message, CancellationToken ct);
}
