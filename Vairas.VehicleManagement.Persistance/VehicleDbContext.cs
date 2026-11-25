using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql.NameTranslation;
using Vairas.VehicleManagement.Domain;

namespace Vairas.VehicleManagement.Persistance;
public sealed class VehicleDbContext(DbContextOptions<VehicleDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder b) => VehicleModel.Configure(b);
}
