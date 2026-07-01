#region Metadata
/*
 * Tool Name     : Pipe Sizing
 * File Name     : PipeSizingData.cs
 * Purpose       : Provides fixture-unit, flow, material C-factor, and pipe ID lookup data.
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
 * Dependencies  : AJTools.Models.PipeSizing
 *
 * Input         : None
 * Output        : Static lookup data for Pipe Sizing calculations.
 *
 * Notes         :
 * - Direct port of the original pyRevit pipe sizing constants.
 * - Fixture, FU table, C-factor, and pipe ID values are intentionally unchanged.
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
using System.Linq;
using AJTools.Models.PipeSizing;

namespace AJTools.Services.PipeSizing
{
    internal static class PipeSizingData
    {
        public static readonly IReadOnlyDictionary<string, PipeFixtureData> FixtureData =
            new Dictionary<string, PipeFixtureData>(StringComparer.Ordinal)
            {
                ["Bathroom Group Private Flush Tank"] = new PipeFixtureData(2.7, 1.5, 3.6),
                ["Bathroom Group Private Flush Valve"] = new PipeFixtureData(6.0, 3.0, 8.0),
                ["Bathtub Private"] = new PipeFixtureData(1.0, 1.0, 1.4),
                ["Bathtub Public"] = new PipeFixtureData(3.0, 3.0, 4.0),
                ["Bidet"] = new PipeFixtureData(1.5, 1.5, 2.0),
                ["Combination Fixture"] = new PipeFixtureData(2.25, 2.25, 3.0),
                ["Dishwashing Machine Private"] = new PipeFixtureData(0.0, 1.4, 1.4),
                ["Drinking Fountain"] = new PipeFixtureData(0.25, 0.0, 0.25),
                ["Kitchen Sink Private"] = new PipeFixtureData(1.0, 1.0, 1.4),
                ["Kitchen Sink Restaurant"] = new PipeFixtureData(3.0, 3.0, 4.0),
                ["Laundry Trays (1 to 3) Private"] = new PipeFixtureData(1.0, 1.0, 1.4),
                ["Lavatory Private"] = new PipeFixtureData(0.5, 0.5, 0.7),
                ["Lavatory Public"] = new PipeFixtureData(1.5, 1.5, 2.0),
                ["Service Sink"] = new PipeFixtureData(2.25, 2.25, 3.0),
                ["Shower Head Private"] = new PipeFixtureData(1.0, 1.0, 1.4),
                ["Shower Head Public"] = new PipeFixtureData(3.0, 3.0, 4.0),
                ["Urinal 1\" Flush Valve"] = new PipeFixtureData(10.0, 0.0, 10.0),
                ["Urinal 3/4\" Flush Valve"] = new PipeFixtureData(5.0, 0.0, 5.0),
                ["Urinal Flush Tank"] = new PipeFixtureData(3.0, 0.0, 3.0),
                ["Washing Machine Private (8 Lb)"] = new PipeFixtureData(1.0, 1.0, 1.4),
                ["Washing Machine Public (15 Lb)"] = new PipeFixtureData(3.0, 3.0, 4.0),
                ["Washing Machine Public (8 Lb)"] = new PipeFixtureData(2.25, 2.25, 3.0),
                ["Water Closet Private Flush Tank"] = new PipeFixtureData(2.2, 0.0, 2.2),
                ["Water Closet Private Flush Valve"] = new PipeFixtureData(6.0, 0.0, 6.0),
                ["Water Closet Public Flush Tank"] = new PipeFixtureData(5.0, 0.0, 5.0),
                ["Water Closet Public Flush Valve"] = new PipeFixtureData(10.0, 0.0, 10.0),
                ["Hose Bibb (1/2\" connection)"] = new PipeFixtureData(2.5, 0.0, 2.5),
                ["Hose Bibb (3/4\" connection)"] = new PipeFixtureData(3.0, 0.0, 3.0),
                ["Bar Sink"] = new PipeFixtureData(1.0, 1.0, 1.4),
                ["Mop Basin / Janitor Sink"] = new PipeFixtureData(2.25, 2.25, 3.0),
                ["Clinic Sink (Flush Valve)"] = new PipeFixtureData(8.0, 0.0, 8.0),
                ["Dishwashing Machine Commercial"] = new PipeFixtureData(0.0, 2.0, 2.0),
                ["Wash Sink (per set of faucets)"] = new PipeFixtureData(1.5, 1.5, 2.0)
            };

        public static readonly IReadOnlyList<FixtureUnitFlowRow> FixtureUnitFlowTable =
            new List<FixtureUnitFlowRow>
            {
                new FixtureUnitFlowRow(1, 3.0, 0), new FixtureUnitFlowRow(2, 5.0, 0),
                new FixtureUnitFlowRow(3, 6.5, 0), new FixtureUnitFlowRow(4, 8.0, 0),
                new FixtureUnitFlowRow(5, 9.4, 15.0), new FixtureUnitFlowRow(6, 10.7, 17.4),
                new FixtureUnitFlowRow(7, 11.8, 19.8), new FixtureUnitFlowRow(8, 12.8, 22.2),
                new FixtureUnitFlowRow(9, 13.7, 24.6), new FixtureUnitFlowRow(10, 14.6, 27.0),
                new FixtureUnitFlowRow(11, 15.4, 27.8), new FixtureUnitFlowRow(12, 16.0, 28.6),
                new FixtureUnitFlowRow(13, 16.5, 29.4), new FixtureUnitFlowRow(14, 17.0, 30.2),
                new FixtureUnitFlowRow(15, 17.5, 31.0), new FixtureUnitFlowRow(16, 18.0, 31.8),
                new FixtureUnitFlowRow(17, 18.4, 32.6), new FixtureUnitFlowRow(18, 18.8, 33.4),
                new FixtureUnitFlowRow(19, 19.2, 34.2), new FixtureUnitFlowRow(20, 19.6, 35.0),
                new FixtureUnitFlowRow(25, 21.5, 38.0), new FixtureUnitFlowRow(30, 23.3, 41.0),
                new FixtureUnitFlowRow(35, 24.9, 43.8), new FixtureUnitFlowRow(40, 26.3, 46.5),
                new FixtureUnitFlowRow(45, 27.7, 49.2), new FixtureUnitFlowRow(50, 29.1, 51.5),
                new FixtureUnitFlowRow(60, 32.0, 54.0), new FixtureUnitFlowRow(70, 35.0, 58.0),
                new FixtureUnitFlowRow(80, 38.0, 62.0), new FixtureUnitFlowRow(90, 41.0, 66.0),
                new FixtureUnitFlowRow(100, 43.5, 71.0), new FixtureUnitFlowRow(120, 48.0, 77.0),
                new FixtureUnitFlowRow(140, 52.5, 83.0), new FixtureUnitFlowRow(160, 57.0, 89.0),
                new FixtureUnitFlowRow(180, 61.0, 95.0), new FixtureUnitFlowRow(200, 65.0, 101.0),
                new FixtureUnitFlowRow(225, 70.0, 107.0), new FixtureUnitFlowRow(250, 75.0, 113.0),
                new FixtureUnitFlowRow(275, 80.0, 118.0), new FixtureUnitFlowRow(300, 85.0, 124.0),
                new FixtureUnitFlowRow(400, 105.0, 148.0), new FixtureUnitFlowRow(500, 124.0, 170.0),
                new FixtureUnitFlowRow(750, 170.0, 208.0), new FixtureUnitFlowRow(1000, 208.0, 239.0)
            };

        public static readonly IReadOnlyList<double> CFactors = new List<double> { 150, 150, 150, 130 };

        public static readonly IReadOnlyDictionary<int, IReadOnlyList<PipeSizeOption>> PipeIdTables =
            new Dictionary<int, IReadOnlyList<PipeSizeOption>>
            {
                [0] = new List<PipeSizeOption>
                {
                    new PipeSizeOption(15.8, "1/2\""), new PipeSizeOption(20.93, "3/4\""),
                    new PipeSizeOption(26.64, "1\""), new PipeSizeOption(35.05, "1 1/4\""),
                    new PipeSizeOption(40.89, "1 1/2\""), new PipeSizeOption(52.5, "2\""),
                    new PipeSizeOption(62.71, "2 1/2\""), new PipeSizeOption(77.93, "3\""),
                    new PipeSizeOption(102.26, "4\""), new PipeSizeOption(154.05, "6\"")
                },
                [1] = new List<PipeSizeOption>
                {
                    new PipeSizeOption(12.42, "1/2\""), new PipeSizeOption(18.16, "3/4\""),
                    new PipeSizeOption(22.89, "1\""), new PipeSizeOption(28.58, "1 1/4\""),
                    new PipeSizeOption(33.88, "1 1/2\""), new PipeSizeOption(44.17, "2\""),
                    new PipeSizeOption(55.0, "2 1/2\""), new PipeSizeOption(65.0, "3\""),
                    new PipeSizeOption(88.0, "4\""), new PipeSizeOption(131.0, "6\"")
                },
                [2] = new List<PipeSizeOption>
                {
                    new PipeSizeOption(14.4, "20mm (1/2\")"), new PipeSizeOption(18.0, "25mm (3/4\")"),
                    new PipeSizeOption(23.2, "32mm (1\")"), new PipeSizeOption(29.0, "40mm (1 1/4\")"),
                    new PipeSizeOption(36.2, "50mm (1 1/2\")"), new PipeSizeOption(45.8, "63mm (2\")"),
                    new PipeSizeOption(54.4, "75mm (2 1/2\")"), new PipeSizeOption(65.4, "90mm (3\")"),
                    new PipeSizeOption(79.8, "110mm (4\")"), new PipeSizeOption(116.0, "160mm (6\")")
                },
                [3] = new List<PipeSizeOption>
                {
                    new PipeSizeOption(13.84, "1/2\""), new PipeSizeOption(19.94, "3/4\""),
                    new PipeSizeOption(26.04, "1\""), new PipeSizeOption(32.13, "1 1/4\""),
                    new PipeSizeOption(38.23, "1 1/2\""), new PipeSizeOption(50.42, "2\""),
                    new PipeSizeOption(62.61, "2 1/2\""), new PipeSizeOption(74.8, "3\""),
                    new PipeSizeOption(99.19, "4\""), new PipeSizeOption(148.0, "6\"")
                }
            };

        public static IReadOnlyList<string> GetSortedFixtureNames()
        {
            return FixtureData.Keys.OrderBy(name => name, StringComparer.Ordinal).ToList();
        }

        internal sealed class FixtureUnitFlowRow
        {
            public FixtureUnitFlowRow(double fixtureUnits, double flushTankGpm, double flushValveGpm)
            {
                FixtureUnits = fixtureUnits;
                FlushTankGpm = flushTankGpm;
                FlushValveGpm = flushValveGpm;
            }

            public double FixtureUnits { get; }
            public double FlushTankGpm { get; }
            public double FlushValveGpm { get; }
        }
    }
}
