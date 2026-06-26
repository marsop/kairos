# Kairos

Personal Time Budgeting and Accounting

## Live Version

Available at https://albertogregorio.com/Kairos/

## Overview

Kairos is a time-tracking application built with Blazor WebAssembly. It helps you track how you spend your time using customizable activities, giving you a balance sheet of your time budget.

## Features

### Time Tracking with Activities
- **Customizable Activities**: Create multiple time trackers with custom names, metadata, emojis, and colors
- **Activity Comments**: Add an optional descriptive comment when starting an activity
- **One-Touch Activation**: Tap a activity to start/stop tracking time
- **Real-time Duration**: See live updates of running duration

### Statistics
- **Data Visualization**: Understand your time distribution with charts and visual styling

### Hardware Integration
- **Timeular Tracker**: Connect via Bluetooth to automatically start/stop activities by flipping your device

### Overview Dashboard
- **Current Balance**: At-a-glance view of your total time balance in hours
- **Active Indicator**: Shows which activity is currently running

### History
- **Event History**: View all recorded time events chronologically and filter by date
- **Daily Breakdown**: See how time was spent each day
- **Detailed Events**: View start times, durations, and associated activities
- **Calendar View**: Visualize daily events in a vertical calendar format

### Sync & Backup
- **Local Export/Import**: Download your data as JSON or export days as CSV files (includes Activity ID, metadata, and comments)
- **Supabase Integration**: Backup and restore data to/from a Supabase cloud database
- **Auto-Sync**: Enable automatic synchronization when signed in to Supabase

### Settings
- **Multi-Language Support**: Available in English, Deutsch, Español, Galego, and Vorarlbergerisch
- **Factory Reset**: Clear all data and start fresh

### Progressive Web App (PWA)
- **Installable**: Add to home screen on mobile devices or desktop
- **Offline Support**: Works offline once installed
- **Service Worker**: Background sync and caching

## Technology Stack

- **.NET 10.0**
- **Blazor WebAssembly** - Web application
- **Shared Razor Components** - Reusable UI library

## Project Structure

```
Kairos/
├── src/
│   ├── Kairos.Shared/       # Shared components, services, models
│   │   ├── Components/       # Reusable UI components
│   │   ├── Layout/           # Application layout
│   │   ├── Models/           # Data models (Activity, ActivityEvent, TimeAccount)
│   │   ├── Pages/            # Application pages (Overview, Activities, History, Statistics, etc.)
│   │   ├── Resources/        # Localization files
│   │   └── Services/         # Core business logic services
│   └── Kairos.Web/          # Blazor WebAssembly project
│       ├── Services/         # Web-specific service implementations
│       └── wwwroot/          # Static assets, CSS, JS
├── tests/                    # Test projects
└── Kairos.sln               # Solution file
```

## Getting Started

### Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

### Running the Web Application

```bash
# Navigate to the web project
cd src/Kairos.Web

# Run the application
dotnet run
```

The application will be available at `https://localhost:5001` or `http://localhost:5000`.

### Building for Production

```bash
# Build the Web application
cd src/Kairos.Web
dotnet publish -c Release
```

## Versioning

This repository uses `Nerdbank.GitVersioning` for assembly and build version metadata.

- The version source of truth is [version.json](version.json).
- The package is applied solution-wide through [Directory.Build.props](Directory.Build.props).
- The app's Settings page shows the generated build version.

The developer and release workflow is documented in [docs/versioning.md](docs/versioning.md).

## Supabase Sync Setup

To enable Supabase synchronization, you need a Supabase project.

1. Go to your Supabase project dashboard
2. Navigate to Project Settings -> API
3. Copy the Project URL and the `anon` `public` key
4. Configure them either in the app's Settings under the Supabase tab, or by adding them to `src/Kairos.Web/wwwroot/appsettings.json`:

```json
{
  "Supabase": {
    "Url": "https://your-project.supabase.co",
    "AnonKey": "your-anon-key"
  }
}
```

## Data Storage

Data is stored locally in the browser's `localStorage` for offline use and quick access. Additionally, it synchronizes with a cloud database (Supabase) to ensure your data is backed up and accessible across devices when signed in.

## Author

Alberto Gregorio (https://albertogregorio.com)
