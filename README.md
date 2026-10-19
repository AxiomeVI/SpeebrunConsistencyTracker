# Speebrun Stats Tracker

A Celeste mod that gives speedrunners statistics on their practice. It tracks segment and room times, shows them as real-time stats and charts, and points at the rooms or segments that need more work.

## High-level Features

- Real-time HUD with statistics displayed after each completed attempt
- Interactive charts: hover tooltips, pinned data points, zoom and pan, a help card on each chart, and Save PNG
- A small live chart of the current run that stays on screen while you play (the trajectory widget)
- Support for SpeedrunTool multiple save state slots
- Data export to clipboard and CSV
- Configurable hotkeys (keycombo behavior like SRT)

## Setup

The mod reads its data from SpeedrunTool's room timer, so two of SpeedrunTool's own settings
decide what it records. Both are under *Mod Options -> Speedrun Tool -> Room Timer*:

- **Room Timer** — `NextRoom` or `CurrentRoom`. With the timer **Off**, nothing is recorded and
  every chart stays empty.
- **Number of Rooms** — how many rooms one attempt spans. This is the segment the metrics and
  charts are computed over; changing it reshapes the current session's view of the same data.

SpeedrunTool `3.26.4` or later is required (`everest.yaml` pins it).

## Usage

### 1. Practice Workflow

* **Set a Save State:** Starting a new practice session. Creating or clearing a save state will reset all current session data for that slot
* **Run the Segment:** Practice the segment as you usually do
* **Review Performance:** After every completed run, a customizable text overlay displays your session statistics. Cycle through performance charts in-game via your configured keybinds or the navigation arrows on the chart overlay

### 2. Real-Time Feedback & Overlays

Configure the overlay to display the metrics that matter most to your current goals. Each metric is switched on separately for the overlay (the **Stats overlay** page in Mod Options) and for exports (the **Export options** page):

* **Target Time Tracking:** Define a goal time for the segment, typed on a keypad or pasted from the clipboard, and track your **Success Rate** in real-time
* **Live Charts:** Cycle through performance charts for the entire segment or individual rooms. On the scatter plot, drag the target line to change the target time
* **Trajectory Widget:** Switch it on from the Run Trajectory chart, then move and resize it. It draws the current run against your average, your best run or your sum of bests

### 3. Exporting

* **Data Export:** Export your complete session history and statistics to the clipboard or to CSV (files are saved to `SCT_Exports/<level>/`, next to your `Mods` folder — the Celeste installation folder in a normal install)
* **Chart Images:** Save PNG on any chart writes an image of it to the same folder

## Charts

All charts are accessible in-game via keybinds and can be individually toggled on the **Charts** page of Mod Options, where their ten colors and the fill opacity can be changed too.

- **Scatter Plot:** outliers and time clusters per room
- **Box Plot:** time distribution as box-and-whisker with hover details
- **Histogram Wall:** a time histogram for the segment and one per room; click a tile to open it
- **Reset Rate per Room:** reset rate and run survival rate by room
- **Problem Rooms:** combined reset rate and time-loss per room, with a slider for the time-loss threshold
- **Time Loss per Room:** median and average loss vs. session best
- **Run Trajectory:** cumulative deviation across rooms, measured from the average, the best run or the sum of bests, with best-split comparison

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
