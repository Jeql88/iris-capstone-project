# IRIS Project - Claude Code Guide

## Project Overview
IRIS (Integrated Remote Infrastructure System) — LAN-only network management system for 80 Windows lab PCs across 4 computer laboratories at the University of San Carlos ACC.

## Tech Stack
- **Language:** C# / .NET 9.0
- **UI:** WPF with WPF-UI (Lepo.co) library
- **Database:** PostgreSQL via Entity Framework Core
- **Agent:** Console app deployed to each lab PC
- **Logging:** Serilog (agent), built-in (UI)

## Project Structure
```
IRIS.sln
├── IRIS.UI/          # WPF desktop dashboard (Admin/IT/Faculty)
│   ├── Views/        # XAML pages organized by role
│   ├── ViewModels/   # MVVM ViewModels
│   ├── Services/     # Navigation, caching, polling
│   ├── Converters/   # XAML value converters
│   ├── Helpers/      # RelayCommand
│   └── Models/       # UI-specific models (PCModel, FileItemModel)
├── IRIS.Core/        # Shared library
│   ├── Data/         # IRISDbContext, EF migrations
│   ├── Models/       # Entity models (17 entities)
│   ├── Services/     # Business logic (13 services)
│   └── DTOs/         # Data transfer objects
├── IRIS.Agent/       # Lightweight client agent (per lab PC)
│   ├── Controllers/  # HTTP API controllers
│   └── Logic/        # Monitoring, screen capture, file management
└── PasswordFix/      # Utility for password migration
```

## Coding Conventions

### Dialogs & Prompts
Always use `ConfirmationDialog` (not `MessageBox.Show`) for user-facing dialogs:
```csharp
using IRIS.UI.Views.Dialogs;

// Confirmation
var dialog = new ConfirmationDialog("Title", "Message", "Warning24", "Yes", "No");
dialog.Owner = Application.Current.MainWindow;
if (dialog.ShowDialog() != true) return;

// Success (no cancel button)
var success = new ConfirmationDialog("Success", "Done!", "Checkmark24", "OK", "Cancel", false);
success.Owner = Application.Current.MainWindow;
success.ShowDialog();
```

### MVVM Pattern
- ViewModels implement `INotifyPropertyChanged`
- Commands use `RelayCommand` from `IRIS.UI.Helpers`
- Collections use `ObservableCollection<T>`
- Async methods suffixed with `Async`

### Naming
- PascalCase: public properties, methods
- _camelCase: private backing fields
- Interfaces prefixed with `I` (e.g., `IRoomService`)

## Build & Run
```bash
dotnet build IRIS.sln
dotnet run --project IRIS.UI      # Dashboard
dotnet run --project IRIS.Agent   # Agent (run as admin for initial setup)
```

## Database
```bash
dotnet ef migrations add MigrationName --project IRIS.Core
dotnet ef database update --project IRIS.Core
```
Connection string in `appsettings.json` — PostgreSQL on localhost:5432, database `iris_db`.

## Default Credentials
- admin / admin (System Administrator)
- itperson / admin (IT Personnel)
- faculty / admin (Faculty)

## Key Ports
- 5057: Screen stream (agent)
- 5065: File management API (agent)
- 5091: Power command server (UI)
