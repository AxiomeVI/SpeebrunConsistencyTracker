# SpeebrunConsistencyTracker

A Celeste mod built for speedrunners to analyze consistency and pinpoint specific rooms or segments that require further practice. Track segment and room times with real-time statistics to emphasize repeatability over PBs.

## High-level Features

- HUD that shows real-time stats at the end of each completed attempt
- Multiple chart types to visualize time distributions, DNF patterns, inconsistency, time loss, and run trajectories
- Generate data exports including metrics for segments, individual rooms, and practice session history (CSV format)

## Usage

### 1. Training Workflow

* **Set a Save State:** Starting a new training session. Creating or clearing a save state will reset all current session data
* **Run the Segment:** Practice the segment as you usually do. To maintain data integrity, **make sure that the "current room / next room" SpeedrunTool setting is properly configured**
* **Review Performance:** After every completed run, a customizable text overlay displays your session statistics. You can also view various performance charts in-game via your configured keybinds (my personal recommendation is to use the default menu directions to cycle through them)

### 2. Real-Time Feedback & Overlays

Configure the overlay to display the metrics that matter most to your current goals:

* **Target Time Tracking:** Define a goal time for the segment and track your **Success Rate** in real-time
* **Live Charts:** Cycle through performance charts for the entire segment or individual rooms using your configured keybinds

### 3. Exporting

* **Data Export:** Export your complete session history and statistics to CSV (exported files are saved to the `/SCT_Exports` directory within your Celeste installation folder)

## Charts

All charts are accessible in-game via keybinds and can be individually toggled in the settings menu.

### Scatter Plot
Displays all room times and segment times as individual dots, organized by room column. Useful for spotting outliers and seeing how times cluster within each room. Enabled by default.

### Room Histogram
A time distribution histogram for a single room. Shows how frequently each time range occurs, making it easy to see whether your times cluster tightly or spread out. Cycles through each room individually. Disabled by default.

### Segment Histogram
Same as the room histogram but for the full segment time. Enabled by default.

### DNF % per Room & Segment Survival Rate
A grouped bar chart with two series per room: the DNF rate (percentage of all attempts that reset in that room) and the survival rate (percentage of attempts still alive after passing through that room). The survival bar shows how many runs made it through. Enabled by default.

### Problem Rooms
A stacked bar chart combining DNF % and time-loss % per room. The time-loss portion highlights rooms where you frequently lose significant time over your gold, based on a configurable threshold. Useful for identifying rooms that need practice. Disabled by default.

### Room Inconsistency
A normalized stacked bar chart ranking rooms by inconsistency from worst to best. Uses two complementary metrics: Relative Median Absolute Deviation (RMAD) and Relative Standard Deviation (RStdDev) to capture overall spread (with some resistance to outliers). The worst room fills the full bar height, all the others are shown proportionally. Disabled by default.

### Time Loss per Room
A grouped bar chart showing median and average time lost per room relative to your gold time in that room, allowing quick comparison between typical loss (median) and overall loss (average). Disabled by default.

### Run Trajectory
A line chart where each attempt is drawn as a line showing cumulative deviation from the per-room average. Lines go up when a room is faster than average and down when slower. The X axis represents the cumulative sum of per-room averages (a run that matches the average in every room follows it exactly). Older attempts are drawn in dark grey and fade toward white as they approach the most recent run, making it easy to see how your trajectory has evolved over the session. Your best attempt, your most recent attempt, and the Sum of Best are highlighted. Disabled by default.

### Box Plot
Shows the statistical distribution of room times as a box-and-whisker plot, with rooms on the left and the full segment on the right (each on its own Y axis). For each column: the whisker spans min to max, the box covers the interquartile range (Q1–Q3), and the white line marks the median. Disabled by default.

## Metrics

- **History:** chronological history of session times
- **Success Rate:** (segment only) percentage of runs finishing within the target time
- **Reset Count:** number of runs that did not finish (for rooms: number of resets occurring in that room)
- **Completed Run Count:** number of runs that finished (for rooms: number of runs that cleared the room)
- **Total Run Count:** Reset count + completed run count
- **Average:** average time across all completed runs
- **Median:** middle value of the run time distribution
- **Reset Rate:** ratio of reset runs over the total number of runs
- **Reset Share:** (rooms only) this room's contribution to the total number of resets
- **Best:** fastest recorded time
- **Worst:** slowest recorded time
- **Gold Rate:** percentage of runs (individual rooms and segment) where gold (best) time was achieved
- **Standard Deviation:** measure of how spread out run times are around the average
- **Relative Standard Deviation:** standard deviation as a percentage of the average, allowing easier comparison across rooms
- **Percentile:** the threshold where n% of runs are faster than this value (default: 90%, adjustable in mod options)
- **Interquartile Range:** lower and upper bounds of the middle 50% of your runs
- **Trend Slope:** measures how session duration affects performance. Values near zero indicate little effect; negative values indicate improving times as the session progresses
- **SoB:** Sum of Best
- **Median Absolute Deviation:** measure of how spread out run times are around the median
- **Relative Median Absolute Deviation:** MAD as a percentage of the median
- **Consistency Score:** composite metric estimating overall consistency. Tighter distributions, times closer to the best, and fewer resets result in a higher score
- **Bimodal Test:** detects multiple peaks in the time distribution, indicating a hit-or-miss strat. The Bimodality Coefficient is compared to a critical threshold of 0.555 — higher values point toward bimodality, lower values toward unimodality
- **Room Dependency:** measures how a poor time in a room impacts the next room, ranging from -1 to 1. A value of 0 indicates no effect, a high positive score suggests mistakes tend to carry over

## Limitations

- Multiple save states are not supported
- Updating current room / next room during an active session will cause inconsistencies in the data