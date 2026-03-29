// Tool Name: Smart Connect - Settings Service
// Description: Persists Smart Connect settings to local JSON storage.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2026-03-25
// Revit Version: 2020
// Dependencies: Newtonsoft.Json, AJTools.Models

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
    /// Loads and saves Smart Connect settings to local JSON storage.
    /// </summary>
    internal sealed class SmartConnectSettingsService
    {
        private const string SettingsFolderName = "AJTools";
        private const string SettingsFileName = "SmartConnect.settings.json";
        private const double MinAllowedAngle = 5.0;
        private const double MaxAllowedAngle = 175.0;
        private const double AngleEqualityTolerance = 0.0001;

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
            string text = rawText?.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out double value) &&
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

        private static SmartConnectSettings CreateDefault()
        {
            return new SmartConnectSettings
            {
                RoutingMode = SmartConnectRoutingMode.SingleElbow,
                SelectedAngleDegrees = 90.0,
                CustomAngles = new List<double>()
            };
        }

        private static SmartConnectSettings Sanitize(SmartConnectSettings settings)
        {
            SmartConnectSettings result = settings ?? CreateDefault();
            result.RoutingMode = SmartConnectRoutingMode.SingleElbow;

            var customAngles = new List<double>();
            if (result.CustomAngles != null)
            {
                foreach (double angle in result.CustomAngles)
                {
                    if (!TryNormalizeAngle(angle, out double normalized))
                    {
                        continue;
                    }

                    if (IsPredefinedAngle(normalized))
                    {
                        continue;
                    }

                    if (!customAngles.Any(existing => AreAnglesEqual(existing, normalized)))
                    {
                        customAngles.Add(normalized);
                    }
                }
            }

            customAngles.Sort();
            result.CustomAngles = customAngles;

            if (!TryNormalizeAngle(result.SelectedAngleDegrees, out double selectedAngle))
            {
                selectedAngle = 90.0;
            }

            if (!IsPredefinedAngle(selectedAngle) &&
                !customAngles.Any(angle => AreAnglesEqual(angle, selectedAngle)))
            {
                customAngles.Add(selectedAngle);
                customAngles.Sort();
            }

            result.SelectedAngleDegrees = selectedAngle;
            return result;
        }

        private static string GetSettingsPath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, SettingsFolderName, SettingsFileName);
        }
    }
}
