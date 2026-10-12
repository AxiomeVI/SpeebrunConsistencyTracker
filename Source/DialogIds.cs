namespace Celeste.Mod.SpeebrunConsistencyTracker;
public static class DialogIds {
    // Main Menu
    public const string SpeebrunConsistencyTracker = "SCT_SPEEBRUN_CONSISTENCY_TRACKER";
    public const string EnabledId = "SCT_ENABLE_MOD";
    public const string ExportNowId = "SCT_EXPORT_NOW";
    public const string ClearSessionId = "SCT_CLEAR_SESSION";
    public const string ClearSessionTitleId = "SCT_CLEAR_SESSION_TITLE";
    public const string ClearSessionMessageId = "SCT_CLEAR_SESSION_MESSAGE";

    // Pages and their shared rows
    public const string BulkHeaderId = "SCT_BULK_HEADER";
    public const string ShowAllId = "SCT_SHOW_ALL";
    public const string HideAllId = "SCT_HIDE_ALL";
    public const string IncludeAllId = "SCT_INCLUDE_ALL";
    public const string IncludeNoneId = "SCT_INCLUDE_NONE";
    public const string DefaultsId = "SCT_DEFAULTS";

    // Export submenu
    public const string SrtExportId = "SCT_EXPORT_WITH_SRT";
    public const string SrtExportDescId = "SCT_EXPORT_WITH_SRT_DESC";
    public const string ExportPageId = "SCT_EXPORT_SUBMENU";
    public const string ExportModeId = "SCT_EXPORT_MOD";
    public const string ExportPathId = "SCT_EXPORT_PATH";

    // Hotkey menu UI
    public const string KeybindConfigId       = "SCT_KEYBIND_CONFIG";
    public const string KeybindComboSubId     = "SCT_KEYBIND_COMBO_SUB";
    public const string KeybindPageComboFmt   = "SCT_KEYBIND_PAGE_COMBO_FMT";
    public const string KeybindClearSubId     = "SCT_KEYBIND_CLEAR_SUB";
    public const string KeybindTimeoutFmt     = "SCT_KEYBIND_TIMEOUT_FMT";

    // Hotkeys
    public const string KeyStatsExportId = "SCT_KEY_STATS_EXPORT";
    public const string KeyStatsExportDescId = "SCT_KEY_STATS_EXPORT_DESC";
    public const string ToggleGraphOverlayId = "SCT_TOGGLE_GRAPH_OVERLAY";
    public const string KeyClearStatsId = "SCT_KEY_CLEAR_STATS";
    public const string KeyClearStatsDescId = "SCT_KEY_CLEAR_STATS_DESC";
    public const string KeyImportTargetTimeId = "SCT_KEY_IMPORT_TARGET_TIME";
    public const string KeyImportTargetTimeDescId = "SCT_KEY_IMPORT_TARGET_TIME_DESC";
    public const string KeyNextGraphId = "SCT_KEY_NEXT_GRAPH";
    public const string KeyPreviousGraphId = "SCT_KEY_PREVIOUS_GRAPH";

    // Target Time Menu
    public const string TargetTimeId = "SCT_TARGET_TIME";
    public const string TargetTimeFormatId = "SCT_TARGET_TIME_FORMAT";
    public const string TargetTimePasteId = "SCT_TARGET_TIME_PASTE";
    public const string TargetTimeClearId = "SCT_TARGET_TIME_CLEAR";

    // Popup message
    public const string PopupTargetTimeSetId = "SCT_POPUP_TARGET_TIME_SET";
    public const string PopupInvalidTargetTimeId = "SCT_INVALID_TIME_IMPORT";
    public const string PopupInvalidTypedTargetTimeId = "SCT_INVALID_TIME_INPUT";
    public const string PopupExportToClipboardId = "SCT_EXPORT_TO_CLIPBOARD";
    public const string PopupInvalidExportId = "SCT_INVALID_EXPORT";
    public const string PopupExportToFileId = "SCT_EXPORT_TO_FILE";
    public const string PopupExportToFileFailedId = "SCT_EXPORT_TO_FILE_FAILED";
    public const string PopupDataClearId = "SCT_DATA_CLEAR";
    public const string PopupNoGraphId = "SCT_NO_GRAPH_ERROR";

