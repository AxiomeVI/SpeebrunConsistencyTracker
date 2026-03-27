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

    public record ExportSettings(string SpreadsheetId, string TabName, string CredentialsPath, int StartRow, int StartCol);
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

    private static string CellNotation(int row, int col)
    {
        string colLetters = "";
        int c = col + 1;
        while (c > 0)
        {
            c--;
            colLetters = (char)('A' + c % 26) + colLetters;
            c /= 26;
        }
        return $"{colLetters}{row + 1}";
    }

    private static (int row, int col) ParseStartCell(string cell)
    {
        if (string.IsNullOrWhiteSpace(cell))
            return (0, 0);

        cell = cell.Trim().ToUpperInvariant();

        int i = 0;
        int col = 0;
        while (i < cell.Length && char.IsLetter(cell[i]))
        {
            col = col * 26 + (cell[i] - 'A' + 1);
            i++;
        }
        col -= 1; // 0-indexed

        if (i == 0 || i == cell.Length || !int.TryParse(cell[i..], out int row) || row < 1)
            return (0, 0);

        return (row - 1, col);
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
        string startCellRaw = json["StartCell"]?.GetValue<string>() ?? "A1";
        (int startRow, int startCol) = ParseStartCell(startCellRaw);
        settings = new ExportSettings(
            SpreadsheetId:   json["SpreadsheetId"]?.GetValue<string>() ?? "",
            TabName:         json["TabName"]?.GetValue<string>() ?? "",
            CredentialsPath: credentialsPath,
            StartRow:        startRow,
            StartCol:        startCol
        );
        return true;
    }

    public static IList<IList<object>> CsvStringToList(string csv)
    {
        if (string.IsNullOrEmpty(csv)) return [];
        return [.. csv
            .Split(["\r\n", "\n"], StringSplitOptions.None)
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

        try
        {
            List<IList<object>> exportData = [];
            List<TableRange> tableRanges = [];
            int rowOffset = 0;

            if (SpeebrunConsistencyTrackerModule.Settings.ExportWithSRT)
            {
                TextInput.SetClipboardText("");
                RoomTimerManager.CmdExportRoomTimes();
                var srtRows = CsvStringToList(TextInput.GetClipboardText());
                AppendTableSection(exportData, srtRows, tableRanges, ref rowOffset);
            }

            var metricRows = MetricsExporter.ExportMetricsToSheet(session, roomCount);
            AppendTableSection(exportData, metricRows, tableRanges, ref rowOffset);

            if (SpeebrunConsistencyTrackerModule.Settings.History)
            {
                var historyRows = SessionHistoryExporter.ExportSessionToSheet(session, roomCount);
                AppendTableSection(exportData, historyRows, tableRanges, ref rowOffset, addSeparator: false);
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

            int sheetId = await EnsureSheetTabExists(service, settings.SpreadsheetId, settings.TabName);

            _ = await service.Spreadsheets.Values
                .Clear(new ClearValuesRequest(), settings.SpreadsheetId, settings.TabName)
                .ExecuteAsync();

            string startCellNotation = CellNotation(settings.StartRow, settings.StartCol);
            string writeRange = $"{settings.TabName}!{startCellNotation}";

            ValueRange body = new() { Values = exportData };
            SpreadsheetsResource.ValuesResource.UpdateRequest request =
                service.Spreadsheets.Values.Update(body, settings.SpreadsheetId, writeRange);
            request.ValueInputOption =
                SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;

            _ = await request.ExecuteAsync();
            SpeebrunConsistencyTrackerModule.PopupMessage(Dialog.Clean(DialogIds.PopupExportToSheetid));
            await ApplyTableFormatting(service, settings.SpreadsheetId, sheetId, tableRanges, settings.StartRow, settings.StartCol);
        }
        catch (Exception ex)
        {
            Logger.Log(LogLevel.Error, nameof(SpeebrunConsistencyTracker), $"Sheet export failed: {ex.Message}");
        }
    }

    private static void AppendTableSection(List<IList<object>> data,
        IList<IList<object>> rows, List<TableRange> tableRanges, ref int rowOffset,
        bool addSeparator = true)
    {
        data.AddRange(rows);
        tableRanges.Add(new TableRange(rowOffset, rowOffset + rows.Count, rows.Max(r => r.Count)));
        if (addSeparator)
        {
            data.Add([]);
            data.Add([]);
            data.Add([]);
            rowOffset += 3 + rows.Count;
        }
        else
        {
            rowOffset += rows.Count;
        }
    }

    private static async Task ApplyTableFormatting(SheetsService service, string spreadsheetId, int sheetId, List<TableRange> tableRanges, int startRow = 0, int startCol = 0)
    {

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
                StartRowIndex = startRow + table.StartRow,
                EndRowIndex = startRow + table.EndRow,
                StartColumnIndex = startCol,
                EndColumnIndex = startCol + table.ColCount
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
                        StartRowIndex = startRow + table.StartRow,
                        EndRowIndex = startRow + table.StartRow + 1,
                        StartColumnIndex = startCol,
                        EndColumnIndex = startCol + table.ColCount
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
                        StartRowIndex = startRow + table.StartRow,
                        EndRowIndex = startRow + table.EndRow,
                        StartColumnIndex = startCol,
                        EndColumnIndex = startCol + 1
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
                        StartRowIndex = startRow + table.StartRow,
                        EndRowIndex = startRow + table.StartRow + 1,
                        StartColumnIndex = startCol,
                        EndColumnIndex = startCol + table.ColCount
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

    private static async Task<int> EnsureSheetTabExists(SheetsService service, string spreadsheetId, string tabName)
    {
        Spreadsheet spreadsheet = await service.Spreadsheets.Get(spreadsheetId).ExecuteAsync();
        Sheet existing = spreadsheet.Sheets.FirstOrDefault(s => s.Properties.Title == tabName);

        if (existing != null)
            return (int)existing.Properties.SheetId;

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
        var response = await service.Spreadsheets.BatchUpdate(addSheet, spreadsheetId).ExecuteAsync();
        return (int)response.Replies[0].AddSheet.Properties.SheetId;
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
