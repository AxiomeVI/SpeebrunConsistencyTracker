namespace Celeste.Mod.SpeebrunConsistencyTracker;
public static class DialogIds {
    // Main Menu
    public const string SpeebrunConsistencyTracker = "SCT_SPEEBRUN_CONSISTENCY_TRACKER";
    public const string EnabledId = "SCT_ENABLE_MOD";

    // Export submenu
    public const string SrtExportId = "SCT_EXPORT_WITH_SRT";
    public const string ExportSubMenu = "SCT_EXPORT_SUBMENU";
    public const string ExportModeId = "SCT_EXPORT_MOD";
    public const string ExportPathId = "SCT_EXPORT_PATH";
    public const string SheetExportExplanationId = "SCT_SHEET_EXPLANATION";

    // Hotkey menu UI
    public const string KeybindConfigId       = "SCT_KEYBIND_CONFIG";
    public const string KeybindComboSubId     = "SCT_KEYBIND_COMBO_SUB";
    public const string KeyConfigTitle        = "SCT_KEY_CONFIG_TITLE";
    public const string BtnConfigTitle        = "SCT_BTN_CONFIG_TITLE";
    public const string KeyConfigChanging     = "SCT_KEY_CONFIG_CHANGING";
    public const string BtnConfigChanging     = "SCT_BTN_CONFIG_CHANGING";
    public const string BtnConfigNoController = "SCT_BTN_CONFIG_NO_CONTROLLER";

    // Hotkeys
    public const string KeyStatsExportId = "SCT_KEY_STATS_EXPORT";
    public const string ToggleGraphOverlayId = "SCT_TOGGLE_GRAPH_OVERLAY";
    public const string KeyClearStatsId = "SCT_KEY_CLEAR_STATS";
    public const string KeyImportTargetTimeId = "SCT_KEY_IMPORT_TARGET_TIME";
    public const string KeyNextGraphId = "SCT_KEY_NEXT_GRAPH";
    public const string KeyPreviousGraphId = "SCT_KEY_PREVIOUS_GRAPH";

    // Target Time Menu
    public const string TargetTimeId = "SCT_TARGET_TIME";
    public const string InputTargetTimeId = "SCT_INPUT_TARGET_TIME";
    public const string ResetTargetTimeId = "SCT_RESET_TARGET_TIME";
    public const string TargetTimeFormatId = "SCT_TARGET_TIME_FORMAT";
    public const string Minutes = "SCT_MINUTES";
    public const string Seconds = "SCT_SECONDS";
    public const string Milliseconds = "SCT_MILLISECONDS";
    public const string MillisecondsFirst = "SCT_MILLISECONDS_FIRST";
    public const string MillisecondsSecond = "SCT_MILLISECONDS_SECOND";
    public const string MillisecondsThird = "SCT_MILLISECONDS_THIRD";

    // Popup message
    public const string PopupTargetTimeSetId = "SCT_POPUP_TARGET_TIME_SET";
    public const string PopupInvalidTargetTimeId = "SCT_INVALID_TIME_IMPORT";
    public const string PopupExportToClipboardId = "SCT_EXPORT_TO_CLIPBOARD";
    public const string PopupInvalidExportId = "SCT_INVALID_EXPORT";
    public const string PopupExportToFileId = "SCT_EXPORT_TO_FILE";
    public const string PopupExportToSheetId = "SCT_EXPORT_TO_SHEET";
    public const string PopupDataClearId = "SCT_DATA_CLEAR";
    public const string PopupNoGraphId = "SCT_NO_GRAPH_ERROR";
    public const string PopupFileNotFoundId = "SCT_FILE_NOT_FOUND";

    // Text Overlay Menu
    public const string IngameOverlayId = "SCT_INGAME_OVERLAY";
    public const string OverlayEnabledId = "SCT_OVERLAY_ENABLED";
    public const string TextSizeId = "SCT_TEXT_SIZE";
    public const string TextPositionId = "SCT_TEXT_POSITION";
    public const string TextOrientationId = "SCT_TEXT_ORIENTATION";
    public const string TextOverlayId = "SCT_TEXT_OVERLAY";
    public const string GraphOverlayId = "SCT_GRAPH_OVERLAY";
    public const string TextAlphaId = "SCT_TEXT_ALPHA";

