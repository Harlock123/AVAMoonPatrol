using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using AVAMoonPatrol.Engine;
using AVAMoonPatrol.Models;
using AVAMoonPatrol.Views;
using Controls = AVAMoonPatrol.Engine.Controls;

namespace AVAMoonPatrol.Tests;

/// <summary>
/// Draws the game in a real (headless) window at the moments worth showing. Set MOONPATROL_SHOTS to a
/// folder to save them as PNGs: the README's screenshots come from here.
/// </summary>
public sealed class ScreenshotTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "moon-ui-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    private (Window W, GameCanvas Canvas, GameEngine G) Open(int seed = 11)
    {
        var g = new GameEngine(seed, HighScoreTable.Load(_dir));
        var canvas = new GameCanvas(g, new SoundManager(silent: true), startTimer: false);
        var w = new Window { Width = 1024, Height = 768, Content = canvas };
        w.Show();
        Pump();
        return (w, canvas, g);
    }

    private static void Pump()
    {
        for (int i = 0; i < 10; i++) Dispatcher.UIThread.RunJobs();
    }

    private static void Shot(Window w, GameCanvas c, string name)
    {
        c.InvalidateVisual();
        Pump();
        var frame = w.CaptureRenderedFrame();
        Assert.NotNull(frame);
        var dir = Environment.GetEnvironmentVariable("MOONPATROL_SHOTS");
        if (string.IsNullOrEmpty(dir)) return;
        Directory.CreateDirectory(dir);
        frame.Save(Path.Combine(dir, $"AVAMoonPatrol-{name}.png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
    }

    private static void Play(GameEngine g, AttractPilot pilot, int steps)
    {
        for (int i = 0; i < steps && g.State == GameState.Playing; i++) g.Step(pilot.Decide(g));
    }

    [AvaloniaFact]
    public void The_title_and_the_start_of_the_course()
    {
        var (w, c, g) = Open();
        g.Idle(60);
        Shot(w, c, "title");
        g.StartGame();
        g.Arrivals(2);
        var pilot = new AttractPilot(2);
        Play(g, pilot, 60 * 4);
        for (int i = 0; i < 60 * 20 && (g.Ufos.Count < 2 || g.Ufos.Any(u => u.X < 30) || g.Phase != Phase.Playing || !g.Hazards.Any(h => !h.Dead && h.X - g.CameraX is > 130 and < 230)); i++) g.Step(pilot.Decide(g));
        Assert.NotEmpty(g.Ufos);
        Shot(w, c, "drive");
        w.Close();
    }

    [AvaloniaFact]
    public void Over_a_crater_under_fire()
    {
        var (w, c, g) = Open(seed: 5);
        g.StartGame();
        g.JumpToPoint(2);
        g.ClearRoad();
        g.Idle(60 * 2);
        g.Put(HazardKind.BigCrater, -8);
        g.Hazards.Add(new Hazard { Kind = HazardKind.BigRock, X = g.BuggyX + 110, Hits = 2 });
        g.Hazards.Add(new Hazard { Kind = HazardKind.SmallCrater, X = g.BuggyX + 190 });
        g.PutUfo(UfoKind.Saucer, 70, 70);
        g.PutUfo(UfoKind.Saucer, 150, 62);
        g.PutUfo(UfoKind.Diver, 200, 92);
        g.PutBomb(196, 120, blasts: true);
        g.Step(new Controls(0, true, true));
        for (int i = 0; i < 14; i++) g.Step(new Controls(0, false, i % 9 == 0));
        Assert.True(g.Airborne);
        Shot(w, c, "jump");
        w.Close();
    }

    [AvaloniaFact]
    public void Tanks_and_darts_further_on()
    {
        var (w, c, g) = Open(seed: 6);
        g.StartGame();
        g.JumpToPoint(16);
        g.ClearRoad();
        g.Idle(60 * 2);
        g.Put(HazardKind.Tank, 160);
        g.Hazards.Add(new Hazard { Kind = HazardKind.Mine, X = g.BuggyX + 196 });
        g.PutUfo(UfoKind.Dart, 90, 60);
        g.PutUfo(UfoKind.Dart, 170, 66);
        g.PutUfo(UfoKind.Diver, 210, 96);
        for (int i = 0; i < 60 * 3 && g.Shells.Count == 0; i++) g.Step(default);
        g.Idle(12);
        Assert.NotEmpty(g.Shells);
        Shot(w, c, "tanks");
        w.Close();
    }

    [AvaloniaFact]
    public void The_bonus_at_point_E()
    {
        var (w, c, g) = Open(seed: 8);
        g.StartGame();
        g.JumpToPoint(3);
        g.ClearRoad();
        for (int i = 0; i < 60 * 20 && g.Letter != 'E'; i++) g.Step(new Controls(1, false, i % 20 == 0));
        g.Idle(30);
        Assert.Equal("POINT E", g.Banner[0]);
        Shot(w, c, "bonus");
        w.Close();
    }

    [AvaloniaFact]
    public void Game_over_initials_and_the_high_scores()
    {
        var (w, c, g) = Open(seed: 4);
        g.StartGame();
        var pilot = new AttractPilot(2);
        for (int i = 0; i < 60 * 60 * 5 && g.Score < 9000; i++) g.Step(pilot.Decide(g));
        for (int i = 0; i < 60 * 60 * 10 && g.State == GameState.Playing; i++) g.Step(default);
        Assert.Equal(GameState.GameOver, g.State);
        g.Idle(20);
        Shot(w, c, "game-over");
        g.Idle(60 * 4);
        Assert.Equal(GameState.EnterInitials, g.State);
        g.Initials!.Type('A');
        g.Initials.Type('V');
        Shot(w, c, "initials");
        g.Initials.Type('A');
        g.Idle(5);
        Assert.Equal(GameState.HighScores, g.State);
        g.Idle(20);
        Shot(w, c, "high-scores");
        w.Close();
    }
}
