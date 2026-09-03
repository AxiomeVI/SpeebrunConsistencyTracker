# SpeebrunConsistencyTracker

A Celeste mod built for speedrunners to analyze consistency and pinpoint specific rooms or segments that require further practice. Track segment and room times with real-time statistics to emphasize repeatability over PBs.

## High-level Features

- Real-time HUD with statistics displayed after each completed attempt
- Interactive charts with hover tooltips, clickable data points, and in-chart navigation arrows
- Support for SpeedrunTool multiple save state slots
- Data export to clipboard and CSV
- Configurable hotkeys (keycombo behavior like SRT)

## Usage

### 1. Practice Workflow

* **Set a Save State:** Starting a new practice session. Creating or clearing a save state will reset all current session data for that slot
* **Run the Segment:** Practice the segment as you usually do
* **Review Performance:** After every completed run, a customizable text overlay displays your session statistics. Cycle through performance charts in-game via your configured keybinds or the navigation arrows on the chart overlay

### 2. Real-Time Feedback & Overlays

Configure the overlay to display the metrics that matter most to your current goals:

* **Target Time Tracking:** Define a goal time for the segment and track your **Success Rate** in real-time
* **Live Charts:** Cycle through performance charts for the entire segment or individual rooms

### 3. Exporting

* **Data Export:** Export your complete session history and statistics to CSV (files are saved to the `/SCT_Exports` directory within your Celeste installation folder)

## Charts

All charts are accessible in-game via keybinds and can be individually toggled in the settings menu.

- **Scatter Plot:** outliers and time clusters per room
- **Room Histogram:** time distribution for a single room
- **Segment Histogram:** time distribution for the full segment
- **DNF % per Room:** reset rate and run survival rate by room
- **Problem Rooms:** combined DNF rate and time-loss per room
- **Time Loss per Room:** median and average loss vs. session best
- **Run Trajectory:** cumulative deviation across rooms, with best-split comparison
- **Box Plot:** time distribution as box-and-whisker with hover details

See [docs/charts.md](docs/charts.md) for full descriptions.

## Metrics

A selection of the available metrics: average, median, standard deviation, sum of best and [many more](docs/metrics.md).

## Building

Requires the .NET 8 SDK.

### With a Celeste install

Clone this repo into the `Mods/` folder of a Celeste installation with Everest. The build
resolves `Celeste.dll`, `FNA.dll`, and `MMHOOK_Celeste.dll` from three directories above the
`Source/` folder, which is where they land when the mod sits at `Mods/SpeebrunConsistencyTracker/`.

You also need `SpeedrunTool.dll` (pinned to version `3.26.4` in `everest.yaml`). Extract it from
`SpeedrunTool.zip` in your `Mods/` folder and place it at `libs/SpeedrunTool.dll`.

Then, from the repo root:

```bash
dotnet build -c Debug
```

`-c Release` additionally packages the mod into `SpeebrunConsistencyTracker.zip` at the repo root.

### Without a Celeste install

Point the build at any folder containing `Celeste.dll`, `FNA.dll`, and `MMHOOK_Celeste.dll` by
overriding `CelestePrefix`:

```bash
dotnet build -c Debug -p:CelestePrefix=/path/to/the/dlls
```

`libs/SpeedrunTool.dll` is still required either way. A relative path for `CelestePrefix` is
resolved against `Source/`, not the repo root — an absolute path avoids the confusion.

## Docs

- [Charts reference](docs/charts.md)
- [Metrics reference](docs/metrics.md)
