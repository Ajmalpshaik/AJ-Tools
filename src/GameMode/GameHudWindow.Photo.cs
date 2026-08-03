#region Metadata
/*
 * Tool Name     : AJ Game Mode (HUD - photo mode)
 * File Name     : GameHudWindow.Photo.cs
 * Purpose       : Partial of GameHudWindow: the K photo mode - hides the HUD for one frame,
 *                 captures the Revit view area as a clean PNG into Pictures\AJ Game Photos, then
 *                 brings the HUD back. See GameHudWindow.xaml.cs for the class metadata, fields
 *                 and full changelog.
 *
 * Author        : Ajmal P.S.
 * Created Date  : 2026-07-29 (split out of GameHudWindow.xaml.cs v1.5.1, code unchanged)
 * License       : All Rights Reserved   |   Repo: AJ-Tools
 */
#endregion

using System;
using System.Windows.Threading;

namespace AJTools.UI.GameMode
{
    public partial class GameHudWindow
    {
        /// <summary>
        /// K: hides the HUD for one frame, captures the Revit view area as a PNG into
        /// Pictures\AJ Game Photos, then brings the HUD back. Uses raw screen pixels, so the photo
        /// is exactly what the player sees (without crosshair/gun).
        /// </summary>
        private void TakePhoto()
        {
            if (!_haveFullRect)
            {
                return;
            }

            PlayLayer.Visibility = System.Windows.Visibility.Collapsed;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    string folder = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "AJ Game Photos");
                    System.IO.Directory.CreateDirectory(folder);
                    string file = System.IO.Path.Combine(
                        folder, "AJ-Game_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");

                    int width = Math.Max(50, _fullRight - _fullLeft);
                    int height = Math.Max(50, _fullBottom - _fullTop);
                    // CA1416: System.Drawing screen capture is Windows-only - which is always true
                    // for a Revit add-in. Same known-noise warning class as the AiShell-era files;
                    // suppressed here with this justification instead of polluting the R25 count.
#pragma warning disable CA1416
                    using (var bitmap = new System.Drawing.Bitmap(width, height))
                    {
                        using (var graphics = System.Drawing.Graphics.FromImage(bitmap))
                        {
                            graphics.CopyFromScreen(_fullLeft, _fullTop, 0, 0, new System.Drawing.Size(width, height));
                        }
                        bitmap.Save(file, System.Drawing.Imaging.ImageFormat.Png);
                    }
#pragma warning restore CA1416

                    _session.Hud.Toast("Photo saved: Pictures\\AJ Game Photos\\" + System.IO.Path.GetFileName(file));
                }
                catch (Exception)
                {
                    _session.Hud.Toast("Photo failed - could not capture the screen");
                }
                finally
                {
                    PlayLayer.Visibility = System.Windows.Visibility.Visible;
                }
            }), DispatcherPriority.Background);
        }
    }
}
