# SpeebrunConsistencyTracker

A mod designed to help speedrunners focus on measuring and improving consistency. Track segment and room times with real-time statistics to emphasize repeatability over PBs

## How to use

Set a target time as your consistency benchmark. The quickest method: copy a time value (e.g., from Astro's reference sheet) and click "Import target time from clipboard" in the mod settings.

Practice your segment, no resets allowed for proper consistency training!

The default in-game overlay displays key information during your practice (you can customize it in the mod options)

After your session, export statistics to clipboard and paste into your preferred spreadsheet. By default, two CSV tables are exported: session history and session statistics

## Statistics

Both segment and individual room data are tracked

Available metrics:

- Average
- Median
- Best / Worst times
- SoB
- Standard Deviation
- Coefficient of Variation: standard deviation normalized by the mean allowing easier comparison across different segments / rooms
- Percentile: P90 indicates 90% of runs were faster than this value. You can set your own percentile in the mod options
- Completed run count
- Completion Rate: percentage of runs reaching the endpoint (or room)
- Share of Resets: the contribution of each room to the total reset count
- Success Rate: percentage of runs finishing within target time
- Linear Regression slope: measures how session duration affects performance. Values closer to zero indicate little effect, while negative values indicate that your times tend to improve as the session progresses, whereas positive values indicate the opposite

## Limitations

- Multiple save states are not supported
- SRT flag endpoint timing is off by 1 frame
- In-game overlay doesn't show room stats

## Details

- Median and percentile are calculated without interpolation
- Linear Regression uses Least Square method