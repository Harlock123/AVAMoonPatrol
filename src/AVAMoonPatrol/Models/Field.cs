namespace AVAMoonPatrol.Models;

/// <summary>A 2D point or vector (y grows downwards).</summary>
public readonly record struct Vec(double X, double Y)
{
    public static Vec operator +(Vec a, Vec b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec operator -(Vec a, Vec b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec operator *(Vec a, double k) => new(a.X * k, a.Y * k);
    public double Length => Math.Sqrt(X * X + Y * Y);
    public override string ToString() => $"({X:0.#}, {Y:0.#})";
}

/// <summary>
/// The screen in game pixels: 256 by 192, landscape like the arcade's. The panel takes the top; below
/// it the sky, the mountains, and the lunar surface the buggy drives along.
/// </summary>
public static class Field
{
    public const int Width = 256;
    public const int Height = 192;
    /// <summary>The bottom of the score panel; the sky starts here.</summary>
    public const int PanelHeight = 40;
    /// <summary>The surface the buggy drives on.</summary>
    public const int GroundY = 164;
    public const double Step = 1.0 / 60;
}
