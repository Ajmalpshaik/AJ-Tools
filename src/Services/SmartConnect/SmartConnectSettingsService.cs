// Tool Name: Connect MEP Elements (Smart Connect) - Settings Service
// Description: Persists Connect MEP Elements settings to local JSON storage.
// Author: Ajmal P.S.
// Version: 2.0.0
// Last Updated: 2026-08-15
// Revit Version: 2020
// Dependencies: Newtonsoft.Json, AJTools.Models
//
// Note: the maximum bend angle is 90 degrees. Earlier builds let the window save angles up to 175,
// which the route builder then rejected on every pick. Anything above 90 found in an older settings
// file is clamped back to 90 on load.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using AJTools.Models;
using Newtonsoft.Json;

namespace AJTools.Services.SmartConnect
{
    /// <summary>
    /// Loads and saves Connect MEP Elements settings to local JSON storage.
    /// </summary>
    internal sealed class SmartConnectSettingsService
    {
        private const string SettingsFolderName = "AJTools";
        private const string SettingsFileName = "SmartConnect.settings.json";

        public const double MinAllowedAngle = 5.0;
        public const double MaxAllowedAngle = 90.0;

        private const double AngleEqualityTolerance = 0.0001;

        private static readonly double[] DefaultFallbackAngles = { 45.0, 30.0, 60.0, 90.0 };

        public SmartConnectSettings Load()
        {
            try
            {
                string settingsPath = GetSettingsPath();
                if (!File.Exists(settingsPath))
                {
                    return CreateDefault();
                }

                string json = File.ReadAllText(settingsPath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return CreateDefault();
                }

                var settings = JsonConvert.DeserializeObject<SmartConnectSettings>(json);
                return Sanitize(settings);
            }
            catch
            {
                return CreateDefault();
            }
        }

        public void Save(SmartConnectSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            try
            {
                string settingsPath = GetSettingsPath();
                string directory = Path.GetDirectoryName(settingsPath);
                if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                SmartConnectSettings sanitized = Sanitize(settings);
                string json = JsonConvert.SerializeObject(sanitized, Formatting.Indented);
                File.WriteAllText(settingsPath, json);
            }
            catch
            {
                // Settings persistence failure should never block tool execution.
            }
        }

        public static bool TryNormalizeAngle(double angleDegrees, out double normalizedAngle)
        {
            normalizedAngle = 0;
            if (double.IsNaN(angleDegrees) || double.IsInfinity(angleDegrees))
            {
                return false;
            }

            if (angleDegrees < MinAllowedAngle || angleDegrees > MaxAllowedAngle)
            {
                return false;
            }

            normalizedAngle = Math.Round(angleDegrees, 2, MidpointRounding.AwayFromZero);
            return true;
        }

        public static bool TryParseAngle(string rawText, out double normalizedAngle)
        {
            normalizedAngle = 0;
            string text = rawText == null ? null : rawText.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            double value;
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) &&
                !double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                return false;
            }

            return TryNormalizeAngle(value, out normalizedAngle);
        }

        public static bool IsPredefinedAngle(double angleDegrees)
        {
            return AreAnglesEqual(angleDegrees, 45.0) || AreAnglesEqual(angleDegrees, 90.0);
        }

        public static bool AreAnglesEqual(double first, double second)
        {
            return Math.Abs(first - second) <= AngleEqualityTolerance;
        }

        /// <summary>
        /// Angles to try for one connection, best first: the chosen angle, then the fallback list
        /// when fallback is enabled. Duplicates are removed so no angle is attempted twice.
        /// </summary>
        public static IList<double> BuildAngleAttemptOrder(SmartConnectSettings settings)
        {
            var order = new List<double>();
            if (settings == null)
            {
                order.Add(90.0);
                return order;
            }

            double selected;
            if (!TryNormalizeAngle(settings.SelectedAngleDegrees, out selected))
            {
                selected = 90.0;
            }

            order.Add(selected);

            if (!settings.UseAngleFallback)
            {
                return order;
            }

            IEnumerable<double> fallbacks = settings.FallbackAngles ?? new List<double>();
            foreach (double candidate in fallbacks)
            {
                double normalized;
                if (!TryNormalizeAngle(candidate, out normalized))
                {
                    continue;
                }

                if (!order.Any(existing => AreAnglesEqual(existing, normalized)))
                {
                    order.Add(normalized);
                }
            }

            return order;
        }

        private static SmartConnectSettings CreateDefault()
        {
            return Sanitize(new SmartConnectSettings());
        }

        private static SmartConnectSettings Sanitize(SmartConnectSettings settings)
        {
            SmartConnectSettings result = settings ?? new SmartConnectSettings();

            if (!Enum.IsDefined(typeof(SmartConnectRoutingMode), result.RoutingMode))
            {
                result.RoutingMode = SmartConnectRoutingMode.Auto;
            }

            if (!Enum.IsDefined(typeof(SmartConnectMoveMode), result.MoveMode))
            {
                result.MoveMode = SmartConnectMoveMode.Both;
            }

            result.CustomAngles = SanitizeAngleList(result.CustomAngles, true, false);

            double selectedAngle;
            if (!TryNormalizeAngle(result.SelectedAngleDegrees, out selectedAngle))
            {
                // Clamp rather than discard, so an older file that stored e.g. 120 degrees
                // lands on the nearest angle the tool can actually build.
                selectedAngle = ClampAngle(result.SelectedAngleDegrees);
            }

            if (!IsPredefinedAngle(selectedAngle) &&
                !result.CustomAngles.Any(angle => AreAnglesEqual(angle, selectedAngle)))
            {
                result.CustomAngles.Add(selectedAngle);
                result.CustomAngles.Sort();
            }

            result.SelectedAngleDegrees = selectedAngle;

            // Order matters here - it is a try-order, not a display list - so it must not be sorted.
            List<double> fallbacks = SanitizeAngleList(result.FallbackAngles, false, true);
            if (fallbacks.Count == 0)
            {
                fallbacks = DefaultFallbackAngles.ToList();
            }

            result.FallbackAngles = fallbacks;

            return result;
        }

        private static List<double> SanitizeAngleList(IEnumerable<double> source, bool excludePredefined, bool preserveOrder)
        {
            var result = new List<double>();
            if (source == null)
            {
                return result;
            }

            foreach (double angle in source)
            {
                double normalized;
                if (!TryNormalizeAngle(angle, out normalized))
                {
                    continue;
                }

                if (excludePredefined && IsPredefinedAngle(normalized))
                {
                    continue;
                }

                if (!result.Any(existing => AreAnglesEqual(existing, normalized)))
                {
                    result.Add(normalized);
                }
            }

            if (!preserveOrder)
            {
                result.Sort();
            }

            return result;
        }

        private static double ClampAngle(double angleDegrees)
        {
            if (double.IsNaN(angleDegrees) || double.IsInfinity(angleDegrees))
            {
                return 90.0;
            }

            if (angleDegrees < MinAllowedAngle)
            {
                return MinAllowedAngle;
            }

            if (angleDegrees > MaxAllowedAngle)
            {
                return MaxAllowedAngle;
            }

            return Math.Round(angleDegrees, 2, MidpointRounding.AwayFromZero);
        }

        private static string GetSettingsPath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, SettingsFolderName, SettingsFileName);
        }
    }
}
