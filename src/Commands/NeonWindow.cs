// Tool Name: Neon Defender Window
// Description: WPF mini-game window used by the Neon Defender command.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-10
// Revit Version: 2020
// Dependencies: System.Windows, Autodesk.Revit.UI
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Autodesk.Revit.UI;

namespace AJTools.Commands
{
    internal class NeonWindow : Window
    {
        private readonly DispatcherTimer _timer;
        private readonly Canvas _canvas;
        private readonly List<Ellipse> _enemies = new List<Ellipse>();
        private readonly Rectangle _player;
        private readonly Random _rand = new Random();
        private int _score;

        public NeonWindow()
        {
            Title = "Neon Defender";
            Width = 640;
            Height = 480;
            Background = new SolidColorBrush(Color.FromRgb(12, 16, 32));
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            _canvas = new Canvas
            {
                Background = Brushes.Transparent
            };
            Content = _canvas;

            _player = new Rectangle
            {
                Width = 24,
                Height = 24,
                Fill = new SolidColorBrush(Color.FromRgb(76, 217, 255))
            };
            _canvas.Children.Add(_player);
            Canvas.SetLeft(_player, 300);
            Canvas.SetTop(_player, 420);

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            _timer.Tick += OnTick;
            _timer.Start();

            KeyDown += OnKeyDown;
        }

        private void OnTick(object sender, EventArgs e)
        {
            // Spawn enemies
            if (_rand.NextDouble() < 0.05)
            {
                var enemy = new Ellipse
                {
                    Width = 16,
                    Height = 16,
                    Fill = new SolidColorBrush(Color.FromRgb(255, 64, 129))
                };
                _canvas.Children.Add(enemy);
                _enemies.Add(enemy);
                Canvas.SetLeft(enemy, _rand.Next(0, (int)(ActualWidth - 32)));
                Canvas.SetTop(enemy, 0);
            }

            // Move enemies
            for (int i = _enemies.Count - 1; i >= 0; i--)
            {
                Ellipse enemy = _enemies[i];
                double y = Canvas.GetTop(enemy) + 3;
                Canvas.SetTop(enemy, y);

                if (y > ActualHeight)
                {
                    _canvas.Children.Remove(enemy);
                    _enemies.RemoveAt(i);
                    continue;
                }

                if (IsColliding(enemy, _player))
                {
                    _timer.Stop();
                    TaskDialog.Show("Neon Defender", $"Game Over! Score: {_score}");
                    Close();
                    return;
                }
            }

            _score++;
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            double x = Canvas.GetLeft(_player);
            if (e.Key == Key.Left)
            {
                x = Math.Max(0, x - 10);
            }
            else if (e.Key == Key.Right)
            {
                x = Math.Min(ActualWidth - _player.Width - 16, x + 10);
            }

            Canvas.SetLeft(_player, x);
        }

        private static bool IsColliding(Shape a, Shape b)
        {
            Rect ra = new Rect(Canvas.GetLeft(a), Canvas.GetTop(a), a.Width, a.Height);
            Rect rb = new Rect(Canvas.GetLeft(b), Canvas.GetTop(b), b.Width, b.Height);
            return ra.IntersectsWith(rb);
        }
    }
}
