#region Metadata
/*
 * Tool Name     : AJ Game Mode (HUD overlay window - core)
 * File Name     : GameHudWindow.xaml.cs
 * Purpose       : The transparent game overlay that sits exactly on top of the Revit view while
 *                 Game Mode runs. This core file owns the window lifetime, the frame tick and the
 *                 pixel-exact placement. The features live in sibling partial files so each stays
 *                 small and easy to edit (Ajmal's request):
 *                   GameHudWindow.Controls.cs - keyboard, mouse look, pause/resume
 *                   GameHudWindow.Weapons.cs  - gun picture, firing FX, holster, weapon cycle
 *                   GameHudWindow.Render.cs   - per-frame HUD drawing (laser, cards, hints)
 *                   GameHudWindow.Photo.cs    - K photo mode
 *
 * Author        : Ajmal P.S.
 * Version       : 1.9.5
 *
 * Created Date  : 2026-07-28
 * Last Updated  : 2026-08-05
 *
 * Target Revit  : 2020 - latest (A: 2020-2024 / B: 2025-2026 / C: 2027+ - verify newest)
 * Framework     : .NET Fx 4.7.2 (2020) / verify 4.8 (2021-2024) | .NET 8 (2025-2026) | 2027+ verify Autodesk SDK
 * Platform      : C# Revit Add-in (WPF, modeless overlay)
 *
 * Dependencies  : GameSession (shared state), ExternalEvent -> GameMotionEngine, user32
 *                 (GetCursorPos/SetCursorPos/SetWindowPos for FPS mouse-look and pixel-exact
 *                 window placement over the Revit view).
 *
 * Input         : Keyboard + mouse while focused. All physics/Revit work happens in the engine.
 * Output        : Visual HUD only - this window never touches the Revit model.
 *
 * Notes         :
 * - Positioning uses raw device pixels end-to-end (UIView.GetWindowRectangle -> SetWindowPos,
 *   GetCursorPos -> SetCursorPos), so mixed-DPI multi-monitor setups need no DIP conversion.
 * - Pause shrinks the overlay to a small "click to continue" pill at the top of the view, so
 *   Revit underneath is fully usable (change VG/filters mid-game, then resume).
 * - Everything on Revit's UI thread; no Application.Current anywhere (house rule).
 * - Visibility values are fully qualified (System.Windows.Visibility.*) per the house rule about
 *   UIElement.Visibility shadowing the enum type name.
 * - ALL instance fields and consts of the class live in THIS file (partial-class house style).
 *
 * Changelog     :
 * v1.9.5 (2026-08-05) - Camera-whip fix (suite 1.40.6), Ajmal's report: "in the selection gun, if I
 *                       shoot anything the camera goes flying/jumping". The SELECTOR is the only
 *                       weapon that changes the Revit SELECTION, and changing the selection makes
 *                       Revit re-lay-out its own chrome (Options Bar in/out, contextual "Modify |
 *                       ..." tab), which moves the game view's window rectangle. The HUD follows
 *                       that rectangle in OnTick via ApplyPixelRect(remember: true), which silently
 *                       moved _full* - and the look centre is derived from _full*. The physical
 *                       pointer was still parked on the OLD centre from the previous SetCursorPos,
 *                       so the next mouse move measured (old centre - new centre) as real aiming
 *                       and turned it straight into yaw/pitch at 0.15 deg/px. Nothing re-centred
 *                       the pointer when the rectangle moved. Three changes: (1) ApplyPixelRect
 *                       re-centres the pointer whenever a remembered rectangle moves the centre and
 *                       the look is active (comparing edge SUMS, so a resize about a fixed middle
 *                       is left alone); (2) StartLook centres before arming _mouseLookActive, so
 *                       the move event WPF raises on capture cannot be read as a turn; (3)
 *                       OnGameMouseMove drops any single step beyond MaxLookStepPx (400 px) and
 *                       re-centres - a backstop for DPI changes / remote-session cursor warps, not
 *                       the fix itself. Physics were ruled out: the engine's dt is already clamped
 *                       to 0.25 s, so a slow frame cannot rocket the player.
 * v1.9.4 (2026-07-29) - Audit pass, zero behaviour change: removed the dead PxToDip helper from
 *                       the Render partial (orphaned when measuring was deleted in v1.8.0) and
 *                       corrected the stale partial-file headers (Weapons listed 3 weapons, Render
 *                       still mentioned the measuring card).
 * v1.9.3 (2026-07-29) - Ajmal's final weapon color scheme applied everywhere (crosshair, laser
 *                       beam/dot/texts, gun accents, glows, weapon text): GUN amber, LASER green
 *                       (the beam itself is green now), CLEANER black & white (white crosshair
 *                       bars with black edges), SNAG red, SELECTOR blue.
 * v1.9.2 (2026-07-29) - (1) The crosshair now wears the active tool's color (gun blue / laser red /
 *                       cleaner amber / snag magenta / selector green) - the weapon indicator that
 *                       survives professional mode. (2) Aim/display sync fix, Ajmal's live report
 *                       ("if I shoot one duct, the work is happening somewhere else... opening
 *                       Properties makes it okay"): Revit can 2D-zoom/pan a perspective view like a
 *                       photo, drifting the picture centre off the camera axis; the engine now
 *                       calls UIView.ZoomToFit on the game view at start, on every resume and on
 *                       every window resize, so the crosshair and the real aim always match.
 * v1.9.1 (2026-07-29) - Teleport visual finalized to Ajmal's VR reference image: thick SOLID green
 *                       ballistic arc (cubic bezier) rising from the muzzle and dropping onto the
 *                       landing point; the landing marker is now a double flattened disc + dot
 *                       lying flat on the floor. Colors/curve match the approved sample render.
 * v1.9.0 (2026-07-29) - (1) PROFESSIONAL MODE (N, remembered in AppData): no gun on screen ever -
 *                       presentable for managers/meetings; every tool works the same, beams start
 *                       from the bottom of the view; muzzle flash/recoil/holster disabled while on.
 *                       (2) 5th weapon SELECTOR (green): shot selects the element in the live Revit
 *                       selection, shot again unselects; the selection stays after exiting the game.
 * v1.8.0 (2026-07-29) - (1) Measuring removed entirely per Ajmal ("not working properly") - the
 *                       rubber-band line, dimension badge and measure card are gone; the laser
 *                       keeps its live distance + element identity. (2) All shortcut keys are now
 *                       REMAPPABLE: pause (Esc) then S opens the new Key Settings window
 *                       (GameKeySettingsWindow + GameKeyBindings, saved to AppData). Esc, mouse,
 *                       wheel, 1-9 and arrows stay fixed. (3) J now resets ALL element colors in
 *                       the game view (Reset-Element-Graphics style), catching snag marks from
 *                       earlier sessions too.
 * v1.7.3 (2026-07-29) - CLEANER rifle alignment fix (Ajmal: "gun and line slightly need to
 *                       rotate"): the rifle is baked with a 16-degree clockwise rotation and
 *                       repositioned so the barrel lies exactly on the beam line to the crosshair
 *                       (muzzle back at the ~17-degree line; fractions 0.028/0.147, display 280,
 *                       bleed 119/-5). Snag blaster confirmed perfect by Ajmal - untouched.
 * v1.7.2 (2026-07-29) - The SNAG MARKER now carries Ajmal's blaster picture ("SNAG gun .png" ->
 *                       Resources\GameSnagGun.png, transparency already real, residual green keyed,
 *                       untouched otherwise; muzzle at the orange tip, fractions 0.022/0.087).
 *                       Weapon cycle now shows: pistol (gun/laser) / rifle (cleaner) / blaster (snag).
 * v1.7.1 (2026-07-29) - The CLEANER weapon now carries Ajmal's rifle picture (Y:\Ajmal Ps\icon\
 *                       rifle.png -> Resources\GameRifle.png; the source already had a transparent
 *                       background - only residual green keyed + RGB blanked under transparency so
 *                       scaled edges can't tint green; kept untouched otherwise, muzzle tracked to
 *                       fractions 0.034/0.095 at the flash hider). Right-click to CLEANER swaps the
 *                       pistol for the rifle; muzzle glow/flash/tracer/laser follow the rifle tip.
 * v1.7.0 (2026-07-29) - Round 11: GREEN rubber-band measure line anchored to the first face (with
 *                       the mm dimension riding the line, live and after locking); 4th weapon SNAG
 *                       MARKER (magenta, J clears); tour (O), flashlight (V), sounds (M, synthesized
 *                       gunshot - no sound files), speed dial (+/-), crouch on C, compass/level and
 *                       snag-count lines in the status card.
 * v1.6.0 (2026-07-29) - Workshop-XR-style teleport, per Ajmal's reference to Autodesk Workshop XR:
 *                       HOLD T shows a glowing dashed jump ARC from the player to the crosshair
 *                       with a pulsing landing ring + distance ("Release T - jump 12,340 mm");
 *                       releasing T is the OK that performs the jump; invalid targets show gray.
 *                       Plus the left-side SAVED POSITIONS panel: every B-saved spot lists its
 *                       number + X/Y/Z in mm; pressing the number jumps there (rows rebuilt via
 *                       the session's BookmarksVersion). T no longer teleports instantly on press.
 * v1.5.1 (2026-07-29) - Restructure only, zero behaviour change (Ajmal: one GameMode folder, one
 *                       file per feature): moved to src/GameMode and split into 5 partial files -
 *                       core here; Controls / Weapons / Render / Photo in their own files.
 * v1.5.0 (2026-07-29) - "Add all" round: right-click now cycles GUN / LASER / CLEANER (amber accent;
 *                       one shot per click, queues the engine's temporary hide; U restores). New
 *                       keys: T teleport, B save position, 1-9 jump to saved positions (with look
 *                       restore via the engine's override), X follow-me section box, K photo mode
 *                       (HUD hidden for one frame, the Revit view area captured via screen pixels
 *                       to Pictures\AJ Game Photos). New HUD: toast card for short status messages
 *                       and a live "Clear height" line (floor-to-obstruction, mm) in the status
 *                       card. Help card rewritten to cover everything.
 * v1.4.0 (2026-07-29) - Scroll-wheel holster; measuring card axis breakdown (X/Y/Z + plan).
 * v1.3.x (2026-07-28) - Laser measuring UI; Ajmal's own gun picture (kept exactly as generated,
 *                       muzzle at the front sight tip); corner placement tuned via rendered
 *                       previews. Full detail in this changelog's history within the repo.
 * v1.1.0 (2026-07-28) - Auto-fire, impact sparks, gun/laser switch on right-click, live laser
 *                       identity, realistic vector pistol.
 * v1.0.0 (2026-07-28) - Initial release.
 *
 * License       : All Rights Reserved
 * Repo          : AJ-Tools
 */
