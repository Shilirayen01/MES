/* =========================================================
   MES OPC - Analytics Worker (KPI + Run Summary)
   Tables de configuration + résultats
   Compatible SQL Server
   ========================================================= */

-- =============== KPI (définitions + variables) ===============

IF OBJECT_ID('dbo.AnalyticsKpiDefinition', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AnalyticsKpiDefinition (
        KpiId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AnalyticsKpiDefinition PRIMARY KEY,
        Code NVARCHAR(50) NOT NULL CONSTRAINT UQ_AnalyticsKpiDefinition_Code UNIQUE,
        Name NVARCHAR(200) NOT NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_AnalyticsKpiDefinition_IsActive DEFAULT(1),
        AppliesToMachineCode NVARCHAR(50) NULL, -- NULL = toutes les machines
        Expression NVARCHAR(2000) NOT NULL,     -- ex: (RT/PlannedTime) * (AvgSpeed/NominalSpeed) * FTQ
        Unit NVARCHAR(50) NULL,
        Notes NVARCHAR(1000) NULL
    );
END

IF OBJECT_ID('dbo.AnalyticsKpiVariable', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AnalyticsKpiVariable (
        VariableId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AnalyticsKpiVariable PRIMARY KEY,
        KpiId INT NOT NULL CONSTRAINT FK_AnalyticsKpiVariable_Def FOREIGN KEY REFERENCES dbo.AnalyticsKpiDefinition(KpiId),
        VariableName NVARCHAR(50) NOT NULL,

        -- 0=Tag, 1=Constant, 2=QualityIsOk
        SourceType TINYINT NOT NULL,

        -- Tag source
        TagNodeId NVARCHAR(400) NULL,
        -- 0=Product, 1=Global
        Scope TINYINT NULL,
        -- 1=Min,2=Max,3=Avg,4=LastAsOfEnd,5=AsOfStart,6=AsOfEnd,7=Delta,8=TWA,9=PercentInRange
        Aggregation TINYINT NULL,
        IsCumulative BIT NULL,

        -- Constant source
        ConstantValue FLOAT NULL,

        -- Robustesse
        LookbackMinutes INT NULL,
        MaxGapSeconds INT NULL,

        -- Missing data
        -- 0=NullResult, 1=UseZero, 2=Fail
        MissingDataMode TINYINT NOT NULL CONSTRAINT DF_AnalyticsKpiVariable_Missing DEFAULT(0),

        -- PercentInRange (optionnel pour KPI ou règles)
        RangeMin FLOAT NULL,
        RangeMax FLOAT NULL,
        PercentThreshold FLOAT NULL
    );

    CREATE INDEX IX_AnalyticsKpiVariable_KpiId ON dbo.AnalyticsKpiVariable(KpiId);
END

-- =============== Quality rules (pour IsOk) ===============

IF OBJECT_ID('dbo.AnalyticsQualityRuleSet', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AnalyticsQualityRuleSet (
        RuleSetId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AnalyticsQualityRuleSet PRIMARY KEY,
        Name NVARCHAR(200) NOT NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_AnalyticsQualityRuleSet_IsActive DEFAULT(1),
        AppliesToMachineCode NVARCHAR(50) NULL,
        -- 0=AND, 1=OR
        LogicMode TINYINT NOT NULL CONSTRAINT DF_AnalyticsQualityRuleSet_Logic DEFAULT(0)
    );
END

IF OBJECT_ID('dbo.AnalyticsQualityRule', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AnalyticsQualityRule (
        RuleId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AnalyticsQualityRule PRIMARY KEY,
        RuleSetId INT NOT NULL CONSTRAINT FK_AnalyticsQualityRule_Set FOREIGN KEY REFERENCES dbo.AnalyticsQualityRuleSet(RuleSetId),
        Code NVARCHAR(50) NOT NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_AnalyticsQualityRule_IsActive DEFAULT(1),

        TagNodeId NVARCHAR(400) NOT NULL,
        -- 0=Product, 1=Global
        Scope TINYINT NOT NULL,

        -- ConditionType:
        -- 1=Equals,2=NotEquals,3=InRange,4=OutOfRange,5=GreaterThan,6=GreaterOrEqual,7=LessThan,8=LessOrEqual
        ConditionType TINYINT NOT NULL,

        -- RuleEvaluationMode:
        -- 1=AnyViolation,2=AsOfEnd,3=MaxValue,4=MinValue,5=PercentInRange
        EvaluationMode TINYINT NOT NULL,

        ExpectedValue FLOAT NULL,
        MinValue FLOAT NULL,
        MaxValue FLOAT NULL,

        -- pour PercentInRange
        PercentThreshold FLOAT NULL,

        LookbackMinutes INT NULL,
        MaxGapSeconds INT NULL
    );

    CREATE INDEX IX_AnalyticsQualityRule_RuleSetId ON dbo.AnalyticsQualityRule(RuleSetId);
END

-- =============== Summary (définition + items) ===============

IF OBJECT_ID('dbo.AnalyticsSummaryDefinition', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AnalyticsSummaryDefinition (
        SummaryId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AnalyticsSummaryDefinition PRIMARY KEY,
        Name NVARCHAR(200) NOT NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_AnalyticsSummaryDefinition_IsActive DEFAULT(1),
        AppliesToMachineCode NVARCHAR(50) NULL
    );
END

IF OBJECT_ID('dbo.AnalyticsSummaryItem', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AnalyticsSummaryItem (
        ItemId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AnalyticsSummaryItem PRIMARY KEY,
        SummaryId INT NOT NULL CONSTRAINT FK_AnalyticsSummaryItem_Def FOREIGN KEY REFERENCES dbo.AnalyticsSummaryDefinition(SummaryId),
        FieldName NVARCHAR(50) NOT NULL,

        -- 0=Tag, 1=Constant, 2=QualityIsOk
        SourceType TINYINT NOT NULL,

        TagNodeId NVARCHAR(400) NULL,
        Scope TINYINT NULL,
        Aggregation TINYINT NULL,
        IsCumulative BIT NULL,

        ConstantValue FLOAT NULL,
        LookbackMinutes INT NULL,
        MaxGapSeconds INT NULL,
        Unit NVARCHAR(50) NULL
    );

    CREATE INDEX IX_AnalyticsSummaryItem_SummaryId ON dbo.AnalyticsSummaryItem(SummaryId);
END

-- =============== Résultats ===============

IF OBJECT_ID('dbo.ProductionRunKpiResult', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProductionRunKpiResult (
        RunId UNIQUEIDENTIFIER NOT NULL,
        KpiCode NVARCHAR(50) NOT NULL,
        Value FLOAT NULL,
        Unit NVARCHAR(50) NULL,
        ComputedAt DATETIME2(7) NOT NULL CONSTRAINT DF_ProductionRunKpiResult_Computed DEFAULT(SYSUTCDATETIME()),
        DetailsJson NVARCHAR(MAX) NULL,
        CONSTRAINT PK_ProductionRunKpiResult PRIMARY KEY (RunId, KpiCode)
    );
END

IF OBJECT_ID('dbo.ProductionRunSummaryValue', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProductionRunSummaryValue (
        RunId UNIQUEIDENTIFIER NOT NULL,
        FieldName NVARCHAR(50) NOT NULL,
        Value FLOAT NULL,
        Unit NVARCHAR(50) NULL,
        ComputedAt DATETIME2(7) NOT NULL CONSTRAINT DF_ProductionRunSummaryValue_Computed DEFAULT(SYSUTCDATETIME()),
        DetailsJson NVARCHAR(MAX) NULL,
        CONSTRAINT PK_ProductionRunSummaryValue PRIMARY KEY (RunId, FieldName)
    );
END

IF OBJECT_ID('dbo.ProductionRunQuality', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProductionRunQuality (
        RunId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ProductionRunQuality PRIMARY KEY,
        IsOk BIT NULL,
        ComputedAt DATETIME2(7) NOT NULL CONSTRAINT DF_ProductionRunQuality_Computed DEFAULT(SYSUTCDATETIME()),
        ReasonJson NVARCHAR(MAX) NULL
    );
END

IF OBJECT_ID('dbo.ProductionRunAnalyticsStatus', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProductionRunAnalyticsStatus (
        RunId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ProductionRunAnalyticsStatus PRIMARY KEY,
        KpiComputedAt DATETIME2(7) NULL,
        SummaryComputedAt DATETIME2(7) NULL,
        QualityComputedAt DATETIME2(7) NULL,
        LastError NVARCHAR(2000) NULL,
        UpdatedAt DATETIME2(7) NOT NULL CONSTRAINT DF_ProductionRunAnalyticsStatus_Updated DEFAULT(SYSUTCDATETIME())
    );
END
