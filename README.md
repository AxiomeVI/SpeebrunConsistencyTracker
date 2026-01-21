# SpeebrunConsistencyTracker

A mod designed to help speedrunners measure consistency during practice. This mod tracks segment times and displays real-time statistics so you can focus on repeatability and consistency, not just PBs

## Tracked Statistics

- History of completed runs during the session
- Average
- Median (calculated without interpolation)
- Best
- Worst
- Standard Deviation (may be slightly inaccurate due to rounding errors)
- Percentile (calculated without interpolation)
- Number of completed runs
- Completion Rate (a run is considered incomplete if the timer started but the endpoint was never reached)
- Success Rate (a run is considered successful if the endpoint is reached within the target time)
- Linear Regression (the slope of the line using Least Square method)