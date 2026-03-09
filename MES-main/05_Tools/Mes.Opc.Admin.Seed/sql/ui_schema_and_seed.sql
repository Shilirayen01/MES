/*
  ui_schema_and_seed.sql

  Purpose
  -------
  Creates the SQL tables needed to store a dynamic MES UI configuration
  (dashboards, zones, widgets and bindings).

  Why this matters
  ----------------
  The Blazor UI renders screens dynamically from these tables. This makes
  your product "commercialisable": you can deploy the same executable to
  different factories and build their UI purely by configuration.

  How to use
  ----------
  1) Select your MES database (same DB used by MachineTagValue / MachineTagMapping)
  2) Run this script once.
  3) Edit the inserted demo rows to point to real MachineCode + OpcNodeId.
*/

SET NOCOUNT ON;

/* ------------------------------
   Tables
--------------------------------*/

IF OBJECT_ID('dbo.UiWidgetBinding', 'U') IS NOT NULL DROP TABLE dbo.UiWidgetBinding;
IF OBJECT_ID('dbo.UiWidget', 'U') IS NOT NULL DROP TABLE dbo.UiWidget;
IF OBJECT_ID('dbo.UiZone', 'U') IS NOT NULL DROP TABLE dbo.UiZone;
IF OBJECT_ID('dbo.UiDashboard', 'U') IS NOT NULL DROP TABLE dbo.UiDashboard;

CREATE TABLE dbo.UiDashboard
(
    Id            UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    Name          NVARCHAR(200)     NOT NULL,
    Description   NVARCHAR(500)     NULL,
    IsDefault     BIT               NOT NULL CONSTRAINT DF_UiDashboard_IsDefault DEFAULT(0),
    CreatedAtUtc  DATETIME2(3)      NOT NULL CONSTRAINT DF_UiDashboard_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    UpdatedAtUtc  DATETIME2(3)      NULL
);

CREATE TABLE dbo.UiZone
(
    Id            UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    DashboardId   UNIQUEIDENTIFIER NOT NULL,
    Title         NVARCHAR(200)     NOT NULL,
    LayoutType    NVARCHAR(50)      NOT NULL,
    PropsJson     NVARCHAR(MAX)     NULL,
    OrderIndex    INT               NOT NULL CONSTRAINT DF_UiZone_OrderIndex DEFAULT(0),
    CreatedAtUtc  DATETIME2(3)      NOT NULL CONSTRAINT DF_UiZone_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    UpdatedAtUtc  DATETIME2(3)      NULL,
    CONSTRAINT FK_UiZone_Dashboard FOREIGN KEY (DashboardId) REFERENCES dbo.UiDashboard(Id)
);

CREATE TABLE dbo.UiWidget
(
    Id            UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    ZoneId        UNIQUEIDENTIFIER NOT NULL,
    Title         NVARCHAR(200)     NOT NULL,
    WidgetType    NVARCHAR(50)      NOT NULL,
    PropsJson     NVARCHAR(MAX)     NULL,
    OrderIndex    INT               NOT NULL CONSTRAINT DF_UiWidget_OrderIndex DEFAULT(0),
    CreatedAtUtc  DATETIME2(3)      NOT NULL CONSTRAINT DF_UiWidget_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    UpdatedAtUtc  DATETIME2(3)      NULL,
    CONSTRAINT FK_UiWidget_Zone FOREIGN KEY (ZoneId) REFERENCES dbo.UiZone(Id)
);

CREATE TABLE dbo.UiWidgetBinding
(
    Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    WidgetId    UNIQUEIDENTIFIER NOT NULL,
    MachineCode NVARCHAR(50)      NOT NULL,
    OpcNodeId   NVARCHAR(400)     NOT NULL,
    BindingRole NVARCHAR(50)      NOT NULL,
    CONSTRAINT FK_UiWidgetBinding_Widget FOREIGN KEY (WidgetId) REFERENCES dbo.UiWidget(Id)
);

/* ------------------------------
   Demo seed
--------------------------------*/

DECLARE @DashboardId UNIQUEIDENTIFIER = NEWID();
DECLARE @ZoneId      UNIQUEIDENTIFIER = NEWID();

DECLARE @WStatus UNIQUEIDENTIFIER = NEWID();
DECLARE @WValue  UNIQUEIDENTIFIER = NEWID();
DECLARE @WCount  UNIQUEIDENTIFIER = NEWID();

INSERT INTO dbo.UiDashboard (Id, Name, Description, IsDefault)
VALUES (@DashboardId, N'Production - Demo', N'Dashboard de démonstration (à adapter).', 1);

INSERT INTO dbo.UiZone (Id, DashboardId, Title, LayoutType, PropsJson, OrderIndex)
VALUES (@ZoneId, @DashboardId, N'Ligne 1', N'Grid', N'{"columns":3}', 0);

INSERT INTO dbo.UiWidget (Id, ZoneId, Title, WidgetType, PropsJson, OrderIndex)
VALUES
(@WStatus, @ZoneId, N'Etat Machine',  N'Status',  N'{"hint":"RUN/STOP/FAULT"}', 0),
(@WValue,  @ZoneId, N'Vitesse',      N'Value',   N'{"unit":"m/min"}', 1),
(@WCount,  @ZoneId, N'Compteur',     N'Counter', N'{"unit":"pcs"}', 2);

/*
  IMPORTANT:
  ----------
  Replace the MachineCode and OpcNodeId below with real values from your
  MachineTagMapping table.

  Example queries:
    SELECT TOP(50) * FROM dbo.MachineTagMapping ORDER BY MachineCode;

  Then copy MachineCode + OpcNodeId into the bindings.
*/

INSERT INTO dbo.UiWidgetBinding (Id, WidgetId, MachineCode, OpcNodeId, BindingRole)
VALUES
(NEWID(), @WStatus, N'MACHINE_001', N'ns=2;s=Machine.Status',  N'Status'),
(NEWID(), @WValue,  N'MACHINE_001', N'ns=2;s=Machine.Speed',   N'Value'),
(NEWID(), @WCount,  N'MACHINE_001', N'ns=2;s=Machine.Counter', N'Counter');

SELECT Id, Name, IsDefault FROM dbo.UiDashboard;
