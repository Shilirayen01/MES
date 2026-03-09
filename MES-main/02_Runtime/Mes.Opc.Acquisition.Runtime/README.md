# Mes.Opc.Acquisition.Runtime — Final (Merged) Package

This package merges the existing runtime project with the NSU (Namespace URI) hardening
needed for Softing dataFEED OPC UA servers.

## Why NSU?
Softing dataFEED can reorder namespace **indexes** between restarts. If NodeIds are stored
as `ns=<index>;...`, subscriptions can break after a server restart.  
This runtime now supports stable NodeIds stored as: `nsu=<NamespaceUri>;...`.

## What changed (high level)
- **Runtime**: resolves both formats:
  - Legacy: `ns=<index>;...`
  - Stable: `nsu=<NamespaceUri>;...` (resolved against `Session.NamespaceUris`)
- **Cycle tracking**: tolerant NodeId matching (`ns=` vs `nsu=`) to avoid breaking existing rules.
- **Database scripts**: widen NodeId columns and upgrade TVP/SP to support long NSU strings.

## Deployment
1) Publish / deploy the service as usual (Windows Service, container, etc.).
2) Execute SQL scripts (recommended order):
   - `Sql/001_CreateProductionRun.sql`
   - `Sql/002_CreateMachineCycleRule.sql`
   - `Sql/004_AddRecoveryColumns_MachineCycleRule.sql` (if you use recovery columns)
   - `Sql/005_WidenNodeIdColumns_For_NSU.sql`
   - `Sql/003_TvpAndProc_V2.sql`  (upgrades TVP + stored procedure to NVARCHAR(1200))
   - `Sql/006_AddAndMigrate_NSU.sql` (adds OpcNodeIdNsu and migrates legacy values)

> Note: `006_AddAndMigrate_NSU.sql` assumes Softing Namespace URI:
> `http://softing.com/UA/DataFEED`. Change `@SoftingNsu` if your server uses another URI.

## Database expectations
This runtime expects existing operational tables such as:
- `Machine`, `MachineTagMapping`, `MachineTagValue` (already present in your OPCDB)

The provided scripts are **upgrades** that can be run safely multiple times.

## Docs
- `Docs/Rapport_Final.docx` — final functional report / design notes.
