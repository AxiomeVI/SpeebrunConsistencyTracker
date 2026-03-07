using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Celeste.Mod.SpeebrunConsistencyTracker.Domain.Sessions;
using Celeste.Mod.SpeebrunConsistencyTracker.Export.History;
using Celeste.Mod.SpeebrunConsistencyTracker.Export.Metrics;
using Celeste.Mod.SpeebrunConsistencyTracker.SessionManagement;
using Celeste.Mod.SpeedrunTool.RoomTimer;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Google.Apis.Services;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Celeste.Mod.SpeebrunConsistencyTracker.Export;

public static class DataExporter
{

    public record ExportSettings(string SpreadsheetId, string TabName, string CredentialsPath);
    private record TableRange(int StartRow, int EndRow, int ColCount);

    private static bool TryGetExportData(SessionManager mgr, out PracticeSession session, out int roomCount)
    {
        if (mgr == null || mgr.CurrentSession?.TotalAttempts == 0)
        {
            session = null;
            roomCount = 0;
            return false;
        }
        session = mgr.CurrentSession;
        roomCount = mgr.DynamicRoomCount();
        return true;
    }

    public static bool TryLoadSettings(out ExportSettings settings)
    {
        settings = null;
        string configFolder = Path.Combine(Everest.PathGame, "SCT_Exports");
        string settingsPath = Path.Combine(configFolder, "settings.json");
        string credentialsPath = Path.Combine(configFolder, "credentials.json");

        if (!File.Exists(settingsPath) || !File.Exists(credentialsPath))
        {
            SpeebrunConsistencyTrackerModule.PopupMessage(Dialog.Clean(DialogIds.PopupFileNotFoundid));
            return false;
        }

        JsonObject json = JsonSerializer.Deserialize<JsonObject>(File.ReadAllText(settingsPath));
        settings = new ExportSettings(
            SpreadsheetId:   json["SpreadsheetId"]?.GetValue<string>() ?? "",
            TabName:         json["TabName"]?.GetValue<string>() ?? "",
            CredentialsPath: credentialsPath
        );
        return true;
    }

    public static IList<IList<object>> CsvStringToList(string csv)
    {
        return [.. csv
            .Split('\n')
            .Select(line => (IList<object>)[.. line.Split(',').Select(cell => (object)cell)])];
    }

    public static async Task ExportToSheet(SessionManager mgr)
    {
        if (!TryGetExportData(mgr, out PracticeSession session, out int roomCount))
        {
            SpeebrunConsistencyTrackerModule.PopupMessage(Dialog.Clean(DialogIds.PopupInvalidExportid));
            return;
        }

        if (!TryLoadSettings(out ExportSettings settings)) return;

        IList<IList<object>> export_data = [];
        List<TableRange> tableRanges = [];
        int rowOffset = 0;

        if (SpeebrunConsistencyTrackerModule.Settings.ExportWithSRT)
        {
            TextInput.SetClipboardText("");
            RoomTimerManager.CmdExportRoomTimes();
            var srtRows = CsvStringToList(TextInput.GetClipboardText());
            foreach (var row in srtRows)
                export_data.Add(row);
            tableRanges.Add(new TableRange(rowOffset, rowOffset + srtRows.Count, srtRows.Max(r => r.Count)));
            export_data.Add([]);
            export_data.Add([]);
            export_data.Add([]);
            rowOffset += 3 + srtRows.Count;
        }

        var metricRows = MetricsExporter.ExportMetricsToSheet(session, roomCount);
        foreach (var row in metricRows)
            export_data.Add(row);
        tableRanges.Add(new TableRange(rowOffset, rowOffset + metricRows.Count, metricRows.Max(r => r.Count)));
        export_data.Add([]);
        export_data.Add([]);
        export_data.Add([]);
        rowOffset += 3 + metricRows.Count;

        if (SpeebrunConsistencyTrackerModule.Settings.History)
        {
            var historyRows = SessionHistoryExporter.ExportSessionToSheet(session, roomCount);
            foreach (var row in historyRows)
                export_data.Add(row);
            tableRanges.Add(new TableRange(rowOffset, rowOffset + historyRows.Count, historyRows.Max(r => r.Count)));
        }

        using FileStream stream = new(settings.CredentialsPath, FileMode.Open, FileAccess.Read);
        ServiceAccountCredential saCredential = ServiceAccountCredential.FromServiceAccountData(stream);
        ServiceAccountCredential scopedCredential = new(
            new ServiceAccountCredential.Initializer(saCredential.Id)
            {
                Scopes = [SheetsService.Scope.Spreadsheets],
                Key = saCredential.Key
            }
        );
        GoogleCredential credential = scopedCredential.ToGoogleCredential();

        SheetsService service = new(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = nameof(SpeebrunConsistencyTracker),
        });

        await EnsureSheetTabExists(service, settings.SpreadsheetId, settings.TabName);

