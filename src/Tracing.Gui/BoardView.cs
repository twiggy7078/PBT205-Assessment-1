using System.ComponentModel;
using System.Globalization;

namespace Pbt205.Tracing.Gui;

/// <summary>Where one person was last seen.</summary>
/// <param name="ClientId">Unique per running person app, so two people using the same name are still told apart.</param>
public sealed record PersonMarker(Guid ClientId, string Person, int X, int Y, DateTimeOffset MovedAt);

/// <summary>
/// Draws the environment: a square grid with a dot for each person.
///
/// The grid lines are only drawn when the squares are big enough to see. On a large board
/// (the brief allows up to 1000 x 1000) they would be a solid block of ink, so the people are
/// drawn on a plain background instead.
/// </summary>
public sealed class BoardView : Panel
{
    private const int MinimumCellForGrid = 8;
    private const int MinimumCellForLabels = 28;

    private readonly Dictionary<Guid, PersonMarker> _people = [];
    private int _boardSize = Board.DefaultSize;

    public BoardView()
    {
        DoubleBuffered = true;
        BackColor = Color.White;
        BorderStyle = BorderStyle.FixedSingle;
    }

    /// <summary>The size of the environment being drawn. People outside it are still shown, at the edge.</summary>
    [DefaultValue(Board.DefaultSize)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public int BoardSize
    {
        get => _boardSize;
        set
        {
            _boardSize = Math.Clamp(value, 2, Board.MaxSize);
            Invalidate();
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int PeopleCount => _people.Count;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyCollection<PersonMarker> People => _people.Values;

    /// <summary>Adds or moves a person, then redraws.</summary>
    public void Update(PersonMarker marker)
    {
        _people[marker.ClientId] = marker;
        Invalidate();
    }

    public void Clear()
    {
        _people.Clear();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        // Keep the board square and centred, whatever shape the window is.
        int side = Math.Min(ClientSize.Width, ClientSize.Height) - 2;
        if (side <= 0)
        {
            return;
        }
        int originX = (ClientSize.Width - side) / 2;
        int originY = (ClientSize.Height - side) / 2;
        float cell = side / (float)BoardSize;

        using var background = new SolidBrush(Color.FromArgb(250, 250, 252));
        g.FillRectangle(background, originX, originY, side, side);

        if (cell >= MinimumCellForGrid)
        {
            using var grid = new Pen(Color.FromArgb(225, 228, 235));
            for (int i = 0; i <= BoardSize; i++)
            {
                float offset = i * cell;
                g.DrawLine(grid, originX + offset, originY, originX + offset, originY + side);
                g.DrawLine(grid, originX, originY + offset, originX + side, originY + offset);
            }
        }

        using var border = new Pen(Color.FromArgb(160, 165, 175));
        g.DrawRectangle(border, originX, originY, side, side);

        // Several people can share a square, so spread them out a little inside it.
        var occupants = new Dictionary<(int X, int Y), int>();

        foreach (PersonMarker person in _people.Values.OrderBy(p => p.Person, StringComparer.OrdinalIgnoreCase))
        {
            int x = Math.Clamp(person.X, 0, BoardSize - 1);
            int y = Math.Clamp(person.Y, 0, BoardSize - 1);
            occupants.TryGetValue((x, y), out int alreadyHere);
            occupants[(x, y)] = alreadyHere + 1;

            float dotSize = Math.Max(6f, Math.Min(cell * 0.45f, 22f));
            float centreX = originX + (x + 0.5f) * cell;
            float centreY = originY + (y + 0.5f) * cell;

            if (alreadyHere > 0)
            {
                // Offset each extra person on the square so they do not hide one another.
                double angle = alreadyHere * (Math.PI / 3);
                float spread = Math.Min(cell * 0.22f, dotSize * 0.8f);
                centreX += (float)(Math.Cos(angle) * spread);
                centreY += (float)(Math.Sin(angle) * spread);
            }

            Color colour = ColourFor(person.ClientId);
            using var fill = new SolidBrush(colour);
            using var outline = new Pen(Color.FromArgb(70, 70, 80));
            g.FillEllipse(fill, centreX - dotSize / 2, centreY - dotSize / 2, dotSize, dotSize);
            g.DrawEllipse(outline, centreX - dotSize / 2, centreY - dotSize / 2, dotSize, dotSize);

            if (cell >= MinimumCellForLabels)
            {
                using var text = new SolidBrush(Color.FromArgb(40, 40, 45));
                using var font = new Font(DefaultFont.FontFamily, 7.5f);
                SizeF size = g.MeasureString(person.Person, font);
                g.DrawString(person.Person, font, text, centreX - size.Width / 2, centreY + dotSize / 2 + 1);
            }
        }

        if (_people.Count == 0)
        {
            using var hint = new SolidBrush(Color.FromArgb(130, 130, 140));
            string message = "No positions yet. Start a person application.";
            SizeF size = g.MeasureString(message, Font);
            g.DrawString(message, Font, hint, originX + (side - size.Width) / 2, originY + side / 2f);
        }

        // Board size caption, so a screenshot shows which size was used.
        using var caption = new SolidBrush(Color.FromArgb(110, 110, 120));
        g.DrawString(
            string.Create(CultureInfo.InvariantCulture, $"{BoardSize} x {BoardSize}"),
            Font, caption, originX + 4, originY + 4);
    }

    /// <summary>A stable colour per person app, so someone keeps the same dot as they move.</summary>
    private static Color ColourFor(Guid clientId)
    {
        int hash = clientId.GetHashCode() & 0x7FFFFFFF;
        return Color.FromArgb(
            80 + hash % 150,
            60 + (hash / 150) % 160,
            70 + (hash / 24000) % 150);
    }
}
