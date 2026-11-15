#nullable enable

using System;
using System.IO;

namespace Celeste.Mod.CelesteCharts;

/// <summary>Writes an exported file without throwing: a failure is handed back, and no half-written file is left.</summary>
internal static class ExportFile {
    /// <summary>
    ///     Makes the file's folder, writes it through <paramref name="write"/> into a file beside it, then puts
    ///     that in its place, replacing a file of the same name. False with the exception when any step fails;
    ///     the file it would have replaced is then left as it was.
    /// </summary>
    public static bool TryWrite(string path, Action<Stream> write, out Exception? error) {
        string? part = null;
        try {
            string full = Path.GetFullPath(path);
            string? folder = Path.GetDirectoryName(full);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
            part = full + ".part";
            using (FileStream file = File.Create(part)) write(file);
            File.Move(part, full, overwrite: true);
            error = null;
            return true;
        } catch (Exception e) {
            error = e;
            if (part != null) {
                try {
                    File.Delete(part);
                } catch (Exception) {
                    // The first failure is the one to report.
                }
            }
            return false;
        }
    }
}