        _ = await service.Spreadsheets.Values
            .Clear(new ClearValuesRequest(), settings.SpreadsheetId, settings.TabName)
            .ExecuteAsync();

        ValueRange body = new() { Values = export_data };
        SpreadsheetsResource.ValuesResource.UpdateRequest request =
            service.Spreadsheets.Values.Update(body, settings.SpreadsheetId, settings.TabName);
        request.ValueInputOption =
            SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;

        _ = await request.ExecuteAsync();
        SpeebrunConsistencyTrackerModule.PopupMessage(Dialog.Clean(DialogIds.PopupExportToSheetid));
        await ApplyTableFormatting(service, settings.SpreadsheetId, settings.TabName, tableRanges);
    }

    private static async Task ApplyTableFormatting(SheetsService service, string spreadsheetId, string tabName, List<TableRange> tableRanges)
    {
        Spreadsheet spreadsheet = await service.Spreadsheets.Get(spreadsheetId).ExecuteAsync();
        int sheetId = (int)spreadsheet.Sheets.First(s => s.Properties.Title == tabName).Properties.SheetId;

        Border thin = new() { Style = "SOLID", Width = 1 };
        Border thick = new() { Style = "SOLID", Width = 2 };
        Border none = new() { Style = "NONE" };

        List<Request> requests = [];

        // Clear all borders on the sheet first
        requests.Add(new Request
        {
            UpdateBorders = new UpdateBordersRequest
            {
                Range = new GridRange
                {
                    SheetId = sheetId,
                    StartRowIndex = 0,
                    EndRowIndex = 1000,
                    StartColumnIndex = 0,
                    EndColumnIndex = 26
                },
                Top = none,
                Bottom = none,
                Left = none,
                Right = none,
                InnerHorizontal = none,
                InnerVertical = none
            }
        });

        // Clear all bold cells
        requests.Add(new Request
        {
            RepeatCell = new RepeatCellRequest
            {
                Range = new GridRange
                {
                    SheetId = sheetId,
                    StartRowIndex = 0,
                    EndRowIndex = 1000,
                    StartColumnIndex = 0,
                    EndColumnIndex = 26
                },
                Cell = new CellData
                {
                    UserEnteredFormat = new CellFormat
                    {
                        TextFormat = new TextFormat { Bold = false }
                    }
                },
                Fields = "userEnteredFormat.textFormat.bold"
            }
        });

        foreach (var table in tableRanges)
        {
            GridRange fullTable = new()
            {
                SheetId = sheetId,
                StartRowIndex = table.StartRow,
                EndRowIndex = table.EndRow,
                StartColumnIndex = 0,
                EndColumnIndex = table.ColCount
            };

            // Thin borders for all cells
            requests.Add(new Request
            {
                UpdateBorders = new UpdateBordersRequest
                {
                    Range = fullTable,
                    Top = thin,
                    Bottom = thin,
                    Left = thin,
                    Right = thin,
                    InnerHorizontal = thin,
                    InnerVertical = thin
                }
            });

            // Thick outer border around the whole table
            requests.Add(new Request
            {
                UpdateBorders = new UpdateBordersRequest
                {
                    Range = fullTable,
                    Top = thick,
                    Bottom = thick,
                    Left = thick,
                    Right = thick
                }
            });

            // Thick border under the header row
            requests.Add(new Request
            {
                UpdateBorders = new UpdateBordersRequest
                {
                    Range = new GridRange
                    {
                        SheetId = sheetId,
                        StartRowIndex = table.StartRow,
                        EndRowIndex = table.StartRow + 1,
                        StartColumnIndex = 0,
                        EndColumnIndex = table.ColCount
                    },
                    Bottom = thick
                }
            });

            // Thick border on the right of the first column
            requests.Add(new Request
            {
                UpdateBorders = new UpdateBordersRequest
                {
                    Range = new GridRange
                    {
                        SheetId = sheetId,
                        StartRowIndex = table.StartRow,
                        EndRowIndex = table.EndRow,
                        StartColumnIndex = 0,
                        EndColumnIndex = 1
                    },
                    Right = thick
                }
            });

            // Bold header row
            requests.Add(new Request
            {
                RepeatCell = new RepeatCellRequest
                {
                    Range = new GridRange
                    {
                        SheetId = sheetId,
                        StartRowIndex = table.StartRow,
                        EndRowIndex = table.StartRow + 1,
                        StartColumnIndex = 0,
                        EndColumnIndex = table.ColCount
                    },
                    Cell = new CellData
                    {
                        UserEnteredFormat = new CellFormat
                        {
                            TextFormat = new TextFormat { Bold = true }
                        }
                    },
                    Fields = "userEnteredFormat.textFormat.bold"
                }
            });
        }

        await service.Spreadsheets.BatchUpdate(
            new BatchUpdateSpreadsheetRequest { Requests = requests },
            spreadsheetId
        ).ExecuteAsync();
    }

    private static async Task ResetSheetTab(SheetsService service, string spreadsheetId, string tabName)
    {
        Spreadsheet spreadsheet = await service.Spreadsheets.Get(spreadsheetId).ExecuteAsync();
        Sheet existingSheet = spreadsheet.Sheets.FirstOrDefault(s => s.Properties.Title == tabName);

        List<Request> requests = [];

        int? tabIndex = null;
        if (existingSheet != null)
        {
            tabIndex = existingSheet.Properties.Index;
            requests.Add(new Request
            {
                DeleteSheet = new DeleteSheetRequest
                {
                    SheetId = existingSheet.Properties.SheetId
                }
            });
        }

        requests.Add(new Request
        {
            AddSheet = new AddSheetRequest
            {
                Properties = new SheetProperties
                {
                    Title = tabName,
                    Index = tabIndex  // null = append at end if tab didn't exist before
                }
            }
        });

        await service.Spreadsheets.BatchUpdate(
            new BatchUpdateSpreadsheetRequest { Requests = requests },
            spreadsheetId
        ).ExecuteAsync();
    }

    private static async Task EnsureSheetTabExists(SheetsService service, string spreadsheetId, string tabName)
    {
        Spreadsheet spreadsheet = await service.Spreadsheets.Get(spreadsheetId).ExecuteAsync();
        bool tabExists = spreadsheet.Sheets.Any(s => s.Properties.Title == tabName);

        if (!tabExists)
        {
            BatchUpdateSpreadsheetRequest addSheet = new()
            {
                Requests = [new Request
                {
                    AddSheet = new AddSheetRequest
                    {
                        Properties = new SheetProperties { Title = tabName }
                    }
                }]
            };
            _ = await service.Spreadsheets.BatchUpdate(addSheet, spreadsheetId).ExecuteAsync();
        }
    }

    public static void ExportToClipboard(SessionManager mgr)
    {
        if (!TryGetExportData(mgr, out PracticeSession session, out int roomCount))
        {
            SpeebrunConsistencyTrackerModule.PopupMessage(Dialog.Clean(DialogIds.PopupInvalidExportid));
            return;
        }

        StringBuilder sb = new();
        if (SpeebrunConsistencyTrackerModule.Settings.ExportWithSRT)
        {
            // Clean current clipboard state in case srt export is done in file
            TextInput.SetClipboardText("");
            RoomTimerManager.CmdExportRoomTimes();
            _ = sb.Append(TextInput.GetClipboardText());
            _ = sb.Append("\n\n\n");
        }
        _ = sb.Append(MetricsExporter.ExportSessionToCsv(session, roomCount));
        if (SpeebrunConsistencyTrackerModule.Settings.History)
        {
            _ = sb.Append("\n\n\n");
            _ = sb.Append(SessionHistoryExporter.ExportSessionToCsv(session, roomCount));
        }
        TextInput.SetClipboardText(sb.ToString());
        SpeebrunConsistencyTrackerModule.PopupMessage(Dialog.Clean(DialogIds.PopupExportToClipBoardid));
    }

    public static void ExportToFiles(SessionManager mgr)
    {
        if (!TryGetExportData(mgr, out PracticeSession session, out int roomCount))
        {
            SpeebrunConsistencyTrackerModule.PopupMessage(Dialog.Clean(DialogIds.PopupInvalidExportid));
            return;
        }

        if (SpeebrunConsistencyTrackerModule.Settings.ExportWithSRT)
            RoomTimerManager.CmdExportRoomTimes();

        string baseFolder = Path.Combine(
            Everest.PathGame,
            "SCT_Exports",
            SanitizeFileName(session.levelName)
        );
        _ = Directory.CreateDirectory(baseFolder);
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        using (StreamWriter writer = File.CreateText(Path.Combine(baseFolder, $"{timestamp}_Metrics.csv")))
        {
            writer.WriteLine(MetricsExporter.ExportSessionToCsv(session, roomCount));
        }
        if (SpeebrunConsistencyTrackerModule.Settings.History)
        {
            using StreamWriter writer = File.CreateText(Path.Combine(baseFolder, $"{timestamp}_History.csv"));
            writer.WriteLine(SessionHistoryExporter.ExportSessionToCsv(session, roomCount));
        }

        SpeebrunConsistencyTrackerModule.PopupMessage(Dialog.Clean(DialogIds.PopupExportToFileid));
    }

    public static string SanitizeFileName(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;
        var invalidChars = Path.GetInvalidFileNameChars().Concat(Path.GetInvalidPathChars()).Distinct().ToArray();
        var sanitized = new string(
            [.. input.Where(ch => !invalidChars.Contains(ch))]
        );
        return sanitized.TrimEnd(' ', '.');
    }
}
