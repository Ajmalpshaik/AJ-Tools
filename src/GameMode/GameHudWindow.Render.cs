#region Metadata
/*
 * Tool Name     : AJ Game Mode (HUD - per-frame drawing)
 * File Name     : GameHudWindow.Render.cs
 * Purpose       : Partial of GameHudWindow: paints the HUD every frame - status card, toast,
 *                 clear height, laser beam + distance + element identity, teleport arc, saved
 *                 positions list, door/window hints and the help card. See GameHudWindow.xaml.cs
 *                 for the class metadata, fields and full changelog.
 *
 * Author        : Ajmal P.S.
 * Created Date  : 2026-07-29 (split out of GameHudWindow.xaml.cs v1.5.1, code unchanged)
 * License       : All Rights Reserved   |   Repo: AJ-Tools
 */
#endregion

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AJTools.Services.GameMode;
using AJTools.Utils;

namespace AJTools.UI.GameMode
{
    public partial class GameHudWindow
    {
        private void RenderHud(GameHudState hud)
        {
            ModeText.Text = hud.ModeLine;
            PositionText.Text = hud.PositionLine;
            FrameText.Text = string.Format("frame {0:0} ms", hud.LastFrameMs);

            // Toast + clear height
            long toastAge = DateTimeOffset.Now.ToUnixTimeMilliseconds() - hud.ToastStampMs;
            if (hud.ToastStampMs > 0 && toastAge < 2600 && !string.IsNullOrEmpty(hud.ToastText))
            {
                ToastText.Text = hud.ToastText;
                ToastCard.Visibility = System.Windows.Visibility.Visible;
            }
            else
            {
                ToastCard.Visibility = System.Windows.Visibility.Collapsed;
            }

            ClearHeightText.Text = hud.ClearHeightMm > 0
                ? string.Format("Clear height {0:N0} mm", hud.ClearHeightMm)
                : "";
            ClearHeightText.Visibility = hud.ClearHeightMm > 0
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;

            CompassText.Text = hud.CompassText;

            SnagText.Text = hud.SnagCount > 0
                ? string.Format("Snags marked: {0}   (J clears, report on exit)", hud.SnagCount)
                : "";
            SnagText.Visibility = hud.SnagCount > 0
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;

            // Laser weapon: beam + live distance + live element identity.
            bool laserOn = _session.Input.WeaponSelect == 1;
            if (laserOn)
            {
                Point muzzle = GetMuzzlePoint();
                Point center = GetCenterPoint();
                LaserLine.X1 = muzzle.X;
                LaserLine.Y1 = muzzle.Y;
                LaserLine.X2 = center.X;
                LaserLine.Y2 = center.Y;
                LaserLine.Opacity = 0.85;

                if (hud.LaserHasHit)
                {
                    Canvas.SetLeft(LaserDot, center.X - (LaserDot.Width / 2));
                    Canvas.SetTop(LaserDot, center.Y - (LaserDot.Height / 2));
                    LaserDot.Opacity = 0.95;
                    LaserDistanceText.Text = string.Format("{0:N0} mm", hud.LaserDistanceMm);
                }
                else
                {
                    LaserDot.Opacity = 0;
                    LaserDistanceText.Text = "- - -";
                }
                LaserDistanceText.Visibility = System.Windows.Visibility.Visible;

                if (hud.LaserHasHit && !string.IsNullOrEmpty(hud.LaserTargetInfo))
                {
                    LaserInfoText.Text = hud.LaserTargetInfo;
                    LaserInfoText.Visibility = System.Windows.Visibility.Visible;
                }
                else
                {
                    LaserInfoText.Visibility = System.Windows.Visibility.Collapsed;
                }
            }
            else
            {
                LaserLine.Opacity = 0;
                LaserDot.Opacity = 0;
                LaserDistanceText.Visibility = System.Windows.Visibility.Collapsed;
                LaserInfoText.Visibility = System.Windows.Visibility.Collapsed;
            }

            // VR-style teleport while T is held (Ajmal's reference): thick green ballistic arc
            // from the muzzle, cresting and dropping onto a landing disc flat on the floor.
            if (_session.Input.TeleportAimHold)
            {
                Point start = GetMuzzlePoint();
                Point end = GetCenterPoint();
                var control1 = new Point((start.X * 0.35) + (end.X * 0.65), Math.Min(start.Y, end.Y) - 300);
                var control2 = new Point(end.X, end.Y - 260);

                var figure = new PathFigure { StartPoint = start };
                figure.Segments.Add(new BezierSegment(control1, control2, end, true));
                var geometry = new PathGeometry();
                geometry.Figures.Add(figure);
                TeleportArc.Data = geometry;

                Color arcColor = hud.TeleportAimValid
                    ? Color.FromRgb(0x3C, 0xE2, 0x4A)
                    : Color.FromRgb(0x8F, 0xA0, 0xB3);
                var arcBrush = new SolidColorBrush(arcColor);
                TeleportArc.Stroke = arcBrush;
                TeleportRing.Stroke = arcBrush;
                TeleportRingInner.Stroke = arcBrush;
                TeleportRingDot.Fill = arcBrush;
                TeleportText.Foreground = arcBrush;

                TeleportArc.Opacity = 0.95;
                Canvas.SetLeft(TeleportRing, end.X - (TeleportRing.Width / 2));
                Canvas.SetTop(TeleportRing, end.Y - (TeleportRing.Height / 2));
                TeleportRing.Opacity = hud.TeleportAimValid ? 0.95 : 0.4;
                Canvas.SetLeft(TeleportRingInner, end.X - (TeleportRingInner.Width / 2));
                Canvas.SetTop(TeleportRingInner, end.Y - (TeleportRingInner.Height / 2));
                TeleportRingInner.Opacity = hud.TeleportAimValid ? 0.8 : 0.3;
                Canvas.SetLeft(TeleportRingDot, end.X - (TeleportRingDot.Width / 2));
                Canvas.SetTop(TeleportRingDot, end.Y - (TeleportRingDot.Height / 2));
                TeleportRingDot.Opacity = hud.TeleportAimValid ? 0.95 : 0;

                TeleportText.Text = hud.TeleportAimValid
                    ? string.Format("Release T  -  jump {0:N0} mm", hud.TeleportAimDistanceMm)
                    : "Aim at a floor or any surface...";
                TeleportText.Visibility = System.Windows.Visibility.Visible;
            }

            // Saved-positions list (left side) - rebuilt only when something was saved
            if (_session.BookmarksVersion != _lastBookmarksVersion)
            {
                _lastBookmarksVersion = _session.BookmarksVersion;
                BookmarkList.Children.Clear();

                var slots = new System.Collections.Generic.List<int>(_session.Bookmarks.Keys);
                slots.Sort();
                foreach (int slot in slots)
                {
                    double[] saved = _session.Bookmarks[slot];
                    string jumpNote = slot <= 9 ? "" : "   (tour only)";
                    BookmarkList.Children.Add(new TextBlock
                    {
                        FontSize = 11.5,
                        Foreground = new SolidColorBrush(Color.FromRgb(0xC9, 0xD4, 0xE0)),
                        Margin = new Thickness(0, 2, 0, 0),
                        Text = string.Format(
                            "{0}    X {1:N0}   Y {2:N0}   Z {3:N0}  mm{4}",
                            slot,
                            saved[0] / Constants.MM_TO_FEET,
                            saved[1] / Constants.MM_TO_FEET,
                            saved[2] / Constants.MM_TO_FEET,
                            jumpNote)
                    });
                }

                BookmarkCard.Visibility = BookmarkList.Children.Count > 0
                    ? System.Windows.Visibility.Visible
                    : System.Windows.Visibility.Collapsed;
            }

            // Door / window hints
            if (hud.DoorHint)
            {
                HintText.Text = "[ E ]   Go through the door";
                HintCard.Visibility = System.Windows.Visibility.Visible;
            }
            else if (hud.WindowHint)
            {
                HintText.Text = "[ Space ]   Jump, then move forward to climb through the window";
                HintCard.Visibility = System.Windows.Visibility.Visible;
            }
            else
            {
                HintCard.Visibility = System.Windows.Visibility.Collapsed;
            }

            HelpCard.Visibility = _helpVisible
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;
        }
    }
}
