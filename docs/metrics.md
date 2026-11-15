# Metrics Reference

Metrics are computed per room and per segment. They are available in the text overlay and in data exports. Each metric has two switches in Mod Options, one on the **Stats overlay** page and one on the **Export options** page, so it can be shown, exported, both or neither. Export-only metrics appear in CSV exports but not in the in-game overlay.

A dash (`-`) means the session holds too little to compute the value yet: no completed run for a segment time, no time in a room, or too few runs (Standard Deviation, Relative Standard Deviation and Trend Slope need two runs, the Multimodal Test and Room Dependency ten; Median Absolute Deviation, Relative Median Absolute Deviation and Interquartile Range show 0 with one run). A count is never a dash: no runs is 0. A cell is left empty only where the metric has no value by definition, such as Success Rate for a room.

**The segment** is SpeedrunTool's room timer segment. A room is a split: entering a room other than the one the last split entered (going back into an earlier room counts), the split that stops the timer, or a split SpeedrunTool makes without a room change, for a heart, a cassette or a summit flag. Staying on an end point or in a finished level adds nothing. Without an end point the segment is SpeedrunTool's *Number of Rooms*. With an end point (an end point you placed, or a room-id end point) it is the rooms up to and including the split on the end point, as long as the most recent run that reached it, and *Number of Rooms* plays no part. A run that reached the end point, or ended the level, is finished: its reload is not a reset, and rooms it runs past the end are outside the segment.

- **History:** every run time from the session, in order
- **Success Rate:** (segment only) how often you finish within your target time, as a share of your *completed* runs, not of every attempt
- **Reset Count:** how many runs you abandoned (for rooms: how many times you reset in that room). Deleting a room time from a finished run does not make it a reset
- **Completed Run Count:** how many runs you finished (for rooms: how many runs made it through that room)
- **Total Run Count:** every attempt, whether you finished or reset
- **Average:** your typical time, pulled toward outliers
- **Median:** your middle time, half your runs are faster, half are slower. Less affected by outliers than average
- **Reset Rate:** how often you reset, as a percentage of all attempts
- **Resets in the first room** depend on SpeedrunTool's Room Timer setting:
  - `CurrentRoom`: the first room is R1. Reloading a save state after the timer ran in it is a reset there, and the attempt counts.
  - `NextRoom`: the room before the first transition is not in the segment. A reload there is no reset and no attempt. R1 is the room after it.
  - Either way, a reload before the timer ran is not recorded. Nothing was played, so it is not a reset.
- **Reset Share:** (rooms only) what portion of all your resets happen in this room. The segment column is left empty: a share of the whole is not a statistic
- **Best:** your fastest time
- **Best Split:** (export only) for each room, the fastest you have ever reached that point across all your attempts. Unlike Sum of Bests, these are real paces from actual runs, not a mix of different runs' best individual rooms: a run stops counting at its first deleted room time
- **Worst:** your slowest time
- **Gold Rate:** how often you match your best time in a room or segment. A gold here is a time equal to your best *this session*, not your all-time best: the reference resets with the session
- **Standard Deviation:** how consistent your times are, low means you're getting similar results every run, high means your times vary a lot
- **Relative Standard Deviation:** same as Standard Deviation but as a percentage, so you can fairly compare consistency across rooms of different lengths
- **Percentile:** the time that beats n% of your runs (default: 90th, meaning 90% of your runs were faster than this)
- **Interquartile Range:** the gap between your 25th and 75th percentile times, the spread of your "typical" runs, ignoring outliers at both ends
- **Trend Slope:** whether you're getting faster or slower as the session goes on. Negative means improving, positive means fading, near zero means no clear trend
- **SoB:** Sum of Bests — the theoretical fastest segment if you combined your best time in every individual room. Not necessarily achievable in a single run. A room's best, and its average, count **every completed time of that room, whatever happened earlier in the run**: a room time you deleted from a run does not make that run's later rooms less real. The Run Trajectory chart reads the same times. A room with no time at all adds nothing, and ends the Trajectory's SoB line
- **Median Absolute Deviation:** how far your typical run strays from the median, a more outlier-resistant version of Standard Deviation
- **Relative Median Absolute Deviation:** same as MAD but as a percentage, for cross-room comparison
- **Multimodal Test:** (export only) detects whether you have two distinct clusters of times, a sign of a hit-or-miss strat with a fast and slow outcome
- **Room Dependency:** (export only) how much a bad room carries over into the next one. High means mistakes tend to snowball; near zero means each room is independent
