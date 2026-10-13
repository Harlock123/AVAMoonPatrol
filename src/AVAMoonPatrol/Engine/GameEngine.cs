using AVAMoonPatrol.Models;

namespace AVAMoonPatrol.Engine;

public enum GameState { Title, Attract, Playing, Paused, GameOver, EnterInitials, HighScores }

/// <summary>What is happening within a game.</summary>
public enum Phase
{
    Playing,
    /// <summary>The buggy is wrecked: a moment before the next one is set down at the last checkpoint.</summary>
    Dying,
}

/// <summary>Sounds the engine asks for; the host decides how they sound.</summary>
public enum Sfx
{
    Fire, Jump, Land, RockHit, RockBreak, UfoArrive, UfoHit, BombBlast, Crater, TankFire, TankHit, ShellHit,
    Crash, Checkpoint, Bonus, ExtraLife, JumpPoints, CourseStart, GameOver, TitleTune, MusicStart, MusicStop,
}

/// <summary>
/// The whole game with no UI. Call <see cref="Update"/> with the elapsed time and the controls; it
/// runs in fixed 60 Hz steps, so it plays the same at any frame rate and a seed always plays the
/// same game.
/// </summary>
public sealed partial class GameEngine
{
    public const int StartingLives = 3;
    public const int FirstExtraLife = 10000;
    public const int ExtraLifeEvery = 20000;
    public const double TitleIdleSeconds = 15;
    public const double AttractSeconds = 45;
    public const double HighScoreSeconds = 9;

    private readonly Random _rng;
    private readonly int _seed;
    private double _accumulator;
    private bool _startPrev;
    private bool _titleTunePending = true;
    private bool _music;
    private double _musicDue = -1;

    public GameEngine(int seed = 1982, HighScoreTable? table = null)
    {
        _seed = seed;
        _rng = new Random(seed);
        HighScores = table ?? HighScoreTable.Load();
    }

    public event Action<Sfx>? Sound;

    public GameState State { get; private set; } = GameState.Title;
    public Phase Phase { get; private set; } = Phase.Playing;
    public double StateTime { get; private set; }
    public double PhaseTime { get; private set; }
    public HighScoreTable HighScores { get; }
    public InitialsEntry? Initials { get; private set; }
    public int NewEntryIndex { get; private set; } = -1;
    public AttractPilot? Pilot { get; private set; }
    public bool Silent { get; set; }

    public int Score { get; private set; }
    public int Lives { get; private set; }
    private int _nextExtraLife;

    /// <summary>0 for the beginner course, 1 and up for the champion courses.</summary>
    public int CourseNumber { get; private set; }
    /// <summary>The last checkpoint passed: 0 is A, 25 is Z.</summary>
    public int LetterIndex { get; private set; }
    public char Letter => Course.Letter(LetterIndex);
    /// <summary>Seconds since the last checkpoint with a time bonus (or the start).</summary>
    public double CourseTime { get; private set; }
    /// <summary>For the high-score table: the checkpoint reached, counting on through the courses.</summary>
    public int Stage => CourseNumber * Course.Letters + LetterIndex + 1;
    /// <summary>How far along the course the buggy is, 0 to 1.</summary>
    public double Progress => Math.Clamp(BuggyX / Course.Length, 0, 1);

    /// <summary>A message across the sky (a checkpoint, a bonus), and how long it has left.</summary>
    public string[] Banner { get; private set; } = [];
    public double BannerTime { get; private set; }

    private void Play(Sfx s)
    {
        if (!Silent) Sound?.Invoke(s);
    }

    public void Update(double dt, Controls controls, bool start)
    {
        _accumulator += Math.Min(dt, 0.25);
        while (_accumulator >= Field.Step)
        {
            _accumulator -= Field.Step;
            Step(controls, start);
        }
    }

