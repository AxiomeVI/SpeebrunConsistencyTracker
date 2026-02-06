# SpeebrunConsistencyTracker

A Celeste mod designed to help speedrunners focus on measuring and improving consistency. Track segment and room times with real-time statistics to emphasize repeatability over PBs

## High level Features

- Toggleable HUD that shows real-time stats at the end of each completed attempts
- Scatter Plots and Histograms: Visualize your times distribution for individual rooms and the whole segment
- Generate data exports including metrics for segments, individual rooms, and practice session history (csv format)

## Available Metrics

- History: chronological history of session times
- Success Rate: (segment only) percentage of runs finishing within the target time
- Dnf Count: number of runs that did not finish (for rooms: number of DNFs occurring in that room)
- Completed Run Count: number of runs that did finish (for rooms: number of runs that cleared the room)
- Total Run Count: dnf count + completed run count
- Average: average time across all completed runs
- Median: middle value of the run time distribution
- Reset Rate: the ratio of dnf runs over the total number of runs
- Reset Share: (Rooms only), the contribution of this room in total number of reset
- Best: fastest recorded time
- Worst: slowest recorded time
- Standard Deviation: measure of how spread out the run times are around the average
- Coefficient Of Variation: Standard Deviation as a percentage of the average, allowing easier comparison across different segments / rooms
- Percentile: n% of your runs were faster than the selected value for n
- Interquartile Range: the lower and upper bound of the middle 50% of your runs (first and third quarter basically)
- Trend Slope: measures how session duration affects performance. Values closer to zero indicate little effect, while negative values indicate that your times tend to improve as the session progresses, whereas positive values indicate the opposite
- SoB: Sum of Best
- Median Absolute Deviation: measure of how spread out the run times are around the median
- Consistency Score: Composite metric estimating how consistent times are. Tighter distributions, times closer to the best, and fewer resets result in a higher score.
- Multimodal Test: detects multiple peaks in the time distribution. Multiple peaks usually indicate hit-or-miss strat in the room
- Room Dependency: measures how a poor time in a room impacts the next room, ranging from -1 to 1. a value of 0 indicates no effect, while 1 means a bad time always leads to a bad time in the following room and -1 means the opposite.

## Limitations

- Multiple save states are not supported