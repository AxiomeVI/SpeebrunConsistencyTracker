using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Celeste.Mod.SpeebrunConsistencyTracker;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Sessions;
using Celeste.Mod.SpeebrunConsistencyTracker.Enums;
using Celeste.Mod.SpeebrunConsistencyTracker.Export.Metrics;
using Celeste.Mod.SpeebrunConsistencyTracker.Integration;
using Celeste.Mod.SpeebrunConsistencyTracker.Metrics;
using Celeste.Mod.SpeebrunConsistencyTracker.SessionManagement;
using Xunit;

namespace SpeebrunConsistencyTracker.UnitTests;

// The CSV export and the overlay text, byte for byte, for fixed sessions and settings. The
// expected text is Golden/metric-output.txt: a change to it is a change a player sees.
// To rewrite it after an intended change, run with SCT_UPDATE_GOLDEN=1 and review its diff.
//
// Both outputs read the module's settings, SessionManager.RoomCount and Srt.Tool, all static, so
// this class shares a collection with SessionManagerTests and the two never run in parallel.
[Collection(StaticStateCollection.Name)]
public sealed class MetricOutputGoldenTests : IDisposable
{
    private static readonly string GoldenPath = Path.Combine(SourceDirectory(), "Golden", "metric-output.txt");

    private readonly ISpeedrunTool _previousTool = Srt.Tool;

    public MetricOutputGoldenTests()
    {
        Srt.Tool = new StubSpeedrunTool { TimerType = RoomTimerMode.NextRoom, NumberOfRooms = 3 };
        SessionManager.ClearAll();
    }

    public void Dispose()
    {
        SetModuleSettings(null);
        SessionManager.ClearAll();
        MetricsExporter.Clear();
        MetricEngine.Clear();
        Srt.Tool = _previousTool;
    }

    // Every metric on, for both outputs, with a target so Success Rate is computed.
    private static SpeebrunConsistencyTrackerModuleSettings AllOn()
    {
        var settings = new SpeebrunConsistencyTrackerModuleSettings();
        foreach (PropertyInfo p in typeof(SpeebrunConsistencyTrackerModuleSettings)
                     .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            if (p.PropertyType == typeof(MetricOutputChoice)) p.SetValue(settings, MetricOutputChoice.Both);
        }
        settings.ResetShare = true;
        settings.MultimodalTest = true;
        settings.RoomDependency = true;
        settings.BestSplit = true;
        settings.PercentileValue = PercentileChoice.P90;
        settings.Minutes = 1;
        settings.Seconds = 1;
        return settings;
    }

    // One reset in room 1, and nothing completed: every "no data" path that still exports a CSV.
    private static PracticeSession OnlyAReset() => TrajectorySessions.Of(3, MetricFunctionsTests.Ms(10000));

    private static IEnumerable<(string Name, Func<PracticeSession> Session, Func<SpeebrunConsistencyTrackerModuleSettings> Settings)> Cases()
    {
        yield return ("main, every metric on", MetricFunctionsTests.Hand, AllOn);
        yield return ("main, default settings", MetricFunctionsTests.Hand, () => new SpeebrunConsistencyTrackerModuleSettings());
        yield return ("one completed run, every metric on", MetricFunctionsTests.OneCompletedRun, AllOn);
        yield return ("only a reset, every metric on", OnlyAReset, AllOn);
        yield return ("empty, every metric on", MetricFunctionsTests.Empty, AllOn);
    }

    private static string Render()
    {
        var text = new StringBuilder();
        foreach ((string name, Func<PracticeSession> makeSession, Func<SpeebrunConsistencyTrackerModuleSettings> makeSettings) in Cases())
        {
            PracticeSession session = makeSession();
            SetModuleSettings(makeSettings());
            SetRoomCount(session.RoomCount);
            MetricsExporter.Clear();
            MetricEngine.Clear();

            text.Append("=== ").Append(name).Append(": csv\n");
            text.Append(MetricsExporter.ExportSessionToCsv(session)).Append('\n');
            text.Append("=== ").Append(name).Append(": overlay\n");
            Assert.True(MetricsExporter.RefreshTextOverlayIfNecessary(session, out List<string> lines));
            foreach (string line in lines) text.Append(line).Append('\n');
        }
        return text.ToString();
    }

    [Fact]
    public void The_csv_and_the_overlay_text_match_the_golden_file()
    {
        string actual = Render();
        if (Environment.GetEnvironmentVariable("SCT_UPDATE_GOLDEN") == "1")
            File.WriteAllText(GoldenPath, actual);

        string expected = File.ReadAllText(GoldenPath).Replace("\r\n", "\n");
        Assert.Equal(expected, actual);
    }

    // SpeebrunConsistencyTrackerModule.Settings reads Instance._Settings. Everest builds the
    // module; a test only needs an object that holds the settings, so no constructor runs.
    private static void SetModuleSettings(SpeebrunConsistencyTrackerModuleSettings settings)
    {
        SpeebrunConsistencyTrackerModule module = null;
        if (settings != null)
        {
            module = (SpeebrunConsistencyTrackerModule)RuntimeHelpers.GetUninitializedObject(typeof(SpeebrunConsistencyTrackerModule));
            module._Settings = settings;
        }
        typeof(SpeebrunConsistencyTrackerModule).GetProperty(nameof(SpeebrunConsistencyTrackerModule.Instance))!
            .SetValue(null, module);
    }

    // With no current session, RoomCount is what was last counted. The CSV takes its room rows from it.
    private static void SetRoomCount(int roomCount) =>
        typeof(SessionManager).GetProperty(nameof(SessionManager.RoomCount))!.SetValue(null, roomCount);

    private static string SourceDirectory([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;
}

// Test classes that write process-wide statics (SessionManager, Srt.Tool, the module instance)
// join this collection, which xUnit runs one class at a time.
[CollectionDefinition(Name)]
public sealed class StaticStateCollection
{
    public const string Name = "Static state";
}