#endregion

using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Autodesk.Revit.UI;
using AJTools.Services.GameMode;

namespace AJTools.UI.GameMode
{
    /// <summary>Transparent overlay HUD + input capture for Game Mode.</summary>
    public partial class GameHudWindow : Window
    {
        private const int PillWidthPx = 400;
        private const int PillHeightPx = 160;
        private const int FireIntervalMs = 130;   // automatic fire cadence while left button held
        private const int ImpactDelayMs = 100;    // splash lands when the bullet visually arrives
        private const int MaxLookStepPx = 400;    // largest believable single mouse-look step (see OnGameMouseMove)

        private readonly GameSession _session;
        private readonly ExternalEvent _frameEvent;
        private readonly DispatcherTimer _timer;
        private readonly GameKeyBindings _keys = GameKeyBindings.Load();

        private IntPtr _hwnd = IntPtr.Zero;
        private bool _paused;
        private bool _ending;
        private bool _closedDown;
        private int _endTicks;
        private bool _helpVisible = true;
        private bool _mouseLookActive;
        private bool _firing;
        private long _lastShotMs;
        private int _shotCount;
        private System.Windows.Shapes.Line[] _sparks;

        // Ajmal's own gun picture (Resources\GameGun.png), used instead of the vector pistol
        // when present. Ajmal confirmed (2026-07-28) the picture must stay EXACTLY as generated -
        // no flip, no tilt - so processing is background-removal + trim only. The gun POINTS
        // UP-LEFT into the scene: the single-dot front sight sits at the top-left tip (= the real
        // muzzle, these fractions), while the round disc at the lower-right end is the striker
        // back plate - NOT the bore (the laser wrongly started there once; Ajmal caught it).
        private const double CustomGunDisplayHeight = 300.0;
        private const double CustomGunBleedRight = 40.0;
        private const double CustomGunBleedBottom = 60.0;
        private const double CustomMuzzleFractionX = 0.041;
        private const double CustomMuzzleFractionY = 0.081;
        private bool _customGunActive;
        private Point _customMuzzleCanvasPoint;

