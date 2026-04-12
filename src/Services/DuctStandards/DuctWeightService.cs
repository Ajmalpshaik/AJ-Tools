using System;
using AJTools.Models.DuctStandards;

namespace AJTools.Services.DuctStandards
{
    internal static class DuctWeightService
    {
        /// <summary>
        /// Calculates sheet area in m2 for a rectangular duct.
        /// Area = 2*(W + H) * L where W,H in meters, L in meters.
        /// </summary>
        public static double CalcRectangularArea(double widthMm, double heightMm, double lengthM)
        {
            double w = widthMm / 1000.0;
            double h = heightMm / 1000.0;
            return 2.0 * (w + h) * lengthM;
        }

        /// <summary>
        /// Calculates sheet area in m2 for a round duct.
        /// Area = pi * D * L.
        /// </summary>
        public static double CalcRoundArea(double diameterMm, double lengthM)
        {
            double d = diameterMm / 1000.0;
            return Math.PI * d * lengthM;
        }

        /// <summary>
        /// Calculates sheet area in m2 for an oval duct using Ramanujan's perimeter approximation.
        /// P ~ pi * [3(a+b) - sqrt((3a+b)(a+3b))]
        /// </summary>
        public static double CalcOvalArea(double widthMm, double heightMm, double lengthM)
        {
            double a = (widthMm / 1000.0) / 2.0;  // semi-major
            double b = (heightMm / 1000.0) / 2.0;  // semi-minor

            double term = (3.0 * a + b) * (a + 3.0 * b);
            double perimeter = term > 0
                ? Math.PI * (3.0 * (a + b) - Math.Sqrt(term))
                : 0.0;

            return perimeter * lengthM;
        }

        /// <summary>
        /// Calculates the base sheet weight in kg (before allowances).
        /// Weight = Area_m2 * Thickness_m * Density_kg/m3
        /// </summary>
        public static double CalcBaseWeight(double areaM2, double thicknessMm, double densityKgM3)
        {
            double thicknessM = thicknessMm / 1000.0;
            return areaM2 * thicknessM * densityKgM3;
        }

        /// <summary>
        /// Calculates the total fabrication weight including allowances.
        /// </summary>
        public static double CalcTotalWeight(double baseWeightKg, bool reinforcementRequired,
            AllowanceSettings allowances, bool includeAllowances)
        {
            if (!includeAllowances || allowances == null)
                return baseWeightKg;

            double totalPct = allowances.SeamPercent
                            + allowances.JointPercent
                            + allowances.FlangePercent
                            + allowances.FittingsPercent
                            + allowances.WastagePercent;

            if (reinforcementRequired)
                totalPct += allowances.ReinforcementPercent;

            double multiplier = 1.0 + (totalPct / 100.0);
            return baseWeightKg * multiplier;
        }
    }
}
