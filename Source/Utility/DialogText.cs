namespace Celeste.Mod.SpeebrunConsistencyTracker.Utility;

public static class DialogText
{
    // Dialog.Clean deletes every {...} but {n} and {break} when the language loads, so a {0} read
    // through it vanishes without an error. Placeholder lines are read raw, through Dialog.Get.
    // Their keys end in _FMT; DialogKeyTests fails a placeholder anywhere else.
    public static string Format(string fmtKey, params object[] args)
        => string.Format(Dialog.Get(fmtKey), args);

    public static string Count(int n, string oneKey, string manyFmtKey)
        => n == 1 ? Dialog.Clean(oneKey) : Format(manyFmtKey, n);

    public static string LabelValue(string label, object value)
        => Format(DialogIds.ChartLabelValueFmt, label, value);
}