        // Ajmal's rifle picture (Resources\GameRifle.png) - the CLEANER weapon's gun. Rotated 16
        // degrees clockwise at bake time (Ajmal: barrel and beam must align) and positioned so the
        // muzzle sits exactly on the ~17-degree line to the crosshair - barrel + beam read as one.
        private const double RifleDisplayHeight = 280.0;
        private const double RifleBleedRight = 119.0;
        private const double RifleBleedBottom = -5.0;
        private const double RifleMuzzleFractionX = 0.028;
        private const double RifleMuzzleFractionY = 0.147;
        private bool _rifleLoaded;
        private Point _rifleMuzzleCanvasPoint;

        // Ajmal's blaster picture (Resources\GameSnagGun.png) - the SNAG MARKER's gun. Also a
        // ready FPS view (barrel up-left); shipped untouched, muzzle at the orange tip.
        private const double SnagGunDisplayHeight = 310.0;
        private const double SnagGunBleedRight = 30.0;
        private const double SnagGunBleedBottom = 55.0;
        private const double SnagMuzzleFractionX = 0.022;
        private const double SnagMuzzleFractionY = 0.087;
        private bool _snagGunLoaded;
        private Point _snagMuzzleCanvasPoint;

        // Scroll-wheel holster: gun slides off-screen; shooting stops but the laser keeps working.
        private bool _holstered;

