-- Enable SQLCMD mode in SSMS to use :r includes

:r ..\Sql\001_CreateProductionRun.sql
:r ..\Sql\002_CreateMachineCycleRule.sql
:r ..\Sql\003_TvpAndProc_V2.sql
:r ..\Sql\004_AddRecoveryColumns_MachineCycleRule.sql
:r ..\Sql\005_WidenNodeIdColumns_For_NSU.sql
:r ..\Sql\006_AddAndMigrate_NSU.sql
:r ..\Sql\007_AddAndMigrate_NSU_MachineCycleRule.sql
