#region Metadata
/*
 * Tool Name     : Pipe Sizing
 * File Name     : PipeSizingCalculator.cs
 * Purpose       : Calculates probable water demand, required internal diameter, selected pipe size, velocity, and friction loss.
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
 * Dependencies  : AJTools.Models.PipeSizing, PipeSizingData
 *
 * Input         : Aggregate fixture units, system type, pipe material, and velocity limit.
 * Output        : Flow rate, selected pipe size, velocity, and friction loss.
 *
 * Notes         :
 * - Direct port of the original pyRevit pipe sizing calculation logic.
 * - The flow table is clamped at the original table maximum, matching the Python tool.
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
using AJTools.Models.PipeSizing;

namespace AJTools.Services.PipeSizing
{
    internal static class PipeSizingCalculator
    {
        private const double GallonPerMinuteToCubicMeterPerSecond = 0.0000630901964;
        private const double MeterPerSecondToFootPerSecond = 3.28084;

        public static double InterpolateGpm(double totalFixtureUnits, bool isFlushValve)
        {
            if (Math.Abs(totalFixtureUnits) < double.Epsilon)
            {
                return 0.0;
            }

            int lowerIndex = 0;
            int upperIndex = 0;
            IReadOnlyList<PipeSizingData.FixtureUnitFlowRow> table = PipeSizingData.FixtureUnitFlowTable;

            for (int i = 0; i < table.Count; i++)
            {
                if (totalFixtureUnits <= table[i].FixtureUnits)
                {
                    upperIndex = i;
                    lowerIndex = i > 0 ? i - 1 : 0;
                    break;
                }
            }

            if (totalFixtureUnits >= table[table.Count - 1].FixtureUnits)
            {
                upperIndex = table.Count - 1;
                lowerIndex = upperIndex;
            }

            PipeSizingData.FixtureUnitFlowRow lower = table[lowerIndex];
            PipeSizingData.FixtureUnitFlowRow upper = table[upperIndex];

            double lowerValue = isFlushValve && lower.FlushValveGpm > 0 ? lower.FlushValveGpm : lower.FlushTankGpm;
            double upperValue = isFlushValve && upper.FlushValveGpm > 0 ? upper.FlushValveGpm : upper.FlushTankGpm;

            if (Math.Abs(upper.FixtureUnits - lower.FixtureUnits) < double.Epsilon)
            {
                return Round2(lowerValue);
            }

            double gpm = lowerValue +
                         (upperValue - lowerValue) *
                         ((totalFixtureUnits - lower.FixtureUnits) / (upper.FixtureUnits - lower.FixtureUnits));

            return Round2(gpm);
        }

        public static PipeSizingResult Calculate(double totalFixtureUnits, bool isFlushValve, int materialIndex, double velocityLimitMs)
        {
            double gpm = InterpolateGpm(totalFixtureUnits, isFlushValve);
            PipeSizingResult result = CalculateDiameter(gpm, materialIndex, velocityLimitMs);
            result.FlowGpm = gpm;
            return result;
        }

        public static PipeSizingResult CalculateDiameter(double gpm, int materialIndex, double velocityLimitMs)
        {
            if (Math.Abs(gpm) < double.Epsilon)
            {
                return new PipeSizingResult
                {
                    FlowGpm = 0.0,
                    SelectedSizeMm = 0.0,
                    SelectedSizeLabel = "0\"",
                    RequiredInternalDiameterMm = 0.0,
                    VelocityFeetPerSecond = 0.0,
                    FrictionLossPsiPer100Ft = 0.0
                };
            }

            if (velocityLimitMs <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(velocityLimitMs), "Velocity limit must be greater than zero.");
            }

            if (!PipeSizingData.PipeIdTables.ContainsKey(materialIndex))
            {
                materialIndex = 0;
            }

            double cFactor = PipeSizingData.CFactors[materialIndex];
            IReadOnlyList<PipeSizeOption> sizes = PipeSizingData.PipeIdTables[materialIndex];

            double cubicMeterPerSecond = GallonPerMinuteToCubicMeterPerSecond * gpm;
            double requiredDiameterMm = Math.Sqrt((cubicMeterPerSecond / velocityLimitMs) * (4.0 / Math.PI)) * 1000.0;
            double exactId = Round2(requiredDiameterMm);

            PipeSizeOption selected = null;
            foreach (PipeSizeOption option in sizes)
            {
                if (option.InternalDiameterMm >= exactId)
                {
                    selected = option;
                    break;
                }
            }

            if (selected == null)
            {
                selected = sizes[sizes.Count - 1];
            }

            double actualDiameterM = selected.InternalDiameterMm / 1000.0;
            double actualVelocityMs = cubicMeterPerSecond / (Math.PI * Math.Pow(actualDiameterM / 2.0, 2));
            double velocityFts = Round2(actualVelocityMs * MeterPerSecondToFootPerSecond);
            double frictionLoss = 4.52 * Math.Pow(gpm, 1.852) /
                                  (Math.Pow(cFactor, 1.852) *
                                   Math.Pow(selected.InternalDiameterMm / 25.4, 4.87));

            return new PipeSizingResult
            {
                FlowGpm = gpm,
                SelectedSizeMm = selected.InternalDiameterMm,
                SelectedSizeLabel = selected.NominalLabel,
                RequiredInternalDiameterMm = exactId,
                VelocityFeetPerSecond = velocityFts,
                FrictionLossPsiPer100Ft = Round2(frictionLoss)
            };
        }

        public static double Round2(double value)
        {
            return Math.Round(value, 2, MidpointRounding.AwayFromZero);
        }
    }
}