        // Left-side saved-positions list: rebuilt whenever the session's BookmarksVersion changes.
        private int _lastBookmarksVersion = -1;

        // Flashlight night mode (V) - purely visual, HUD-side.
        private bool _flashlightOn;

        // Professional mode (N): no gun on screen, ever - all tools still work. Persisted.
        private bool _professional = GamePrefs.LoadProfessionalMode();

        // Synthesized gunshot sound (built once, played per shot when sounds are on).
        private byte[] _shotWavBytes;

        private int _fullLeft;
        private int _fullTop;
        private int _fullRight;
        private int _fullBottom;
        private bool _haveFullRect;

        internal GameHudWindow(GameSession session, ExternalEvent frameEvent)
        {
            _session = session;
            _frameEvent = frameEvent;
            InitializeComponent();

            _timer = new DispatcherTimer(DispatcherPriority.Input)
            {
                Interval = TimeSpan.FromMilliseconds(GameTuning.FrameIntervalMs)
            };
            _timer.Tick += OnTick;

            Loaded += OnLoadedOnce;
            Deactivated += OnDeactivatedPause;
            LostMouseCapture += OnLostCapture;
            Closed += OnClosedCleanup;

            PreviewKeyDown += OnGameKeyDown;
            PreviewKeyUp += OnGameKeyUp;
            PreviewMouseMove += OnGameMouseMove;
            PreviewMouseDown += OnGameMouseDown;
            PreviewMouseUp += OnGameMouseUp;
            PreviewMouseWheel += OnGameMouseWheel;

            CreateImpactSparks();
            LoadCustomGunImage();
            ApplyWeaponVisuals();
        }

        #region Window lifetime

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            _hwnd = new WindowInteropHelper(this).Handle;

            if (_session.Hud.RectValid)
            {
                ApplyPixelRect(_session.Hud.RectLeft, _session.Hud.RectTop, _session.Hud.RectRight, _session.Hud.RectBottom, true);
            }
        }

        private void OnLoadedOnce(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoadedOnce;
            _timer.Start();
            EnterPlay();
        }

