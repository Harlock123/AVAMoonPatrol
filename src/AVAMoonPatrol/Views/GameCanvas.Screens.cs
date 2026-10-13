using Avalonia;
using Avalonia.Media;
using AVAMoonPatrol.Engine;
using AVAMoonPatrol.Models;

namespace AVAMoonPatrol.Views;

public sealed partial class GameCanvas
{
    private const double BarLeft = 22, BarRight = 234;

    /// <summary>The panel along the top: scores and time, the course from A to Z, buggies left and the course.</summary>
    private void DrawPanel(DrawingContext dc)
    {
        var g = Engine;
        Fill(dc, 0xFF0E0E26, 0, 0, Field.Width, Field.PanelHeight);
        Fill(dc, 0xFF3A3E8C, 0, Field.PanelHeight - 1, Field.Width, 1);
        bool playing = g.State is GameState.Playing or GameState.Paused or GameState.GameOver or GameState.EnterInitials or GameState.Attract;

        Text(dc, "SCORE", P(8, 7), 28, Red);
        Text(dc, (playing ? g.Score : 0).ToString("D6"), P(30, 7), 30, White);
        Text(dc, "HIGH", P(98, 7), 28, Red);
        Text(dc, Math.Max(g.HighScores.TopScore, g.Score).ToString("D6"), P(116, 7), 30, White);
        Text(dc, "TIME", P(184, 7), 28, Red);
        Text(dc, (playing ? (int)g.CourseTime : 0).ToString("D3"), P(204, 7), 30, White);

        // The course, A to Z, with the buggy's place on it.
        Fill(dc, 0xFF3A3E8C, BarLeft, 20, BarRight - BarLeft, 1);
        double step = (BarRight - BarLeft) / (Course.Letters - 1);
        for (int i = 0; i < Course.Letters; i++)
        {
            bool passed = playing && i <= g.LetterIndex;
            var brush = Course.IsMajor(i) ? (passed ? Yellow : Brush(0xFF8A7A30)) : passed ? White : Dim;
            Text(dc, Course.Letter(i).ToString(), P(BarLeft + i * step, 16), Course.IsMajor(i) ? 26 : 20, brush, center: true);
        }
        if (playing)
        {
            double mx = BarLeft + g.Progress * (BarRight - BarLeft);
            Fill(dc, 0xFFC77CF2, mx - 2, 21, 5, 2);
            Fill(dc, 0xFFC77CF2, mx - 1, 23, 3, 1);
        }

        if (playing)
        {
            for (int i = 0; i < Math.Min(g.Lives - (g.Phase == Phase.Playing ? 1 : 0), 6); i++)
                DrawSprite(dc, Sprites.Body, 8 + i * 14, 28, Sprites.Buggy, px: 0.5);
            Text(dc, $"POINT {g.Letter}", P(128, 32), 30, Yellow, center: true);
        }
        Text(dc, g.CourseNumber == 0 ? "BEGINNER COURSE" : $"CHAMPION COURSE {g.CourseNumber}", P(248, 32), 24, Lilac, right: true);
    }

    private static void Panel(DrawingContext dc, double left, double top, double right, double bottom) =>
        dc.FillRectangle(Brush(0xD8000010), new Rect(P(left, top), P(right, bottom)));

    private void DrawTitle(DrawingContext dc)
    {
        Panel(dc, 20, 44, 236, 150);
        Text(dc, "AVA MOON PATROL", P(128, 54), 76, Lilac, center: true);
        Text(dc, "AFTER THE 1982 ARCADE CLASSIC", P(128, 65), 26, Dim, center: true);
        Row(Sprites.SaucerArt[0], Sprites.Saucer, 76, "SAUCER", "100");
        Row(Sprites.DiverArt[0], Sprites.Diver, 85, "DIVER: ITS BOMBS BLOW CRATERS", "200");
        Row(Sprites.DartArt[0], Sprites.Dart, 94, "DART", "300");
        Row(Sprites.SmallRock, Sprites.Buggy, 103, "ROCK  (BIG ROCK, TWO SHOTS: 200)", "100");
        Row(Sprites.Tank, Sprites.Buggy, 112, "TANK, AND 50 FOR ITS SHELLS", "500");
        Row(Sprites.Mine[0], Sprites.Buggy, 121, "JUMP A CRATER, ROCK OR MINE", "50-100");
        Text(dc, "BONUS AT E, J, O, T AND Z: BEAT THE AVERAGE TIME", P(128, 130), 26, Yellow, center: true);
        if (_clock % 1 < 0.65)
            Text(dc, "PRESS FIRE OR ENTER TO START", P(128, 139), 32, White, center: true);
        Text(dc, "© 2026 LONNIE WATSON", P(128, 146.5), 22, Dim, center: true);

        void Row(Sprites.Sprite s, Sprites.Palette pal, double y, string name, string points)
        {
            DrawSprite(dc, s, 50 - s.Width / 2.0, y - s.Height / 2.0, pal);
            Text(dc, name, P(66, y), 28, White);
            Text(dc, points, P(222, y), 28, White, right: true);
        }
    }

    /// <summary>A checkpoint for the table: the letter, with a star for each champion course.</summary>
    public static string StageName(int stage)
    {
        int s = Math.Max(1, stage) - 1;
        return Course.Letter(s % Course.Letters) + new string('*', Math.Min(3, s / Course.Letters));
    }

    private void DrawHighScores(DrawingContext dc)
    {
        Panel(dc, 44, 44, 212, 152);
        Text(dc, "TOP PATROLS", P(128, 53), 60, Yellow, center: true);
        Text(dc, "RANK    SCORE   NAME  POINT", P(128, 63), 26, Dim, center: true);
        var table = Engine.HighScores.Entries;
        for (int i = 0; i < table.Count; i++)
        {
            var e = table[i];
            bool fresh = i == Engine.NewEntryIndex;
            if (fresh && _clock % 0.5 > 0.35) continue;
            var brush = fresh ? Yellow : i == 0 ? Lilac : White;
            Text(dc, $"{i + 1,2}.  {e.Score,8}   {e.Initials}  {StageName(e.Stage),-4}", P(128, 71 + i * 7.2), 30, brush, center: true);
        }
        Text(dc, "* A CHAMPION COURSE", P(128, 143), 22, Dim, center: true);
        if (_clock % 1 < 0.65) Text(dc, "PRESS FIRE TO PLAY", P(128, 149), 28, White, center: true);
    }

    private void DrawInitials(DrawingContext dc)
    {
        var entry = Engine.Initials;
        if (entry is null) return;
        Panel(dc, 50, 60, 206, 128);
        Text(dc, "A NEW HIGH SCORE!", P(128, 68), 48, Yellow, center: true);
        Text(dc, $"{Engine.Score}", P(128, 80), 44, White, center: true);
        for (int i = 0; i < HighScoreTable.InitialsLength; i++)
        {
            var at = P(116 + i * 12, 98);
            bool cursor = i == entry.Cursor;
            Text(dc, entry.Initials[i].ToString(), at, 76, cursor ? Yellow : White, center: true);
            if (cursor && _clock % 0.6 < 0.4)
                dc.FillRectangle(Yellow, new Rect(at.X - 16, at.Y + 34, 32, 5));
        }
        Text(dc, "TYPE, OR UP/DOWN AND SPACE.  ENTER WHEN DONE", P(128, 120), 24, Dim, center: true);
    }
}
