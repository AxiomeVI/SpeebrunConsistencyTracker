# Metrics Reference

Metrics are computed per room and per segment. They are available in the text overlay and in data exports. Export-only metrics appear in CSV and Google Sheets exports but not in the in-game overlay.

- **History:** every run time from the session, in order
- **Success Rate:** (segment only) how often you finish within your target time
- **Reset Count:** how many runs you abandoned (for rooms: how many times you reset in that room)
- **Completed Run Count:** how many runs you finished (for rooms: how many runs made it through that room)
- **Total Run Count:** every attempt, whether you finished or reset
- **Average:** your typical time, pulled toward outliers
- **Median:** your middle time, half your runs are faster, half are slower. Less affected by outliers than average
- **Reset Rate:** how often you reset, as a percentage of all attempts
- **Reset Share:** (rooms only) what portion of all your resets happen in this room
- **Best:** your fastest time
- **Best Split:** (export only) for each room, the fastest you have ever reached that point across all your attempts. Unlike Sum of Bests, these are real paces from actual runs, not a mix of different runs' best individual rooms
- **Worst:** your slowest time
- **Gold Rate:** how often you match your personal best time in a room or segment
- **Standard Deviation:** how consistent your times are, low means you're getting similar results every run, high means your times vary a lot
- **Relative Standard Deviation:** same as Standard Deviation but as a percentage, so you can fairly compare consistency across rooms of different lengths
- **Percentile:** the time that beats n% of your runs (default: 90th, meaning 90% of your runs were faster than this)
- **Interquartile Range:** the gap between your 25th and 75th percentile times, the spread of your "typical" runs, ignoring outliers at both ends
- **Trend Slope:** whether you're getting faster or slower as the session goes on. Negative means improving, positive means fading, near zero means no clear trend
- **SoB:** Sum of Bests — the theoretical fastest segment if you combined your best time in every individual room. Not necessarily achievable in a single run
- **Median Absolute Deviation:** how far your typical run strays from the median, a more outlier-resistant version of Standard Deviation
- **Relative Median Absolute Deviation:** same as MAD but as a percentage, for cross-room comparison
- **Bimodal Test:** (export only) detects whether you have two distinct clusters of times, a sign of a hit-or-miss strat with a fast and slow outcome
- **Room Dependency:** (export only) how much a bad room carries over into the next one. High means mistakes tend to snowball; near zero means each room is independent
