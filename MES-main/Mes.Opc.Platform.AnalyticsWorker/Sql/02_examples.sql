/* =========================================================
   EXEMPLES de configuration (KPI + Quality + Summary)
   A adapter (TagNodeId / MachineCode / Vitesses / Unités)
   ========================================================= */

-- Nettoyage (optionnel)
-- DELETE FROM AnalyticsKpiVariable;
-- DELETE FROM AnalyticsKpiDefinition;
-- DELETE FROM AnalyticsSummaryItem;
-- DELETE FROM AnalyticsSummaryDefinition;
-- DELETE FROM AnalyticsQualityRule;
-- DELETE FROM AnalyticsQualityRuleSet;

DECLARE @MachineCode nvarchar(50) = 'MC_02';

-------------------------------------------------------------
-- 1) QUALITY RULES (produit OK / NOK)
-- Exemple :
-- - ScrapFlag doit rester à 0 pendant le run (AnyViolation + Equals 0)
-- - Température doit être dans [720, 780] au moins 95% du temps (PercentInRange)
-------------------------------------------------------------

INSERT INTO AnalyticsQualityRuleSet (Name, IsActive, AppliesToMachineCode, LogicMode)
VALUES ('Default Quality Rules', 1, @MachineCode, 0); -- 0=AND

DECLARE @RuleSetId int = SCOPE_IDENTITY();

INSERT INTO AnalyticsQualityRule (RuleSetId, Code, IsActive, TagNodeId, Scope, ConditionType, EvaluationMode, ExpectedValue)
VALUES
(@RuleSetId, 'SCRAP_FLAG', 1, 'ns=22;s=...ScrapFlag...', 0, 1, 1, 0);
-- Scope=0 Product ; ConditionType=1 Equals ; EvaluationMode=1 AnyViolation

INSERT INTO AnalyticsQualityRule (RuleSetId, Code, IsActive, TagNodeId, Scope, ConditionType, EvaluationMode, MinValue, MaxValue, PercentThreshold)
VALUES
(@RuleSetId, 'TEMP_IN_RANGE', 1, 'ns=22;s=...Temp...', 0, 3, 5, 720, 780, 95);
-- ConditionType=3 InRange ; EvaluationMode=5 PercentInRange

-------------------------------------------------------------
-- 2) KPI - RT (Running Time)
-- Exemple cumulatif (Delta as-of start/end)
-- DM_OperatingHours est GLOBAL (ProductionRunId = NULL)
-------------------------------------------------------------

INSERT INTO AnalyticsKpiDefinition (Code, Name, IsActive, AppliesToMachineCode, Expression, Unit, Notes)
VALUES ('RT_HOURS', 'Running Time (heures)', 1, @MachineCode, 'RT', 'h',
        'RT = Delta(as-of end - as-of start) sur un compteur horaire cumulatif');

DECLARE @KpiRtId int = SCOPE_IDENTITY();

INSERT INTO AnalyticsKpiVariable (KpiId, VariableName, SourceType, TagNodeId, Scope, Aggregation, IsCumulative, LookbackMinutes, MissingDataMode)
VALUES
(@KpiRtId, 'RT', 0, 'ns=22;s=...DM_OperatingHours...', 1, 7, 1, 10, 0);
-- SourceType=0 Tag ; Scope=1 Global ; Aggregation=7 Delta

-------------------------------------------------------------
-- 3) KPI - SE (Speed Efficiency)
-- SE(%) = (AvgSpeed_TWA / NominalSpeed) * 100
-- LineSpeed est GLOBAL (ProductionRunId = NULL)
-------------------------------------------------------------

INSERT INTO AnalyticsKpiDefinition (Code, Name, IsActive, AppliesToMachineCode, Expression, Unit)
VALUES ('SE', 'Speed Efficiency', 1, @MachineCode, '(AvgSpeed / NominalSpeed) * 100', '%');

DECLARE @KpiSeId int = SCOPE_IDENTITY();

INSERT INTO AnalyticsKpiVariable (KpiId, VariableName, SourceType, TagNodeId, Scope, Aggregation, LookbackMinutes, MissingDataMode)
VALUES
(@KpiSeId, 'AvgSpeed', 0, 'ns=22;s=...LineSpeed...', 1, 8, 10, 0);
-- Aggregation=8 TWA (time-weighted average)

INSERT INTO AnalyticsKpiVariable (KpiId, VariableName, SourceType, ConstantValue, MissingDataMode)
VALUES
(@KpiSeId, 'NominalSpeed', 1, 90.0, 2);
-- SourceType=1 Constant ; MissingDataMode=2 Fail (obligatoire)

