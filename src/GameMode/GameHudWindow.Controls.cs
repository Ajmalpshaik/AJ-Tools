#region Metadata
/*
 * Tool Name     : AJ Game Mode (HUD - controls)
 * File Name     : GameHudWindow.Controls.cs
 * Purpose       : Partial of GameHudWindow: FPS mouse-look, every keyboard/mouse handler, and the
 *                 pause triggers. All letter-key actions resolve through GameKeyBindings, so every
 *                 shortcut is remappable in the Key Settings window (pause, then S). Fixed keys:
 *                 Esc, mouse buttons, scroll wheel, number keys 1-9 and the arrow keys.
 *                 See GameHudWindow.xaml.cs for the class metadata, fields and full changelog.
 *
 * Author        : Ajmal P.S.
 * Created Date  : 2026-07-29 (rebuilt for remappable keys in HUD v1.8.0)
 * License       : All Rights Reserved   |   Repo: AJ-Tools
 */
#endregion

using System;
using System.Windows.Input;
using AJTools.Services.GameMode;

namespace AJTools.UI.GameMode
{
    public partial class GameHudWindow
    {
        #region Mouse look

        private void StartLook()
        {
            // Park the pointer on the look centre BEFORE arming the look. Capturing the mouse makes
            // WPF deliver a move event straight away; with the look already armed, that event would
            // be measured from wherever the pointer happened to be sitting and snap the camera round.
            bool captured = CaptureMouse();
            Cursor = Cursors.None;
            CenterCursor();
            _mouseLookActive = captured;
        }

        private void ReleaseLook()
        {
            bool wasActive = _mouseLookActive;
            _mouseLookActive = false;
            Cursor = Cursors.Arrow;
            if (wasActive)
            {
                try { ReleaseMouseCapture(); } catch (Exception) { }
            }
        }

        private void CenterCursor()
        {
            if (!_haveFullRect)
            {
                return;
            }

            GameNative.SetCursorPos((_fullLeft + _fullRight) / 2, (_fullTop + _fullBottom) / 2);
        }

        private void OnGameMouseMove(object sender, MouseEventArgs e)
        {
            if (_paused || _ending || !_mouseLookActive || !_haveFullRect)
            {
                return;
            }

            GameNative.NativePoint cursor;
            if (!GameNative.GetCursorPos(out cursor))
            {
                return;
            }

            int centerX = (_fullLeft + _fullRight) / 2;
            int centerY = (_fullTop + _fullBottom) / 2;
            int dx = cursor.X - centerX;
            int dy = cursor.Y - centerY;
            if (dx == 0 && dy == 0)
            {
                return;
            }

            // Backstop against a pointer that is out of step with the look centre. A single mouse
            // step this large is not real aiming - it means something moved the centre out from
            // under the cursor (the Revit view was resized, the display DPI changed, a remote
            // session re-warped the pointer). Turning it into rotation throws the camera across the
            // model; re-centre and drop the step instead. ApplyPixelRect re-centres on every
            // remembered rectangle change, so this should stay quiet - it is the safety net, not
            // the fix.
            if (Math.Abs(dx) > MaxLookStepPx || Math.Abs(dy) > MaxLookStepPx)
            {
                GameNative.SetCursorPos(centerX, centerY);
                e.Handled = true;
                return;
            }

            // While the tour drives the camera, the mouse stays parked.
            if (_session.Hud.TourRunning)
            {
                GameNative.SetCursorPos(centerX, centerY);
                e.Handled = true;
                return;
            }

            GameInputState input = _session.Input;
            input.YawDeg -= dx * GameTuning.MouseDegPerPixel;
            input.PitchDeg -= dy * GameTuning.MouseDegPerPixel;
            if (input.PitchDeg > GameTuning.PitchClampDeg) { input.PitchDeg = GameTuning.PitchClampDeg; }
            if (input.PitchDeg < -GameTuning.PitchClampDeg) { input.PitchDeg = -GameTuning.PitchClampDeg; }

            GameNative.SetCursorPos(centerX, centerY);
            e.Handled = true;
        }

        private void OnGameMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_ending || _closedDown)
            {
                return;
            }

            if (_paused)
            {
                EnterPlay();
                e.Handled = true;
                return;
            }

            if (!_mouseLookActive)
            {
                StartLook();
            }