    // Text Overlay Menu
    public const string OverlayEnabledId = "SCT_OVERLAY_ENABLED";
    public const string TextSizeId = "SCT_TEXT_SIZE";
    public const string TextPositionId = "SCT_TEXT_POSITION";
    public const string TextOrientationId = "SCT_TEXT_ORIENTATION";
    public const string StatsOverlayPageId = "SCT_TEXT_OVERLAY";
    public const string ChartsPageId = "SCT_GRAPH_OVERLAY";
    public const string TextAlphaId = "SCT_TEXT_ALPHA";

    // Graph Overlay Menu
    public const string GraphEnabledId = "SCT_GRAPH_ENABLED";
    public const string GraphScatterId = "SCT_SCATTER";
    public const string GraphDnfPercentId = "SCT_DNF_PERCENT_BAR_CHART";
    public const string GraphProblemRoomsId = "SCT_PROBLEM_ROOM_BAR_CHART";
    public const string RoomColorId = "SCT_ROOM_COLOR";
    public const string SegmentColorId = "SCT_SEGMENT_COLOR";
    public const string PrimaryColorId = "SCT_PRIMARY_COLOR";
    public const string SecondaryColorId = "SCT_SECONDARY_COLOR";
    public const string BestColorId = "SCT_BEST_COLOR";
    public const string LastColorId = "SCT_LAST_COLOR";
    public const string SobColorId = "SCT_SOB_COLOR";
    public const string ColorsHeaderId = "SCT_COLORS_HEADER";
    public const string ContentHeaderId = "SCT_CONTENT_HEADER";
    public const string FillOpacityId = "SCT_CHART_OPACITY";
    public const string FillOpacityDescId = "SCT_FILL_OPACITY_DESC";
    public const string TimeLossThresholdId = "SCT_TIME_LOSS_THRESHOLD";
    public const string TimeLossThresholdDescId = "SCT_TIME_LOSS_THRESHOLD_DESC";
    public const string GraphTimeLossId = "SCT_TIME_LOSS_CHART";
    public const string GraphRunTrajectoryId = "SCT_TRAJECTORY_GRAPH";

    // Stats Menu
    public const string ExtraSectionsId = "SCT_EXPORT_ONLY";
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
    public const string ShowTargetLineId = "SCT_SHOW_TARGET_LINE";
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
    public const string MultimodalTestId = "SCT_MULTIMODAL_TEST";
    public const string RoomDependencyId = "SCT_ROOM_DEPENDENCY";
    public const string BestSplitId = "SCT_BEST_SPLIT";
    public const string RelMadId = "SCT_RELATIVE_MAD";
    public const string GoldRateId = "SCT_GOLD_RATE";
    public const string GroupRunsId = "SCT_GROUP_RUNS";
    public const string GroupTimesId = "SCT_GROUP_TIMES";
    public const string GroupSpreadId = "SCT_GROUP_SPREAD";

    // Graph Overlay (charts)
    public const string GraphBoxPlotId = "SCT_BOX_PLOT_GRAPH";