    // Graph Overlay Menu
    public const string GraphEnabledId = "SCT_GRAPH_ENABLED";
    public const string GraphScatterId = "SCT_SCATTER";
    public const string GraphRoomHistogramId = "SCT_ROOM_HISTOGRAM";
    public const string GraphSegmentHistogramId = "SCT_SEGMENT_HISTOGRAM";
    public const string GraphDnfPercentId = "SCT_DNF_PERCENT_BAR_CHART";
    public const string GraphProblemRoomsId = "SCT_PROBLEM_ROOM_BAR_CHART";
    public const string RoomColorId = "SCT_ROOM_COLOR";
    public const string SegmentColorId = "SCT_SEGMENT_COLOR";
    public const string ChartOpacityId = "SCT_CHART_OPACITY";
    public const string TimeLossThresholdId = "SCT_TIME_LOSS_THRESHOLD";
    public const string TimeLossThresholdDescId = "SCT_TIME_LOSS_THRESHOLD_DESC";
    public const string GraphTimeLossId = "SCT_TIME_LOSS_CHART";
    public const string GraphRunTrajectoryId = "SCT_TRAJECTORY_GRAPH";

    // Stats Menu
    public const string StatsSubMenuId = "SCT_STATS_SUBMENU";
    public const string ExportOnlyId = "SCT_EXPORT_ONLY";
    public const string SuccessRateId = "SCT_SUCCESS_RATE";
    public const string ResetRateId = "SCT_RESET_RATE";
    public const string ResetShareId = "SCT_RESET_SHARE";
    public const string AverageId = "SCT_AVERAGE";
    public const string MedianId = "SCT_MEDIAN";
    public const string MadId = "SCT_MAD";
    public const string MinimumId = "SCT_MINIMUM";
    public const string MaximumId = "SCT_MAXIMUM";
    public const string StandardDeviationId = "SCT_STANDARD_DEVIATION";
    public const string CoefficientOfVariationId = "SCT_COEFFICIENT_OF_VARIATION";
    public const string TargetTimeStatId = "SCT_TARGET_TIME_STAT";
    public const string PercentileValueId = "SCT_PERCENTILE_VALUE";
    public const string PercentileId = "SCT_PERCENTILE";
    public const string InterquartileRangeId = "SCT_IQR";
    public const string RunHistoryId = "SCT_RUN_HISTORY";
    public const string SuccessRateSubTextId = "SCT_SUCCESS_RATE_SUBTEXT";
    public const string CompletedRunCountId = "SCT_COMPLETED_RUN_COUNT";
    public const string LinearRegressionId = "SCT_LINEAR_REGRESSION";
    public const string SoBId = "SCT_SOB";
    public const string TotalRunCountId = "SCT_TOTAL_RUN_COUNT";
    public const string DnfCountId = "SCT_DNF_COUNT";
    public const string ConsistencyScoreId = "SCT_CONSISTENCY_SCORE";
    public const string ButtonAllOffId = "SCT_ALL_METRICS_OFF_BUTTON";
    public const string ButtonAllOnId = "SCT_ALL_METRICS_ON_BUTTON";
    public const string AllOnDescId = "SCT_ALL_ON_DESC";
    public const string ButtonResetId = "SCT_RESET_METRICS_BUTTON";
    public const string MultimodalTestId = "SCT_MULTIMODAL_TEST";
    public const string RoomDependencyId = "SCT_ROOM_DEPENDENCY";
    public const string MetricsSubHeaderId = "SCT_METRICS_SUBHEADER";
    public const string RelMadId = "SCT_RELATIVE_MAD";
    public const string GoldRateId = "SCT_GOLD_RATE";

    // Graph Overlay (charts)
    public const string GraphBoxPlotId = "SCT_BOX_PLOT_GRAPH";
}
