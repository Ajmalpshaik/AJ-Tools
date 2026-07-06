using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using AJTools.Models.DuctStandards;

namespace AJTools.Services.DuctStandards
{
    internal static class DuctStandardsConfigService
    {
        private static readonly string ConfigFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AJ Tools", "DuctStandards");

        private static readonly string ConfigPath = Path.Combine(ConfigFolder, "duct_standards_config.json");

        public static string GetConfigFilePath()
        {
            return ConfigPath;
        }

        public static DuctStandardsConfig Load()
        {
            return Load(out _);
        }

        /// <summary>
        /// Loads the saved config. <paramref name="configWasInvalid"/> is true when the saved file
        /// existed but could not be read/parsed, so the caller can warn the modeller that their
        /// customized rules/materials were NOT used and generic defaults were substituted instead.
        /// </summary>
        public static DuctStandardsConfig Load(out bool configWasInvalid)
        {
            configWasInvalid = false;
            EnsureDefault();
            try
            {
                string json = File.ReadAllText(ConfigPath);
                var config = JsonConvert.DeserializeObject<DuctStandardsConfig>(json);
                if (config == null)
                {
                    configWasInvalid = true;
                    return CreateDefault();
                }

                return config;
            }
            catch
            {
                configWasInvalid = true;
                return CreateDefault();
            }
        }

