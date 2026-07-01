#region Metadata
/*
 * Tool Name     : Pipe Sizing
 * File Name     : PipeSizingCsvExporter.cs
 * Purpose       : Exports the Pipe Sizing fixture rows and summary values to CSV.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-07-01
 * Last Updated  : 2026-07-01
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Microsoft.Win32, WPF MessageBox
 *
 * Input         : Display rows and calculated summary values.
 * Output        : User-selected CSV report file.
 *
 * Notes         :
 * - Ported from the original pyRevit pipe sizing report export.
 * - Uses proper CSV escaping while keeping the same column order and summary rows.
 *
 * Changelog     :
 * v1.0.0 (2026-07-01) - Initial C# port for Pipe Sizing.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using Microsoft.Win32;

namespace AJTools.Services.PipeSizing
{
    internal static class PipeSizingCsvExporter
    {
        private const string DialogTitle = "Pipe Sizing";

        public static void Export(
            IEnumerable<string[]> fixtureRows,
            string aggregateFu,
            string flowGpm,
            string exactId,
            string selectedPipe,
            string velocity,
            string friction)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "CSV Files (*.csv)|*.csv",
                FileName = "PipeSizingReport.csv"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                var lines = new List<string>
                {
                    ToCsvLine("Fixture Name", "Supply", "Quantity", "FU/Fixture", "Total FU")
                };

                foreach (string[] row in fixtureRows ?? Enumerable.Empty<string[]>())
                {
                    lines.Add(ToCsvLine(row));
                }

                lines.Add(string.Empty);
                lines.Add(ToCsvLine("Aggregate FU", aggregateFu));
                lines.Add(ToCsvLine("Flow Rate (GPM)", flowGpm));
                lines.Add(ToCsvLine("Calculated ID (mm)", exactId));
                lines.Add(ToCsvLine("Selected Pipe", selectedPipe));
                lines.Add(ToCsvLine("Velocity", velocity));
                lines.Add(ToCsvLine("Friction Loss", friction));

                File.WriteAllLines(dialog.FileName, lines, Encoding.UTF8);
                MessageBox.Show("Report saved successfully.", DialogTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to save report: " + ex.Message, DialogTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string ToCsvLine(params string[] values)
        {
            return string.Join(",", (values ?? new string[0]).Select(Escape));
        }

        private static string Escape(string value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            bool mustQuote = value.Contains(",") || value.Contains("\"") || value.Contains("\r") || value.Contains("\n");
            string escaped = value.Replace("\"", "\"\"");
            return mustQuote ? "\"" + escaped + "\"" : escaped;
        }
    }
}
