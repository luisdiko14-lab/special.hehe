# NEXUS — PC Command Center

**Project Nexus + GameHub + Sentinel + Windows Toolbox** in one native Windows desktop application, written in C# with Windows Forms and .NET 10 LTS.

Nexus is designed as a lightweight control center: monitor your PC, keep your games organised, and audit files from one dark, unified interface.

## Modules

### ◈ Project Nexus — System dashboard
- Live CPU utilization using Windows system counters
- Actual physical RAM usage and installed memory
- Used space on the Windows system drive
- Live network download and upload rates
- Windows uptime and basic system information
- Shortcuts to Task Manager and Resource Monitor

### ▦ GameHub — Game library
- Add Windows game executables to a local library
- Launch games directly from Nexus
- Search, remove and open a game's containing folder
- Track launch count, last-played date and playtime while Nexus is running
- Preserve library data between launches

GameHub stores its library at:

`%LOCALAPPDATA%\Nexus\games.json`

Removing an entry only removes it from the Nexus library. It never deletes the game itself.

### ⚙ Windows Toolbox — Process manager and system shortcuts
- Live process list with PID, memory usage, window title and executable path
- Search processes by name, PID or window title
- End a selected process with a confirmation prompt and protected-process guard
- Open the selected executable in File Explorer
- Quick shortcuts to Services, Device Manager, Startup folder and Disk Cleanup
- Auto-refreshes process information every five seconds

### ⬡ Sentinel — File integrity audit
- Choose a folder and create a SHA-256 inventory
- Identify files with identical hashes
- Show file sizes, modification times and basic file categories
- Skip inaccessible files and avoid following directory junctions/symbolic links
- Export the audit to CSV
- Limit each scan to 12,000 files to keep the operation bounded

**Security note:** Sentinel is a file-integrity and inventory tool, not an antivirus engine. A hash or file extension does not establish whether a file is malicious. Keep Microsoft Defender or another trusted endpoint-protection product enabled.

## Get the Windows EXE

Every push to `main`, pull request to `main`, version-tag push and manual workflow dispatch can run the Windows build.

1. Open the repository's **Actions** tab.
2. Select **Build Nexus Windows EXE**.
3. Open a successful run.
4. Download the **Nexus-Windows-x64** artifact.
5. Extract it and run `Nexus.exe`.

The workflow publishes a **self-contained, single-file Windows x64 executable**. Every successful build on `main` also commits the latest binary to `dist/Nexus.exe`, so you can grab it directly from the repository without opening Actions. The .NET runtime does not need to be installed separately on the target PC.

- Direct EXE: [`dist/Nexus.exe`](dist/Nexus.exe)
- ZIP / build artifact: available from the successful Actions run.

Pushing a version tag such as `v1.0.0` also creates a GitHub Release with the ZIP attached.

## Build locally

Requirements: Windows and the .NET 10 SDK.

Run from the repository root:

```powershell
dotnet run --project Nexus.Desktop/Nexus.Desktop.csproj
```

Publish a self-contained EXE:

```powershell
dotnet publish Nexus.Desktop/Nexus.Desktop.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:PublishTrimmed=false
```

The published executable is in:

`Nexus.Desktop/bin/Release/net10.0-windows/win-x64/publish/Nexus.exe`

## Technology

- C# / .NET 10 LTS (Nexus Desktop 1.1.0)
- Windows Forms
- Windows kernel APIs for CPU and physical-memory metrics
- .NET networking APIs for throughput sampling
- SHA-256 for file inventory
- GitHub Actions on `windows-latest` for automated publishing

## Repository layout

```text
Nexus.Desktop/
  Models/
  Modules/
    OverviewPage.cs
    GameHubPage.cs
    SentinelPage.cs
  Services/
    SystemMetricsService.cs
    GameLibraryService.cs
    SentinelScanner.cs
  NexusForm.cs
  Theme.cs
  Program.cs
  Nexus.Desktop.csproj

.github/
  workflows/
    build-windows-exe.yml
```