    // Chart text. An *Fmt constant names a line read through DialogText.Format, never Dialog.Clean.
    public const string ChartLabelValueFmt = "SCT_CHART_LABEL_VALUE_FMT";
    public const string ChartSegment = "SCT_CHART_SEGMENT";
    public const string ChartSegmentRoomsFmt = "SCT_CHART_SEGMENT_ROOMS_FMT";
    public const string ChartOneRoom = "SCT_CHART_ONE_ROOM";
    public const string ChartRoomsFmt = "SCT_CHART_ROOMS_FMT";
    public const string ChartOneRun = "SCT_CHART_ONE_RUN";
    public const string ChartOneCompletedRun = "SCT_CHART_ONE_COMPLETED_RUN";
    public const string ChartCompletedRunsFmt = "SCT_CHART_COMPLETED_RUNS_FMT";
    public const string ChartScale = "SCT_CHART_SCALE";
    public const string ChartThreshold = "SCT_CHART_THRESHOLD";
    public const string ChartSegmentTime = "SCT_CHART_SEGMENT_TIME";
    public const string ChartRunsFmt = "SCT_CHART_RUNS_FMT";
    public const string ChartDelete = "SCT_CHART_DELETE";
    public const string ChartAbsolute = "SCT_CHART_ABSOLUTE";
    public const string ChartRelative = "SCT_CHART_RELATIVE";
    public const string ChartScatterTitle = "SCT_CHART_SCATTER_TITLE";
    public const string ChartTargetFmt = "SCT_CHART_TARGET_FMT";
    public const string ChartRunTimeFmt = "SCT_CHART_RUN_TIME_FMT";
    public const string ChartBoxPlotTitle = "SCT_CHART_BOX_PLOT_TITLE";
    public const string ChartStatMax = "SCT_CHART_STAT_MAX";
    public const string ChartStatMedian = "SCT_CHART_STAT_MEDIAN";
    public const string ChartStatMin = "SCT_CHART_STAT_MIN";
    public const string ChartHistogramTitleFmt = "SCT_CHART_HISTOGRAM_TITLE_FMT";
    public const string ChartBinOneRunFmt = "SCT_CHART_BIN_ONE_RUN_FMT";
    public const string ChartBinRunsFmt = "SCT_CHART_BIN_RUNS_FMT";
    public const string ChartResetSurvivalTitle = "SCT_CHART_RESET_SURVIVAL_TITLE";
    public const string ChartResetRate = "SCT_CHART_RESET_RATE";
    public const string ChartRunsAlive = "SCT_CHART_RUNS_ALIVE";
    public const string ChartProblemRoomsTitleFmt = "SCT_CHART_PROBLEM_ROOMS_TITLE_FMT";
    public const string ChartOverThresholdFmt = "SCT_CHART_OVER_THRESHOLD_FMT";
    public const string ChartTimeLossTitle = "SCT_CHART_TIME_LOSS_TITLE";
    public const string ChartMedianLoss = "SCT_CHART_MEDIAN_LOSS";
    public const string ChartAvgLoss = "SCT_CHART_AVG_LOSS";
    public const string ChartTrajectoryTitle = "SCT_CHART_TRAJECTORY_TITLE";
    public const string ChartTrajectoryTitleSob = "SCT_CHART_TRAJECTORY_TITLE_SOB";
    public const string ChartAverage = "SCT_CHART_AVERAGE";
    public const string ChartAvg = "SCT_CHART_AVG";
    public const string ChartSob = "SCT_CHART_SOB";
    public const string ChartVsAvg = "SCT_CHART_VS_AVG";
    public const string ChartVsSob = "SCT_CHART_VS_SOB";
    public const string ChartBestRun = "SCT_CHART_BEST_RUN";
    public const string ChartBest = "SCT_CHART_BEST";
    public const string ChartVsBestRun = "SCT_CHART_VS_BEST_RUN";
    public const string ChartTrajectoryTitleBest = "SCT_CHART_TRAJECTORY_TITLE_BEST";
    public const string ChartTrajectoryNoBestRun = "SCT_CHART_TRAJECTORY_NO_BEST_RUN";
    public const string ChartRefTipBestRun = "SCT_CHART_REF_TIP_BEST_RUN";
    public const string ChartRefTipAverage = "SCT_CHART_REF_TIP_AVERAGE";
    public const string ChartRefTipSob = "SCT_CHART_REF_TIP_SOB";
    public const string ChartBaseline = "SCT_CHART_BASELINE";
    public const string ChartWidgetTip = "SCT_CHART_WIDGET_TIP";
    public const string ChartWidgetPlace = "SCT_CHART_WIDGET_PLACE";
    public const string ChartOn = "SCT_CHART_ON";
    public const string ChartOff = "SCT_CHART_OFF";
    public const string TrajectoryWidgetDeltaDescId = "SCT_TRAJECTORY_WIDGET_DELTA_DESC";
    public const string ChartWidgetPlaceTip = "SCT_CHART_WIDGET_PLACE_TIP";
    public const string ChartWidget = "SCT_CHART_WIDGET";
    public const string TrajectoryWidgetId = "SCT_TRAJECTORY_WIDGET";
    public const string TrajectoryWidgetDescId = "SCT_TRAJECTORY_WIDGET_DESC";
    public const string TrajectoryWidgetOpacityId = "SCT_TRAJECTORY_WIDGET_OPACITY";
    public const string TrajectoryWidgetHeaderId = "SCT_TRAJECTORY_WIDGET_HEADER";
    public const string TrajectoryWidgetDeltaId = "SCT_TRAJECTORY_WIDGET_DELTA";
    public const string ChartVsRunFmt = "SCT_CHART_VS_RUN_FMT";
    public const string ChartRunFmt = "SCT_CHART_RUN_FMT";
    public const string ChartVsBestSplit = "SCT_CHART_VS_BEST_SPLIT";
    public const string ChartCumul = "SCT_CHART_CUMUL";
    public const string ChartDifference = "SCT_CHART_DIFFERENCE";
    public const string ChartRoom = "SCT_CHART_ROOM";
    public const string ChartLegendAllThree = "SCT_CHART_LEGEND_ALL_THREE";
    public const string ChartLegendSobBest = "SCT_CHART_LEGEND_SOB_BEST";
    public const string ChartLegendBestLast = "SCT_CHART_LEGEND_BEST_LAST";
    public const string ChartLegendBest = "SCT_CHART_LEGEND_BEST";
    public const string ChartLegendLast = "SCT_CHART_LEGEND_LAST";

