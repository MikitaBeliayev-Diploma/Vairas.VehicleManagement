using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql.NameTranslation;
using Vairas.VehicleManagement.Domain;

namespace Vairas.VehicleManagement.Persistance;
public static class VehicleModel
{
    public static void Configure(ModelBuilder b)
    {
        b.Entity<Vehicle>(e =>
        {
            e.ToTable("vehicle");
            e.HasKey(x => x.VehicleId);
            e.HasAlternateKey(x => new { x.VehicleId, x.SchoolId });
            e.HasIndex(x => new { x.SchoolId, x.RegistrationCountryCode, x.RegistrationNumber }).IsUnique();
            e.HasIndex(x => new { x.SchoolId, x.BranchId, x.Status });
            e.Property(x => x.RegistrationCountryCode).HasMaxLength(2);
            e.Property(x => x.RegistrationNumber).HasMaxLength(32);
            e.Property(x => x.Manufacturer).HasMaxLength(100);
            e.Property(x => x.Model).HasMaxLength(100);
            e.Property(x => x.Revision).IsConcurrencyToken().HasDefaultValue(1L);
            e.Property(x => x.Status).HasDefaultValue(VehicleStatus.Inactive);
        });
        b.Entity<VehicleCategory>(e =>
        {
            e.ToTable("vehicle_supported_category");
            e.HasKey(x => new { x.VehicleId, x.SchoolId, x.CategoryCode });
            e.Property(x => x.CategoryCode).HasMaxLength(16);
            e.HasOne<Vehicle>().WithMany().HasForeignKey(x => new { x.VehicleId, x.SchoolId }).HasPrincipalKey(x => new { x.VehicleId, x.SchoolId }).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<TeacherAssignment>(e =>
        {
            e.ToTable("vehicle_teacher_assignment");
            e.HasKey(x => new { x.VehicleId, x.SchoolId, x.TeacherId });
            e.HasIndex(x => new { x.SchoolId, x.TeacherId });
            e.HasIndex(x => new { x.SchoolId, x.SchoolMembershipId });
            e.HasOne<Vehicle>().WithMany().HasForeignKey(x => new { x.VehicleId, x.SchoolId }).HasPrincipalKey(x => new { x.VehicleId, x.SchoolId }).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<Maintenance>(e =>
        {
            e.ToTable("maintenance");
            e.HasKey(x => x.MaintenanceId);
            e.HasAlternateKey(x => new { x.MaintenanceId, x.VehicleId, x.SchoolId });
            e.HasIndex(x => new { x.SchoolId, x.VehicleId, x.Status });
            e.HasIndex(x => new { x.VehicleId, x.SchoolId }).IsUnique().HasFilter("status = 'InProgress'::vm_maintenance_status");
            e.Property(x => x.Revision).IsConcurrencyToken().HasDefaultValue(1L);
            e.Property(x => x.Status).HasDefaultValue(MaintenanceStatus.Scheduled);
            e.HasOne<Vehicle>().WithMany().HasForeignKey(x => new { x.VehicleId, x.SchoolId }).HasPrincipalKey(x => new { x.VehicleId, x.SchoolId }).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<VehicleOperation>(e =>
        {
            e.ToTable("vehicle_operation");
            e.HasKey(x => x.OperationId);
            e.Property(x => x.CommandName).HasMaxLength(80);
            e.Property(x => x.RequestFingerprint).HasMaxLength(64);
            e.Property(x => x.ActorService).HasMaxLength(120);
            e.HasIndex(x => new { x.SchoolId, x.VehicleId, x.RecordedAt });
            e.HasOne<Vehicle>().WithMany().HasForeignKey(x => new { x.VehicleId, x.SchoolId }).HasPrincipalKey(x => new { x.VehicleId, x.SchoolId }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Maintenance>().WithMany().HasForeignKey(x => new { x.MaintenanceId, x.VehicleId, x.SchoolId }).HasPrincipalKey(x => new { x.MaintenanceId, x.VehicleId, x.SchoolId }).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<CommandRequest>(e =>
        {
            e.ToTable("command_request");
            e.HasKey(x => x.RequestId);
            e.HasIndex(x => new { x.SchoolId, x.ActorUserId, x.CommandName, x.IdempotencyKey }).IsUnique();
            e.Property(x => x.IdempotencyKey).HasMaxLength(128);
            e.Property(x => x.CommandName).HasMaxLength(80);
            e.Property(x => x.Fingerprint).HasMaxLength(64);
            e.HasOne<Vehicle>().WithMany().HasForeignKey(x => new { x.VehicleId, x.SchoolId }).HasPrincipalKey(x => new { x.VehicleId, x.SchoolId }).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<StateChangeOperation>(e =>
        {
            e.ToTable("vehicle_state_change_operation");
            e.HasKey(x => x.OperationId);
            e.Property(x => x.OperationType).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.ResumeStatus).HasConversion<string>().HasMaxLength(32);
            e.Property(x => x.Fingerprint).HasMaxLength(64);
            e.Property(x => x.Reason).HasMaxLength(1000);
            e.Property(x => x.CorrelationId).HasMaxLength(128);
            e.Property(x => x.ErrorCode).HasMaxLength(120);
            e.HasIndex(x => new { x.SchoolId, x.VehicleId }).IsUnique().HasFilter("status NOT IN ('Completed','Rejected')");
            e.HasIndex(x => new { x.NextAttemptAt, x.LeaseUntil }).HasFilter("status NOT IN ('Completed','Rejected')");
            e.HasOne<Vehicle>().WithMany().HasForeignKey(x => new { x.VehicleId, x.SchoolId }).HasPrincipalKey(x => new { x.VehicleId, x.SchoolId }).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<OutboxMessage>(e =>
        {
            e.ToTable("outbox_message");
            e.HasKey(x => x.MessageId);
            e.Property(x => x.EventType).HasMaxLength(80);
            e.Property(x => x.CorrelationId).HasMaxLength(128);
            e.HasIndex(x => new { x.NextAttemptAt, x.LeaseUntil }).HasFilter("published_at IS NULL");
            e.HasOne<Vehicle>().WithMany().HasForeignKey(x => new { x.VehicleId, x.SchoolId }).HasPrincipalKey(x => new { x.VehicleId, x.SchoolId }).OnDelete(DeleteBehavior.Restrict);
        });
        foreach (var entity in b.Model.GetEntityTypes())
            foreach (var property in entity.GetProperties())
                property.SetColumnName(Regex.Replace(property.Name, "([a-z0-9])([A-Z])", "$1_$2").ToLowerInvariant());
    }
}