            if (e.ChangedButton == MouseButton.Left)
            {
                byte weapon = _session.Input.WeaponSelect;
                if (weapon == 1)
                {
                    // Laser: pure pointer - distance and element identity update live by aiming.
                }
                else if (_holstered && !_professional)
                {
                    // Clicking with the gun away draws it again instead of firing.
                    DrawGun();
                }
                else if (weapon == 2)
                {
                    // Cleaner: one shot per click - the engine temporarily hides what was hit.
                    _session.Input.CleanShotQueued = true;
                    FireOnce();
                }
                else if (weapon == 3)
                {
                    // Snag marker: one shot per click - paints red + adds to the punch list.
                    _session.Input.SnagShotQueued = true;
                    FireOnce();
                }
                else if (weapon == 4)
                {
                    // Selector: one shot per click - selects/unselects the element in Revit.
                    _session.Input.SelectShotQueued = true;
                    FireOnce();
                }
                else
                {
                    // Gun: hold to keep firing - OnTick repeats the shot every FireIntervalMs.
                    _firing = true;
                    FireOnce();
                }
                e.Handled = true;
            }
            else if (e.ChangedButton == MouseButton.Right)
            {
                ToggleWeapon();
                e.Handled = true;
            }
        }

        private void OnGameMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                _firing = false;
            }
        }

        private void OnGameMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (_paused || _ending || _closedDown || _professional)
            {
                return;
            }

            if (e.Delta < 0)
            {
                HolsterGun();
            }
            else if (e.Delta > 0)
            {
                DrawGun();
            }
            e.Handled = true;
        }

        private void OnLostCapture(object sender, MouseEventArgs e)
        {
            if (!_paused && !_ending && !_closedDown)
            {
                PauseGame();
            }
        }

        private void OnDeactivatedPause(object sender, EventArgs e)
        {
            if (!_paused && !_ending && !_closedDown)
            {
                PauseGame();
            }
        }

        #endregion

        #region Keyboard (remappable via GameKeyBindings)

        private void OnGameKeyDown(object sender, KeyEventArgs e)
        {
            if (_ending || _closedDown)
            {
                return;
            }

            Key key = e.Key == Key.System ? e.SystemKey : e.Key;
            GameInputState input = _session.Input;

            if (_paused)
            {
                if (key == Key.Escape)
                {
                    StopGame("You ended the game.");
                    e.Handled = true;
                }
                else if (key == Key.S)
                {
                    OpenKeySettings();
                    e.Handled = true;
                }
                return;
            }

            // Fixed keys: Esc pauses, 1-9 jump to saved positions, arrows always move.
            if (key == Key.Escape)
            {
                PauseGame();
                e.Handled = true;
                return;
            }
            if (key >= Key.D1 && key <= Key.D9)
            {
                if (!e.IsRepeat) { input.JumpBookmarkSlot = key - Key.D1 + 1; }
                e.Handled = true;
                return;
            }
            if (key >= Key.NumPad1 && key <= Key.NumPad9)
            {
                if (!e.IsRepeat) { input.JumpBookmarkSlot = key - Key.NumPad1 + 1; }
                e.Handled = true;
                return;
            }
            if (key == Key.Up) { input.MoveForward = true; AutoHideHelp(); e.Handled = true; return; }
            if (key == Key.Down) { input.MoveBack = true; AutoHideHelp(); e.Handled = true; return; }
            if (key == Key.Left) { input.StrafeLeft = true; AutoHideHelp(); e.Handled = true; return; }
            if (key == Key.Right) { input.StrafeRight = true; AutoHideHelp(); e.Handled = true; return; }

            string action = _keys.FindAction(key);
            if (action == null)
            {
                return;
            }

            switch (action)
            {
                case GameKeyBindings.MoveForward:
                    input.MoveForward = true;
                    AutoHideHelp();
                    break;
                case GameKeyBindings.MoveBack:
                    input.MoveBack = true;
                    AutoHideHelp();
                    break;
                case GameKeyBindings.StrafeLeft:
                    input.StrafeLeft = true;
                    AutoHideHelp();
                    break;
                case GameKeyBindings.StrafeRight:
                    input.StrafeRight = true;
                    AutoHideHelp();
                    break;
                case GameKeyBindings.Sprint:
                    input.Sprint = true;
                    break;
                case GameKeyBindings.JumpUp:
                    if (!e.IsRepeat) { input.JumpQueued = true; }
                    input.AscendKey = true;
                    break;
                case GameKeyBindings.CrouchDown:
                    input.DescendKey = true;
                    break;
                case GameKeyBindings.Door:
                    if (!e.IsRepeat) { input.DoorPassQueued = true; }
                    break;
                case GameKeyBindings.Fly:
                    if (!e.IsRepeat) { input.FlyToggleQueued = true; }
                    break;
                case GameKeyBindings.Ghost:
                    if (!e.IsRepeat) { input.GhostToggleQueued = true; }
                    break;
                case GameKeyBindings.Respawn:
                    if (!e.IsRepeat) { input.RespawnQueued = true; }
                    break;
                case GameKeyBindings.Teleport:
                    if (!e.IsRepeat) { StartTeleportAim(); }
                    break;
                case GameKeyBindings.SavePosition:
                    if (!e.IsRepeat) { input.SaveBookmarkQueued = true; }
                    break;
                case GameKeyBindings.UnhideAll:
                    if (!e.IsRepeat) { input.UnhideAllQueued = true; }
                    break;
                case GameKeyBindings.ResetGraphics:
                    if (!e.IsRepeat) { input.ResetGraphicsQueued = true; }
                    break;
                case GameKeyBindings.Tour:
                    if (!e.IsRepeat) { input.TourToggleQueued = true; }
                    break;
                case GameKeyBindings.Photo:
                    if (!e.IsRepeat) { TakePhoto(); }
                    break;
                case GameKeyBindings.Flashlight:
                    if (!e.IsRepeat)
                    {
                        _flashlightOn = !_flashlightOn;
                        FlashlightOverlay.Visibility = _flashlightOn
                            ? System.Windows.Visibility.Visible
                            : System.Windows.Visibility.Collapsed;
                    }
                    break;
                case GameKeyBindings.SoundToggle:
                    if (!e.IsRepeat)
                    {
                        input.SoundOn = !input.SoundOn;
                        _session.Hud.Toast(input.SoundOn ? "Sounds ON" : "Sounds OFF");
                    }
                    break;
                case GameKeyBindings.SpeedUp:
                    if (!e.IsRepeat)
                    {
                        input.SpeedFactor = Math.Min(3.0, Math.Round((input.SpeedFactor + 0.2) * 10) / 10);
                        _session.Hud.Toast(string.Format("Speed x{0:0.0}", input.SpeedFactor));
                    }
                    break;
                case GameKeyBindings.SpeedDown:
                    if (!e.IsRepeat)
                    {
                        input.SpeedFactor = Math.Max(0.4, Math.Round((input.SpeedFactor - 0.2) * 10) / 10);
                        _session.Hud.Toast(string.Format("Speed x{0:0.0}", input.SpeedFactor));
                    }
                    break;
                case GameKeyBindings.Help:
                    if (!e.IsRepeat) { _helpVisible = !_helpVisible; }
                    break;
                case GameKeyBindings.Pause:
                    PauseGame();
                    break;
                case GameKeyBindings.Professional:
                    if (!e.IsRepeat) { ToggleProfessionalMode(); }
                    break;
                default:
                    return;
            }

            e.Handled = true;
        }

        private void OnGameKeyUp(object sender, KeyEventArgs e)
        {
            Key key = e.Key == Key.System ? e.SystemKey : e.Key;
            GameInputState input = _session.Input;

            if (key == Key.Up) { input.MoveForward = false; e.Handled = true; return; }
            if (key == Key.Down) { input.MoveBack = false; e.Handled = true; return; }
            if (key == Key.Left) { input.StrafeLeft = false; e.Handled = true; return; }
            if (key == Key.Right) { input.StrafeRight = false; e.Handled = true; return; }

            string action = _keys.FindAction(key);
            if (action == null)
            {
                return;
            }

            switch (action)
            {
                case GameKeyBindings.MoveForward:
                    input.MoveForward = false;
                    break;
                case GameKeyBindings.MoveBack:
                    input.MoveBack = false;
                    break;
                case GameKeyBindings.StrafeLeft:
                    input.StrafeLeft = false;
                    break;
                case GameKeyBindings.StrafeRight:
                    input.StrafeRight = false;
                    break;
                case GameKeyBindings.Sprint:
                    input.Sprint = false;
                    break;
                case GameKeyBindings.JumpUp:
                    input.AscendKey = false;
                    break;
                case GameKeyBindings.CrouchDown:
                    input.DescendKey = false;
                    break;
                case GameKeyBindings.Teleport:
                    FinishTeleportAim(true);
                    break;
                default:
                    return;
            }

            e.Handled = true;
        }

        private void AutoHideHelp()
        {
            if (_helpVisible)
            {
                _helpVisible = false;
            }
        }

        /// <summary>Opens the remappable key settings (only reachable while paused).</summary>
        private void OpenKeySettings()
        {
            try
            {
                var window = new GameKeySettingsWindow(_keys) { Owner = this };
                window.ShowDialog();
            }
            catch (Exception)
            {
                // Settings must never take the game down.
            }
        }

        #endregion
    }
}