        public static bool Save(DuctStandardsConfig config)
        {
            try
            {
                EnsureFolder();
                string json = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(ConfigPath, json);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static DuctStandardsConfig ImportFromFile(string filePath)
        {
            string json = File.ReadAllText(filePath);
            var config = JsonConvert.DeserializeObject<DuctStandardsConfig>(json);
            if (config == null)
                throw new InvalidOperationException("Invalid JSON configuration file.");
            return config;
        }

        public static bool ExportToFile(DuctStandardsConfig config, string filePath)
        {
            try
            {
                string json = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(filePath, json);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static DuctStandardsConfig CreateDefault()
        {
            var config = new DuctStandardsConfig
            {
                General = new GeneralSettings(),
                DefaultMaterial = "GI",
                DefaultPressureClass = "low",
                PressureClasses = new List<string> { "low", "medium", "high" },
                Allowances = new AllowanceSettings(),
                ParameterMap = new DuctParameterMap(),
                Materials = new List<MaterialInfo>
                {
                    new MaterialInfo { Name = "GI", DensityKgM3 = 7850.0 },
                    new MaterialInfo { Name = "Stainless Steel", DensityKgM3 = 8000.0 },
                    new MaterialInfo { Name = "Aluminium", DensityKgM3 = 2700.0 },
                    new MaterialInfo { Name = "Black Steel", DensityKgM3 = 7850.0 }
                },
                Rules = CreateDefaultRules()
            };
            return config;
        }

        public static void ResetToDefault()
        {
            Save(CreateDefault());
        }

        private static void EnsureDefault()
        {
            if (!File.Exists(ConfigPath))
            {
                Save(CreateDefault());
            }
        }

        private static void EnsureFolder()
        {
            if (!Directory.Exists(ConfigFolder))
                Directory.CreateDirectory(ConfigFolder);
        }

        private static List<DuctRule> CreateDefaultRules()
        {
            var rules = new List<DuctRule>();

            // --- Rectangular ---
            // Low pressure
            rules.Add(new DuctRule { Shape = "rectangular", Pressure = "low", MinMm = 0, MaxMm = 400, ThicknessMm = 0.60, Gauge = "26", Reinforcement = false });
            rules.Add(new DuctRule { Shape = "rectangular", Pressure = "low", MinMm = 401, MaxMm = 550, ThicknessMm = 0.80, Gauge = "24", Reinforcement = false });
            rules.Add(new DuctRule { Shape = "rectangular", Pressure = "low", MinMm = 551, MaxMm = 1200, ThicknessMm = 1.00, Gauge = "22", Reinforcement = true });
            rules.Add(new DuctRule { Shape = "rectangular", Pressure = "low", MinMm = 1201, MaxMm = 99999, ThicknessMm = 1.20, Gauge = "20", Reinforcement = true });
            // Medium pressure
            rules.Add(new DuctRule { Shape = "rectangular", Pressure = "medium", MinMm = 0, MaxMm = 400, ThicknessMm = 0.80, Gauge = "24", Reinforcement = false });
            rules.Add(new DuctRule { Shape = "rectangular", Pressure = "medium", MinMm = 401, MaxMm = 550, ThicknessMm = 1.00, Gauge = "22", Reinforcement = false });
            rules.Add(new DuctRule { Shape = "rectangular", Pressure = "medium", MinMm = 551, MaxMm = 1200, ThicknessMm = 1.20, Gauge = "20", Reinforcement = true });
            rules.Add(new DuctRule { Shape = "rectangular", Pressure = "medium", MinMm = 1201, MaxMm = 99999, ThicknessMm = 1.50, Gauge = "18", Reinforcement = true });
            // High pressure
            rules.Add(new DuctRule { Shape = "rectangular", Pressure = "high", MinMm = 0, MaxMm = 400, ThicknessMm = 1.00, Gauge = "22", Reinforcement = false });
            rules.Add(new DuctRule { Shape = "rectangular", Pressure = "high", MinMm = 401, MaxMm = 550, ThicknessMm = 1.20, Gauge = "20", Reinforcement = true });
            rules.Add(new DuctRule { Shape = "rectangular", Pressure = "high", MinMm = 551, MaxMm = 1200, ThicknessMm = 1.50, Gauge = "18", Reinforcement = true });
            rules.Add(new DuctRule { Shape = "rectangular", Pressure = "high", MinMm = 1201, MaxMm = 99999, ThicknessMm = 1.90, Gauge = "16", Reinforcement = true });

            // --- Round ---
            // Low pressure
            rules.Add(new DuctRule { Shape = "round", Pressure = "low", MinMm = 0, MaxMm = 406, ThicknessMm = 0.55, Gauge = "26", Reinforcement = false });
            rules.Add(new DuctRule { Shape = "round", Pressure = "low", MinMm = 407, MaxMm = 559, ThicknessMm = 0.70, Gauge = "24", Reinforcement = false });
            rules.Add(new DuctRule { Shape = "round", Pressure = "low", MinMm = 560, MaxMm = 1219, ThicknessMm = 0.86, Gauge = "22", Reinforcement = false });
            rules.Add(new DuctRule { Shape = "round", Pressure = "low", MinMm = 1220, MaxMm = 99999, ThicknessMm = 1.00, Gauge = "20", Reinforcement = true });
            // Medium pressure
            rules.Add(new DuctRule { Shape = "round", Pressure = "medium", MinMm = 0, MaxMm = 381, ThicknessMm = 0.61, Gauge = "24", Reinforcement = false });
            rules.Add(new DuctRule { Shape = "round", Pressure = "medium", MinMm = 382, MaxMm = 686, ThicknessMm = 0.76, Gauge = "22", Reinforcement = false });
            rules.Add(new DuctRule { Shape = "round", Pressure = "medium", MinMm = 687, MaxMm = 1067, ThicknessMm = 0.91, Gauge = "20", Reinforcement = false });
            rules.Add(new DuctRule { Shape = "round", Pressure = "medium", MinMm = 1068, MaxMm = 1524, ThicknessMm = 1.21, Gauge = "18", Reinforcement = true });
            rules.Add(new DuctRule { Shape = "round", Pressure = "medium", MinMm = 1525, MaxMm = 99999, ThicknessMm = 1.52, Gauge = "16", Reinforcement = true });
            // High pressure
            rules.Add(new DuctRule { Shape = "round", Pressure = "high", MinMm = 0, MaxMm = 381, ThicknessMm = 0.76, Gauge = "22", Reinforcement = false });
            rules.Add(new DuctRule { Shape = "round", Pressure = "high", MinMm = 382, MaxMm = 686, ThicknessMm = 0.91, Gauge = "20", Reinforcement = false });
            rules.Add(new DuctRule { Shape = "round", Pressure = "high", MinMm = 687, MaxMm = 1067, ThicknessMm = 1.21, Gauge = "18", Reinforcement = true });
            rules.Add(new DuctRule { Shape = "round", Pressure = "high", MinMm = 1068, MaxMm = 1524, ThicknessMm = 1.52, Gauge = "16", Reinforcement = true });
            rules.Add(new DuctRule { Shape = "round", Pressure = "high", MinMm = 1525, MaxMm = 99999, ThicknessMm = 1.90, Gauge = "14", Reinforcement = true });

            // --- Oval ---
            // Low pressure
            rules.Add(new DuctRule { Shape = "oval", Pressure = "low", MinMm = 0, MaxMm = 400, ThicknessMm = 0.60, Gauge = "26", Reinforcement = false });
            rules.Add(new DuctRule { Shape = "oval", Pressure = "low", MinMm = 401, MaxMm = 550, ThicknessMm = 0.80, Gauge = "24", Reinforcement = false });
            rules.Add(new DuctRule { Shape = "oval", Pressure = "low", MinMm = 551, MaxMm = 1200, ThicknessMm = 1.00, Gauge = "22", Reinforcement = true });
            rules.Add(new DuctRule { Shape = "oval", Pressure = "low", MinMm = 1201, MaxMm = 99999, ThicknessMm = 1.20, Gauge = "20", Reinforcement = true });
            // Medium pressure
            rules.Add(new DuctRule { Shape = "oval", Pressure = "medium", MinMm = 0, MaxMm = 400, ThicknessMm = 0.80, Gauge = "24", Reinforcement = false });
            rules.Add(new DuctRule { Shape = "oval", Pressure = "medium", MinMm = 401, MaxMm = 550, ThicknessMm = 1.00, Gauge = "22", Reinforcement = false });
            rules.Add(new DuctRule { Shape = "oval", Pressure = "medium", MinMm = 551, MaxMm = 1200, ThicknessMm = 1.20, Gauge = "20", Reinforcement = true });
            rules.Add(new DuctRule { Shape = "oval", Pressure = "medium", MinMm = 1201, MaxMm = 99999, ThicknessMm = 1.50, Gauge = "18", Reinforcement = true });
            // High pressure
            rules.Add(new DuctRule { Shape = "oval", Pressure = "high", MinMm = 0, MaxMm = 400, ThicknessMm = 1.00, Gauge = "22", Reinforcement = false });
            rules.Add(new DuctRule { Shape = "oval", Pressure = "high", MinMm = 401, MaxMm = 550, ThicknessMm = 1.20, Gauge = "20", Reinforcement = true });
            rules.Add(new DuctRule { Shape = "oval", Pressure = "high", MinMm = 551, MaxMm = 1200, ThicknessMm = 1.50, Gauge = "18", Reinforcement = true });
            rules.Add(new DuctRule { Shape = "oval", Pressure = "high", MinMm = 1201, MaxMm = 99999, ThicknessMm = 1.90, Gauge = "16", Reinforcement = true });

            return rules;
        }
    }
}