    public const string GraphHistogramWallId = "SCT_HISTOGRAM_WALL_GRAPH";
    public const string ChartSavePng = "SCT_CHART_SAVE_PNG";
    public const string PopupChartSavedFmt = "SCT_CHART_SAVED_FMT";
    public const string ChartSpread = "SCT_CHART_SPREAD";
    public const string ChartMedianLine = "SCT_CHART_MEDIAN_LINE";
    public const string ChartWallTitle = "SCT_CHART_WALL_TITLE";
    public const string ChartWallBack = "SCT_CHART_WALL_BACK";
    public const string ChartHintTitle = "SCT_CHART_HINT_TITLE";
    public const string ChartClearPins = "SCT_CHART_CLEAR_PINS";
    public const string ChartResetZoom = "SCT_CHART_RESET_ZOOM";
    public const string HintSubScatter = "SCT_HINT_SUB_SCATTER";
    public const string HintSubBox = "SCT_HINT_SUB_BOX";
    public const string HintSubHistogram = "SCT_HINT_SUB_HISTOGRAM";
    public const string HintSubResets = "SCT_HINT_SUB_RESETS";
    public const string HintSubProblem = "SCT_HINT_SUB_PROBLEM";
    public const string HintSubTimeLoss = "SCT_HINT_SUB_TIME_LOSS";
    public const string HintSubTrajectory = "SCT_HINT_SUB_TRAJECTORY";
    public const string HintSubWall = "SCT_HINT_SUB_WALL";
    public const string HintAboutScatter = "SCT_HINT_ABOUT_SCATTER";
    public const string HintMouseScatter = "SCT_HINT_MOUSE_SCATTER";
    public const string HintMouseScatterTarget = "SCT_HINT_MOUSE_SCATTER_TARGET";
    public const string HintAboutBox = "SCT_HINT_ABOUT_BOX";
    public const string HintMouseBox = "SCT_HINT_MOUSE_BOX";
    public const string HintAboutHistogram = "SCT_HINT_ABOUT_HISTOGRAM";
    public const string HintMouseHistogram = "SCT_HINT_MOUSE_HISTOGRAM";
    public const string HintAboutResets = "SCT_HINT_ABOUT_RESETS";
    public const string HintAboutProblem = "SCT_HINT_ABOUT_PROBLEM";
    public const string HintAboutTimeLoss = "SCT_HINT_ABOUT_TIME_LOSS";
    public const string HintMouseBars = "SCT_HINT_MOUSE_BARS";
    public const string HintAboutTrajectory = "SCT_HINT_ABOUT_TRAJECTORY";
    public const string HintMouseTrajectory = "SCT_HINT_MOUSE_TRAJECTORY";
    public const string HintAboutWall = "SCT_HINT_ABOUT_WALL";
    public const string HintMouseWall = "SCT_HINT_MOUSE_WALL";
    public const string ChartHintClose = "SCT_CHART_HINT_CLOSE";
}
