using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using DB = Autodesk.Revit.DB;

namespace AJTools
{
    [Transaction(TransactionMode.Manual)]
    public class CmdSnakeGame : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, DB.ElementSet elements)
        {
            try
            {
                using (SnakeForm gameForm = new SnakeForm())
                {
                    gameForm.ShowDialog();
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }

    internal class SnakeForm : Form
    {
        private readonly Timer gameTimer;
        private readonly List<Point> snake = new List<Point>();
        private readonly Random random = new Random();
        private Point food;
        private int directionX;
        private int directionY;
        private int score;
        private bool isGameOver;

        private const int TileSize = 20;
        private const int BoardWidth = 30;
        private const int BoardHeight = 20;

        private readonly Color bgColor = Color.FromArgb(5, 5, 5);
        private readonly Color gridColor = Color.FromArgb(10, 31, 31);
        private readonly Color snakeColor = Color.FromArgb(57, 255, 20);
        private readonly Color headColor = Color.White;
        private readonly Color foodColor = Color.FromArgb(255, 0, 255);

        public SnakeForm()
        {
            Text = "Revit Cyber Snake";
            DoubleBuffered = true;
            ClientSize = new Size(BoardWidth * TileSize, BoardHeight * TileSize);
            BackColor = bgColor;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;

            InitializeGame();

            gameTimer = new Timer
            {
                Interval = 100
            };
            gameTimer.Tick += GameLoop;
            gameTimer.Start();
        }

        private void InitializeGame()
        {
            score = 0;
            isGameOver = false;
            snake.Clear();
            snake.Add(new Point(BoardWidth / 2, BoardHeight / 2));
            snake.Add(new Point(BoardWidth / 2, BoardHeight / 2 + 1));
            snake.Add(new Point(BoardWidth / 2, BoardHeight / 2 + 2));
            directionX = 0;
            directionY = -1;
            SpawnFood();
        }

        private void SpawnFood()
        {
            int x;
            int y;

            do
            {
                x = random.Next(0, BoardWidth);
                y = random.Next(0, BoardHeight);
            } while (snake.Contains(new Point(x, y)));

            food = new Point(x, y);
        }

        private void GameLoop(object sender, EventArgs e)
        {
            if (isGameOver)
                return;

            Point head = snake[0];
            Point newHead = new Point(head.X + directionX, head.Y + directionY);

            if (newHead.X < 0 || newHead.X >= BoardWidth || newHead.Y < 0 || newHead.Y >= BoardHeight)
            {
                GameOver();
                return;
            }

            if (snake.Contains(newHead))
            {
                GameOver();
                return;
            }

            snake.Insert(0, newHead);

            if (newHead == food)
            {
                score += 10;
                Text = $"Revit Cyber Snake - Score: {score}";
                SpawnFood();
            }
            else
            {
                snake.RemoveAt(snake.Count - 1);
            }

            Invalidate();
        }

        private void GameOver()
        {
            isGameOver = true;
            gameTimer.Stop();
            MessageBox.Show($"System Failure.{Environment.NewLine}Final Score: {score}", "Game Over", MessageBoxButtons.OK, MessageBoxIcon.Information);
            Close();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Up:
                    if (directionY != 1)
                    {
                        directionX = 0;
                        directionY = -1;
                    }
                    return true;
                case Keys.Down:
                    if (directionY != -1)
                    {
                        directionX = 0;
                        directionY = 1;
                    }
                    return true;
                case Keys.Left:
                    if (directionX != 1)
                    {
                        directionX = -1;
                        directionY = 0;
                    }
                    return true;
                case Keys.Right:
                    if (directionX != -1)
                    {
                        directionX = 1;
                        directionY = 0;
                    }
                    return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;

            using (Pen gridPen = new Pen(gridColor))
            {
                for (int x = 0; x <= BoardWidth; x++)
                {
                    g.DrawLine(gridPen, x * TileSize, 0, x * TileSize, BoardHeight * TileSize);
                }

                for (int y = 0; y <= BoardHeight; y++)
                {
                    g.DrawLine(gridPen, 0, y * TileSize, BoardWidth * TileSize, y * TileSize);
                }
            }

            Rectangle foodRect = new Rectangle(food.X * TileSize + 2, food.Y * TileSize + 2, TileSize - 4, TileSize - 4);
            using (SolidBrush brush = new SolidBrush(foodColor))
            {
                g.FillRectangle(brush, foodRect);
            }

            for (int i = 0; i < snake.Count; i++)
            {
                Point p = snake[i];
                Rectangle rect = new Rectangle(p.X * TileSize + 1, p.Y * TileSize + 1, TileSize - 2, TileSize - 2);
                using (SolidBrush brush = new SolidBrush(i == 0 ? headColor : snakeColor))
                {
                    g.FillRectangle(brush, rect);
                }
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            gameTimer.Stop();
            gameTimer.Dispose();
            base.OnFormClosed(e);
        }
    }
}
