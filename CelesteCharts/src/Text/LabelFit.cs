#nullable enable

namespace Celeste.Mod.CelesteCharts;

/// <summary>What to do when a row of labels under bands would not fit.</summary>
internal enum LabelFit {
    /// <summary>Keep every Nth label: bin labels, where the missing ones are obvious.</summary>
    Thin,

    /// <summary>Shorten each label to its band with an ellipsis: room names, which must all be there.</summary>
    Shorten,

    /// <summary>
    ///     Put every other label one line lower, as SpeebrunConsistencyTracker does, when each row then
    ///     fits; otherwise thin. Needs <c>XLabelHeight(..., rows: 2)</c> below the plot.
    /// </summary>
    Stagger,
}
