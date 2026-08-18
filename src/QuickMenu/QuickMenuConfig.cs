#region Metadata
/*
 * Tool Name     : AJ Quick Menu (saved layout)
 * File Name     : QuickMenuConfig.cs
 * Purpose       : Ajmal's own wheel layout - how many slots, how big the wheel is, and which tool
 *                 sits in each slot. Saved to %APPDATA%\AJTools\quickmenu-slots.txt so it survives
 *                 Revit restarts and add-in updates.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-08-18
 * Last Updated  : 2026-08-18
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : AJTools.Utils.AppDataConfigStore (shared %APPDATA%\AJTools\ path builder)
 *
 * Input         : quickmenu-slots.txt, if it exists.
 * Output        : Same file on Save. Never touches the Revit model.
 *
 * Notes         :
 * - Plain key=value text, same shape as the other AJ Tools config stores. All file IO is
 *   best-effort: a missing, locked or corrupt file falls back to the defaults rather than throwing
 *   in front of Ajmal.
 * - A slot stores the command CLASS name, not the button label, so renaming a ribbon button keeps
 *   the layout intact. An empty value is an empty slot, which the wheel draws greyed out.
 * - Slots is always exactly SlotCount long after Load() - the wheel and the customise window can
 *   both index it without guarding.
 *
 * Changelog     :
 * v1.0.0 (2026-08-18) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using AJTools.Utils;

namespace AJTools.Services.QuickMenu
{
    /// <summary>The saved Quick Menu wheel layout.</summary>
    internal sealed class QuickMenuConfig
    {
        internal const int MinSlotCount = 4;
        internal const int MaxSlotCount = 12;
        internal const int DefaultSlotCount = 8;

        internal const int MinDiameter = 420;
        internal const int MaxDiameter = 760;
        internal const int DefaultDiameter = 560;

        private const string FileName = "quickmenu-slots.txt";

        /// <summary>
        /// What a brand-new install starts with - the everyday tools, in reading order round the
        /// wheel from 12 o'clock. Any of these that is not on the ribbon is simply left blank.
        /// </summary>
        private static readonly string[] DefaultSlotKeys =
        {
            "AJTools.Commands.CmdUnhideAll",
            "AJTools.Commands.CmdToggleRevitLinks",
            "AJTools.Commands.GraphicsTools.CmdHighlightSelection",
            "AJTools.Commands.CmdSmartSelection",
            "AJTools.Commands.CmdColorize",
            "AJTools.Commands.CmdFilterPro",
            "AJTools.Commands.CmdPinElements",
            "AJTools.Commands.CmdMatchElevation"
        };

        private QuickMenuConfig()
        {
            SlotCount = DefaultSlotCount;
            Diameter = DefaultDiameter;
            Slots = new List<string>();
        }

        /// <summary>How many wedges the wheel is cut into (4 to 12).</summary>
        public int SlotCount { get; set; }

        /// <summary>Outer diameter of the wheel in WPF units (420 to 760).</summary>
        public int Diameter { get; set; }

        /// <summary>One command class name per slot, "" for an empty slot. Always SlotCount long.</summary>
        public List<string> Slots { get; private set; }

        /// <summary>Reads the saved layout, or hands back the defaults if there is nothing saved.</summary>
        internal static QuickMenuConfig Load()
        {
            var config = new QuickMenuConfig();
            var readSlots = new Dictionary<int, string>();

            try
            {
                string path = AppDataConfigStore.GetPath(FileName);
                if (File.Exists(path))
                {
                    foreach (string rawLine in File.ReadAllLines(path))
                    {
                        string line = (rawLine ?? string.Empty).Trim();
                        if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                        {
                            continue;
                        }

                        int split = line.IndexOf('=');
                        if (split <= 0)
                        {
                            continue;
                        }

                        string name = line.Substring(0, split).Trim();
                        string value = line.Substring(split + 1).Trim();

                        if (string.Equals(name, "SlotCount", StringComparison.OrdinalIgnoreCase))
                        {
                            config.SlotCount = ParseInt(value, DefaultSlotCount);
                        }
                        else if (string.Equals(name, "Diameter", StringComparison.OrdinalIgnoreCase))
                        {
                            config.Diameter = ParseInt(value, DefaultDiameter);
                        }
                        else if (name.StartsWith("Slot", StringComparison.OrdinalIgnoreCase))
                        {
                            int slotNumber = ParseInt(name.Substring(4), 0);
                            if (slotNumber >= 1 && slotNumber <= MaxSlotCount)
                            {
                                readSlots[slotNumber] = value;
                            }
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < DefaultSlotKeys.Length; i++)
                    {
                        readSlots[i + 1] = DefaultSlotKeys[i];
                    }
                }
            }
            catch (Exception)
            {
                // Unreadable file - fall through with whatever was gathered plus the defaults below.
            }

            config.SlotCount = Clamp(config.SlotCount, MinSlotCount, MaxSlotCount);
            config.Diameter = Clamp(config.Diameter, MinDiameter, MaxDiameter);

            config.Slots = new List<string>(config.SlotCount);
            for (int i = 1; i <= config.SlotCount; i++)
            {
                string value;
                config.Slots.Add(readSlots.TryGetValue(i, out value) ? (value ?? string.Empty) : string.Empty);
            }

            return config;
        }

        /// <summary>Writes the layout back. Best-effort - returns false if the file could not be written.</summary>
        internal bool Save()
        {
            try
            {
                Normalize();

                string path = AppDataConfigStore.GetPath(FileName);
                string folder = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                var lines = new List<string>
                {
                    "# AJ Tools - Quick Menu layout. Slot 1 is at the top, then clockwise.",
                    "SlotCount=" + SlotCount.ToString(CultureInfo.InvariantCulture),
                    "Diameter=" + Diameter.ToString(CultureInfo.InvariantCulture)
                };

                for (int i = 0; i < Slots.Count; i++)
                {
                    lines.Add("Slot" + (i + 1).ToString(CultureInfo.InvariantCulture) + "=" + Slots[i]);
                }

                File.WriteAllLines(path, lines.ToArray());
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Forces SlotCount/Diameter into range and makes Slots exactly SlotCount long.</summary>
        internal void Normalize()
        {
            SlotCount = Clamp(SlotCount, MinSlotCount, MaxSlotCount);
            Diameter = Clamp(Diameter, MinDiameter, MaxDiameter);

            if (Slots == null)
            {
                Slots = new List<string>();
            }

            while (Slots.Count < SlotCount)
            {
                Slots.Add(string.Empty);
            }

            while (Slots.Count > SlotCount)
            {
                Slots.RemoveAt(Slots.Count - 1);
            }

            for (int i = 0; i < Slots.Count; i++)
            {
                if (Slots[i] == null)
                {
                    Slots[i] = string.Empty;
                }
            }
        }

        private static int ParseInt(string text, int fallback)
        {
            int value;
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
                ? value
                : fallback;
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            if (value < minimum)
            {
                return minimum;
            }

            return value > maximum ? maximum : value;
        }
    }
}