        /// <summary>Stops the game and closes the overlay (also used by the ribbon toggle).</summary>
        public void StopGame(string reason)
        {
            _session.Running = false;
            _session.Hud.Alive = false;
            _session.Hud.StopReason = reason;
            BeginEndSequence(reason);
        }

        private void BeginEndSequence(string reason)
        {
            if (_ending || _closedDown)
            {
                return;
            }

            _ending = true;
            _paused = true;
            ReleaseLook();

            PlayLayer.Visibility = System.Windows.Visibility.Collapsed;
            PauseLayer.Visibility = System.Windows.Visibility.Visible;
            PauseTitle.Text = "GAME ENDED";
            PauseLine1.Text = string.IsNullOrWhiteSpace(reason) ? "Thanks for playing." : reason;
            PauseLine2.Text = "The AJ Game View stays in the project - run Game Mode again to continue from here.";
            ShrinkToPill();

            _endTicks = 45; // ~1.5 s at 33 ms ticks, then the window closes itself.
        }

        private void OnClosedCleanup(object sender, EventArgs e)
        {
            if (_closedDown)
            {
                return;
            }

            _closedDown = true;
            _session.Running = false;

            try { _timer.Stop(); } catch (Exception) { }
            ReleaseLook();
            try
            {
                if (_frameEvent != null)
                {
                    _frameEvent.Dispose();
                }
            }
            catch (Exception) { }

            if (AJTools.Commands.GameMode.CmdGameMode.ActiveHud == this)
            {
                AJTools.Commands.GameMode.CmdGameMode.ActiveHud = null;
            }
        }

        #endregion

        #region Frame tick

        private void OnTick(object sender, EventArgs e)
        {
            if (_closedDown)
            {
                return;
            }

            if (_ending)
            {
                _endTicks--;
                if (_endTicks <= 0)
                {
                    try { _timer.Stop(); } catch (Exception) { }
                    Close();
                }
                return;
            }

            GameHudState hud = _session.Hud;

            if (!hud.Alive || !_session.Running)
            {
                BeginEndSequence(hud.StopReason);
                return;
            }

            if (_paused)
            {
                return;
            }

            // Follow the Revit view window if it moved/resized.
            if (hud.RectValid &&
                (hud.RectLeft != _fullLeft || hud.RectTop != _fullTop ||
                 hud.RectRight != _fullRight || hud.RectBottom != _fullBottom))
            {
                ApplyPixelRect(hud.RectLeft, hud.RectTop, hud.RectRight, hud.RectBottom, true);
            }

            // Ask Revit for the next physics/camera frame (multiple raises coalesce naturally).
            try
            {
                _frameEvent.Raise();
            }
            catch (Exception)
            {
                StopGame("Revit stopped accepting game frames.");
                return;
            }

            // Look override requested by the engine (bookmark jumps restore the saved direction).
            if (hud.LookOverridePending)
            {
                _session.Input.YawDeg = hud.LookOverrideYawDeg;
                _session.Input.PitchDeg = hud.LookOverridePitchDeg;
                hud.LookOverridePending = false;
            }

            // Automatic fire while the left button is held (gun mode only, gun drawn).
            if (_firing && _session.Input.WeaponSelect == 0 && !_holstered)
            {
                long nowMs = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                if (nowMs - _lastShotMs >= FireIntervalMs)
                {
                    FireOnce();
                }
            }

            RenderHud(hud);
        }

        #endregion

        #region Play / pause

        private void EnterPlay()
        {
            if (_ending || _closedDown)
            {
                return;
            }

            _paused = false;
            PauseLayer.Visibility = System.Windows.Visibility.Collapsed;
            PlayLayer.Visibility = System.Windows.Visibility.Visible;

            if (_haveFullRect)
            {
                ApplyPixelRect(_fullLeft, _fullTop, _fullRight, _fullBottom, false);
            }
            else if (_session.Hud.RectValid)
            {
                ApplyPixelRect(_session.Hud.RectLeft, _session.Hud.RectTop, _session.Hud.RectRight, _session.Hud.RectBottom, true);
            }

            Activate();
            Focus();
            Keyboard.Focus(this);
            StartLook();

            // Re-fit the picture to the window: while paused the view may have been zoomed or
            // panned, which would make shots land beside the crosshair (Ajmal's sync report).
            _session.Input.ResyncViewQueued = true;

            try { _frameEvent.Raise(); } catch (Exception) { }
        }

