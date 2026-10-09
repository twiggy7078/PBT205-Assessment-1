namespace Pbt205.Tracing;

/// <summary>
/// The environment: a square grid, 10 x 10 by default and configurable up to 1000 x 1000.
/// Each person's position and the contact history are kept by <see cref="ContactTracker"/>.
/// </summary>
public sealed class Board(int size = Board.DefaultSize)
{
    public const int DefaultSize = 10;
    public const int MaxSize = 1000;

    public int Size { get; } = size is >= 2 and <= MaxSize
        ? size
        : throw new ArgumentOutOfRangeException(nameof(size), $"Board size must be between 2 and {MaxSize}.");

    /// <summary>Keeps a move inside the board, so a person can't walk off the edge.</summary>
    public (int X, int Y) Clamp(int x, int y) =>
        (Math.Clamp(x, 0, Size - 1), Math.Clamp(y, 0, Size - 1));
}
