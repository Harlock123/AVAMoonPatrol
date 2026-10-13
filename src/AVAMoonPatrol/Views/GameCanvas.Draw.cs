using System.Globalization;
using Avalonia;
using Avalonia.Media;
using AVAMoonPatrol.Engine;
using AVAMoonPatrol.Models;

namespace AVAMoonPatrol.Views;

public sealed partial class GameCanvas
{
    private static readonly Dictionary<uint, IBrush> BrushCache = [];
    private static readonly Typeface Retro = new(new FontFamily("avares://AVAMoonPatrol/Assets/Fonts#VT323"));

    public static IBrush Brush(uint argb)
    {
        if (!BrushCache.TryGetValue(argb, out var b))
        {
            b = new SolidColorBrush(Color.FromUInt32(argb)).ToImmutable();
            BrushCache[argb] = b;
        }
        return b;
    }

    private static readonly IBrush White = Brush(0xFFF2F2F2);
    private static readonly IBrush Yellow = Brush(0xFFFFE14A);
    private static readonly IBrush Red = Brush(0xFFFF4D4D);
    private static readonly IBrush Lilac = Brush(0xFFC77CF2);
    private static readonly IBrush Dim = Brush(0xFF8C8CA8);

    private static readonly IBrush Sky = new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
        GradientStops = { new GradientStop(Color.FromUInt32(0xFF04041A), 0), new GradientStop(Color.FromUInt32(0xFF1E1650), 1) },
    }.ToImmutable();

    private const uint MountainColour = 0xFF3A3E8C, MountainRidge = 0xFF5D63BA;
    private const uint HillColour = 0xFF246858, HillRidge = 0xFF43A88E;
    private const uint GroundColour = 0xFFB08656, GroundTop = 0xFFE2BC80, GroundDark = 0xFF7E5A36, Pebble = 0xFF8A6440;
    private const uint Hole = 0xFF24160C;

    /// <summary>Game pixels to the 1024×768 screen.</summary>
    private static Point P(double x, double y) => new(x * Scale, y * Scale);

    private static void Fill(DrawingContext dc, uint argb, double x, double y, double w, double h) =>
        dc.FillRectangle(Brush(argb), new Rect(x * Scale, y * Scale, w * Scale, h * Scale));

    private int Frame => (int)(_clock * 6) % 2;

    private void DrawScreen(DrawingContext dc)
    {
        var g = Engine;
        double cam = g.CameraX;
        DrawSky(dc, cam);
        DrawRoad(dc, cam);
        switch (g.State)
        {
            case GameState.Title:
                DrawBuggy(dc);
                DrawTitle(dc);
                break;
            case GameState.HighScores:
                DrawBuggy(dc);
                DrawHighScores(dc);
                break;
            default:
                DrawPlay(dc, cam);
                break;
        }
        DrawPanel(dc);
    }

    // ----- the scenery --------------------------------------------------------------------------

    private static uint Hash(long x)
    {
        unchecked
        {
            uint h = (uint)x * 2654435761u;
            h ^= h >> 15;
            h *= 2246822519u;
            return h ^ (h >> 13);
        }
    }

    private void DrawSky(DrawingContext dc, double cam)
    {
        dc.FillRectangle(Sky, new Rect(P(0, Field.PanelHeight), P(Field.Width, Field.GroundY)));

        // Stars: barely moving, a few twinkling.
        double starShift = cam * 0.03;
        for (int i = 0; i < 70; i++)
        {
            uint h = Hash(i + 99);
            double x = ((h % 512) - starShift % 512 + 512) % 512;
            if (x >= Field.Width) continue;
            double y = Field.PanelHeight + 2 + (h >> 9) % 90;
            bool bright = (h >> 20) % 5 == 0 && (_clock * 1.5 + i) % 3 > 0.4;
            Fill(dc, bright ? 0xFFF4F0E6 : 0xFF7C7CB8, x, y, 1, 1);
        }

        // The far mountains, then the near hills with their domes, each scrolling slower than the road.
        double far = cam * 0.15, near = cam * 0.4;
        for (int x = 0; x < Field.Width; x++)
        {
            double a = x + far;
            double hm = 34 + 14 * Math.Sin(a * 2 * Math.PI / 181) + 9 * Math.Sin(a * 2 * Math.PI / 67 + 1) + 4 * Math.Sin(a * 2 * Math.PI / 23 + 2);
            double top = Math.Round(150 - hm);
            Fill(dc, MountainRidge, x, top, 1, 1);
            Fill(dc, MountainColour, x, top + 1, 1, Field.GroundY - top - 1);
        }
        for (int x = 0; x < Field.Width; x++)
        {
            double b = x + near;
            double hh = 13 + 6 * Math.Sin(b * 2 * Math.PI / 97) + 3 * Math.Sin(b * 2 * Math.PI / 41 + 0.5);
            double top = Math.Round(Field.GroundY - hh);
            Fill(dc, HillRidge, x, top, 1, 1);
            Fill(dc, HillColour, x, top + 1, 1, Field.GroundY - top - 1);
        }
        // Domes of the moon base, on the hills every so often.
        for (long k = (long)Math.Floor(near / 150) - 1; k <= (long)((near + Field.Width) / 150) + 1; k++)
        {
            if (Hash(k) % 3 == 0) continue;
            double x = k * 150 + Hash(k) % 60 - near;
            double hh = 13 + 6 * Math.Sin((x + near) * 2 * Math.PI / 97) + 3 * Math.Sin((x + near) * 2 * Math.PI / 41 + 0.5);
            double baseY = Field.GroundY - hh + 1;
            dc.DrawEllipse(Brush(0xFF9FC8D8), null, P(x, baseY), 7 * Scale, 5 * Scale);
            dc.FillRectangle(Brush(HillColour), new Rect(P(x - 8, baseY), P(x + 8, baseY + 6)));
            Fill(dc, 0xFFFFE14A, x - 3, baseY - 2, 1, 1);
            Fill(dc, 0xFFFFE14A, x + 2, baseY - 3, 1, 1);
        }
    }

    private void DrawRoad(DrawingContext dc, double cam)
    {
        Fill(dc, GroundColour, 0, Field.GroundY, Field.Width, Field.Height - Field.GroundY);
        Fill(dc, GroundTop, 0, Field.GroundY, Field.Width, 1);
        Fill(dc, GroundDark, 0, Field.Height - 6, Field.Width, 6);
        long first = (long)Math.Floor(cam);
        for (long wx = first; wx < first + Field.Width + 1; wx++)
        {
            uint h = Hash(wx);
            if (h % 9 != 0) continue;
            Fill(dc, (h >> 8) % 3 == 0 ? GroundTop : Pebble, wx - cam, Field.GroundY + 3 + (h >> 4) % 19, (h >> 12) % 2 + 1, 1);
        }
        foreach (var h in Engine.Hazards)
            if (h.IsCrater && !h.Dead) DrawCrater(dc, h.X - cam, h.Width, h.Kind == HazardKind.BigCrater ? 9 : 6);
    }

    private static void DrawCrater(DrawingContext dc, double x0, double width, double depth)
    {
        for (int c = 0; c < width; c++)
        {
            double u = (c + 0.5) / width;
            double d = Math.Max(2, Math.Round(depth * Math.Sqrt(1 - (2 * u - 1) * (2 * u - 1))));
            Fill(dc, Hole, x0 + c, Field.GroundY, 1, d);
        }
        Fill(dc, GroundTop, x0 - 3, Field.GroundY - 1, 3, 1);
        Fill(dc, GroundTop, x0 + width, Field.GroundY - 1, 3, 1);
    }

    // ----- the patrol ---------------------------------------------------------------------------

    private void DrawPlay(DrawingContext dc, double cam)
    {
        var g = Engine;
        foreach (var h in g.Hazards)
        {
            if (h.Dead || h.IsCrater) continue;
            double x = h.X - cam;
            var art = h.Kind switch
            {
                HazardKind.SmallRock => Sprites.SmallRock,
                HazardKind.BigRock => h.Hits < 2 ? Sprites.CrackedRock : Sprites.BigRock,
                HazardKind.Mine => Sprites.Mine[Frame],
                _ => Sprites.Tank,
            };
            DrawSprite(dc, art, x, Field.GroundY - art.Height, Sprites.Buggy);
        }
        foreach (var s in g.Shells) DrawSprite(dc, Sprites.Shell, s.X - cam, Field.GroundY - 6, Sprites.Buggy);
        foreach (var u in g.Ufos)
        {
            var art = Sprites.Art(u.Kind)[Frame];
            DrawSprite(dc, art, u.X - art.Width / 2.0, u.Y - art.Height / 2.0, Sprites.Colours(u.Kind));
        }
        foreach (var b in g.Bombs) DrawSprite(dc, Sprites.Bomb, b.X - 1.5, b.Y - 2, Sprites.Buggy);
        foreach (var s in g.UpShots) DrawSprite(dc, Sprites.UpShot, s.X - 0.5, s.Y, Sprites.Buggy);
        if (g.ForwardShot is { } f) DrawSprite(dc, Sprites.ForwardShot, f.X - cam, f.Y, Sprites.Buggy);
        if (g.Phase == Phase.Playing) DrawBuggy(dc);

        foreach (var fx in g.Effects)
        {
            double x = fx.Pos.X - cam, y = fx.Pos.Y;
            switch (fx.Kind)
            {
                case EffectKind.Burst or EffectKind.BigBurst:
                    var frames = Sprites.Burst;
                    int i = Math.Min(frames.Length - 1, (int)(fx.Age / fx.Life * frames.Length));
                    double px = fx.Kind == EffectKind.BigBurst ? 2.5 : 1;
                    DrawSprite(dc, frames[i], x - 4 * px, y - 4 * px, Sprites.Buggy, px: px);
                    break;
                case EffectKind.Score:
                    Text(dc, fx.Text, P(x, y - fx.Age * 10), 30, White, center: true);
                    break;
                case EffectKind.Debris:
                    if (fx.Text == "wheel") DrawSprite(dc, Sprites.Wheel[(int)(fx.Age * 10) % 2], x - 2.5, y - 2.5, Sprites.Buggy);
                    else Fill(dc, Sprites.Buggy.A, x - 2, y - 1, 4, 2);
                    break;
            }
        }

        if (g.Banner.Length > 0)
        {
            double top = 58, h = g.Banner.Length * 10 + 6;
            dc.FillRectangle(Brush(0xC0000000), new Rect(P(56, top - 6), P(200, top - 6 + h)));
            for (int i = 0; i < g.Banner.Length; i++)
                Text(dc, g.Banner[i], P(128, top + i * 10), i == 0 ? 40 : 32, i == 0 ? Yellow : White, center: true);
        }

        switch (g.State)
        {
            case GameState.Paused:
                Text(dc, "PAUSED", P(128, 100), 64, White, center: true);
                Text(dc, "ESC to quit, any other key to resume", P(128, 114), 28, Dim, center: true);
                break;
            case GameState.Attract:
                Text(dc, "DEMONSTRATION", P(128, 104), 40, Red, center: true);
                if (_clock % 1 < 0.6) Text(dc, "PRESS FIRE TO PLAY", P(128, 116), 32, White, center: true);
                break;
            case GameState.GameOver:
                Text(dc, "GAME OVER", P(128, 100), 72, Red, center: true);
                break;
            case GameState.EnterInitials:
                DrawInitials(dc);
                break;
        }
    }

    /// <summary>The buggy: its body riding on three wheels that bob over the ground and hang down in a jump.</summary>
    private void DrawBuggy(DrawingContext dc)
    {
        var g = Engine;
        double x = g.ScreenX, y = g.BuggyY;
        double lift = g.Airborne ? -1 : 0;
        DrawSprite(dc, Sprites.Body, x - 12, y - 14 + lift, Sprites.Buggy);
        int frame = (int)(g.Odometer / 3) % 2;
        double[] offsets = [-8, 0, 8];
        for (int i = 0; i < offsets.Length; i++)
        {
            double bob = !g.Airborne && Math.Sin(g.Odometer * 0.35 + i * 2.1) > 0.7 ? -1 : 0;
            DrawSprite(dc, Sprites.Wheel[frame], x + offsets[i] - 2.5, y - 5 + bob, Sprites.Buggy);
        }
    }

    /// <summary>Draws a sprite with its top-left at a game point; A and B take the palette's colours.</summary>
    private static void DrawSprite(DrawingContext dc, Sprites.Sprite s, double x, double y, Sprites.Palette pal, bool flip = false, double px = 1)
    {
        for (int r = 0; r < s.Height; r++)
        {
            var row = s.Rows[r];
            for (int c = 0; c < s.Width; c++)
            {
                char ch = row[flip ? s.Width - 1 - c : c];
                if (ch == '.') continue;
                uint argb = ch switch
                {
                    'A' => pal.A,
                    'B' => pal.B,
                    _ => Sprites.Fixed.GetValueOrDefault(ch, 0xFFFF00FF),
                };
                var a = P(x + c * px, y + r * px);
                dc.FillRectangle(Brush(argb), new Rect(a.X, a.Y, Scale * px + 0.3, Scale * px + 0.3));
            }
        }
    }

    private static void Text(DrawingContext dc, string text, Point at, double size, IBrush brush, bool center = false, bool right = false)
    {
        var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Retro, size, brush);
        double x = center ? at.X - ft.Width / 2 : right ? at.X - ft.Width : at.X;
        dc.DrawText(ft, new Point(x, at.Y - ft.Height / 2));
    }
}
