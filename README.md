# Kairos

Personal Time Budgeting and Accounting

## Live Version

Available at https://albertogregorio.com/kairos/

## Overview

Kairos is a time-tracking application built with Blazor WebAssembly. It helps you track how you spend your time using customizable activities, giving you a balance sheet of your time budget.

## Features

### Time Tracking with Activities
- **Customizable Activities**: Create multiple time trackers with custom names, metadata, emojis, and colors
- **Activity Comments**: Add an optional descriptive comment when starting an activity
- **One-Touch Activation**: Tap an activity to start/stop tracking time
- **Real-time Duration**: See live updates of running duration

### Statistics
- **Data Visualization**: Understand your time distribution with charts and visual styling
- **Budgets**: Set rolling budgets (daily, weekly, monthly) for individual activities

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
- **Advanced Options**: Configure activity groups, sticky events duration, auto-deletion of short events, and toggle Timeular integration
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
│   ├── Kairos.Core/         # Domain models and core entities
│   ├── Kairos.Application/  # Business logic and service contracts
│   ├── Kairos.Infrastructure/ # Supabase and browser-specific implementations
│   ├── Kairos.App/          # Razor UI (pages, components, layout, static assets)
│   └── Kairos.Web/          # Blazor WebAssembly host/composition root
│       └── wwwroot/         # Host static assets and bootstrap files
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

The application will be available at `http://localhost:5111`.

### Building for Production

```bash
# Build the Web application
cd src/Kairos.Web
dotnet publish -c Release
```

## Versioning

This repository uses `Nerdbank.GitVersioning` (NBGV) for automatic assembly and build version metadata derived from git history.

**Version Source of Truth**: [version.json](version.json) at the repository root

- The `version` field defines the next release line.
- Builds from `master` are treated as public-release branches.
- Tags matching `v1.2.3` are also treated as public releases.

**Configuration**: [Directory.Build.props](Directory.Build.props) applies NBGV solution-wide, so individual project files should not manually set `Version`, `AssemblyVersion`, `FileVersion`, or `InformationalVersion`.

**Automatic Versioning**:
- Builds from `master` use the stable release line from [version.json](version.json)
- Builds from other branches remain unique and traceable through git-derived prerelease metadata
- The app's Settings page displays the generated build version

**Bumping the Version**: When starting a new release line, update the `version` field in [version.json](version.json):
- `1.1` for the next minor release
- `2.0` for the next major release

Commit this change before producing release builds so version height is calculated from the correct point in history.

**Release Workflow**:
1. Merge intended changes
2. Update [version.json](version.json) if moving to a new release line
3. Build from `master`
4. Optionally create a matching git tag (e.g., `v1.0.0`)

## Supabase Sync Setup

To enable Supabase synchronization, you need a Supabase project.

Apply the SQL migrations in the `supabase/` folder (including `activity_events.sql`) using the Supabase SQL Editor before enabling sync.

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
