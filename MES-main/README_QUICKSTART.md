# Mes OPC – Démarrage rapide (API + Realtime + UI)

Cette solution permet :
- l’acquisition OPC UA (projet runtime),
- une **API REST** (projet `Mes.Opc.Platform.Api`),
- la diffusion **temps réel** via SignalR (projet `Mes.Opc.Platform.Realtime`),
- l’affichage web dynamique (projet Blazor `mes-opc-ui`).

## Prérequis
- .NET SDK **8.0**
- SQL Server (ou Azure SQL) accessible
- Serveur(s) OPC UA accessible(s) (ou simulation)

## 1) Configurer la connexion SQL
⚠️ Par sécurité, les identifiants ont été retirés des `appsettings`.

Édite ces fichiers et renseigne une vraie chaîne de connexion :

- `02_Runtime/Mes.Opc.Acquisition.Runtime/appsettings.json`
- `02_Runtime/Mes.Opc.Acquisition.Runtime/appsettings.Development.json`
- `03_Platform/Mes.Opc.Platform.Realtime/appsettings.json`
- `03_Platform/Mes.Opc.Platform.Api/appsettings.json`

Clé :
`ConnectionStrings:DefaultConnection`

Exemple :
`Server=localhost;Database=OpcDb;User Id=sa;Password=...;TrustServerCertificate=True`

## 2) Préparer la base de données (UI dynamique)
Exécute **une fois** le script SQL :
- `05_Tools/Mes.Opc.Admin.Seed/sql/ui_schema_and_seed.sql`

Puis remplace les valeurs de démonstration dans `UiWidgetBinding` (MachineCode / OpcNodeId)
par les valeurs réelles présentes dans ta table `MachineTagMapping`.

## 3) Démarrer l’API REST
Démarre le projet :
- `03_Platform/Mes.Opc.Platform.Api`

Il héberge notamment :
- `/api/machines`
- `/api/machines/{machineCode}/tags`
- `/api/ui/dashboards/default`

(Par défaut : `https://localhost:61818/` via `launchSettings.json`.)

## 4) Démarrer le backend temps réel
Démarre le projet :
- `03_Platform/Mes.Opc.Platform.Realtime`

Il héberge :
- `/opcHub` (SignalR)

(Par défaut : `https://localhost:61820/` via `launchSettings.json`.)

## 5) Démarrer l’UI web
Démarre le projet :
- `04_Frontend/mes-opc-ui`

L’UI utilise :
- `ApiBaseUrl` (REST) et `RealtimeBaseUrl` (SignalR) dans :
  `04_Frontend/mes-opc-ui/wwwroot/appsettings.json`

## 6) Vérifications rapides
1) Vérifie l’API UI :
- `GET https://localhost:61818/api/ui/dashboards/default`
Doit retourner un JSON Dashboard.

2) Vérifie SignalR :
- Ouvre l’UI (url affichée quand tu lances `mes-opc-ui`)
En haut, le badge doit passer à **Connected**.

Bon test !