-------------------------------------------------------------
-- 4) KPI - FTQ (First Time Quality)
-- Ici FTQ par run = IsOk (0/1) calculé par le RuleSet.
-- Ensuite FTQ sur une période = AVG(IsOk) dans tes requêtes BI.
-------------------------------------------------------------

INSERT INTO AnalyticsKpiDefinition (Code, Name, IsActive, AppliesToMachineCode, Expression, Unit)
VALUES ('FTQ', 'FTQ (par run: OK=1, NOK=0)', 1, @MachineCode, 'IsOk', 'ratio');

DECLARE @KpiFtqId int = SCOPE_IDENTITY();

INSERT INTO AnalyticsKpiVariable (KpiId, VariableName, SourceType, MissingDataMode)
VALUES
(@KpiFtqId, 'IsOk', 2, 0);
-- SourceType=2 QualityIsOk

-------------------------------------------------------------
-- 5) KPI - OEE (exemple simple)
-- OEE = Availability * Performance * Quality
-- Availability = RT / PlannedTime
-- Performance  = AvgSpeed / NominalSpeed
-- Quality      = FTQ (0/1)
-------------------------------------------------------------

INSERT INTO AnalyticsKpiDefinition (Code, Name, IsActive, AppliesToMachineCode, Expression, Unit)
VALUES ('OEE', 'OEE', 1, @MachineCode, '(RT / PlannedTime) * (AvgSpeed / NominalSpeed) * FTQ', 'ratio');

DECLARE @KpiOeeId int = SCOPE_IDENTITY();

-- réutiliser RT (Delta)
INSERT INTO AnalyticsKpiVariable (KpiId, VariableName, SourceType, TagNodeId, Scope, Aggregation, IsCumulative, LookbackMinutes, MissingDataMode)
VALUES
(@KpiOeeId, 'RT', 0, 'ns=22;s=...DM_OperatingHours...', 1, 7, 1, 10, 0);

-- PlannedTime constant (heures) - à adapter
INSERT INTO AnalyticsKpiVariable (KpiId, VariableName, SourceType, ConstantValue, MissingDataMode)
VALUES
(@KpiOeeId, 'PlannedTime', 1, 2.0, 2);

-- AvgSpeed TWA
INSERT INTO AnalyticsKpiVariable (KpiId, VariableName, SourceType, TagNodeId, Scope, Aggregation, LookbackMinutes, MissingDataMode)
VALUES
(@KpiOeeId, 'AvgSpeed', 0, 'ns=22;s=...LineSpeed...', 1, 8, 10, 0);

-- NominalSpeed constant
INSERT INTO AnalyticsKpiVariable (KpiId, VariableName, SourceType, ConstantValue, MissingDataMode)
VALUES
(@KpiOeeId, 'NominalSpeed', 1, 90.0, 2);

-- FTQ variable (QualityIsOk)
INSERT INTO AnalyticsKpiVariable (KpiId, VariableName, SourceType, MissingDataMode)
VALUES
(@KpiOeeId, 'FTQ', 2, 0);

-------------------------------------------------------------
-- 6) SUMMARY (rapport fin de run)
-------------------------------------------------------------

INSERT INTO AnalyticsSummaryDefinition (Name, IsActive, AppliesToMachineCode)
VALUES ('End-Of-Run Summary', 1, @MachineCode);

DECLARE @SummaryId int = SCOPE_IDENTITY();

-- MaxLength : tag produit, Max
INSERT INTO AnalyticsSummaryItem (SummaryId, FieldName, SourceType, TagNodeId, Scope, Aggregation, Unit)
VALUES
(@SummaryId, 'MaxLength', 0, 'ns=22;s=...LengthMax...', 0, 2, 'm');
-- Aggregation=2 Max

-- AvgSpeed : global, TWA
INSERT INTO AnalyticsSummaryItem (SummaryId, FieldName, SourceType, TagNodeId, Scope, Aggregation, Unit)
VALUES
(@SummaryId, 'AvgSpeed', 0, 'ns=22;s=...LineSpeed...', 1, 8, 'm/min');

-- LastSection : produit, LastAsOfEnd
INSERT INTO AnalyticsSummaryItem (SummaryId, FieldName, SourceType, TagNodeId, Scope, Aggregation)
VALUES
(@SummaryId, 'LastSection', 0, 'ns=22;s=...AnnealingSection...', 0, 4);