    /// <summary>One 1/60 s step.</summary>
    public void Step(Controls c, bool start = false)
    {
        double dt = Field.Step;
        StateTime += dt;
        bool startPressed = start && !_startPrev;
        _startPrev = start;
        UpdateEffects(dt);

        switch (State)
        {
            case GameState.Title:
                if (_titleTunePending) { _titleTunePending = false; Play(Sfx.TitleTune); }
                Cruise(dt);
                if (startPressed || (c.Fire && StateTime > 0.6)) StartGame();
                else if (StateTime > TitleIdleSeconds) StartAttract();
                break;
            case GameState.Attract:
                Simulate(dt, Pilot!.Decide(this));
                if (startPressed) { StartGame(); break; }
                if (State == GameState.Attract && StateTime > AttractSeconds) ShowHighScores(-1);
                break;
            case GameState.Playing:
                Simulate(dt, c);
                break;
            case GameState.GameOver:
                if (StateTime > 3)
                {
                    if (HighScores.Qualifies(Score))
                    {
                        State = GameState.EnterInitials;
                        StateTime = 0;
                        Initials = new InitialsEntry(HighScores.LastInitials);
                    }
                    else ShowHighScores(-1);
                }
                break;
            case GameState.EnterInitials:
                if (Initials is { IsDone: true } done)
                {
                    int place = HighScores.Insert(done.Initials, Score, Stage);
                    HighScores.Save();
                    ShowHighScores(place);
                }
                break;
            case GameState.HighScores:
                Cruise(dt);
                if (startPressed || (c.Fire && StateTime > 1)) StartGame();
                else if (StateTime > HighScoreSeconds) ReturnToTitle();
                break;
        }
    }

    public void StartGame()
    {
        Pilot = null;
        Begin(GameState.Playing);
    }

    private void StartAttract()
    {
        Pilot = new AttractPilot(_rng.Next());
        Begin(GameState.Attract);
    }

    private void Begin(GameState state)
    {
        State = state;
        StateTime = 0;
        Score = 0;
        Lives = StartingLives;
        _nextExtraLife = FirstExtraLife;
        NewEntryIndex = -1;
        CourseNumber = 0;
        LetterIndex = 0;
        CourseTime = 0;
        _lastMajor = 0;
        Effects.Clear();
        SetDown();
    }

    public void ReturnToTitle()
    {
        State = GameState.Title;
        StateTime = 0;
        Pilot = null;
        _titleTunePending = true;
        Music(false);
        ClearField();
        Effects.Clear();
    }

    private void ShowHighScores(int place)
    {
        State = GameState.HighScores;
        StateTime = 0;
        NewEntryIndex = place;
        Pilot = null;
        Music(false);
        ClearField();
    }

    public void Pause()
    {
        if (State != GameState.Playing) return;
        State = GameState.Paused;
        Music(false);
    }

    public void Resume()
    {
        if (State != GameState.Paused) return;
        State = GameState.Playing;
        if (Phase == Phase.Playing) Music(true);
    }

    private void GameOver()
    {
        Music(false);
        if (State == GameState.Attract)
        {
            ShowHighScores(-1);
            return;
        }
        Play(Sfx.GameOver);
        State = GameState.GameOver;
        StateTime = 0;
    }

    internal void AddScore(int points)
    {
        Score += points;
        while (Score >= _nextExtraLife)
        {
            Lives++;
            _nextExtraLife += ExtraLifeEvery;
            Play(Sfx.ExtraLife);
        }
    }

    /// <summary>The patrol's driving music loops while a buggy is on the road.</summary>
    private void Music(bool on)
    {
        _musicDue = -1;
        if (on == _music) return;
        _music = on;
        Play(on ? Sfx.MusicStart : Sfx.MusicStop);
    }

    private void ShowBanner(double seconds, params string[] lines)
    {
        Banner = lines;
        BannerTime = seconds;
    }

    private void UpdateEffects(double dt)
    {
        foreach (var e in Effects)
        {
            e.Age += dt;
            if (e.Kind == EffectKind.Debris)
            {
                e.Vel = e.Vel with { Y = e.Vel.Y + Gravity * dt };
                e.Pos += e.Vel * dt;
                if (e.Pos.Y > Field.GroundY + 2) e.Vel = new Vec(e.Vel.X * 0.5, -Math.Abs(e.Vel.Y) * 0.35);
            }
        }
        Effects.RemoveAll(e => e.Done);
        if (BannerTime > 0)
        {
            BannerTime -= dt;
            if (BannerTime <= 0) Banner = [];
        }
    }

    private void Burst(Vec at, bool big = false) =>
        Effects.Add(new Effect { Kind = big ? EffectKind.BigBurst : EffectKind.Burst, Pos = at, Life = big ? 0.7 : 0.35 });

    private void Popup(Vec at, int points) =>
        Effects.Add(new Effect { Kind = EffectKind.Score, Pos = at, Life = 0.9, Text = points.ToString() });
}
