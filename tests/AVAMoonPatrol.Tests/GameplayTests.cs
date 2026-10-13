using AVAMoonPatrol.Engine;
using AVAMoonPatrol.Models;
using AVAMoonPatrol.Views;

namespace AVAMoonPatrol.Tests;

public sealed class GameplayTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "moon-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    private GameEngine NewGame(int seed = 1)
    {
        var g = new GameEngine(seed, new HighScoreTable(_dir));
        g.StartGame();
        return g;
    }

    /// <summary>A game on an empty road under an empty sky.</summary>
    private GameEngine EmptyRoad()
    {
        var g = NewGame();
        g.ClearRoad();
        return g;
    }

    private static void Steps(GameEngine g, int n, Controls c = default)
    {
        for (int i = 0; i < n; i++) g.Step(c);
    }

    private static readonly Controls Fire = new(0, false, true);
    private static readonly Controls Jump = new(0, true, false);

    [Fact]
    public void A_game_starts_at_point_A_on_the_beginner_course()
    {
        var g = NewGame();
        Assert.Equal(3, g.Lives);
        Assert.Equal('A', g.Letter);
        Assert.Equal(0, g.CourseNumber);
        Assert.False(g.Airborne);
        Assert.Equal(Field.GroundY, g.BuggyY);
        Assert.NotEmpty(g.Hazards);
        Assert.All(g.Hazards, h => Assert.True(h.X >= Course.SafeStart));
    }

    [Fact]
    public void The_road_is_the_same_every_time_and_gets_harder_as_it_goes()
    {
        Assert.Equal(Course.Generate(7, 0, 5), Course.Generate(7, 0, 5));
        Assert.NotEqual(Course.Generate(7, 0, 5), Course.Generate(8, 0, 5));
        for (int course = 0; course < 3; course++)
            for (int section = 0; section < Course.Letters - 1; section++)
            {
                var road = Course.Generate(3, course, section);
                int d = Course.Difficulty(course, section);
                Assert.NotEmpty(road);
                Assert.All(road, h => Assert.InRange(h.X, section * Course.SectionLength + Course.SafeStart, (section + 1) * Course.SectionLength));
                for (int i = 1; i < road.Count; i++)
                    Assert.True(road[i].X - (road[i - 1].X + Course.Width(road[i - 1].Kind)) >= 72, $"gap at {course}/{section}");
                if (d < 2) Assert.All(road, h => Assert.True(h.Kind is HazardKind.SmallCrater or HazardKind.SmallRock));
                if (d < 10) Assert.DoesNotContain(road, h => h.Kind == HazardKind.Mine);
                if (d < 15) Assert.DoesNotContain(road, h => h.Kind == HazardKind.Tank);
            }
        var later = Enumerable.Range(15, 10).SelectMany(s => Course.Generate(3, 0, s)).Select(h => h.Kind).ToHashSet();
        Assert.Contains(HazardKind.Mine, later);
        Assert.Contains(HazardKind.Tank, later);
    }

    [Fact]
    public void Right_speeds_up_left_slows_down_and_letting_go_cruises()
    {
        var g = EmptyRoad();
        double cruiseX = g.ScreenX;
        Steps(g, 120, new Controls(1, false, false));
        Assert.Equal(GameEngine.MaxSpeed, g.Speed);
        Assert.True(g.ScreenX > cruiseX + 20, "a fast buggy rides further right");
        Steps(g, 180, new Controls(-1, false, false));
        Assert.Equal(GameEngine.MinSpeed, g.Speed);
        Assert.True(g.ScreenX < cruiseX);
        Steps(g, 120);
        Assert.Equal(GameEngine.CruiseSpeed, g.Speed, 3);
    }

    [Fact]
    public void A_jump_arcs_up_and_comes_down_and_there_is_no_jumping_in_mid_air()
    {
        var g = EmptyRoad();
        var heard = new List<Sfx>();
        g.Sound += heard.Add;
        g.Step(Jump);
        Assert.True(g.Airborne);
        double top = g.BuggyY;
        int steps = 1;
        while (g.Airborne && steps < 200)
        {
            g.Step(Jump);
            top = Math.Min(top, g.BuggyY);
            steps++;
            if (steps == 20) Assert.True(g.Vy > -GameEngine.JumpSpeed + 50, "holding jump doesn't jump again in the air");
        }
        Assert.InRange(Field.GroundY - top, 30, 38);
        Assert.InRange(steps / 60.0, 0.85, 0.98);
        Assert.Equal(Field.GroundY, g.BuggyY);
        Assert.Equal(1, heard.Count(s => s == Sfx.Jump));
        Assert.Contains(Sfx.Land, heard);
    }

    [Fact]
    public void Driving_into_a_crater_loses_the_buggy()
    {
        var g = EmptyRoad();
        g.Put(HazardKind.BigCrater, 10);
        Steps(g, 60);
        Assert.Equal(Phase.Dying, g.Phase);
        Assert.Contains("crater", g.LossLog.Single());
    }

    [Theory]
    [InlineData(HazardKind.SmallCrater, 50)]
    [InlineData(HazardKind.BigCrater, 80)]
    [InlineData(HazardKind.SmallRock, 80)]
    [InlineData(HazardKind.BigRock, 100)]
    [InlineData(HazardKind.Mine, 100)]
    public void Jumping_over_something_scores(HazardKind kind, int points)
    {
        var g = EmptyRoad();
        // Craters are jumped from their edge, things standing up from a little way off.
        g.Put(kind, Course.IsCrater(kind) ? -8 : 6);
        g.Step(Jump);
        Steps(g, 90);
        Assert.Equal(Phase.Playing, g.Phase);
        Assert.Equal(points, g.Score);
    }

    [Fact]
    public void A_rock_in_the_way_wrecks_the_buggy()
    {
        var g = EmptyRoad();
        g.Put(HazardKind.SmallRock, 20);
        Steps(g, 60);
        Assert.Equal(Phase.Dying, g.Phase);
        Assert.Contains("SmallRock", g.LossLog.Single());
    }

    [Fact]
    public void The_forward_cannon_breaks_rocks_and_a_big_one_takes_two_shots()
    {
        var g = EmptyRoad();
        var rock = g.Put(HazardKind.SmallRock, 70);
        Steps(g, 40, Fire);
        Assert.True(rock.Dead);
        Assert.Equal(100, g.Score);

        g = EmptyRoad();
        var big = g.Put(HazardKind.BigRock, 90);
        Steps(g, 20); // the cannon is ready
        g.Step(Fire);
        Steps(g, 25);
        Assert.False(big.Dead);
        Assert.Equal(1, big.Hits);
        Steps(g, 30, Fire);
        Assert.True(big.Dead);
        Assert.Equal(200, g.Score);
    }

    [Fact]
    public void Mines_cannot_be_shot()
    {
        var g = EmptyRoad();
        var mine = g.Put(HazardKind.Mine, 70);
        Steps(g, 40, Fire);
        Assert.False(mine.Dead);
        Steps(g, 60, Fire);
        Assert.Equal(Phase.Dying, g.Phase);
    }

    [Fact]
    public void The_upward_cannon_brings_down_what_flies()
    {
        var g = EmptyRoad();
        var u = g.PutUfo(UfoKind.Diver, g.ScreenX - 3, 100);
        Steps(g, 40, Fire);
        Assert.True(u.Dead);
        Assert.Empty(g.Ufos);
        Assert.Equal(200, g.Score);
    }

    [Fact]
    public void A_bomb_on_the_buggy_wrecks_it_and_a_diver_bomb_blows_a_crater_ahead()
    {
        var g = EmptyRoad();
        g.PutBomb(g.ScreenX, 110);
        Steps(g, 90);
        Assert.Equal(Phase.Dying, g.Phase);
        Assert.Contains("bombed", g.LossLog.Single());

        g = EmptyRoad();
        g.PutBomb(g.ScreenX + 90, 140, blasts: true);
        Steps(g, 30);
        var crater = Assert.Single(g.Hazards);
        Assert.True(crater.Blown);
        Assert.Equal(HazardKind.SmallCrater, crater.Kind);
        Assert.True(crater.X > g.BuggyX);
        // And it is a real hole: drive on without jumping and the buggy goes in.
        Steps(g, 120);
        Assert.Equal(Phase.Dying, g.Phase);
    }

    [Fact]
    public void Tanks_fire_shells_along_the_ground_and_the_cannon_stops_both()
    {
        var g = EmptyRoad();
        var heard = new List<Sfx>();
        g.Sound += heard.Add;
        g.Put(HazardKind.Tank, 160);
        Steps(g, 60 * 3);
        Assert.Contains(Sfx.TankFire, heard);
        Assert.Equal(Phase.Dying, g.Phase);
        Assert.Contains("shell", g.LossLog.Single());

        g = EmptyRoad();
        var tank = g.Put(HazardKind.Tank, 160);
        Steps(g, 60 * 3, Fire);
        Assert.True(tank.Dead);
        Assert.Equal(Phase.Playing, g.Phase);
        Assert.True(g.Score >= 500);
    }

    [Fact]
    public void Reaching_E_pays_a_bonus_for_beating_the_average_time()
    {
        var g = NewGame();
        g.JumpToPoint(3);
        g.ClearRoad();
        for (int i = 0; i < 60 * 20 && g.Letter != 'E'; i++) g.Step(new Controls(1, false, false));
        Assert.Equal('E', g.Letter);
        Assert.Equal("POINT E", g.Banner[0]);
        int time = (int)Math.Round((Course.SectionLength - 30) / 100.0);
        Assert.InRange(g.Score, 1000 + (11 - time - 2) * 100, 1000 + (11 - time + 1) * 100);
        Assert.Equal(11, GameEngine.AverageTime(3, 4));
        Assert.Equal(53, GameEngine.AverageTime(0, 5));
    }

    [Fact]
    public void A_lost_buggy_is_replaced_at_the_last_checkpoint_with_the_road_as_it_was()
    {
        var g = NewGame(seed: 9);
        g.JumpToPoint(6);
        g.Arrivals(1e9);
        var road = g.Hazards.Where(h => h.X >= 6 * Course.SectionLength).Select(h => (h.Kind, h.X)).ToList();
        for (int i = 0; i < 60 * 30 && g.Phase == Phase.Playing; i++) g.Step(Fire);
        Assert.Equal(Phase.Dying, g.Phase);
        for (int i = 0; i < 60 * 5 && g.Phase == Phase.Dying; i++) g.Step(default);
        Assert.Equal(Phase.Playing, g.Phase);
        Assert.Equal(2, g.Lives);
        Assert.Equal('G', g.Letter);
        Assert.Equal(6 * Course.SectionLength + 30, g.BuggyX);
        Assert.Equal(road, g.Hazards.Where(h => h.X >= 6 * Course.SectionLength).Select(h => (h.Kind, h.X)).ToList());
        Assert.All(g.Hazards, h => Assert.False(h.Dead));
    }

    [Fact]
    public void Z_completes_the_course_and_the_champion_course_follows()
    {
        var g = NewGame();
        g.JumpToPoint(24);
        g.ClearRoad();
        for (int i = 0; i < 60 * 20 && g.CourseNumber == 0; i++) g.Step(new Controls(1, false, false));
        Assert.Equal(1, g.CourseNumber);
        Assert.Equal('A', g.Letter);
        Assert.Contains("COURSE COMPLETE", g.Banner[0]);
        Assert.True(g.BuggyX < 50);
        Assert.True(g.Score >= 1000);
        Steps(g, 10);
        Assert.NotEmpty(g.Hazards);
        Assert.Equal(10, g.Difficulty);
        Assert.Equal(27, g.Stage);
        Assert.Equal("A*", GameCanvas.StageName(g.Stage));
        Assert.Equal("E", GameCanvas.StageName(5));
    }

    [Fact]
    public void A_bonus_buggy_at_ten_thousand_and_every_twenty_thousand_after()
    {
        var g = EmptyRoad();
        g.AddScore(9990);
        Assert.Equal(3, g.Lives);
        g.AddScore(20);
        Assert.Equal(4, g.Lives);
        g.AddScore(19000);
        Assert.Equal(4, g.Lives);
        g.AddScore(1000);
        Assert.Equal(5, g.Lives);
    }

    [Fact]
    public void Losing_the_last_buggy_ends_the_game()
    {
        var g = NewGame();
        for (int life = 0; life < 3; life++)
        {
            g.ClearRoad();
            g.Put(HazardKind.BigCrater, 5);
            for (int i = 0; i < 60 * 5 && g.State == GameState.Playing && g.Lives == 3 - life; i++) g.Step(default);
        }
        Assert.Equal(GameState.GameOver, g.State);
    }

    [Fact]
    public void The_demonstration_pilot_drives_well_and_the_game_ends_in_the_table()
    {
        var g = NewGame(seed: 3);
        var pilot = new AttractPilot(3);
        for (int i = 0; i < 60 * 60 * 4 && g.State == GameState.Playing; i++) g.Step(pilot.Decide(g));
        Assert.True(g.LetterIndex >= 9 || g.CourseNumber > 0, $"reached {g.Letter}; lost: {string.Join("; ", g.LossLog)}");
        for (int i = 0; i < 60 * 60 * 10 && g.State == GameState.Playing; i++) g.Step(default);
        Assert.Equal(GameState.GameOver, g.State);
        g.Idle(60 * 4);
        Assert.Equal(GameState.EnterInitials, g.State);
        g.Initials!.Finish();
        g.Idle(2);
        Assert.Equal(GameState.HighScores, g.State);
        Assert.Equal(g.Score, HighScoreTable.Load(_dir).TopScore);
        Assert.Equal(g.Stage, HighScoreTable.Load(_dir).Entries[0].Stage);
    }

    [Fact]
    public void The_same_seed_plays_the_same_game()
    {
        string Run()
        {
            var g = NewGame(seed: 21);
            var pilot = new AttractPilot(5);
            for (int i = 0; i < 60 * 60; i++) g.Step(pilot.Decide(g));
            return $"{g.Letter} {g.Score} {g.Lives} {g.BuggyX:0.###} {g.Hazards.Count} {g.Ufos.Count}";
        }
        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void The_title_plays_its_tune_and_the_demonstration_has_sound()
    {
        var g = new GameEngine(5, new HighScoreTable(_dir));
        var heard = new List<Sfx>();
        g.Sound += heard.Add;
        g.Idle(1);
        Assert.Equal([Sfx.TitleTune], heard);
        g.Idle((int)(60 * (GameEngine.TitleIdleSeconds + 5)));
        Assert.Equal(GameState.Attract, g.State);
        Assert.Contains(Sfx.CourseStart, heard);
        Assert.Contains(Sfx.MusicStart, heard);
        Assert.Contains(Sfx.Fire, heard);
        g.Idle((int)(60 * (GameEngine.AttractSeconds + 1)));
        Assert.Contains(Sfx.UfoArrive, heard);
        Assert.Equal(GameState.HighScores, g.State);
        Assert.Equal(Sfx.MusicStop, heard.Last(s => s is Sfx.MusicStart or Sfx.MusicStop));
        g.Idle((int)(60 * (GameEngine.HighScoreSeconds + 1)));
        Assert.Equal(GameState.Title, g.State);
    }

    [Fact]
    public void Pausing_stops_the_music_and_resuming_brings_it_back()
    {
        var g = EmptyRoad();
        var heard = new List<Sfx>();
        g.Sound += heard.Add;
        Steps(g, 60 * 3);
        Assert.Contains(Sfx.MusicStart, heard);
        g.Pause();
        Assert.Equal(Sfx.MusicStop, heard[^1]);
        double x = g.BuggyX;
        Steps(g, 60);
        Assert.Equal(x, g.BuggyX);
        g.Resume();
        Assert.Equal(Sfx.MusicStart, heard[^1]);
    }

    [Fact]
    public void Every_sound_is_there()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "Sounds");
        var files = Enum.GetValues<Sfx>().Where(s => s is not (Sfx.MusicStart or Sfx.MusicStop)).Select(s => s.ToString()).Append("Drive");
        Assert.All(files, f => Assert.True(File.Exists(Path.Combine(folder, f + ".wav")), f));
    }

    [Fact]
    public void Every_sprite_is_well_formed()
    {
        Assert.Equal(24, Sprites.Body.Width);
        Assert.All(Sprites.Wheel, s => Assert.Equal(5, s.Width));
        Assert.Equal(Sprites.BigRock.Width, Sprites.CrackedRock.Width);
        Assert.Equal((int)Course.Width(HazardKind.Tank), Sprites.Tank.Width);
        Assert.Equal((int)Course.Width(HazardKind.BigRock), Sprites.BigRock.Width);
        Assert.Equal((int)Course.Width(HazardKind.SmallRock), Sprites.SmallRock.Width);
        Assert.All(Enum.GetValues<UfoKind>(), k => Assert.Equal(2, Sprites.Art(k).Length));
    }
}
