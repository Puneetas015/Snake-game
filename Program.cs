// ============================================================================
// Classic Snake Game - "Premium Arcade Terminal" Edition
// Target Framework: .NET 8.0+
//
// Engineering notes:
//  - No Console.Clear() is used inside the game loop. Instead we track the
//    previous board state and only overwrite cells that actually changed,
//    which eliminates screen flicker.
//  - Console.KeyAvailable is polled in a non-blocking way so the loop keeps
//    running at a fixed tick rate regardless of whether the player is
//    pressing keys.
//
// A NOTE ON GLYPH CHOICES (important for alignment):
//  - True color emoji (like the apple or green-circle emoji) render as
//    DOUBLE-WIDTH characters in virtually every terminal font, because they
//    carry an "emoji presentation" that forces a 2-cell-wide glyph. Using
//    them in a character grid game would silently break alignment on most
//    terminals (Windows Terminal, iTerm, gnome-terminal, etc.).
//  - To get the same bold, "premium arcade" look WITHOUT breaking the grid,
//    this version uses solid single-width Unicode symbols from the Geometric
//    Shapes / Block Elements / Dingbats ranges instead:
//      Border      -> '█' (full block)
//      Snake Head  -> '●' (bright green filled circle)
//      Snake Body  -> '■' / '▓' / '▒' (a shade gradient that tapers toward
//                     the tail for a clean, stylized look)
//      Food        -> '♥' (solid red heart glyph, single cell)
//    These are guaranteed single-width in standard monospace terminal fonts,
//    so the board never drifts out of alignment.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace SnakeGame
{
    // Simple 2D integer point used for snake segments, food position, etc.
    public struct Point
    {
        public int X;
        public int Y;

        public Point(int x, int y)
        {
            X = x;
            Y = y;
        }

        public override bool Equals(object? obj)
        {
            return obj is Point p && p.X == X && p.Y == Y;
        }

        public override int GetHashCode() => (X, Y).GetHashCode();
    }

    // Directions the snake can move in.
    public enum Direction
    {
        Up,
        Down,
        Left,
        Right
    }

    // Represents the snake itself: its body segments and movement logic.
    public class Snake
    {
        public LinkedList<Point> Body { get; private set; }
        public Direction CurrentDirection { get; private set; }
        public bool JustAte { get; private set; }

        public Snake(Point start)
        {
            Body = new LinkedList<Point>();
            Body.AddFirst(start);
            CurrentDirection = Direction.Right;
        }

        // Changes direction but prevents the snake from reversing directly
        // into itself (e.g. can't go Left while currently moving Right).
        public void ChangeDirection(Direction newDirection)
        {
            bool isOpposite =
                (CurrentDirection == Direction.Up && newDirection == Direction.Down) ||
                (CurrentDirection == Direction.Down && newDirection == Direction.Up) ||
                (CurrentDirection == Direction.Left && newDirection == Direction.Right) ||
                (CurrentDirection == Direction.Right && newDirection == Direction.Left);

            if (!isOpposite)
            {
                CurrentDirection = newDirection;
            }
        }

        public Point GetNextHeadPosition()
        {
            Point head = Body.First!.Value;

            return CurrentDirection switch
            {
                Direction.Up => new Point(head.X, head.Y - 1),
                Direction.Down => new Point(head.X, head.Y + 1),
                Direction.Left => new Point(head.X - 1, head.Y),
                Direction.Right => new Point(head.X + 1, head.Y),
                _ => head
            };
        }

        // Moves the snake forward. If growing, the tail is not removed.
        public void Move(Point newHead, bool grow)
        {
            Body.AddFirst(newHead);
            JustAte = grow;

            if (!grow)
            {
                Body.RemoveLast();
            }
        }

        // Checks if the given point collides with the snake's own body.
        public bool CollidesWithSelf(Point point)
        {
            foreach (Point segment in Body)
            {
                if (segment.Equals(point))
                {
                    return true;
                }
            }
            return false;
        }
    }

    // Represents the food item on the board.
    public class Food
    {
        public Point Position { get; private set; }
        private readonly Random _random = new Random();

        public Food(int boardWidth, int boardHeight, Snake snake)
        {
            Respawn(boardWidth, boardHeight, snake);
        }

        // Picks a new random position that does not overlap the snake body.
        public void Respawn(int boardWidth, int boardHeight, Snake snake)
        {
            Point candidate;
            do
            {
                int x = _random.Next(0, boardWidth);
                int y = _random.Next(0, boardHeight);
                candidate = new Point(x, y);
            }
            while (snake.CollidesWithSelf(candidate));

            Position = candidate;
        }
    }

    // Handles all console drawing, including flicker-free partial redraws
    // and the "premium arcade" visual styling.
    public class Renderer
    {
        // --- Glyph palette (see the alignment note at the top of the file) ---
        private const char GlyphBorder = '█';
        private const char GlyphHead = '●';
        private const char GlyphBodySolid = '■';   // near the head: solid
        private const char GlyphBodyMedium = '▓';  // mid-body: medium shade
        private const char GlyphBodyLight = '▒';   // near the tail: light shade (taper)
        private const char GlyphFood = '♥';

        private readonly int _boardWidth;
        private readonly int _boardHeight;
        private readonly int _originX;   // Left offset of the play area in the console
        private readonly int _originY;   // Top offset of the play area in the console (below HUD)

        // Tracks what character + color currently occupies each cell so we
        // only rewrite cells whose content has changed since the last frame.
        private readonly char[,] _previousChar;
        private readonly ConsoleColor[,] _previousColor;
        private bool _firstDraw = true;

        public Renderer(int boardWidth, int boardHeight, int originX, int originY)
        {
            _boardWidth = boardWidth;
            _boardHeight = boardHeight;
            _originX = originX;
            _originY = originY;
            _previousChar = new char[boardWidth, boardHeight];
            _previousColor = new ConsoleColor[boardWidth, boardHeight];
        }

        // Draws the thick double-layer border once. Called only at startup.
        public void DrawBorder()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;

            for (int x = -1; x <= _boardWidth; x++)
            {
                WriteRaw(x, -1, GlyphBorder);
                WriteRaw(x, _boardHeight, GlyphBorder);
            }
            for (int y = -1; y <= _boardHeight; y++)
            {
                WriteRaw(-1, y, GlyphBorder);
                WriteRaw(_boardWidth, y, GlyphBorder);
            }

            Console.ResetColor();
        }

        // Renders one frame: snake body (with tapering shade), food, and
        // clears cells the snake vacated. Only writes to the console where a
        // cell's character OR color actually differs from the previous
        // frame — this is the anti-flicker technique.
        public void Draw(Snake snake, Food food)
        {
            char[,] desiredChar = new char[_boardWidth, _boardHeight];
            ConsoleColor[,] desiredColor = new ConsoleColor[_boardWidth, _boardHeight];

            for (int x = 0; x < _boardWidth; x++)
            {
                for (int y = 0; y < _boardHeight; y++)
                {
                    desiredChar[x, y] = ' ';
                    desiredColor[x, y] = ConsoleColor.Black;
                }
            }

            // Food: bright red solid heart glyph.
            desiredChar[food.Position.X, food.Position.Y] = GlyphFood;
            desiredColor[food.Position.X, food.Position.Y] = ConsoleColor.Red;

            // Snake: head is a bold bright-green circle; body tapers from a
            // solid block near the head to lighter shades toward the tail.
            int bodyLength = snake.Body.Count;
            int index = 0;
            foreach (Point segment in snake.Body)
            {
                if (segment.X < 0 || segment.X >= _boardWidth || segment.Y < 0 || segment.Y >= _boardHeight)
                {
                    index++;
                    continue;
                }

                if (index == 0)
                {
                    desiredChar[segment.X, segment.Y] = GlyphHead;
                    desiredColor[segment.X, segment.Y] = ConsoleColor.Green;
                }
                else
                {
                    // Taper effect: divide the tail into three shade bands.
                    double progress = bodyLength <= 1 ? 0 : (double)index / bodyLength;
                    char shape;
                    ConsoleColor color;

                    if (progress < 0.34)
                    {
                        shape = GlyphBodySolid;
                        color = ConsoleColor.Green;
                    }
                    else if (progress < 0.67)
                    {
                        shape = GlyphBodyMedium;
                        color = ConsoleColor.DarkGreen;
                    }
                    else
                    {
                        shape = GlyphBodyLight;
                        color = ConsoleColor.DarkGreen;
                    }

                    desiredChar[segment.X, segment.Y] = shape;
                    desiredColor[segment.X, segment.Y] = color;
                }

                index++;
            }

            // Diff against the previous frame and only write changed cells.
            for (int x = 0; x < _boardWidth; x++)
            {
                for (int y = 0; y < _boardHeight; y++)
                {
                    char c = desiredChar[x, y];
                    ConsoleColor color = desiredColor[x, y];

                    if (_firstDraw || _previousChar[x, y] != c || _previousColor[x, y] != color)
                    {
                        Console.ForegroundColor = color;
                        WriteRaw(x, y, c);
                        _previousChar[x, y] = c;
                        _previousColor[x, y] = color;
                    }
                }
            }

            Console.ResetColor();
            _firstDraw = false;
        }

        // Draws a stylish boxed HUD panel above the board showing the score
        // (green) and high score (yellow), with a cyan border and a red
        // food-legend hint to tie the color coding together.
        public void DrawHud(int score, int highScore)
        {
            int panelWidth = _boardWidth + 2; // matches the board's outer border width
            int panelLeft = _originX - 1;
            int panelTop = _originY - 4;

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.SetCursorPosition(panelLeft, panelTop);
            Console.Write("╔" + new string('═', panelWidth - 2) + "╗");

            Console.SetCursorPosition(panelLeft, panelTop + 2);
            Console.Write("╚" + new string('═', panelWidth - 2) + "╝");

            // Middle row: colored score / high score text, framed by the
            // cyan side borders.
            Console.SetCursorPosition(panelLeft, panelTop + 1);
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("║");

            string scoreLabel = " SCORE: ";
            string scoreValue = score.ToString();
            string highLabel = "   HIGH SCORE: ";
            string highValue = highScore.ToString();
            string foodLegend = "   ♥ FOOD ";

            int textLength = scoreLabel.Length + scoreValue.Length + highLabel.Length + highValue.Length + foodLegend.Length;
            int innerWidth = panelWidth - 2;
            int rightPad = Math.Max(0, innerWidth - textLength);

            Console.ForegroundColor = ConsoleColor.White;
            Console.Write(scoreLabel);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write(scoreValue);

            Console.ForegroundColor = ConsoleColor.White;
            Console.Write(highLabel);
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write(highValue);

            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write(foodLegend);

            Console.Write(new string(' ', rightPad));

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("║");

            Console.ResetColor();
        }

        // Draws a centered "GAME OVER" panel inside the play field, using a
        // box-drawn frame so it never wraps or spills past the borders.
        public void DrawGameOverPanel(int score, int highScore)
        {
            string[] lines =
            {
                "GAME OVER",
                $"Final Score: {score}",
                $"High Score: {highScore}",
                "",
                "Press R to Restart",
                "Press Q to Quit"
            };

            int contentWidth = 0;
            foreach (string line in lines)
            {
                contentWidth = Math.Max(contentWidth, line.Length);
            }

            int panelWidth = contentWidth + 4;  // 2 chars padding each side
            int panelHeight = lines.Length + 2; // top + bottom border

            // Clamp the panel so it always fits within the board.
            panelWidth = Math.Min(panelWidth, _boardWidth);

            int startX = (_boardWidth - panelWidth) / 2;
            int startY = (_boardHeight - panelHeight) / 2;
            if (startY < 0) startY = 0;

            Console.ForegroundColor = ConsoleColor.Magenta;

            // Top border.
            WriteTextAt(startX, startY, "╔" + new string('═', panelWidth - 2) + "╗");

            // Middle rows: either a centered content line or a blank interior row.
            for (int i = 0; i < lines.Length; i++)
            {
                string content = CenterWithinWidth(lines[i], panelWidth - 2);

                Console.ForegroundColor = ConsoleColor.Magenta;
                WriteTextAt(startX, startY + 1 + i, "║");

                Console.ForegroundColor = (i == 0) ? ConsoleColor.Red : ConsoleColor.White;
                WriteTextAt(startX + 1, startY + 1 + i, content);

                Console.ForegroundColor = ConsoleColor.Magenta;
                WriteTextAt(startX + panelWidth - 1, startY + 1 + i, "║");
            }

            // Bottom border.
            WriteTextAt(startX, startY + panelHeight - 1, "╚" + new string('═', panelWidth - 2) + "╝");

            Console.ResetColor();
        }

        // Centers a string within a fixed width using spaces on both sides.
        private static string CenterWithinWidth(string text, int width)
        {
            if (text.Length >= width)
            {
                return text.Substring(0, width);
            }

            int totalPadding = width - text.Length;
            int leftPadding = totalPadding / 2;
            int rightPadding = totalPadding - leftPadding;

            return new string(' ', leftPadding) + text + new string(' ', rightPadding);
        }

        // Writes a full text string starting at board-relative coordinates.
        private void WriteTextAt(int boardX, int boardY, string text)
        {
            int consoleX = _originX + boardX;
            int consoleY = _originY + boardY;

            if (consoleY >= 0)
            {
                Console.SetCursorPosition(Math.Max(consoleX, 0), consoleY);
                Console.Write(text);
            }
        }

        // Low-level helper: writes a single character at board-relative
        // coordinates, translated into actual console coordinates.
        private void WriteRaw(int boardX, int boardY, char c)
        {
            int consoleX = _originX + boardX;
            int consoleY = _originY + boardY;

            if (consoleX >= 0 && consoleY >= 0)
            {
                Console.SetCursorPosition(consoleX, consoleY);
                Console.Write(c);
            }
        }
    }

    // Orchestrates the game: input, update logic, collision detection,
    // scoring, and drives the render loop.
    public class GameEngine
    {
        private const int BoardWidth = 40;
        private const int BoardHeight = 20;
        private const int OriginX = 2;
        private const int OriginY = 6;
        private const int TickMilliseconds = 120; // Controls game speed

        private Snake _snake;
        private Food _food;
        private readonly Renderer _renderer;

        private int _score;
        private int _highScore;
        private bool _isGameOver;

        public GameEngine()
        {
            Point startPosition = new Point(BoardWidth / 2, BoardHeight / 2);
            _snake = new Snake(startPosition);
            _food = new Food(BoardWidth, BoardHeight, _snake);
            _renderer = new Renderer(BoardWidth, BoardHeight, OriginX, OriginY);
            _score = 0;
            _highScore = 0;
        }

        public void Run()
        {
            // UTF-8 output ensures the block/box-drawing glyphs render
            // correctly across terminals.
            Console.OutputEncoding = Encoding.UTF8;
            Console.CursorVisible = false;
            Console.Title = "Snake - Premium Arcade Terminal Edition";

            Console.SetCursorPosition(OriginX, 1);
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("=== S N A K E ===  Arcade Terminal Edition");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.SetCursorPosition(OriginX, 2);
            Console.WriteLine("WASD / Arrow Keys to move   |   Q to quit");
            Console.ResetColor();

            _renderer.DrawBorder();

            while (true)
            {
                PlayRound();

                _renderer.DrawGameOverPanel(_score, _highScore);

                ConsoleKey choice = WaitForRestartChoice();
                if (choice == ConsoleKey.Q)
                {
                    break;
                }

                ResetGame();
            }

            Console.SetCursorPosition(0, OriginY + BoardHeight + 2);
            Console.CursorVisible = true;
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("Thanks for playing!");
            Console.ResetColor();
        }

        // Runs a single playthrough (from spawn until death).
        private void PlayRound()
        {
            _isGameOver = false;

            while (!_isGameOver)
            {
                DateTime tickStart = DateTime.Now;

                HandleInput();
                UpdateGameState();
                _renderer.Draw(_snake, _food);
                _renderer.DrawHud(_score, _highScore);

                TimeSpan elapsed = DateTime.Now - tickStart;
                int remaining = TickMilliseconds - (int)elapsed.TotalMilliseconds;
                if (remaining > 0)
                {
                    Thread.Sleep(remaining);
                }
            }
        }

        // Non-blocking input check: only reads a key if one is actually
        // waiting in the input buffer, so the loop never stalls.
        private void HandleInput()
        {
            while (Console.KeyAvailable)
            {
                ConsoleKeyInfo keyInfo = Console.ReadKey(true);

                switch (keyInfo.Key)
                {
                    case ConsoleKey.W:
                    case ConsoleKey.UpArrow:
                        _snake.ChangeDirection(Direction.Up);
                        break;
                    case ConsoleKey.S:
                    case ConsoleKey.DownArrow:
                        _snake.ChangeDirection(Direction.Down);
                        break;
                    case ConsoleKey.A:
                    case ConsoleKey.LeftArrow:
                        _snake.ChangeDirection(Direction.Left);
                        break;
                    case ConsoleKey.D:
                    case ConsoleKey.RightArrow:
                        _snake.ChangeDirection(Direction.Right);
                        break;
                    case ConsoleKey.Q:
                        _isGameOver = true; // Allow quitting mid-round
                        break;
                }
            }
        }

        // Advances the simulation by one tick: move snake, check collisions,
        // handle eating food.
        private void UpdateGameState()
        {
            Point nextHead = _snake.GetNextHeadPosition();

            // Wall collision check.
            if (nextHead.X < 0 || nextHead.X >= BoardWidth || nextHead.Y < 0 || nextHead.Y >= BoardHeight)
            {
                _isGameOver = true;
                return;
            }

            bool willEat = nextHead.Equals(_food.Position);

            // Self-collision check. If about to eat, the tail won't move,
            // so the full body must be considered; otherwise the tail cell
            // is vacated and safe to enter.
            LinkedList<Point> bodyToCheck = new LinkedList<Point>(_snake.Body);
            if (!willEat)
            {
                bodyToCheck.RemoveLast();
            }

            foreach (Point segment in bodyToCheck)
            {
                if (segment.Equals(nextHead))
                {
                    _isGameOver = true;
                    return;
                }
            }

            _snake.Move(nextHead, willEat);

            if (willEat)
            {
                _score++;
                if (_score > _highScore)
                {
                    _highScore = _score;
                }
                _food.Respawn(BoardWidth, BoardHeight, _snake);
            }
        }

        // Blocks (briefly polling) until the player chooses to restart or quit.
        private ConsoleKey WaitForRestartChoice()
        {
            while (true)
            {
                if (Console.KeyAvailable)
                {
                    ConsoleKeyInfo keyInfo = Console.ReadKey(true);
                    if (keyInfo.Key == ConsoleKey.R || keyInfo.Key == ConsoleKey.Q)
                    {
                        return keyInfo.Key;
                    }
                }
                Thread.Sleep(50);
            }
        }

        // Resets snake/food/score for a fresh round, keeping the high score.
        private void ResetGame()
        {
            Point startPosition = new Point(BoardWidth / 2, BoardHeight / 2);
            _snake = new Snake(startPosition);
            _food = new Food(BoardWidth, BoardHeight, _snake);
            _score = 0;
        }
    }

    // Entry point.
    public static class Program
    {
        public static void Main(string[] args)
        {
            GameEngine engine = new GameEngine();
            engine.Run();
        }
    }
}
