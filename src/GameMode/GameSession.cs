#region Metadata
/*
 * Tool Name     : AJ Game Mode (session state)
 * File Name     : GameSession.cs
 * Purpose       : Shared state for the Game Mode walkthrough - player position, movement mode,
 *                 the input snapshot written by the HUD window, the HUD feedback written by the
 *                 motion engine, and all movement tuning constants (defined in mm, used in feet).
 *
 * Author        : Ajmal P.S.
 * Version       : 1.8.1
 *
 * Created Date  : 2026-07-28
 * Last Updated  : 2026-07-29
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in
 *
 * Dependencies  : Autodesk Revit API, AJTools.Utils (Constants)
 *
 * Input         : Written by GameHudWindow (keys/mouse) and GameMotionEngine (physics results).
 * Output        : Plain in-memory state - nothing here touches the model.
 *
 * Notes         :
 * - Everything runs on Revit's UI thread (WPF timer + ExternalEvent), so plain fields are safe -
 *   no locking needed.
 * - All distances the player feels are declared in mm (Ajmal thinks in mm) and converted to feet
 *   once, here, via Constants.MM_TO_FEET.
 * - Self-contained: deleting the src/GameMode folder, the Game* images in Resources and the one
 *   ribbon entry removes the whole tool.
 *
 * Changelog     :
 * v1.8.1 (2026-07-29) - ResyncViewQueued input for the aim/display sync fix (suite 1.38.2): the HUD
 *                       queues a re-fit of the picture to the window on resume, the engine answers
 *                       with UIView.ZoomToFit. (Entry added in the 1.38.4 audit - the field shipped
 *                       with 1.38.2 but this changelog was never stamped.)
 * v1.8.0 (2026-07-29) - SelectShotQueued for the new SELECTOR weapon (Revit selection add/remove).
 * v1.7.0 (2026-07-29) - Measuring removed entirely (Ajmal: "not working properly, remove that") -
 *                       all measure state gone. SnagClearQueued renamed ResetGraphicsQueued: J now
 *                       requests a full reset of every element override in the game view (same
 *                       approach as the Reset Element Graphics tool), not just this session's marks.
 * v1.6.0 (2026-07-29) - Round-11 state: snag-marker weapon + clear + count, tour toggle + running
 *                       flag, live speed factor, sound toggle, compass/level text, and the
 *                       engine-projected screen positions for the green rubber-band measure line.
 * v1.5.1 (2026-07-29) - Removed the follow-me section box input (Ajmal: "X no need, remove that").
 * v1.5.0 (2026-07-29) - Workshop-XR-style teleport state (TeleportAimHold input; TeleportAimValid +
 *                       distance outputs) and BookmarksVersion for the new left-side saved-positions
 *                       list.
 * v1.4.0 (2026-07-29) - "Add all" round: WeaponSelect (gun/laser/cleaner) replaces the LaserModeOn
 *                       bool; one-shot inputs for teleport, bookmarks (save/jump), cleaner shot,
 *                       unhide-all and section-box toggle; HUD feedback for look override, toasts
 *                       and live clear height; per-session bookmark store.
 * v1.3.0 (2026-07-29) - Measuring also reports the axis breakdown Ajmal asked for: MeasureDeltaXMm /
 *                       MeasureDeltaYMm alongside the existing Vertical (Z) value.
 * v1.2.0 (2026-07-28) - Added the laser measuring state (MeasureHold input; MeasurePhase +
 *                       Total/Horizontal/Vertical mm outputs) for the BIM-360-style hold-and-release
 *                       face-to-face measure Ajmal asked for.
 * v1.1.0 (2026-07-28) - Ajmal's feedback round after playing: sprint raised 2.2x -> 3.0x ("shift
 *                       running speed increase"); weapon system reworked - LaserOn/L-key toggle and
 *                       per-shot ShootQueued/HitInfo replaced by LaserModeOn (right-click switches
 *                       GUN <-> LASER in the HUD) and a live LaserTargetInfo readout (the laser now
 *                       continuously names the element pointed at; shooting is pure fun, no info).
 * v1.0.0 (2026-07-28) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using Autodesk.Revit.DB;
using AJTools.Utils;

namespace AJTools.Services.GameMode
{
    /// <summary>How the player currently moves through the model.</summary>
    internal enum GameMoveMode
    {
        /// <summary>On the floor: gravity, jumping, stairs, collision.</summary>
        Walk,

        /// <summary>Free flight: no gravity, but walls/slabs still block.</summary>
        Fly,

        /// <summary>Ghost flight: no gravity and nothing blocks - pass through everything.</summary>
        Ghost
    }

    /// <summary>
    /// All movement tuning in one place. Values are stated in mm / mm-per-second and exposed in
    /// Revit-internal feet so the rest of the game code never repeats a conversion.
    /// </summary>
    internal static class GameTuning
    {
        // --- Player body ---
        public const double EyeHeightMm = 1700.0;       // camera height above the floor
        public const double CrouchEyeHeightMm = 1000.0; // eye height while C is held (crouch)
        public const double BodyRadiusMm = 350.0;       // how close the player can get to a face
        public const double StepUpMm = 350.0;           // max height difference walked up/down (stairs)

        // --- Speeds ---
        public const double WalkSpeedMmPerSec = 3500.0;
        public const double FlySpeedMmPerSec = 7000.0;
        public const double SprintFactor = 3.0;
        public const double JumpSpeedMmPerSec = 4800.0; // ~1.15 m jump peak - clears a window sill
        public const double GravityMmPerSec2 = 9810.0;
        public const double MaxFallMmPerSec = 15000.0;

        // --- Interaction ---
        public const double DoorReachMm = 1600.0;       // how near a door must be for [E] to work
        public const double DoorPassExtraMm = 900.0;    // how far beyond the door face you step out
        public const double FloorSearchMm = 500000.0;   // give up looking for a floor 500 m below
        public const double AimRangeMm = 300000.0;      // laser/bullet range (300 m)

        // --- Look ---
        public const double MouseDegPerPixel = 0.15;
        public const double PitchClampDeg = 85.0;

        // --- Frame pacing ---
        public const int FrameIntervalMs = 33;          // HUD timer tick (~30 fps target)

        public static double EyeHeightFt { get { return EyeHeightMm * Constants.MM_TO_FEET; } }
        public static double CrouchEyeHeightFt { get { return CrouchEyeHeightMm * Constants.MM_TO_FEET; } }
        public static double BodyRadiusFt { get { return BodyRadiusMm * Constants.MM_TO_FEET; } }
        public static double StepUpFt { get { return StepUpMm * Constants.MM_TO_FEET; } }
        public static double WalkSpeedFtPerSec { get { return WalkSpeedMmPerSec * Constants.MM_TO_FEET; } }
        public static double FlySpeedFtPerSec { get { return FlySpeedMmPerSec * Constants.MM_TO_FEET; } }
        public static double JumpSpeedFtPerSec { get { return JumpSpeedMmPerSec * Constants.MM_TO_FEET; } }
        public static double GravityFtPerSec2 { get { return GravityMmPerSec2 * Constants.MM_TO_FEET; } }
        public static double MaxFallFtPerSec { get { return MaxFallMmPerSec * Constants.MM_TO_FEET; } }
        public static double DoorReachFt { get { return DoorReachMm * Constants.MM_TO_FEET; } }
        public static double DoorPassExtraFt { get { return DoorPassExtraMm * Constants.MM_TO_FEET; } }
        public static double FloorSearchFt { get { return FloorSearchMm * Constants.MM_TO_FEET; } }
        public static double AimRangeFt { get { return AimRangeMm * Constants.MM_TO_FEET; } }
    }

    /// <summary>
    /// The input snapshot: written by the HUD window (keyboard/mouse), read - and for one-shot
    /// actions consumed - by the motion engine.
    /// </summary>
    internal sealed class GameInputState
    {
        // Held movement keys
        public bool MoveForward;
        public bool MoveBack;
        public bool StrafeLeft;
        public bool StrafeRight;
        public bool AscendKey;      // fly/ghost up (Space held)
        public bool DescendKey;     // fly/ghost down (C or Ctrl held)
        public bool Sprint;         // Shift held

        // Look direction - owned by the HUD (mouse), applied by the engine
        public double YawDeg;
        public double PitchDeg;

        // One-shot actions - set true by the HUD, set back to false by the engine when handled
        public bool JumpQueued;
        public bool DoorPassQueued;
        public bool RespawnQueued;
        public bool FlyToggleQueued;
        public bool GhostToggleQueued;

        // Weapon selection owned by the HUD (right-click cycles): 0 = gun (HUD-side fun fire),
        // 1 = laser (live distance + identity), 2 = cleaner (temporary hide, U restores),
        // 3 = snag marker (paint red + report), 4 = selector (Revit selection add/remove).
        public byte WeaponSelect;

        // Workshop-XR-style teleport: hold T = aiming (engine feeds the arc target), release T =
        // the HUD queues the actual jump.
        public bool TeleportAimHold;

        // One-shot extras (engine consumes)
        public bool TeleportQueued;          // set on T release - jump to the aimed point
        public bool SaveBookmarkQueued;      // B - save current position into the next slot
        public int JumpBookmarkSlot;         // 1..9 to jump, 0 = none
        public bool CleanShotQueued;         // cleaner weapon shot
        public bool UnhideAllQueued;         // U - restore everything temporarily hidden
        public bool SnagShotQueued;          // snag-marker weapon shot (paints red + report list)
        public bool SelectShotQueued;        // selector weapon shot (adds/removes Revit selection)
        public bool ResetGraphicsQueued;     // J - reset ALL element colors in the game view
        public bool ResyncViewQueued;        // re-fit the picture to the window (aim/display sync)
        public bool TourToggleQueued;        // O - auto-walk through the saved positions

        // Live walking-speed multiplier ( + / - keys, 0.4 .. 3.0 )
        public double SpeedFactor = 1.0;

        // Shot sound effects on/off (M toggles)
        public bool SoundOn = true;
    }

    /// <summary>
    /// What the engine tells the HUD after each frame - hints, laser distance, shot results,
    /// where the Revit view sits on screen, and whether the session is still alive.
    /// </summary>
    internal sealed class GameHudState
    {
        public string ModeLine = "";
        public string PositionLine = "";
        public bool DoorHint;
        public bool WindowHint;
        public bool NoFloorHover;

        public bool LaserHasHit;
        public double LaserDistanceMm;

        /// <summary>Live identity of whatever the laser touches (empty when no hit / laser off).</summary>
        public string LaserTargetInfo = "";

        // Teleport aiming (while T is held): is there a valid landing point, and how far is it.
        public bool TeleportAimValid;
        public double TeleportAimDistanceMm;

        // Compass + level ("N | Level 03"), snag count, tour state.
        public string CompassText = "";
        public int SnagCount;
        public bool TourRunning;

        // Engine -> HUD: apply this look direction on the next tick (bookmark jumps restore the
        // saved view direction; the HUD owns yaw/pitch, so the engine asks instead of writing).
        public bool LookOverridePending;
        public double LookOverrideYawDeg;
        public double LookOverridePitchDeg;

        // Short status toast ("Saved position 3", "Hidden: Duct ...", "Photo saved ..."), shown
        // for ~2.5 s from the stamp.
        public string ToastText = "";
        public long ToastStampMs;

        // Live floor-to-obstruction clear height under/above the player, mm (0 = unknown).
        public double ClearHeightMm;

        /// <summary>Show a toast on the HUD (usable from engine and HUD alike - same thread).</summary>
        public void Toast(string text)
        {
            ToastText = text;
            ToastStampMs = System.DateTimeOffset.Now.ToUnixTimeMilliseconds();
        }

        // The Revit view's window rectangle in raw screen pixels (device pixels, can be negative
        // on a second monitor). The HUD overlay follows this rectangle.
        public bool RectValid;
        public int RectLeft;
        public int RectTop;
        public int RectRight;
        public int RectBottom;

        public double LastFrameMs;

        public bool Alive = true;
        public string StopReason = "";
    }

    /// <summary>One running Game Mode session (there is at most one at a time).</summary>
    internal sealed class GameSession
    {
        public GameSession(ElementId gameViewId, string documentTitle, XYZ startPosition, double startYawDeg)
        {
            GameViewId = gameViewId;
            DocumentTitle = documentTitle;
            Position = startPosition;
            StartPosition = startPosition;
            Input = new GameInputState();
            Hud = new GameHudState();
            Input.YawDeg = startYawDeg;
            Input.PitchDeg = 0.0;
            Mode = GameMoveMode.Walk;
            Running = true;
        }

        public ElementId GameViewId { get; private set; }
        public string DocumentTitle { get; private set; }

        /// <summary>Camera (eye) position in Revit internal feet.</summary>
        public XYZ Position;
        public XYZ StartPosition;
        public double VerticalVelocityFtPerSec;
        public bool Grounded;
        public GameMoveMode Mode;

        public bool Running;

        public GameInputState Input { get; private set; }
        public GameHudState Hud { get; private set; }

        /// <summary>
        /// Saved positions for this game session: slot (1, 2, 3, ... no limit) ->
        /// [x, y, z, yawDeg, pitchDeg]. Number keys jump to 1..9; the tour and the
        /// left-side list cover every slot.
        /// </summary>
        public System.Collections.Generic.Dictionary<int, double[]> Bookmarks { get; private set; }
            = new System.Collections.Generic.Dictionary<int, double[]>();

        /// <summary>Next bookmark slot B will write into (just keeps counting up - no limit).</summary>
        public int NextBookmarkSlot = 1;

        /// <summary>Bumped on every save so the HUD knows to rebuild the left-side positions list.</summary>
        public int BookmarksVersion;
    }
}
