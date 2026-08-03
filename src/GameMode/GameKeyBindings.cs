#region Metadata
/*
 * Tool Name     : AJ Game Mode (key bindings)
 * File Name     : GameKeyBindings.cs
 * Purpose       : Remappable shortcut keys for every Game Mode action (Ajmal's ask: "settings...
 *                 I can press what key, that will save for that key"). Defaults match the original
 *                 layout; changes persist in %AppData%\AJTools\ajgame-keys.txt. Esc, the mouse,
 *                 the scroll wheel, the number keys 1-9 and the arrow keys stay fixed.
 *
 * Author        : Ajmal P.S.
 * Version       : 1.0.0
 *
 * Created Date  : 2026-07-29
 * Last Updated  : 2026-07-29
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : WPF Key enum only - no Revit API.
 *
 * Input/Output  : Plain "ActionId=KeyName" lines in AppData; unknown lines ignored, missing
 *                 actions fall back to their defaults.
 *
 * Changelog     :
 * v1.0.0 (2026-07-29) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Collections.Generic;
using System.Windows.Input;

namespace AJTools.Services.GameMode
{
    /// <summary>One remappable game action.</summary>
    internal sealed class GameKeyAction
    {
        public GameKeyAction(string id, string display, Key defaultKey)
        {
            Id = id;
            Display = display;
            DefaultKey = defaultKey;
        }

        public string Id { get; private set; }
        public string Display { get; private set; }
        public Key DefaultKey { get; private set; }
    }

    /// <summary>The full remappable action list + the live key map with load/save.</summary>
    internal sealed class GameKeyBindings
    {
        public const string MoveForward = "MoveForward";
        public const string MoveBack = "MoveBack";
        public const string StrafeLeft = "StrafeLeft";
        public const string StrafeRight = "StrafeRight";
        public const string Sprint = "Sprint";
        public const string JumpUp = "JumpUp";
        public const string CrouchDown = "CrouchDown";
        public const string Door = "Door";
        public const string Fly = "Fly";
        public const string Ghost = "Ghost";
        public const string Respawn = "Respawn";
        public const string Teleport = "Teleport";
        public const string SavePosition = "SavePosition";
        public const string UnhideAll = "UnhideAll";
        public const string ResetGraphics = "ResetGraphics";
        public const string Tour = "Tour";
        public const string Photo = "Photo";
        public const string Flashlight = "Flashlight";
        public const string SoundToggle = "SoundToggle";
        public const string SpeedUp = "SpeedUp";
        public const string SpeedDown = "SpeedDown";
        public const string Help = "Help";
        public const string Pause = "Pause";
        public const string Professional = "Professional";

        public static readonly IReadOnlyList<GameKeyAction> Actions = new List<GameKeyAction>
        {
            new GameKeyAction(MoveForward,  "Walk forward",                          Key.W),
            new GameKeyAction(MoveBack,     "Walk back",                             Key.S),
            new GameKeyAction(StrafeLeft,   "Walk left",                             Key.A),
            new GameKeyAction(StrafeRight,  "Walk right",                            Key.D),
            new GameKeyAction(Sprint,       "Run (hold)",                            Key.LeftShift),
            new GameKeyAction(JumpUp,       "Jump / fly up (hold)",                  Key.Space),
            new GameKeyAction(CrouchDown,   "Crouch / fly down (hold)",              Key.C),
            new GameKeyAction(Door,         "Go through door",                       Key.E),
            new GameKeyAction(Fly,          "Fly on / off",                          Key.F),
            new GameKeyAction(Ghost,        "Ghost on / off",                        Key.G),
            new GameKeyAction(Respawn,      "Respawn to start",                      Key.R),
            new GameKeyAction(Teleport,     "Teleport (hold + release)",             Key.T),
            new GameKeyAction(SavePosition, "Save position",                         Key.B),
            new GameKeyAction(UnhideAll,    "Bring hidden elements back",            Key.U),
            new GameKeyAction(ResetGraphics,"Reset all colors in game view",         Key.J),
            new GameKeyAction(Tour,         "Tour through saved positions",          Key.O),
            new GameKeyAction(Photo,        "Photo (clean screenshot)",              Key.K),
            new GameKeyAction(Flashlight,   "Flashlight on / off",                   Key.V),
            new GameKeyAction(SoundToggle,  "Sounds on / off",                       Key.M),
            new GameKeyAction(SpeedUp,      "Speed up",                              Key.OemPlus),
            new GameKeyAction(SpeedDown,    "Speed down",                            Key.OemMinus),
            new GameKeyAction(Help,         "Help card on / off",                    Key.H),
            new GameKeyAction(Pause,        "Pause",                                 Key.P),
            new GameKeyAction(Professional, "Professional mode - no gun shown",      Key.N)
        };

        private readonly Dictionary<string, Key> _keyByAction = new Dictionary<string, Key>();
        private readonly Dictionary<Key, string> _actionByKey = new Dictionary<Key, string>();

        private GameKeyBindings()
        {
        }

        public Key Get(string actionId)
        {
            Key key;
            return _keyByAction.TryGetValue(actionId, out key) ? key : Key.None;
        }

        /// <summary>The action bound to this key, or null.</summary>
        public string FindAction(Key key)
        {
            string action;
            return _actionByKey.TryGetValue(key, out action) ? action : null;
        }

        /// <summary>Assign a key; returns false when another action already uses it.</summary>
        public bool TrySet(string actionId, Key key)
        {
            string existing;
            if (_actionByKey.TryGetValue(key, out existing) && existing != actionId)
            {
                return false;
            }

            Key oldKey;
            if (_keyByAction.TryGetValue(actionId, out oldKey))
            {
                _actionByKey.Remove(oldKey);
            }
            _keyByAction[actionId] = key;
            _actionByKey[key] = actionId;
            return true;
        }

        /// <summary>Assign a key unconditionally, stealing it from any current holder.</summary>
        public void ForceSet(string actionId, Key key)
        {
            string holder = FindAction(key);
            if (holder != null && holder != actionId)
            {
                _actionByKey.Remove(key);
                _keyByAction.Remove(holder);
            }

            Key oldKey;
            if (_keyByAction.TryGetValue(actionId, out oldKey))
            {
                _actionByKey.Remove(oldKey);
            }
            _keyByAction[actionId] = key;
            _actionByKey[key] = actionId;
        }

        public static GameKeyBindings Load()
        {
            var bindings = new GameKeyBindings();
            foreach (GameKeyAction action in Actions)
            {
                bindings.TrySet(action.Id, action.DefaultKey);
            }

            try
            {
                string file = GetFilePath();
                if (System.IO.File.Exists(file))
                {
                    foreach (string line in System.IO.File.ReadAllLines(file))
                    {
                        int split = line.IndexOf('=');
                        if (split <= 0)
                        {
                            continue;
                        }

                        string id = line.Substring(0, split).Trim();
                        string keyName = line.Substring(split + 1).Trim();
                        Key key;
                        if (bindings._keyByAction.ContainsKey(id) && Enum.TryParse(keyName, out key))
                        {
                            // Free the saved key from whichever default holds it, then assign.
                            string holder = bindings.FindAction(key);
                            if (holder != null && holder != id)
                            {
                                bindings._actionByKey.Remove(key);
                                bindings._keyByAction.Remove(holder);
                            }
                            Key old = bindings.Get(id);
                            bindings._actionByKey.Remove(old);
                            bindings._keyByAction[id] = key;
                            bindings._actionByKey[key] = id;
                        }
                    }
                }
            }
            catch (Exception)
            {
                // A broken file must never stop the game - defaults stay.
            }

            return bindings;
        }

        public void Save()
        {
            try
            {
                string file = GetFilePath();
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file));
                var lines = new List<string>();
                foreach (GameKeyAction action in Actions)
                {
                    lines.Add(action.Id + "=" + Get(action.Id));
                }
                System.IO.File.WriteAllLines(file, lines);
            }
            catch (Exception)
            {
                // Saving is best-effort; the in-memory map still applies for this session.
            }
        }

        private static string GetFilePath()
        {
            return System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "AJTools",
                "ajgame-keys.txt");
        }
    }
}
