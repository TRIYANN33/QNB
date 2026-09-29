# QNB

Application Windows développée en **C# / .NET 8** avec **Windows Forms**.

## Prérequis

- Windows 10/11
- Visual Studio 2022 ou version compatible
- .NET 8 SDK

## Démarrage

1. Cloner le dépôt.
2. Ouvrir `QNB.sln` dans Visual Studio.
3. Restaurer les dépendances si nécessaire.
4. Compiler puis lancer le projet `QNB`.

## Structure

- `QNB.sln` — solution Visual Studio
- `QNB.csproj` — projet C# Windows Forms
- `Program.cs` — point d'entrée
- `MainForm.cs` — fenêtre principale

## Objectif

Cette base servira au développement progressif de l'application QNB en C#.

## Publication autonome Windows x64

Depuis Windows avec le SDK .NET 8 :

```powershell
dotnet restore QNB.csproj -r win-x64
dotnet publish QNB.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./publish
```

Le fichier `publish/QNB.exe` est autonome. Les données SQLite sont conservées dans `%LOCALAPPDATA%\QNB\qnb.db` et ne sont pas incluses dans l'exécutable. Sauvegardez-les avant toute mise à niveau. La migration du schéma est additive ; aucun effacement automatique des comptes, imports ou opérations au démarrage.
