# Metrics Reference

Metrics are computed per room and per segment. They are available in the text overlay and in data exports. Export-only metrics appear in CSV and Google Sheets exports but not in the in-game overlay.

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
- **Interquartile Range:** the difference between the 3rd and the 1st quartile
- **Trend Slope:** measures how session duration affects performance. Values near zero indicate little effect; negative values indicate improving times as the session progresses
- **SoB:** Sum of Best
- **Median Absolute Deviation:** measure of how spread out run times are around the median
- **Relative Median Absolute Deviation:** MAD as a percentage of the median
- **Bimodal Test:** detects multiple peaks in the time distribution, indicating a hit-or-miss strat. The Bimodality Coefficient is compared to a critical threshold of 0.555 — higher values point toward bimodality, lower values toward unimodality
- **Room Dependency:** measures how a poor time in a room impacts the next room, ranging from -1 to 1. A value of 0 indicates no effect, a high positive score suggests mistakes tend to carry over
