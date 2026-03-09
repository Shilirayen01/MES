# DbDeploy

Ce dossier contient un "master" script pour déployer / mettre à jour la base.

## Fichiers
- `RunAll.sql` : script concaténé (pas besoin du mode SQLCMD)
- `RunAll_SqlCmdIncludes.sql` : version avec `:r` (nécessite SQLCMD mode dans SSMS)

## Ordre d'exécution
L'ordre est basé sur la numérotation des scripts dans `../Sql/`.

> Conseil : exécuter sur la bonne base (ex: `USE [OPCDB_New];`) avant de lancer.
