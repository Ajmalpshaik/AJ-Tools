using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using MediaColor = System.Windows.Media.Color;
using WGrid = System.Windows.Controls.Grid;
using WVisibility = System.Windows.Visibility;
using WEllipse = System.Windows.Shapes.Ellipse;
using WPolygon = System.Windows.Shapes.Polygon;
using WRectangle = System.Windows.Shapes.Rectangle;
using WPoint = System.Windows.Point;

namespace AJTools
{
    [Transaction(TransactionMode.Manual)]
    public class CmdNeonDefender : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                NeonDefenderWindow gameWindow = new NeonDefenderWindow();
                gameWindow.ShowDialog();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }

    internal class NeonDefenderWindow : Window
    {
        private static readonly Random _rng = new Random();

        private Canvas _gameCanvas;
        private DispatcherTimer _gameLoop;
        private TextBlock _scoreText;
        private TextBlock _livesText;
        private TextBlock _gameOverText;

        private bool _isRunning;
        private int _score;
        private int _lives;
        private int _frameCount;
        private DateTime _startTime;
        private double _spawnCharge;

        private WPolygon _playerShip;
        private WPoint _playerPosition;
        private double _playerSpeed = 5.0;
        private double _playerAngle;

        private readonly List<Bullet> _bullets = new List<Bullet>();
        private readonly List<Enemy> _enemies = new List<Enemy>();
        private readonly List<Particle> _particles = new List<Particle>();

        private bool _moveUp, _moveDown, _moveLeft, _moveRight;
        private WPoint _mousePosition;
        private bool _isMouseDown;
        private long _lastShotTime;

        private readonly Brush NeonGreen = new SolidColorBrush(MediaColor.FromRgb(57, 255, 20));
        private readonly Brush NeonPink = new SolidColorBrush(MediaColor.FromRgb(255, 0, 255));
        private readonly Brush NeonRed = new SolidColorBrush(MediaColor.FromRgb(255, 0, 85));
        private readonly Brush NeonYellow = new SolidColorBrush(MediaColor.FromRgb(255, 255, 0));
        private readonly Brush DarkBg = new RadialGradientBrush(MediaColor.FromRgb(26, 26, 26), MediaColor.FromRgb(0, 0, 0))
        { Center = new WPoint(0.5, 0.5), RadiusX = 1.0, RadiusY = 1.0 };

        public NeonDefenderWindow()
        {
            Title = "Neon Defender: Omni Assault";
            Width = 800;
            Height = 600;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = Brushes.Black;
            ResizeMode = ResizeMode.NoResize;

            WGrid mainGrid = new WGrid();
            Content = mainGrid;

            _gameCanvas = new Canvas
            {
                Background = DarkBg,
                ClipToBounds = true,
                Cursor = Cursors.Cross,
                Width = 800,
                Height = 600
            };
            mainGrid.Children.Add(_gameCanvas);

            Canvas uiLayer = new Canvas { IsHitTestVisible = false };
            mainGrid.Children.Add(uiLayer);

            _scoreText = CreateText(20, 20, "SCORE: 0", NeonGreen, 20);
            uiLayer.Children.Add(_scoreText);

            _livesText = CreateText(680, 20, "LIVES: 3", NeonGreen, 20);
            uiLayer.Children.Add(_livesText);

            _gameOverText = CreateText(250, 250, "SYSTEM FAILURE\nPRESS SPACE TO REBOOT", NeonRed, 30);
            _gameOverText.Visibility = WVisibility.Hidden;
            _gameOverText.TextAlignment = TextAlignment.Center;
            uiLayer.Children.Add(_gameOverText);

            KeyDown += OnKeyDown;
            KeyUp += OnKeyUp;
            _gameCanvas.MouseMove += OnMouseMove;
            _gameCanvas.MouseLeftButtonDown += (s, e) => _isMouseDown = true;
            _gameCanvas.MouseLeftButtonUp += (s, e) => _isMouseDown = false;
            Closed += (s, e) => StopGame();
            Loaded += (s, e) => _mousePosition = new WPoint(GetCanvasWidth() / 2, GetCanvasHeight() / 2);

            InitGame();
        }

        private TextBlock CreateText(double x, double y, string text, Brush color, double size)
        {
            TextBlock tb = new TextBlock
            {
                Text = text,
                Foreground = color,
                FontSize = size,
                FontFamily = new FontFamily("Consolas"),
                FontWeight = FontWeights.Bold,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = ((SolidColorBrush)color).Color,
                    BlurRadius = 10,
                    ShadowDepth = 0
                }
            };
            Canvas.SetLeft(tb, x);
            Canvas.SetTop(tb, y);
            return tb;
        }

        private void InitGame()
        {
            _gameCanvas.Children.Clear();
            _bullets.Clear();
            _enemies.Clear();
            _particles.Clear();

            _score = 0;
            _lives = 3;
            _frameCount = 0;
            _spawnCharge = 0;
            _startTime = DateTime.Now;
            _isRunning = true;
            _scoreText.Text = "SCORE: 0";
            _livesText.Text = "LIVES: 3";
            _gameOverText.Visibility = WVisibility.Hidden;

            _playerShip = new WPolygon
            {
                Points = new PointCollection { new WPoint(20, 0), new WPoint(-15, 15), new WPoint(-5, 0), new WPoint(-15, -15) },
                Stroke = NeonGreen,
                StrokeThickness = 2,
                Fill = Brushes.Transparent,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = ((SolidColorBrush)NeonGreen).Color,
                    BlurRadius = 15,
                    ShadowDepth = 0
                }
            };

            _playerPosition = new WPoint(GetCanvasWidth() / 2, GetCanvasHeight() / 2);
            _gameCanvas.Children.Add(_playerShip);

            _gameLoop?.Stop();
            _gameLoop = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            _gameLoop.Tick += GameLoop_Tick;
            _gameLoop.Start();
        }

        private double GetCanvasWidth()
        {
            double w = _gameCanvas.ActualWidth;
            if (w <= 0) w = _gameCanvas.RenderSize.Width;
            if (w <= 0) w = _gameCanvas.Width;
            return w > 0 ? w : 800;
        }

        private double GetCanvasHeight()
        {
            double h = _gameCanvas.ActualHeight;
            if (h <= 0) h = _gameCanvas.RenderSize.Height;
            if (h <= 0) h = _gameCanvas.Height;
            return h > 0 ? h : 600;
        }

        private double CalculateDifficulty()
        {
            // Start easy, then climb steadily every ~20s, capped to avoid runaway speeds.
            double elapsedSeconds = (DateTime.Now - _startTime).TotalSeconds;
            double factor = 1 + (elapsedSeconds / 20.0);
            return Math.Min(factor, 4.0);
        }

        private double GetSpawnIntervalFrames(double difficultyFactor)
        {
            // Longer intervals at the start (~1.3s), shrinking toward ~0.3s as difficulty rises.
            return Math.Max(18.0, 80.0 / difficultyFactor);
        }

        private double GetEnemySpeed(double difficultyFactor)
        {
            double baseSpeed = 1.5 + _rng.NextDouble(); // gentler than the original 2-3 range
            double scaled = baseSpeed + (difficultyFactor - 1) * 0.7;
            return Math.Min(scaled, 6.0);
        }

        private void StopGame()
        {
            _gameLoop?.Stop();
            _isRunning = false;
        }

        private void GameOver()
        {
            _isRunning = false;
            _gameOverText.Visibility = WVisibility.Visible;
        }

        private void GameLoop_Tick(object sender, EventArgs e)
        {
            if (!_isRunning) return;

            UpdatePlayer();
            UpdateBullets();
            UpdateEnemies();
            UpdateParticles();
            Draw();

            _frameCount++;
        }

        private void UpdatePlayer()
        {
            double maxW = GetCanvasWidth();
            double maxH = GetCanvasHeight();

            if (_moveUp && _playerPosition.Y > 20) _playerPosition.Y -= _playerSpeed;
            if (_moveDown && _playerPosition.Y < maxH - 20) _playerPosition.Y += _playerSpeed;
            if (_moveLeft && _playerPosition.X > 20) _playerPosition.X -= _playerSpeed;
            if (_moveRight && _playerPosition.X < maxW - 20) _playerPosition.X += _playerSpeed;

            Vector dir = _mousePosition - _playerPosition;
            _playerAngle = Math.Atan2(dir.Y, dir.X) * (180 / Math.PI);

            if (_isMouseDown)
            {
                long now = DateTime.Now.Ticks / 10000;
                if (now - _lastShotTime > 150)
                {
                    SpawnBullet();
                    _lastShotTime = now;
                }
            }
        }

        private void SpawnBullet()
        {
            double rads = _playerAngle * (Math.PI / 180);
            Vector velocity = new Vector(Math.Cos(rads) * 12, Math.Sin(rads) * 12);

            WEllipse bulletShape = new WEllipse { Width = 6, Height = 6, Fill = NeonPink, Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = ((SolidColorBrush)NeonPink).Color, BlurRadius = 8, ShadowDepth = 0 } };
            _gameCanvas.Children.Add(bulletShape);
            _bullets.Add(new Bullet { Shape = bulletShape, Position = _playerPosition, Velocity = velocity });
        }

        private void UpdateBullets()
        {
            double maxW = GetCanvasWidth();
            double maxH = GetCanvasHeight();

            for (int i = _bullets.Count - 1; i >= 0; i--)
            {
                Bullet b = _bullets[i];
                b.Position += b.Velocity;

                if (b.Position.X < 0 || b.Position.X > maxW || b.Position.Y < 0 || b.Position.Y > maxH)
                {
                    _gameCanvas.Children.Remove(b.Shape);
                    _bullets.RemoveAt(i);
                }
            }
        }

        private void UpdateEnemies()
        {
            double difficulty = CalculateDifficulty();
            double spawnIntervalFrames = GetSpawnIntervalFrames(difficulty);
            _spawnCharge += 1;

            // Use a charge/interval model so increasing difficulty spawns enemies more often over time.
            while (_spawnCharge >= spawnIntervalFrames)
            {
                SpawnEnemy(difficulty);
                _spawnCharge -= spawnIntervalFrames;
            }

            for (int i = _enemies.Count - 1; i >= 0; i--)
            {
                Enemy en = _enemies[i];

                Vector toPlayer = _playerPosition - en.Position;
                if (toPlayer.Length > 0.001)
                {
                    toPlayer.Normalize();
                }
                en.Position += toPlayer * en.Speed;
                en.Angle += 2;

                bool hit = false;
                for (int j = _bullets.Count - 1; j >= 0; j--)
                {
                    if ((_bullets[j].Position - en.Position).Length < 15)
                    {
                        CreateExplosion(en.Position, en.Color);
                        _gameCanvas.Children.Remove(_bullets[j].Shape);
                        _bullets.RemoveAt(j);
                        hit = true;
                        _score += 100;
                        _scoreText.Text = "SCORE: " + _score;
                        break;
                    }
                }

                if (!hit && (en.Position - _playerPosition).Length < 25)
                {
                    CreateExplosion(en.Position, NeonRed);
                    hit = true;
                    _lives--;
                    _livesText.Text = "LIVES: " + _lives;
                    if (_lives <= 0) GameOver();
                }

                if (hit)
                {
                    _gameCanvas.Children.Remove(en.Shape);
                    _enemies.RemoveAt(i);
                }
            }
        }

        private void SpawnEnemy(double difficultyFactor)
        {
            int side = _rng.Next(0, 4);
            double w = GetCanvasWidth();
            double h = GetCanvasHeight();
            WPoint spawnPos;

            if (side == 0) { spawnPos = new WPoint(_rng.NextDouble() * w, -30); }
            else if (side == 1) { spawnPos = new WPoint(w + 30, _rng.NextDouble() * h); }
            else if (side == 2) { spawnPos = new WPoint(_rng.NextDouble() * w, h + 30); }
            else { spawnPos = new WPoint(-30, _rng.NextDouble() * h); }

            Brush color = _rng.NextDouble() > 0.5 ? NeonRed : NeonYellow;

            WRectangle rect = new WRectangle
            {
                Width = 20,
                Height = 20,
                Stroke = color,
                StrokeThickness = 2,
                RenderTransformOrigin = new WPoint(0.5, 0.5),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = ((SolidColorBrush)color).Color,
                    BlurRadius = 8,
                    ShadowDepth = 0
                }
            };

            _gameCanvas.Children.Add(rect);
            _enemies.Add(new Enemy
            {
                Shape = rect,
                Position = spawnPos,
                Speed = GetEnemySpeed(difficultyFactor),
                Color = color
            });
        }

        private void UpdateParticles()
        {
            for (int i = _particles.Count - 1; i >= 0; i--)
            {
                Particle p = _particles[i];
                p.Position += p.Velocity;
                p.Shape.Opacity -= 0.05;

                if (p.Shape.Opacity <= 0)
                {
                    _gameCanvas.Children.Remove(p.Shape);
                    _particles.RemoveAt(i);
                }
            }
        }

        private void CreateExplosion(WPoint pos, Brush color)
        {
            for (int i = 0; i < 8; i++)
            {
                WEllipse pShape = new WEllipse { Width = 4, Height = 4, Fill = color };
                Vector velocity = new Vector((_rng.NextDouble() - 0.5) * 10, (_rng.NextDouble() - 0.5) * 10);

                _gameCanvas.Children.Add(pShape);
                _particles.Add(new Particle { Shape = pShape, Position = pos, Velocity = velocity });
            }
        }

        private void Draw()
        {
            Canvas.SetLeft(_playerShip, _playerPosition.X);
            Canvas.SetTop(_playerShip, _playerPosition.Y);
            _playerShip.RenderTransform = new RotateTransform(_playerAngle, 0, 0);

            foreach (var b in _bullets)
            {
                Canvas.SetLeft(b.Shape, b.Position.X - 3);
                Canvas.SetTop(b.Shape, b.Position.Y - 3);
            }

            foreach (var e in _enemies)
            {
                Canvas.SetLeft(e.Shape, e.Position.X - 10);
                Canvas.SetTop(e.Shape, e.Position.Y - 10);
                e.Shape.RenderTransform = new RotateTransform(e.Angle);
            }

            foreach (var p in _particles)
            {
                Canvas.SetLeft(p.Shape, p.Position.X);
                Canvas.SetTop(p.Shape, p.Position.Y);
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.W) _moveUp = true;
            if (e.Key == Key.S) _moveDown = true;
            if (e.Key == Key.A) _moveLeft = true;
            if (e.Key == Key.D) _moveRight = true;
            if (e.Key == Key.Space && !_isRunning) InitGame();
        }

        private void OnKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.W) _moveUp = false;
            if (e.Key == Key.S) _moveDown = false;
            if (e.Key == Key.A) _moveLeft = false;
            if (e.Key == Key.D) _moveRight = false;
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            _mousePosition = e.GetPosition(_gameCanvas);
        }
    }

    internal class Bullet
    {
        public WEllipse Shape { get; set; }
        public WPoint Position { get; set; }
        public Vector Velocity { get; set; }
    }

    internal class Enemy
    {
        public WRectangle Shape { get; set; }
        public WPoint Position { get; set; }
        public double Speed { get; set; }
        public double Angle { get; set; }
        public Brush Color { get; set; }
    }

    internal class Particle
    {
        public WEllipse Shape { get; set; }
        public WPoint Position { get; set; }
        public Vector Velocity { get; set; }
    }
}
