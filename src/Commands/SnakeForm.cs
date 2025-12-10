// Tool Name: Cyber Snake Form
// Description: WinForms mini-game UI used by the Cyber Snake command.
// Author: Ajmal P.S.
// Version: 1.0.0
// Last Updated: 2025-12-10
// Revit Version: 2020
// Dependencies: System.Windows.Forms, System.Drawing
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace AJTools.Commands
{
    internal enum Direction
    {
        Up,
        Down,
        Left,
        Right
    }

    /// <summary>
    /// Simple WinForms snake game used for the Cyber Snake command.
    /// </summary>
    internal class SnakeForm : Form
    {
        private readonly Timer _timer = new Timer();
        private readonly List<Point> _snake = new List<Point>();
        private Point _food;
        private Direction _direction = Direction.Right;
        private const int CellSize = 10;
        private readonly int _gridWidth = 40;
        private readonly int _gridHeight = 30;

        /// <summary>
        /// Initializes the game window and seeds the first round.
        /// </summary>
        public SnakeForm()
        {
            Text = "Cyber Snake";
            Width = _gridWidth * CellSize + 16;
            Height = _gridHeight * CellSize + 39;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            _timer.Interval = 120;
            _timer.Tick += Timer_Tick;

            KeyDown += SnakeForm_KeyDown;

            ResetGame();
        }

        /// <summary>
        /// Resets the snake, direction, and food, then restarts the timer loop.
        /// </summary>
        private void ResetGame()
        {
            _snake.Clear();
            _snake.Add(new Point(5, 5));
            _snake.Add(new Point(4, 5));
            _snake.Add(new Point(3, 5));
            _direction = Direction.Right;
            SpawnFood();
            _timer.Start();
        }

        private void SpawnFood()
        {
            Random rnd = new Random();
            _food = new Point(rnd.Next(0, _gridWidth), rnd.Next(0, _gridHeight));
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            MoveSnake();
            Invalidate();
        }

        private void MoveSnake()
        {
            Point head = _snake[0];
            Point newHead = head;

            switch (_direction)
            {
                case Direction.Up: newHead.Y -= 1; break;
                case Direction.Down: newHead.Y += 1; break;
                case Direction.Left: newHead.X -= 1; break;
                case Direction.Right: newHead.X += 1; break;
            }

            // Check collisions
            // End the round if the snake hits a wall or itself.
            if (newHead.X < 0 || newHead.Y < 0 || newHead.X >= _gridWidth || newHead.Y >= _gridHeight || _snake.Contains(newHead))
            {
                _timer.Stop();
                MessageBox.Show("Game over!", "Cyber Snake");
                ResetGame();
                return;
            }

            _snake.Insert(0, newHead);

            if (newHead == _food)
            {
                SpawnFood();
            }
            else
            {
                _snake.RemoveAt(_snake.Count - 1);
            }
        }

        private void SnakeForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Up && _direction != Direction.Down) _direction = Direction.Up;
            else if (e.KeyCode == Keys.Down && _direction != Direction.Up) _direction = Direction.Down;
            else if (e.KeyCode == Keys.Left && _direction != Direction.Right) _direction = Direction.Left;
            else if (e.KeyCode == Keys.Right && _direction != Direction.Left) _direction = Direction.Right;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            Brush snakeBrush = Brushes.LimeGreen;
            Brush foodBrush = Brushes.OrangeRed;

            foreach (Point p in _snake)
            {
                g.FillRectangle(snakeBrush, p.X * CellSize, p.Y * CellSize, CellSize, CellSize);
            }

            g.FillRectangle(foodBrush, _food.X * CellSize, _food.Y * CellSize, CellSize, CellSize);
        }
    }
}
