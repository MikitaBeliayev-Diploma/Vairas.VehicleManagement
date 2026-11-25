# Vairas.VehicleManagement — code coverage

**Line coverage 81.41 %** (1213/1490 executable lines), branch coverage 65.51 % — threshold 70 % **met**. Generated 2026-11-25.

| Assembly | Line % | Lines | Branch % |
|---|---:|---:|---:|
| Vairas.VehicleManagement | 65.53 | 424/647 | 49.17 |
| Vairas.VehicleManagement.Business | 93.76 | 616/657 | 75.43 |
| Vairas.VehicleManagement.Domain | 50.0 | 1/2 | 100.0 |
| Vairas.VehicleManagement.Persistance | 93.48 | 172/184 | 85.29 |

## How it was measured

- Tool: coverlet (`XPlat Code Coverage`) driven by `Backend/local/coverage.py`; settings `Vairas.VehicleManagement/coverage.runsettings` (migrations and test assemblies excluded).
- Test projects: `Vairas.VehicleManagement.Tests`; line hits of all test projects of the service are merged (maximum per line), so shared code is never counted twice.
- Filter: `TestCategory!=Broker`. Broker tests need the Service Bus emulator and are executed (and passed) separately by `local/test_all.py`.
- The numbers are measured line coverage, not a statement about functional completeness; acceptance status is tracked in `Documentation/Final-Verification/AC-Traceability-Matrix.xlsx`.

## Reproduce

```powershell
cd Backend
py -3 local/coverage.py --services Vairas.VehicleManagement --filter "TestCategory!=Broker" --threshold 70
```

## Files

`coverage-summary.json` (machine-readable, per file) and `index.html` (per-file table, lowest coverage first) are in this folder.

## Least covered files

| File | Line % | Lines |
|---|---:|---:|
| `Vairas.VehicleManagement.Business/VehicleService.VehicleSchedulingReads.cs` | 0.0 | 0/10 |
| `Workers/ServiceBusPublisher.cs` | 0.0 | 0/31 |
| `Startup/DatabaseMigration.cs` | 0.0 | 0/7 |
| `Startup/DatabaseProvisioning.cs` | 0.0 | 0/25 |
| `Integrations/DependencyCorrelationHandler.cs` | 0.0 | 0/9 |
| `Integrations/DependencyTransport.cs` | 0.0 | 0/25 |
| `Integrations/SchedulingGate.cs` | 0.0 | 0/53 |
| `Integrations/SchoolAuthorityClient.cs` | 0.0 | 0/2 |
