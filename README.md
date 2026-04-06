# NativeCal

NativeCal is a **local-first native Windows calendar app** built with **WinUI 3** and **SQLite**.

It focuses on a polished desktop experience with fast calendar navigation, multiple views, editable events, multiple calendars, built-in holiday calendars, and solid regression coverage for the non-UI logic.

![NativeCal screenshot](./Native-Cal-windows.png)

## Features

- **Month, Week, Day, and Agenda views**
- **Multiple calendars** with color coding and visibility toggles
- **Event create / edit / delete** flows
- **All-day and timed events**
- **Drag-and-drop / resize interactions** in the calendar UI
- **Built-in US and Canada holiday calendars**
- **Theme settings**: System, Light, Dark
- **First day of week** setting: Sunday, Monday, or Saturday
- **Default reminder** setting for new events
- **Localized time / weekday formatting** based on the active culture
- **SQLite-backed local storage** with no account required

## Tech Stack

- **.NET 10**
- **WinUI 3 / Windows App SDK**
- **CommunityToolkit.Mvvm**
- **sqlite-net-pcl**
- **xUnit** for regression tests

## Project Structure

```text
NativeCal/         Main WinUI desktop application
NativeCal.Tests/   Unit and regression tests
ScreenCap/         Utility project
clicker/           Utility project
```

## Getting Started

### Requirements

- Windows
- .NET 10 SDK
- x64 environment

### Restore

```bash
dotnet restore Native-Cal.sln
```

### Build

```bash
dotnet build Native-Cal.sln --nologo
```

### Run

```bash
dotnet run --project NativeCal/NativeCal.csproj
```

### Test

```bash
dotnet test Native-Cal.sln --nologo
```

## Notes

- App data is stored locally in SQLite under the user profile.
- Holiday data is fetched from the public **Nager.Date** API.
- Recurrence selection currently exists in the UI, but full recurrence expansion is not implemented yet.
- Tests focus on models, helpers, services, and view-model behavior.

## Recent Polish Work

This repo includes fixes and regression coverage for:

- all-day event range handling
- calendar deletion invariants
- agenda and month multi-day overlap handling
- reminder/default-draft behavior
- localization-sensitive date/time display
- reserved holiday calendar name protection
- stale update protection in the data layer

## License

This project is licensed under **0BSD (Zero-Clause BSD)**.

That means people can use, copy, modify, publish, and distribute it with essentially no restrictions, subject to the standard no-warranty disclaimer.

See [LICENSE](./LICENSE).
