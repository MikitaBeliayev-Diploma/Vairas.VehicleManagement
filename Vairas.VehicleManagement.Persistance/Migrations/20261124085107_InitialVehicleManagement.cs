using Microsoft.EntityFrameworkCore.Migrations;
using Vairas.VehicleManagement.Domain;

namespace Vairas.VehicleManagement.Persistance.Migrations;
public partial class InitialVehicleManagement : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Preserve the original PostgreSQL enum ordering, including nonalphabetical labels.
        migrationBuilder.AlterDatabase()
            .Annotation("Npgsql:Enum:vm_vehicle_status", "Active,Inactive,UnderMaintenance")
            .Annotation("Npgsql:Enum:vm_transmission", "Manual,Automatic")
            .Annotation("Npgsql:Enum:vm_maintenance_type", "Service,Repair,Inspection")
            .Annotation("Npgsql:Enum:vm_maintenance_status", "Scheduled,InProgress,Completed,Cancelled")
            .Annotation("Npgsql:Enum:vm_channel", "Web,MCP,Internal");

        migrationBuilder.CreateTable(
            name: "vehicle",
            columns: table => new
            {
                vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                school_id = table.Column<Guid>(type: "uuid", nullable: false),
                branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                registration_country_code = table.Column<string>(type: "varchar(2)", maxLength: 2, nullable: false),
                registration_number = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                manufacturer = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                model = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                manufacture_year = table.Column<short>(type: "smallint", nullable: false),
                transmission = table.Column<Transmission>(type: "vm_transmission", nullable: false),
                status = table.Column<VehicleStatus>(type: "vm_vehicle_status", nullable: false, defaultValue: VehicleStatus.Inactive),
                revision = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("vehicle_pkey", x => x.vehicle_id);
                table.CheckConstraint("vm_country_shape", "registration_country_code ~ '^[A-Z]{2}$'");
                table.CheckConstraint("vm_registration_shape", "length(trim(registration_number)) > 0 AND registration_number = upper(trim(registration_number))");
                table.CheckConstraint("vm_details_nonblank", "length(trim(manufacturer)) > 0 AND length(trim(model)) > 0");
                table.CheckConstraint("vm_year_storage_range", "manufacture_year BETWEEN 1886 AND 9999");
                table.CheckConstraint("vm_revision_time", "revision > 0 AND updated_at >= created_at");
            });

        migrationBuilder.CreateTable(
            name: "vehicle_supported_category",
            columns: table => new
            {
                vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                school_id = table.Column<Guid>(type: "uuid", nullable: false),
                category_code = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("vehicle_supported_category_pkey", x => new { x.vehicle_id, x.school_id, x.category_code });
                table.CheckConstraint("vm_category_shape", "length(trim(category_code)) > 0 AND category_code = upper(trim(category_code))");
            });

        migrationBuilder.CreateTable(
            name: "vehicle_teacher_assignment",
            columns: table => new
            {
                vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                school_id = table.Column<Guid>(type: "uuid", nullable: false),
                teacher_id = table.Column<Guid>(type: "uuid", nullable: false),
                school_membership_id = table.Column<Guid>(type: "uuid", nullable: false),
                assigned_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
            },
            constraints: table => table.PrimaryKey("vehicle_teacher_assignment_pkey", x => new { x.vehicle_id, x.school_id, x.teacher_id }));

        migrationBuilder.CreateTable(
            name: "maintenance",
            columns: table => new
            {
                maintenance_id = table.Column<Guid>(type: "uuid", nullable: false),
                school_id = table.Column<Guid>(type: "uuid", nullable: false),
                vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                type = table.Column<MaintenanceType>(type: "vm_maintenance_type", nullable: false),
                scheduled_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                started_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                completed_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                cancelled_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                status = table.Column<MaintenanceStatus>(type: "vm_maintenance_status", nullable: false, defaultValue: MaintenanceStatus.Scheduled),
                comment = table.Column<string>(type: "text", nullable: true),
                revision = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("maintenance_pkey", x => x.maintenance_id);
                table.CheckConstraint("vm_maintenance_revision_time", "revision > 0 AND updated_at >= created_at");
                table.CheckConstraint("vm_maintenance_lifecycle_shape", "(status = 'Scheduled' AND started_at IS NULL AND completed_at IS NULL AND cancelled_at IS NULL) OR (status = 'InProgress' AND started_at IS NOT NULL AND completed_at IS NULL AND cancelled_at IS NULL) OR (status = 'Completed' AND started_at IS NOT NULL AND completed_at IS NOT NULL AND cancelled_at IS NULL) OR (status = 'Cancelled' AND started_at IS NULL AND completed_at IS NULL AND cancelled_at IS NOT NULL)");
                table.CheckConstraint("vm_maintenance_time_order", "(started_at IS NULL OR started_at >= created_at) AND (completed_at IS NULL OR completed_at >= started_at) AND (cancelled_at IS NULL OR cancelled_at >= created_at)");
                table.CheckConstraint("vm_maintenance_updated_time", "(started_at IS NULL OR updated_at >= started_at) AND (completed_at IS NULL OR updated_at >= completed_at) AND (cancelled_at IS NULL OR updated_at >= cancelled_at)");
            });

        migrationBuilder.CreateTable(
            name: "vehicle_operation",
            columns: table => new
            {
                operation_id = table.Column<Guid>(type: "uuid", nullable: false),
                school_id = table.Column<Guid>(type: "uuid", nullable: false),
                vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                maintenance_id = table.Column<Guid>(type: "uuid", nullable: true),
                actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                actor_service = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: true),
                channel = table.Column<Channel>(type: "vm_channel", nullable: false),
                command_name = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false),
                request_fingerprint = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                expected_vehicle_revision = table.Column<long>(type: "bigint", nullable: true),
                expected_maintenance_revision = table.Column<long>(type: "bigint", nullable: true),
                resulting_vehicle_revision = table.Column<long>(type: "bigint", nullable: false),
                resulting_maintenance_revision = table.Column<long>(type: "bigint", nullable: true),
                reason = table.Column<string>(type: "text", nullable: true),
                confirmation_id = table.Column<Guid>(type: "uuid", nullable: true),
                confirmed_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                recorded_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("vehicle_operation_pkey", x => x.operation_id);
                table.CheckConstraint("vm_operation_shape", "length(trim(command_name)) > 0 AND request_fingerprint ~ '^[0-9a-f]{64}$'");
                table.CheckConstraint("vm_operation_revisions", "(expected_vehicle_revision IS NULL OR expected_vehicle_revision > 0) AND resulting_vehicle_revision > 0 AND (resulting_maintenance_revision IS NULL OR resulting_maintenance_revision > 0)");
                table.CheckConstraint("vm_operation_actor", "(actor_user_id IS NOT NULL AND actor_service IS NULL) OR (actor_user_id IS NULL AND actor_service IS NOT NULL AND length(trim(actor_service)) > 0 AND channel = 'Internal')");
                table.CheckConstraint("vm_operation_maintenance_shape", "(maintenance_id IS NULL AND expected_maintenance_revision IS NULL AND resulting_maintenance_revision IS NULL) OR (maintenance_id IS NOT NULL AND resulting_maintenance_revision IS NOT NULL AND (expected_maintenance_revision IS NULL OR expected_maintenance_revision > 0))");
                table.CheckConstraint("vm_operation_revision_order", "(expected_vehicle_revision IS NULL OR resulting_vehicle_revision >= expected_vehicle_revision) AND (expected_maintenance_revision IS NULL OR resulting_maintenance_revision > expected_maintenance_revision)");
                table.CheckConstraint("vm_confirmation_pair", "(confirmation_id IS NULL) = (confirmed_at IS NULL)");
                table.CheckConstraint("vm_mcp_confirmation", "channel <> 'MCP' OR confirmation_id IS NOT NULL");
                table.CheckConstraint("vm_confirmation_time", "confirmed_at IS NULL OR confirmed_at <= recorded_at");
            });

        migrationBuilder.CreateIndex(
            name: "vehicle_vehicle_id_school_id_idx", table: "vehicle",
            columns: new[] { "vehicle_id", "school_id" }, unique: true);
        migrationBuilder.CreateIndex(
            name: "vehicle_school_id_registration_country_code_registration_nu_idx", table: "vehicle",
            columns: new[] { "school_id", "registration_country_code", "registration_number" }, unique: true);
        migrationBuilder.CreateIndex(
            name: "vehicle_school_id_branch_id_status_idx", table: "vehicle",
            columns: new[] { "school_id", "branch_id", "status" });
        migrationBuilder.CreateIndex(
            name: "vehicle_teacher_assignment_school_id_teacher_id_idx", table: "vehicle_teacher_assignment",
            columns: new[] { "school_id", "teacher_id" });
        migrationBuilder.CreateIndex(
            name: "vehicle_teacher_assignment_school_id_school_membership_id_idx", table: "vehicle_teacher_assignment",
            columns: new[] { "school_id", "school_membership_id" });
        migrationBuilder.CreateIndex(
            name: "maintenance_maintenance_id_vehicle_id_school_id_idx", table: "maintenance",
            columns: new[] { "maintenance_id", "vehicle_id", "school_id" }, unique: true);
        migrationBuilder.CreateIndex(
            name: "maintenance_school_id_vehicle_id_status_idx", table: "maintenance",
            columns: new[] { "school_id", "vehicle_id", "status" });
        migrationBuilder.CreateIndex(
            name: "vehicle_operation_school_id_vehicle_id_recorded_at_idx", table: "vehicle_operation",
            columns: new[] { "school_id", "vehicle_id", "recorded_at" });

        migrationBuilder.AddForeignKey(
            name: "vehicle_supported_category_vehicle_id_school_id_fkey",
            table: "vehicle_supported_category", columns: new[] { "vehicle_id", "school_id" },
            principalTable: "vehicle", principalColumns: new[] { "vehicle_id", "school_id" },
            onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(
            name: "vehicle_teacher_assignment_vehicle_id_school_id_fkey",
            table: "vehicle_teacher_assignment", columns: new[] { "vehicle_id", "school_id" },
            principalTable: "vehicle", principalColumns: new[] { "vehicle_id", "school_id" },
            onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(
            name: "maintenance_vehicle_id_school_id_fkey",
            table: "maintenance", columns: new[] { "vehicle_id", "school_id" },
            principalTable: "vehicle", principalColumns: new[] { "vehicle_id", "school_id" },
            onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(
            name: "vehicle_operation_vehicle_id_school_id_fkey",
            table: "vehicle_operation", columns: new[] { "vehicle_id", "school_id" },
            principalTable: "vehicle", principalColumns: new[] { "vehicle_id", "school_id" },
            onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(
            name: "vehicle_operation_maintenance_id_vehicle_id_school_id_fkey",
            table: "vehicle_operation", columns: new[] { "maintenance_id", "vehicle_id", "school_id" },
            principalTable: "maintenance", principalColumns: new[] { "maintenance_id", "vehicle_id", "school_id" },
            onDelete: ReferentialAction.Restrict);

        // EF Core has no operation for PostgreSQL FK deferrability. Preserve the original semantics.
        migrationBuilder.Sql("""
            ALTER TABLE vehicle_supported_category ALTER CONSTRAINT vehicle_supported_category_vehicle_id_school_id_fkey DEFERRABLE INITIALLY IMMEDIATE;
            ALTER TABLE vehicle_teacher_assignment ALTER CONSTRAINT vehicle_teacher_assignment_vehicle_id_school_id_fkey DEFERRABLE INITIALLY IMMEDIATE;
            ALTER TABLE maintenance ALTER CONSTRAINT maintenance_vehicle_id_school_id_fkey DEFERRABLE INITIALLY IMMEDIATE;
            ALTER TABLE vehicle_operation ALTER CONSTRAINT vehicle_operation_vehicle_id_school_id_fkey DEFERRABLE INITIALLY IMMEDIATE;
            ALTER TABLE vehicle_operation ALTER CONSTRAINT vehicle_operation_maintenance_id_vehicle_id_school_id_fkey DEFERRABLE INITIALLY IMMEDIATE;
            """);

        migrationBuilder.CreateIndex(
            name: "vm_one_inprogress_per_vehicle", table: "maintenance",
            columns: new[] { "vehicle_id", "school_id" }, unique: true,
            filter: "status = 'InProgress'::vm_maintenance_status");

        var assembly = typeof(InitialVehicleManagement).Assembly;
        using var stream = assembly.GetManifestResourceStream(assembly.GetManifestResourceNames().Single(n => n.EndsWith(".Sql.Technical.sql", StringComparison.Ordinal)))!;
        using var reader = new StreamReader(stream);
        migrationBuilder.Sql(reader.ReadToEnd());
    }
    protected override void Down(MigrationBuilder migrationBuilder) => throw new NotSupportedException("Restore a reviewed backup; automatic destructive downgrades are disabled.");
}