        private void PauseGame()
        {
            if (_paused || _ending || _closedDown)
            {
                return;
            }

            _paused = true;
            ReleaseLook();
            ClearHeldKeys();

            PlayLayer.Visibility = System.Windows.Visibility.Collapsed;
            PauseLayer.Visibility = System.Windows.Visibility.Visible;
            PauseTitle.Text = "PAUSED";
            PauseLine1.Text = "Click here to continue playing";
            PauseLine2.Text = "Revit is free to use now. S = key settings, Esc here = exit game.";
            ShrinkToPill();
        }

        private void ShrinkToPill()
        {
            if (!_haveFullRect)
            {
                return;
            }

            int centerX = (_fullLeft + _fullRight) / 2;
            int left = centerX - (PillWidthPx / 2);
            int top = _fullTop + 10;
            MoveWindowPixels(left, top, PillWidthPx, PillHeightPx);
        }

        private void ClearHeldKeys()
        {
            GameInputState input = _session.Input;
            input.MoveForward = false;
            input.MoveBack = false;
            input.StrafeLeft = false;
            input.StrafeRight = false;
            input.AscendKey = false;
            input.DescendKey = false;
            input.Sprint = false;
            _firing = false;
            FinishTeleportAim(false);
        }

        #endregion

        #region Pixel-exact window placement

        private void ApplyPixelRect(int left, int top, int right, int bottom, bool remember)
        {
            if (remember)
            {
                // Does this rectangle move the CENTRE (the origin every mouse-look delta is measured
                // from)? Compare the sums rather than the edges: a pure resize that keeps the middle
                // where it is needs no re-centre.
                bool centreMoved = _haveFullRect &&
                                   ((left + right) != (_fullLeft + _fullRight) ||
                                    (top + bottom) != (_fullTop + _fullBottom));

                _fullLeft = left;
                _fullTop = top;
                _fullRight = right;
                _fullBottom = bottom;
                _haveFullRect = true;

                // Revit moves the view rectangle whenever its own chrome changes - and changing the
                // SELECTION does exactly that (the Options Bar appears/disappears, the contextual
                // "Modify | ..." tab swaps in). The SELECTOR weapon is the only one that touches the
                // Revit selection, so every selector shot shifted this rectangle. The look centre
                // moved with it while the pointer stayed parked on the OLD centre, so the very next
                // mouse move was read as one huge turn and the camera flew off across the model
                // (Ajmal's report). Put the pointer back on the new centre so the next delta is
                // measured from where the pointer actually is.
                if (centreMoved && _mouseLookActive)
                {
                    CenterCursor();
                }
            }

            MoveWindowPixels(left, top, Math.Max(200, right - left), Math.Max(150, bottom - top));
        }

        private void MoveWindowPixels(int left, int top, int width, int height)
        {
            if (_hwnd == IntPtr.Zero)
            {
                return;
            }

            GameNative.SetWindowPos(
                _hwnd,
                IntPtr.Zero,
                left,
                top,
                width,
                height,
                GameNative.SWP_NOZORDER | GameNative.SWP_NOACTIVATE);
        }

        #endregion

        /// <summary>Minimal user32 interop for cursor centering and pixel-exact placement.</summary>
        internal static class GameNative
        {
            public const uint SWP_NOZORDER = 0x0004;
            public const uint SWP_NOACTIVATE = 0x0010;

            [StructLayout(LayoutKind.Sequential)]
            public struct NativePoint
            {
                public int X;
                public int Y;
            }

            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool GetCursorPos(out NativePoint point);

            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool SetCursorPos(int x, int y);

            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool SetWindowPos(
                IntPtr hWnd,
                IntPtr hWndInsertAfter,
                int x,
                int y,
                int width,
                int height,
                uint flags);
        }
    }
}
