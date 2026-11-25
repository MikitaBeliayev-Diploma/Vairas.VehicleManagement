using System.Linq.Expressions;
using Vairas.VehicleManagement.Domain;

namespace Vairas.VehicleManagement.Business;

public interface ICategoryCatalogue
{
    string Version { get; }

    bool Contains(string code);
}
