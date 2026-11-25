using System.Linq.Expressions;
using System.Text.Json;
using Vairas.VehicleManagement.Business;
using Vairas.VehicleManagement.Domain;

namespace Vairas.VehicleManagement.Tests;

public sealed class TestCatalogue : ICategoryCatalogue
{
    public string Version => "synthetic-unit-v1";
    public bool Contains(string code) => code is "B" or "BE";
}
