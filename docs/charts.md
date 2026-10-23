# Charts Reference

The mod draws seven charts. Each can be switched on or off on the **Charts** page of Mod Options. The "<" and ">" buttons under the chart, or your configured keybinds, cycle through the enabled charts in the order below.

## Colors

The Charts page sets ten colors, each on a color wheel that also offers the fourteen named colors as presets:

- **Room color** and **Segment color:** the scatter plot, box plot and Time Distributions, including their axis labels
- **Bars, first** and **Bars, second:** the two series of the bar charts (Reset Rate per Room, Problem Rooms, Time Loss per Room)
- **Best run**, **Last run** and **Sum of bests:** the highlighted lines of the Run Trajectory
- **Target line:** the target time on the scatter plot and Time Distributions, red by default
- **Ahead** and **Behind:** the gaps in the Run Trajectory's table and the widget, green and red by default. A paler shade of each marks a room that went the other way: ahead overall but lost time there, or behind but gained. The Ahead and Behind wheels show that shade next to After, and a Preview row under them shows a sample gap in each of the four colors

**Fill opacity** fades the filled shapes only: bars, boxes and dots. Lines and text always draw at full color.

## What every chart shares

- **Subtitle.** One line under the title says what the chart shows.
- **Help card.** The "?" button shows a card that explains how to read the chart and what the mouse does. Hovering shows it, and a click keeps it open. Whether it stays open is saved.
- **Save PNG**, at the top right, writes the chart to `SCT_Exports/<level>/`, beside the CSV exports.
- **Buttons under the chart.** "<" and ">" move to the previous and next chart. At the bottom left sit Reset zoom (while the chart is zoomed), Clear pins and Delete.
- **Pins.** Click a mark to pin it. Clear pins unpins everything. Delete removes the pinned runs from the session.
- **Hiding rooms.** On the charts with a column per room, click a room's label (R1, R2...) to hide it. A thin strip stays in its place: click it to bring the room back.
- **Zoom and pan.** The scatter plot and the run trajectory zoom with the wheel around the mouse and pan by dragging. Reset zoom, or a double click, shows everything again.
- **Sideways scroll.** The scatter plot, box plot and the three bar charts keep every room column at least 96 px wide. With more rooms than fit, the columns scroll sideways: use the wheel over the room labels, or drag the bar under them. On the scatter plot and the box plot the segment column stays pinned on the right.
- **Switches.** A choice on a chart is a caption and a switch under it: "Scale: Absolute | Relative", "Scale: Absolute | Relative | Independent" on the Time Distributions wall, "Median band: On | Off", "Median: On | Off", "Baseline: Average | Best run | SoB", "Widget: On | Off".
- **Saved choices.** These survive a restart: Absolute or Relative on the scatter plot and the box plot, the wall's scale, the scatter plot's "Median band", the histogram's median line, the trajectory's baseline, whether the help card is open, and the last chart shown.

## Scatter Plot

One dot per run, a column per room, and the segment on the right. In a column the oldest run is on the left. A band behind the dots marks the middle half of the times, with a line at the median ("Median band", on by default). Absolute reads the rooms as times. Relative reads each room as a percent of its own median. A target line, red by default, can be dragged up or down to change the target time. Enabled by default.

## Box Plot

The spread of the times in each room and in the segment. A box covers the middle half (Q1 to Q3), its line is the median, and the whiskers reach the fastest and slowest time. Absolute and Relative work as on the scatter plot. Hovering a box shows Max, Q3, Median, Q1 and Min. Clicking pins the numbers. The chart has no outlier values. Disabled by default.

## Time Distributions

A wall with one time histogram for the segment and one for each room. A bar counts the runs that fell in a range of times, and a dashed line marks the median ("Median: On | Off"). Click a tile to open that histogram full size. Back, a right click or a double-click returns to the wall. Scroll or drag to reach the other rooms. Enabled by default.

The Scale switch under the wall sets how the room tiles are read. Absolute and Relative put every room on one shared scale, centred on each room's median, so the widths compare: Absolute in time, Relative as a percent of the median, so short and long rooms can be compared. Runs too far out to fit are counted in the end bar, and a number at the edge says how many. Independent gives each tile its own range, from its fastest to its slowest run: it shows a room's shape, not how it compares. The segment tile always keeps its own range. Independent is the default.

With a histogram open, the previous and next chart buttons and hotkeys go round the histograms instead. The run count follows each title; the segment's counts completed runs only.

## Reset Rate per Room

A grouped bar chart with two series per room: the reset rate (the share of the runs that reached the room that ended there) and the share of all runs that reached it. Enabled by default.

## Problem Rooms

A stacked bar chart per room, counted over the runs that reached it: the share that ended there, and the share that finished it slower than the time-loss threshold over the session best. A "Threshold:" slider under the chart sets it, from 1 to 118 frames; it is the Time loss threshold of Mod Options. Disabled by default.

## Time Loss per Room

A grouped bar chart of the median and the average time lost per room against the session best in that room. A big gap between the two means rare disasters. Disabled by default.

## Run Trajectory

A line chart where each run is one line, read left to right, one room at a time. An Average | Best run | SoB switch under the chart picks the baseline the runs are compared with. Against Average, the middle line is the average run: a line above it is ahead, below it behind. Against SoB, the top line is the sum of bests, and the further below it a run is, the more time it lost. Against Best run, the flat line is your fastest completed run of the session; until one is complete the average is used, and the title says so. Older runs are dark grey and fade toward white for the most recent. The best run, the last run and the sum of bests use their own colors.

Click a line to pin that run. While the columns are wide enough (about 20 rooms), the times stand along the pinned line and a table sits under the chart. Each room shows the gap at the end of the room (cumul) and in that room alone (room). With more rooms, the chart draws dots only and a card for the room under the mouse. Zooming in brings the table back. Click a second line to compare the two runs. "vs Best Split" compares the pinned run with every other run: the Ahead color means the run was ahead of them all. Enabled by default.

### The widget

Under the chart, bottom left, "Widget: On | Off" keeps a small chart of the current run on screen while you play. It draws one line, a room at a time, against the baseline chosen on the chart: in the Ahead color above the middle line where the run is ahead, in the Behind color below it where it is behind. A new run clears it.

Its baseline leaves the current run out, so a new best room shows as a gain. On the first run of a session there is nothing to compare with, and it shows an empty widget with its middle line.

It is hidden while a chart is open. Switched on while it is still where it ships, it starts in Edit mode, to show it can be moved. To move or resize it later, click Edit mode, beside the switch: it shows over the Run Trajectory chart with a frame. Drag its middle to move it, an edge or a corner to resize it. Each change is saved as you make it. Click Edit mode again, or close the chart, to hide it.

Mod Options has its on/off switch, its opacity, and Time delta, on by default: the gap to the baseline as a number in the widget's corner.
