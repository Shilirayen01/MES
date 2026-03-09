# Mes.Opc.Platform.AnalyticsWorker

Worker **d'analyse post-run** pour :

- calcul des **KPI** (RT, SE, FTQ, OEE, …)
- génération du **résumé de production** (fin de run)

Le même moteur analytique sert aux deux usages : **seule la configuration** (tags, fenêtres, agrégations, formules) change.

## Principe clé : vérité produit vs vérité machine

Le système distingue explicitement deux familles de tags dans `MachineTagValue` :

### 1) Tags produit (liés à un `ProductionRunId`)
- **Vérité produit** (valeurs propres à une bobine / spool / run)
- lecture **directe** via le `ProductionRunId` du run
- pas d’ambiguïté en **multi-spool** : un tag produit appartient à un run (GUID)

### 2) Tags machine globaux (sans `ProductionRunId`)
- **Vérité machine** (état ligne, vitesse, défauts, consommation, etc.)
- stockés **sans rattachement** (`ProductionRunId = NULL`)
- rattachés **dynamiquement** au run **au moment du calcul** par corrélation temporelle :
  - même `MachineCode`
  - `SourceTimestamp` dans l’intervalle `[StartTs, EndTs]` du run (avec un lookback configurable si besoin)

➡️ Donc : **le rattachement Run–Tag est effectué au calcul analytique et non à l’écriture**, ce qui garantit un fonctionnement multi-spool robuste, sans ambiguïté et totalement paramétrable.

## Flux de traitement

1. Le worker récupère des runs à traiter (ex: `Completed`) dans `ProductionRun`.
2. Il charge la configuration analytique (définitions de tags + profils d’agrégats + formules).
3. Pour chaque tag requis :
   - **TagScope=Product** → requête par `ProductionRunId = run.RunId`
   - **TagScope=Global** → requête par `MachineCode` + fenêtre temporelle du run
4. Il exécute les agrégats (last, avg, sum, duration, count, …) et calcule les formules.
5. Il écrit les résultats (KPI + Summary) dans des tables dédiées.

## Configuration (100% paramétrable)

La configuration est stockée en DB (voir `Sql/01_create_tables.sql` et `Sql/02_examples.sql`).

- `AnalyticsTagDefinition` : liste des tags utilisables (OpcNodeId, type, **TagScope**, …)
- `AnalyticsProfile` : un profil (ex: `KPI_Default`, `Summary_Default`)
- `AnalyticsProfileItem` : quels tags + quel agrégat + quelles fenêtres
- `AnalyticsFormula` : formules (ex: OEE = Availability * Performance * Quality)

## Multi-spool

Le run est identifié par :
- `MachineCode`
- `ScopeKey` (SP1, SP2, …)
- `RunId` (GUID stable)

Les tags produit sont attachés au `RunId` → support naturel du multi-spool.
Les tags globaux sont rattachés par **temps** → pas de conflit entre spools.

## Démarrage

- Renseigner la connexion SQL dans `appsettings.json` (`ConnectionStrings:MesOpc`).
- Créer les tables : `Sql/01_create_tables.sql`
- Charger les exemples : `Sql/02_examples.sql`
- Lancer :

```bash
dotnet run --project Mes.Opc.Platform.AnalyticsWorker
```

## Points d’attention

- Les requêtes se basent sur `SourceTimestamp` (vérité temporelle OPC) et utilisent `CreatedAt` comme tiebreaker.
- Un lookback configuré évite les erreurs sur des agrégats nécessitant une valeur juste avant `StartTs`.

---

Si tu veux, je peux aussi te fournir :
- un script de seed “réel” basé sur ta nomenclature de tags
- des exemples de formules KPI (RT/SE/FTQ/OEE) adaptés à MC_03 et SP1..SPn
