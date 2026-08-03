#region Metadata
/*
 * Tool Name     : AJ Game Mode (HUD - weapons and firing)
 * File Name     : GameHudWindow.Weapons.cs
 * Purpose       : Partial of GameHudWindow: the gun/rifle/blaster picture loaders, weapon cycling
 *                 (gun / laser / cleaner / snag / selector), professional mode, scroll-wheel
 *                 holster, firing, muzzle flash, bullet tracer, shot sound and the impact spark
 *                 splash. See GameHudWindow.xaml.cs for the class metadata, fields and full
 *                 changelog.
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
using System.Windows.Media.Animation;
using AJTools.Services.GameMode;

namespace AJTools.UI.GameMode
{
    public partial class GameHudWindow
    {
        /// <summary>
        /// Swaps the vector pistol for Ajmal's own gun picture when Resources\GameGun.png exists
        /// beside the deployed DLL. Any failure silently keeps the vector gun.
        /// </summary>
        private void LoadCustomGunImage()
        {
            try
            {
                string assemblyDir = System.IO.Path.GetDirectoryName(typeof(GameHudWindow).Assembly.Location);
                if (string.IsNullOrEmpty(assemblyDir))
                {
                    return;
                }

                string path = System.IO.Path.Combine(assemblyDir, "Resources", "GameGun.png");
                if (!System.IO.File.Exists(path))
                {
                    return;
                }

                var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(path, UriKind.Absolute);
                bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();

                double height = CustomGunDisplayHeight;
                double width = height * bitmap.PixelWidth / Math.Max(1, bitmap.PixelHeight);
                GunImage.Source = bitmap;
                GunImage.Width = width;
                GunImage.Height = height;

                // GunCanvas is 300x240 anchored bottom-right; the picture bleeds well past the
                // window corner so the hand feels grounded and the arm's cut end stays hidden.
                double left = 300.0 - width + CustomGunBleedRight;
                double top = 240.0 - height + CustomGunBleedBottom;
                Canvas.SetLeft(GunImage, left);
                Canvas.SetTop(GunImage, top);

                _customMuzzleCanvasPoint = new Point(
                    left + (width * CustomMuzzleFractionX),
                    top + (height * CustomMuzzleFractionY));

                Canvas.SetLeft(ImageMuzzleGlow, _customMuzzleCanvasPoint.X - (ImageMuzzleGlow.Width / 2));
                Canvas.SetTop(ImageMuzzleGlow, _customMuzzleCanvasPoint.Y - (ImageMuzzleGlow.Height / 2));
                Canvas.SetLeft(MuzzleFlash, _customMuzzleCanvasPoint.X - (MuzzleFlash.Width / 2));
                Canvas.SetTop(MuzzleFlash, _customMuzzleCanvasPoint.Y - (MuzzleFlash.Height / 2));

                GunLocal.Visibility = System.Windows.Visibility.Collapsed;
                GunImage.Visibility = System.Windows.Visibility.Visible;
                ImageMuzzleGlow.Visibility = System.Windows.Visibility.Visible;
                _customGunActive = true;
            }
            catch (Exception)
            {
                _customGunActive = false;
            }

            LoadRifleImage();
        }

        /// <summary>
        /// Loads the CLEANER weapon's rifle picture (Resources\GameRifle.png). Missing file =
        /// the cleaner simply keeps using whatever gun is active.
        /// </summary>
        private void LoadRifleImage()
        {
            try
            {
                string assemblyDir = System.IO.Path.GetDirectoryName(typeof(GameHudWindow).Assembly.Location);
                if (string.IsNullOrEmpty(assemblyDir))
                {
                    return;
                }

                string path = System.IO.Path.Combine(assemblyDir, "Resources", "GameRifle.png");
                if (!System.IO.File.Exists(path))
                {
                    return;
                }

                var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(path, UriKind.Absolute);
                bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();

                double height = RifleDisplayHeight;
                double width = height * bitmap.PixelWidth / Math.Max(1, bitmap.PixelHeight);
                RifleImage.Source = bitmap;
                RifleImage.Width = width;
                RifleImage.Height = height;

                double left = 300.0 - width + RifleBleedRight;
                double top = 240.0 - height + RifleBleedBottom;
                Canvas.SetLeft(RifleImage, left);
                Canvas.SetTop(RifleImage, top);

                _rifleMuzzleCanvasPoint = new Point(
                    left + (width * RifleMuzzleFractionX),
                    top + (height * RifleMuzzleFractionY));
                _rifleLoaded = true;
            }
            catch (Exception)
            {
                _rifleLoaded = false;
            }

            LoadSnagGunImage();
        }

        /// <summary>
        /// Loads the SNAG MARKER's blaster picture (Resources\GameSnagGun.png). Missing file =
        /// the snag weapon simply keeps the pistol.
        /// </summary>
        private void LoadSnagGunImage()
        {
            try
            {
                string assemblyDir = System.IO.Path.GetDirectoryName(typeof(GameHudWindow).Assembly.Location);
                if (string.IsNullOrEmpty(assemblyDir))
                {
                    return;
                }

                string path = System.IO.Path.Combine(assemblyDir, "Resources", "GameSnagGun.png");
                if (!System.IO.File.Exists(path))
                {
                    return;
                }

                var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(path, UriKind.Absolute);
                bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();

                double height = SnagGunDisplayHeight;
                double width = height * bitmap.PixelWidth / Math.Max(1, bitmap.PixelHeight);
                SnagImage.Source = bitmap;
                SnagImage.Width = width;
                SnagImage.Height = height;

                double left = 300.0 - width + SnagGunBleedRight;
                double top = 240.0 - height + SnagGunBleedBottom;
                Canvas.SetLeft(SnagImage, left);
                Canvas.SetTop(SnagImage, top);

                _snagMuzzleCanvasPoint = new Point(
                    left + (width * SnagMuzzleFractionX),
                    top + (height * SnagMuzzleFractionY));
                _snagGunLoaded = true;
            }
            catch (Exception)
            {
                _snagGunLoaded = false;
            }
        }

        /// <summary>Scroll down: the gun slides away. No shooting, but the laser keeps working.</summary>
        private void HolsterGun()
        {
            if (_holstered)
            {
                return;
            }

            _holstered = true;
            _firing = false;
            var slide = new DoubleAnimation(0, 430, TimeSpan.FromMilliseconds(220))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            GunRecoil.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, slide);
            ApplyWeaponVisuals();
        }

        /// <summary>Scroll up (or click while holstered): the gun comes back.</summary>
        private void DrawGun()
        {
            if (!_holstered)
            {
                return;
            }

            _holstered = false;
            var slide = new DoubleAnimation(430, 0, TimeSpan.FromMilliseconds(220))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            GunRecoil.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, slide);
            ApplyWeaponVisuals();
        }

        private void ToggleWeapon()
        {
            GameInputState input = _session.Input;
            input.WeaponSelect = (byte)((input.WeaponSelect + 1) % 5);
            _firing = false;
            ApplyWeaponVisuals();
        }

        /// <summary>
        /// N: Professional Mode - no gun on screen ever (presentable for managers/meetings), while
        /// every tool keeps working; beams and shots start from the bottom of the view like a
        /// laser pointer. Remembered across sessions.
        /// </summary>
        private void ToggleProfessionalMode()
        {
            _professional = !_professional;
            _holstered = false;
            _firing = false;
            GamePrefs.SaveProfessionalMode(_professional);
            GunRecoil.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, null);
            GunRecoil.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, null);
            ApplyWeaponVisuals();
            _session.Hud.Toast(_professional
                ? "Professional mode ON - no gun shown, all tools work the same (N brings it back)"
                : "Professional mode OFF - guns are back");
        }

        private void ApplyWeaponVisuals()
        {
            // Ajmal's color scheme (2026-07-29): GUN amber, LASER green, CLEANER black & white,
            // SNAG red, SELECTOR blue - applied everywhere the weapon color appears.
            byte weapon = _session.Input.WeaponSelect;
            Color weaponColor;
            bool blackAndWhite = false;
            switch (weapon)
            {
                case 1:
                    weaponColor = Color.FromRgb(0x3C, 0xE2, 0x4A); // laser - green
                    break;
                case 2:
                    weaponColor = Color.FromRgb(0xF5, 0xF5, 0xF5); // cleaner - white (with black edges)
                    blackAndWhite = true;
                    break;
                case 3:
                    weaponColor = Color.FromRgb(0xFF, 0x3B, 0x30); // snag marker - red
                    break;
                case 4:
                    weaponColor = Color.FromRgb(0x00, 0xC8, 0xFF); // selector - blue
                    break;
                default:
                    weaponColor = Color.FromRgb(0xFF, 0xC5, 0x3D); // gun - amber
                    break;
            }
            var weaponBrush = new SolidColorBrush(weaponColor);

            // The crosshair always wears the active tool's color (the only weapon indicator in
            // professional mode). Cleaner = white bars with black edges ("black and white").
            var barStroke = blackAndWhite
                ? new SolidColorBrush(Color.FromRgb(0x10, 0x10, 0x10))
                : new SolidColorBrush(Color.FromArgb(0x00, 0x00, 0x00, 0x00));
            foreach (object child in Crosshair.Children)
            {
                var bar = child as System.Windows.Shapes.Rectangle;
                if (bar != null)
                {
                    bar.Fill = new SolidColorBrush(Color.FromArgb(0xE6, weaponColor.R, weaponColor.G, weaponColor.B));
                    bar.Stroke = barStroke;
                    bar.StrokeThickness = blackAndWhite ? 1.2 : 0;
                }
            }

            AccentStripe.Fill = weaponBrush;
            MuzzleGlow.Fill = weaponBrush;
            MuzzleGlow.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = weaponColor,
                BlurRadius = 12,
                ShadowDepth = 0,
                Opacity = 0.9
            };
            ImageMuzzleGlow.Fill = weaponBrush;
            ImageMuzzleGlow.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = weaponColor,
                BlurRadius = 12,
                ShadowDepth = 0,
                Opacity = 0.9
            };

            // Professional mode: no gun ever - hide every weapon visual and stop here.
            if (_professional)
            {
                RifleImage.Visibility = System.Windows.Visibility.Collapsed;
                SnagImage.Visibility = System.Windows.Visibility.Collapsed;
                GunImage.Visibility = System.Windows.Visibility.Collapsed;
                GunLocal.Visibility = System.Windows.Visibility.Collapsed;
                ImageMuzzleGlow.Visibility = System.Windows.Visibility.Collapsed;
                UpdateWeaponText(weapon);
                return;
            }

            // The CLEANER carries the rifle, the SNAG MARKER carries the blaster,
            // everything else carries the pistol.
            bool rifleNow = weapon == 2 && _rifleLoaded;
            bool snagNow = weapon == 3 && _snagGunLoaded;
            RifleImage.Visibility = rifleNow
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;
            SnagImage.Visibility = snagNow
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;
            if (_customGunActive)
            {
                GunImage.Visibility = (rifleNow || snagNow)
                    ? System.Windows.Visibility.Collapsed
                    : System.Windows.Visibility.Visible;
            }
            GunLocal.Visibility = (rifleNow || snagNow || _customGunActive)
                ? System.Windows.Visibility.Collapsed
                : System.Windows.Visibility.Visible;

            if (rifleNow || snagNow || _customGunActive)
            {
                Point muzzle = snagNow
                    ? _snagMuzzleCanvasPoint
                    : (rifleNow ? _rifleMuzzleCanvasPoint : _customMuzzleCanvasPoint);
                Canvas.SetLeft(ImageMuzzleGlow, muzzle.X - (ImageMuzzleGlow.Width / 2));
                Canvas.SetTop(ImageMuzzleGlow, muzzle.Y - (ImageMuzzleGlow.Height / 2));
                Canvas.SetLeft(MuzzleFlash, muzzle.X - (MuzzleFlash.Width / 2));
                Canvas.SetTop(MuzzleFlash, muzzle.Y - (MuzzleFlash.Height / 2));
                ImageMuzzleGlow.Visibility = System.Windows.Visibility.Visible;
            }

            UpdateWeaponText(weapon);
        }

        private void UpdateWeaponText(byte weapon)
        {
            string name;
            Color color;
            switch (weapon)
            {
                case 1:
                    name = "LASER - live distance + element name";
                    color = Color.FromRgb(0x7A, 0xE8, 0x86);
                    break;
                case 2:
                    name = "CLEANER - shot hides the element, U restores";
                    color = Color.FromRgb(0xF0, 0xF0, 0xF0);
                    break;
                case 3:
                    name = "SNAG MARKER - shot paints red + report on exit, J resets";
                    color = Color.FromRgb(0xFF, 0x6A, 0x6A);
                    break;
                case 4:
                    name = "SELECTOR - shot selects in Revit (again = unselect)";
                    color = Color.FromRgb(0x7A, 0xC8, 0xFF);
                    break;
                default:
                    name = "GUN - hold left click to fire";
                    color = Color.FromRgb(0xFF, 0xC5, 0x3D);
                    break;
            }

            if (_professional)
            {
                WeaponText.Text = "Tool: " + name + "   |   professional mode (N = guns back)";
                WeaponText.Foreground = new SolidColorBrush(Color.FromRgb(0x9F, 0xD0, 0xFF));
            }
            else if (_holstered)
            {
                WeaponText.Text = "Weapon: away   (scroll up or click = draw)   |   " + name;
                WeaponText.Foreground = new SolidColorBrush(Color.FromRgb(0x8F, 0xA0, 0xB3));
            }
            else
            {
                WeaponText.Text = "Weapon: " + name + "   (right-click = next)";
                WeaponText.Foreground = new SolidColorBrush(color);
            }
        }

        /// <summary>Hold T: teleport aiming starts - the landing ring pulses while the engine feeds the target.</summary>
        private void StartTeleportAim()
        {
            if (_session.Input.TeleportAimHold)
            {
                return;
            }

            _session.Input.TeleportAimHold = true;
            var pulse = new DoubleAnimation(0.85, 1.15, TimeSpan.FromMilliseconds(600))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };
            TeleportRingScale.BeginAnimation(ScaleTransform.ScaleXProperty, pulse);
            TeleportRingScale.BeginAnimation(ScaleTransform.ScaleYProperty, pulse);
        }

        /// <summary>Release T (jump = true) or pause/cancel (jump = false): aiming ends.</summary>
        private void FinishTeleportAim(bool jump)
        {
            if (!_session.Input.TeleportAimHold)
            {
                return;
            }

            _session.Input.TeleportAimHold = false;
            if (jump)
            {
                _session.Input.TeleportQueued = true;
            }

            TeleportRingScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            TeleportRingScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            TeleportArc.Opacity = 0;
            TeleportRing.Opacity = 0;
            TeleportRingInner.Opacity = 0;
            TeleportRingDot.Opacity = 0;
            TeleportText.Visibility = System.Windows.Visibility.Collapsed;
        }

        private Point GetCenterPoint()
        {
            return new Point(ActualWidth / 2.0, ActualHeight / 2.0);
        }

        private Point GetMuzzlePoint()
        {
            // No gun on screen (professional mode or holstered): beams read as a handheld
            // pointer from the bottom of the view.
            if (_professional || _holstered)
            {
                return new Point(ActualWidth / 2.0, ActualHeight + 30);
            }

            // GunCanvas: 300x240, right margin 12, bottom margin 8.
            if (_session.Input.WeaponSelect == 3 && _snagGunLoaded)
            {
                return new Point(
                    ActualWidth - 12 - 300 + _snagMuzzleCanvasPoint.X,
                    ActualHeight - 8 - 240 + _snagMuzzleCanvasPoint.Y);
            }

            if (_session.Input.WeaponSelect == 2 && _rifleLoaded)
            {
                return new Point(
                    ActualWidth - 12 - 300 + _rifleMuzzleCanvasPoint.X,
                    ActualHeight - 8 - 240 + _rifleMuzzleCanvasPoint.Y);
            }

            if (_customGunActive)
            {
                return new Point(
                    ActualWidth - 12 - 300 + _customMuzzleCanvasPoint.X,
                    ActualHeight - 8 - 240 + _customMuzzleCanvasPoint.Y);
            }

            // Vector pistol: muzzle sits at (53,55) inside the canvas.
            return new Point(ActualWidth - 259, ActualHeight - 193);
        }

        /// <summary>One shot: gun FX immediately, impact splash ~100 ms later when the bullet lands.</summary>
        private void FireOnce()
        {
            _lastShotMs = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            _shotCount++;
            PlayShootFx();
            PlayImpactSplash();
            PlayShotSound();
        }

        // CA1416: SoundPlayer is Windows-only - always true for a Revit add-in (same known-noise
        // class as the AiShell-era files; suppressed with this justification).
#pragma warning disable CA1416
        /// <summary>Small synthesized gunshot (no sound files shipped). M toggles sounds off.</summary>
        private void PlayShotSound()
        {
            if (!_session.Input.SoundOn)
            {
                return;
            }

            try
            {
                if (_shotWavBytes == null)
                {
                    _shotWavBytes = BuildShotWav();
                }

                var player = new System.Media.SoundPlayer(new System.IO.MemoryStream(_shotWavBytes));
                player.Play();
            }
            catch (Exception)
            {
                // Sound is a bonus - never let it break a shot.
            }
        }
#pragma warning restore CA1416

        /// <summary>90 ms of decaying noise = a small "pop" gunshot, built in memory as a WAV.</summary>
        private static byte[] BuildShotWav()
        {
            const int rate = 11025;
            int count = (int)(rate * 0.09);
            var data = new byte[count];
            var random = new Random(7);
            for (int i = 0; i < count; i++)
            {
                double envelope = Math.Exp(-6.0 * i / count);
                double value = ((random.NextDouble() * 2.0) - 1.0) * envelope * 0.55;
                data[i] = (byte)(128 + (int)(value * 127));
            }

            using (var stream = new System.IO.MemoryStream())
            using (var writer = new System.IO.BinaryWriter(stream))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + count);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                writer.Write(16);
                writer.Write((short)1);   // PCM
                writer.Write((short)1);   // mono
                writer.Write(rate);
                writer.Write(rate);       // byte rate (8-bit mono)
                writer.Write((short)1);   // block align
                writer.Write((short)8);   // bits per sample
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                writer.Write(count);
                writer.Write(data);
                writer.Flush();
                return stream.ToArray();
            }
        }

        private void PlayShootFx()
        {
            Point muzzle = GetMuzzlePoint();
            Point center = GetCenterPoint();

            // Muzzle flash + recoil belong to a visible gun only.
            if (!_professional)
            {
                MuzzleFlash.BeginAnimation(OpacityProperty, new DoubleAnimation(0.95, 0.0, TimeSpan.FromMilliseconds(90)));
            }

            // Tracer line
            TracerLine.X1 = muzzle.X;
            TracerLine.Y1 = muzzle.Y;
            TracerLine.X2 = center.X;
            TracerLine.Y2 = center.Y;
            TracerLine.BeginAnimation(OpacityProperty, new DoubleAnimation(0.9, 0.0, TimeSpan.FromMilliseconds(140)));

            // The bullet itself - a bright dot flying muzzle -> crosshair.
            var flightTime = TimeSpan.FromMilliseconds(110);
            BulletDot.BeginAnimation(Canvas.LeftProperty, new DoubleAnimation(muzzle.X - 4.5, center.X - 4.5, flightTime));
            BulletDot.BeginAnimation(Canvas.TopProperty, new DoubleAnimation(muzzle.Y - 4.5, center.Y - 4.5, flightTime));
            var bulletFade = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(60))
            {
                BeginTime = TimeSpan.FromMilliseconds(80)
            };
            BulletDot.BeginAnimation(OpacityProperty, bulletFade);

            if (!_professional)
            {
                // Recoil kicks straight back along the 45-degree barrel axis (down-right on screen).
                var recoilTime = TimeSpan.FromMilliseconds(55);
                var kickX = new DoubleAnimation(0, 7, recoilTime) { AutoReverse = true };
                var kickY = new DoubleAnimation(0, 7, recoilTime) { AutoReverse = true };
                GunRecoil.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, kickX);
                GunRecoil.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, kickY);
            }
        }

        /// <summary>Eight reusable spark lines for the bullet-impact splash, created once.</summary>
        private void CreateImpactSparks()
        {
            _sparks = new System.Windows.Shapes.Line[8];
            for (int i = 0; i < _sparks.Length; i++)
            {
                var spark = new System.Windows.Shapes.Line
                {
                    Stroke = new SolidColorBrush(Color.FromRgb(0xFF, 0xD0, 0x60)),
                    StrokeThickness = 2.5,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                    Opacity = 0,
                    IsHitTestVisible = false,
                    Effect = new System.Windows.Media.Effects.DropShadowEffect
                    {
                        Color = Color.FromRgb(0xFF, 0xB3, 0x47),
                        BlurRadius = 8,
                        ShadowDepth = 0,
                        Opacity = 0.9
                    }
                };
                _sparks[i] = spark;
                FxCanvas.Children.Add(spark);
            }
        }

        /// <summary>Splash at the crosshair: expanding ring + 8 sparks flying outward, slightly delayed.</summary>
        private void PlayImpactSplash()
        {
            Point center = GetCenterPoint();
            var delay = TimeSpan.FromMilliseconds(ImpactDelayMs);

            // Expanding ring
            Canvas.SetLeft(ImpactRing, center.X - (ImpactRing.Width / 2));
            Canvas.SetTop(ImpactRing, center.Y - (ImpactRing.Height / 2));
            var grow = TimeSpan.FromMilliseconds(200);
            ImpactScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.2, 1.5, grow) { BeginTime = delay });
            ImpactScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.2, 1.5, grow) { BeginTime = delay });
            ImpactRing.BeginAnimation(OpacityProperty, new DoubleAnimation(0.9, 0.0, grow) { BeginTime = delay });

            // Sparks flying outward; the whole burst rotates a little every shot so it looks alive.
            if (_sparks == null)
            {
                return;
            }

            double burstOffsetDeg = (_shotCount * 23) % 45;
            var sparkTime = TimeSpan.FromMilliseconds(230);
            for (int i = 0; i < _sparks.Length; i++)
            {
                double angle = (burstOffsetDeg + (i * 45.0)) * Math.PI / 180.0;
                double cos = Math.Cos(angle);
                double sin = Math.Sin(angle);

                System.Windows.Shapes.Line spark = _sparks[i];
                spark.X1 = center.X + (cos * 6);
                spark.Y1 = center.Y + (sin * 6);
                spark.X2 = center.X + (cos * 20);
                spark.Y2 = center.Y + (sin * 20);

                spark.BeginAnimation(System.Windows.Shapes.Line.X1Property,
                    new DoubleAnimation(center.X + (cos * 6), center.X + (cos * 26), sparkTime) { BeginTime = delay });
                spark.BeginAnimation(System.Windows.Shapes.Line.Y1Property,
                    new DoubleAnimation(center.Y + (sin * 6), center.Y + (sin * 26), sparkTime) { BeginTime = delay });
                spark.BeginAnimation(System.Windows.Shapes.Line.X2Property,
                    new DoubleAnimation(center.X + (cos * 20), center.X + (cos * 46), sparkTime) { BeginTime = delay });
                spark.BeginAnimation(System.Windows.Shapes.Line.Y2Property,
                    new DoubleAnimation(center.Y + (sin * 20), center.Y + (sin * 46), sparkTime) { BeginTime = delay });
                spark.BeginAnimation(OpacityProperty,
                    new DoubleAnimation(1.0, 0.0, sparkTime) { BeginTime = delay });
            }
        }
    }
}
