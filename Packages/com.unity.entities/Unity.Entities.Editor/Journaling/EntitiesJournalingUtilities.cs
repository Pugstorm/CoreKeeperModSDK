using System;
using System.IO;
using System.Text;
using UnityEditor;

namespace Unity.Entities.Editor
{
    static class EntitiesJournalingUtilities
    {
        const int k_BufferSize = 16 * 1024 * 1024;

        public static void ExportToCSV()
        {
#if !DISABLE_ENTITIES_JOURNALING
            var filePath = EditorUtility.SaveFilePanel("Export to CSV", null, "entities-journaling-export", "csv");
            if (string.IsNullOrEmpty(filePath))
                return;

            FileStream fileStream;
            try
            {
                fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: k_BufferSize, useAsync: true);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogException(e);
                return;
            }

            var index = 0;
            var byteCount = 0UL;
            var rowCount = EntitiesJournaling.RecordCount + 1;
            var rows = EntitiesJournaling.ExportToCSV();
            var buffer = new byte[k_BufferSize];
            try
            {
                foreach (var row in rows)
                {
                    var cancelled = EditorUtility.DisplayCancelableProgressBar("Exporting to CSV", $"Row {FormattingUtility.CountToString(index + 1)} of {FormattingUtility.CountToString(rowCount)} ({FormattingUtility.BytesToString(byteCount)})", (float)index++ / rowCount);
                    if (cancelled)
                        break;

                    // CSV RFC 4180 specify line ending should be CRLF regardless of platform
                    var line = row.WithCRLF();
                    var length = Encoding.UTF8.GetBytes(line, 0, line.Length, buffer, 0);
                    fileStream.Write(buffer, 0, length);
                    byteCount += (ulong)length;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                fileStream.Dispose();
            }
#endif
        }

        static string WithCRLF(this string value) => value.TrimEnd('\r', '\n') + "\r\n";
    }
}
