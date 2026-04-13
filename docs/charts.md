# Charts Reference

All charts are accessible in-game via keybinds and can be individually toggled in the settings menu. Use the navigation arrows on the chart overlay or your configured keybinds to cycle between them.

## Scatter Plot

Displays all room times and segment times as individual dots, organized by room column. Useful for spotting outliers and seeing how times cluster within each room. Enabled by default.

## Room Histogram

A time distribution histogram for a single room. Shows how frequently each time range occurs, making it easy to see whether your times cluster tightly or spread out. Cycles through each room individually. Disabled by default.

## Segment Histogram

Same as the room histogram but for the full segment time. Enabled by default.

## DNF % per Room & Segment Survival Rate

A grouped bar chart with two series per room: the DNF rate (percentage of all attempts that reset in that room) and the survival rate (percentage of attempts still alive after passing through that room). The survival bar shows how many runs made it through. Enabled by default.

## Problem Rooms

A stacked bar chart combining DNF % and time-loss % per room. The time-loss portion highlights rooms where you frequently lose significant time over your gold, based on a configurable threshold. Useful for identifying rooms that need practice. Disabled by default.

## Room Inconsistency

A normalized stacked bar chart ranking rooms by inconsistency from worst to best. Uses two complementary metrics: Relative Median Absolute Deviation (RMAD) and Relative Standard Deviation (RStdDev) to capture overall spread (with some resistance to outliers). The worst room fills the full bar height, all the others are shown proportionally. Disabled by default.

## Time Loss per Room

A grouped bar chart showing median and average time lost per room relative to your gold time in that room, allowing quick comparison between typical loss (median) and overall loss (average). Disabled by default.

## Run Trajectory

A line chart where each attempt is drawn as a line showing cumulative deviation from the per-room average. Lines go up when a room is faster than average and down when slower. The X axis represents the cumulative sum of per-room averages (a run that matches the average in every room follows it exactly). Older attempts are drawn in dark grey and fade toward white as they approach the most recent run, making it easy to see how your trajectory has evolved over the session. Your best attempt, your most recent attempt, and the Sum of Best are highlighted. A comparison line shows how the current run tracks against the best recorded split time per room. Disabled by default.

## Box Plot

Shows the statistical distribution of room times as a box-and-whisker plot, with rooms on the left and the full segment on the right (each on its own Y axis). For each column: the whisker spans min to max, the box covers the interquartile range (Q1–Q3), and the white line marks the median. Hover over a column to see the median, IQR bounds, whiskers, and any outlier values. Disabled by default.
