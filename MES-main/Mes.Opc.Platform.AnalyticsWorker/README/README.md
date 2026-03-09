# Mes.Opc.Platform.AnalyticsWorker

Worker .NET (BackgroundService) qui calcule automatiquement :
1) des **KPI** paramétrables (RT, SE, FTQ, OEE, etc.) via une expression + variables
2) un **Run Summary** paramétrable (Max/Avg/Last/TWA sur une liste de tags)

Conforme au document : **TS_MES_OPC_KPI_et_Summary_FINAL.pdf** (Version 1.1 - 23/01/2026).

---

## 1) Principe (très simple)

Quand un `ProductionRun` passe au statut **Completed** :

- le worker lit les valeurs dans `MachineTagValue`
- il calcule :
  - **Quality / IsOk** (si un RuleSet est configuré)
  - **KPI** (expressions)
  - **Summary** (liste de champs)
- il écrit les résultats en base :
  - `ProductionRunKpiResult`
  - `ProductionRunSummaryValue`
  - `ProductionRunQuality`
  - `ProductionRunAnalyticsStatus` (pour éviter de recalculer en boucle)

---

## 2) Contrainte multi-spool (IMPORTANT)

- `MachineTagValue.ProductionRunId` **est rempli uniquement** pour les tags scope **Product** (SP1..SP5) qui ont une règle active.
- Les tags **globaux machine** restent **ProductionRunId = NULL**.
- Donc le worker lit les tags selon **Scope** :
  - Product -> `WHERE ProductionRunId = @RunId`
  - Global  -> `WHERE MachineCode = @MachineCode AND ProductionRunId IS NULL AND SourceTimestamp BETWEEN [Start-Lookback, End]`

---

## 3) Aggrégations supportées

- Min / Max / Avg
- AsOfStart / AsOfEnd / LastAsOfEnd
- Delta (AsOfEnd - AsOfStart)
- Time-Weighted Average (TWA)
- PercentInRange (0..100%)

Les règles "robustes" sont appliquées :
- dedup par `SourceTimestamp` (garde la ligne la plus récente `CreatedAt/Id`)
- "as-of" = dernière valeur <= borne
- lookback configurable (défaut 10 minutes)
- si gap trop grand -> `TWA` / `PercentInRange` retourne NULL (données insuffisantes)

---

## 4) Installer

1) Ajouter le projet au `.sln`
2) Ajouter la connection string dans `appsettings.json`
3) Exécuter le script SQL `Sql/01_create_tables.sql`
4) Ajouter des définitions de KPI / Summary (scripts d'exemple `Sql/02_examples.sql`)
5) Lancer le worker.

---

## 5) Scripts SQL

- `Sql/01_create_tables.sql` : crée les tables de config + résultats
- `Sql/02_examples.sql` : exemples RT, SE, FTQ, OEE + summary

---
