# Vairas Vehicle Management

School-owned vehicles, supported categories, teacher assignments and operational eligibility (ASP.NET Core 10,
PostgreSQL, EF Core/Npgsql). Scheduling owns bookings; Vehicle only answers eligibility and keeps vehicle state
changes consistent with Scheduling's vehicle gate through a recoverable saga and an outbox.

## Projects

| Project | Responsibility |
|---|---|
| `Vairas.VehicleManagement` | HTTP API: controllers, `Configuration/Configure*`, security, dependency clients, workers |
| `Vairas.VehicleManagement.Business` | Application services, ports, saga/outbox processing |
| `Vairas.VehicleManagement.Domain` | Vehicle entities and rules |
| `Vairas.VehicleManagement.Persistance` | `VehicleDbContext`, migrations, store |
| `Vairas.VehicleManagement.Tests` | NUnit tests (PostgreSQL via Testcontainers) |

## Dependencies

PostgreSQL 17, Authentication, User Management, School Management and Scheduling (vehicle gate); optionally Service
Bus for the status-event outbox. Local origin `https://localhost:7400`.

## Essential configuration

Options are bound and validated at startup (`Configuration/ConfigureOptions.cs`). Use `__` in environment variables.

| Section | Notes |
|---|---|
| `ConnectionStrings:VehicleManagement` | required, restricted runtime role |
| `Authentication` | HTTPS `Authority`/`Issuer`; distinct `UserAudience` and `WorkloadAudience`; `WorkloadClients` |
| `Dependencies` | HTTPS origins; `ClientId` and `ClientSecret` required |
| `Catalogue:Path` | versioned category catalogue file (`catalogue/demo-categories.json` locally) |
| `Scheduling:Enabled` | `false` returns `503 SCHEDULING_INTEGRATION_NOT_READY` for state changes |
| `Workers` | `Enabled`, `OutboxEnabled`, Service Bus namespace or local `ConnectionString`, `Topic` |

## Run locally

```powershell
dotnet run --project Vairas.VehicleManagement --launch-profile https
```

The complete local stack is started from `../local/Start.ps1`. Database roles/grants: `database/`; apply
migrations with `dotnet run --project Vairas.VehicleManagement -- --migrate` (migration role) after `--provision`.

## Tests

```powershell
dotnet test Vairas.VehicleManagement.slnx        # Docker required for PostgreSQL tests
./scripts/verify.ps1 -SkipIntegration            # unit tests with the coverage gate
```

API exports: `api/`. Extended notes, validation reports and evidence: `../../Documentation/Backend/Vairas.VehicleManagement/`.

## Tests and coverage

Requirements: .NET SDK 10 and Docker (PostgreSQL integration tests start a throw-away container via Testcontainers).

```powershell
dotnet test Vairas.VehicleManagement.slnx                  # unit + integration tests
dotnet test Vairas.VehicleManagement.slnx --collect:"XPlat Code Coverage" --settings coverage.runsettings --results-directory TestResults   # measure coverage
```

**Coverage report:** [docs/coverage/README.md](docs/coverage/README.md) (summary, per-assembly table, least covered files); machine-readable `docs/coverage/coverage-summary.json` and `docs/coverage/index.html` (per-file table). The raw cobertura XML of a local run is written to `TestResults/`.