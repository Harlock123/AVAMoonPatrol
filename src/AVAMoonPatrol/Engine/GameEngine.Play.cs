using AVAMoonPatrol.Models;

namespace AVAMoonPatrol.Engine;

public sealed partial class GameEngine
{
    public const double MinSpeed = 34;
    public const double CruiseSpeed = 60;
    public const double MaxSpeed = 110;
    public const double Gravity = 330;
    public const double JumpSpeed = 150;
    /// <summary>Half the buggy's length; it is 14 pixels tall.</summary>
    public const double HalfLength = 11;
    public const double BuggyHeight = 14;
    public const double ForwardShotSpeed = 280;
    public const double ForwardShotRange = 110;
    public const double UpShotSpeed = 260;
    public const int MaxUpShots = 3;
    public const double FireEvery = 0.28;
    public const double ShellSpeed = 90;

    /// <summary>The buggy's place on the course (its middle) and the bottom of its wheels.</summary>
    public double BuggyX { get; private set; }
    public double BuggyY { get; private set; } = Field.GroundY;
    public double Vy { get; private set; }
    public bool Airborne { get; private set; }
    public double Speed { get; private set; } = CruiseSpeed;
    /// <summary>Where the buggy sits on the screen: further right the faster it goes.</summary>
    public double ScreenX { get; private set; } = TargetScreenX(CruiseSpeed);
    /// <summary>The course position at the left edge of the screen.</summary>
    public double CameraX => BuggyX - ScreenX;
    /// <summary>Distance the wheels have rolled, for drawing them turn.</summary>
    public double Odometer { get; private set; }

    public List<Hazard> Hazards { get; } = [];
    public List<Ufo> Ufos { get; } = [];
    public List<Bomb> Bombs { get; } = [];
    public List<Shell> Shells { get; } = [];
    public List<Shot> UpShots { get; } = [];
    public Shot? ForwardShot { get; private set; }
    public List<Effect> Effects { get; } = [];

    private readonly HashSet<int> _laidOut = [];
    private double _fireCooldown;
    private double _ufoTimer;
    private int _lastMajor;

    public int Difficulty => Course.Difficulty(CourseNumber, LetterIndex);

    public static double TargetScreenX(double speed) => 52 + (speed - MinSpeed) / (MaxSpeed - MinSpeed) * 64;

    /// <summary>On the title and the tables: an empty road rolls by.</summary>
    private void Cruise(double dt)
    {
        Speed = CruiseSpeed;
        ScreenX = TargetScreenX(Speed);
        BuggyX += Speed * dt;
        Odometer += Speed * dt;
    }

    private void Simulate(double dt, Controls c)
    {
        PhaseTime += dt;
        if (_musicDue >= 0 && PhaseTime >= _musicDue && Phase == Phase.Playing) Music(true);
        if (Phase == Phase.Dying)
        {
            UpdateSky(dt);
            if (PhaseTime > 2.6)
            {
                Lives--;
                if (Lives <= 0) GameOver();
                else SetDown();
            }
            return;
        }

        CourseTime += dt;
        Drive(dt, c);
        LayOut();
        Fire(dt, c);
        MoveShots(dt);
        Tanks(dt);
        UpdateSky(dt);
        Collide();
        Checkpoints();
    }

    // ----- the buggy ----------------------------------------------------------------------------

    private void Drive(double dt, Controls c)
    {
        if (!Airborne)
        {
            if (c.Dx > 0) Speed = Math.Min(MaxSpeed, Speed + 70 * dt);
            else if (c.Dx < 0) Speed = Math.Max(MinSpeed, Speed - 90 * dt);
            else Speed += Math.Clamp(CruiseSpeed - Speed, -40 * dt, 40 * dt);
            if (c.Jump)
            {
                Airborne = true;
                Vy = -JumpSpeed;
                Play(Sfx.Jump);
            }
        }
        double target = TargetScreenX(Speed);
        ScreenX += Math.Clamp(target - ScreenX, -50 * dt, 50 * dt);
        BuggyX += Speed * dt;
        Odometer += Speed * dt;
        if (Airborne)
        {
            Vy += Gravity * dt;
            BuggyY += Vy * dt;
            if (BuggyY >= Field.GroundY)
            {
                BuggyY = Field.GroundY;
                Vy = 0;
                Airborne = false;
                Play(Sfx.Land);
            }
        }
    }

    private void Fire(double dt, Controls c)
    {
        _fireCooldown -= dt;
        if (!c.Fire || _fireCooldown > 0) return;
        bool fired = false;
        if (UpShots.Count < MaxUpShots)
        {
            UpShots.Add(new Shot { X = ScreenX - 3, Y = BuggyY - BuggyHeight - 2 });
            fired = true;
        }
        if (ForwardShot is null)
        {
            ForwardShot = new Shot { X = BuggyX + HalfLength + 2, Y = BuggyY - 6, FromX = BuggyX + HalfLength };
            fired = true;
        }
        if (fired)
        {
            _fireCooldown = FireEvery;
            Play(Sfx.Fire);
        }
    }

    private void MoveShots(double dt)
    {
        foreach (var s in UpShots)
        {
            s.Y -= UpShotSpeed * dt;
            if (s.Y < Field.PanelHeight) s.Dead = true;
            foreach (var u in Ufos)
            {
                if (u.Dead || s.Dead || Math.Abs(u.X - s.X) > 7 || Math.Abs(u.Y - s.Y) > 5) continue;
                s.Dead = u.Dead = true;
                AddScore(u.Points);
                var at = new Vec(CameraX + u.X, u.Y);
                Burst(at);
                Popup(at, u.Points);
                Play(Sfx.UfoHit);
            }
            foreach (var b in Bombs)
            {
                if (b.Dead || s.Dead || Math.Abs(b.X - s.X) > 4 || Math.Abs(b.Y - s.Y) > 5) continue;
                s.Dead = b.Dead = true;
                Burst(new Vec(CameraX + b.X, b.Y));
                Play(Sfx.ShellHit);
            }
        }
        UpShots.RemoveAll(s => s.Dead);

        if (ForwardShot is not { } f) return;
        f.X += ForwardShotSpeed * dt;
        if (f.X - f.FromX > ForwardShotRange) { ForwardShot = null; return; }
        foreach (var sh in Shells)
        {
            if (sh.Dead || Math.Abs(sh.X - f.X) > 5 || f.Y < Field.GroundY - 9) continue;
            sh.Dead = true;
            ForwardShot = null;
            AddScore(50);
            Burst(new Vec(sh.X, Field.GroundY - 4));
            Play(Sfx.ShellHit);
            return;
        }
        foreach (var h in Hazards)
        {
            if (h.Dead || h.IsCrater || h.Kind == HazardKind.Mine) continue;
            if (f.X < h.X || f.X > h.Right + 4 || f.Y < Field.GroundY - h.Height - 1) continue;
            ForwardShot = null;
            var at = new Vec(h.X + h.Width / 2, Field.GroundY - h.Height / 2);
            if (--h.Hits > 0)
            {
                Burst(at with { X = h.X });
                Play(Sfx.RockHit);
                return;
            }
            h.Dead = true;
            int points = h.Kind switch { HazardKind.SmallRock => 100, HazardKind.BigRock => 200, _ => 500 };
            AddScore(points);
            Burst(at, big: h.Kind != HazardKind.SmallRock);
            Popup(at with { Y = at.Y - 8 }, points);
            Play(h.Kind == HazardKind.Tank ? Sfx.TankHit : Sfx.RockBreak);
            return;
        }
    }

    // ----- the road -----------------------------------------------------------------------------

    /// <summary>Lays out the stretches just ahead, and forgets what has scrolled away behind.</summary>
    private void LayOut()
    {
        int from = Math.Max(0, (int)Math.Floor((BuggyX - 200) / Course.SectionLength));
        int to = Math.Min(Course.Letters - 2, (int)Math.Floor((BuggyX + 420) / Course.SectionLength));
        for (int s = from; s <= to; s++)
        {
            if (!_laidOut.Add(s)) continue;
            foreach (var spec in Course.Generate(_seed, CourseNumber, s))
                Hazards.Add(new Hazard { Kind = spec.Kind, X = spec.X, Hits = spec.Kind == HazardKind.BigRock ? 2 : 1 });
        }
        double behind = CameraX - 40;
        Hazards.RemoveAll(h => h.Right < behind);
        Shells.RemoveAll(s => s.Dead || s.X < behind);
    }

    private void Tanks(double dt)
    {
        foreach (var h in Hazards)
        {
            if (h.Kind != HazardKind.Tank || h.Dead) continue;
            double ahead = h.X - BuggyX;
            if (ahead < 24 || ahead > 210) continue;
            h.Reload -= dt;
            if (h.Reload > 0) continue;
            h.Reload = Math.Max(1.4, 2.6 - Difficulty * 0.03);
            Shells.Add(new Shell { X = h.X - 1 });
            Play(Sfx.TankFire);
        }
        foreach (var s in Shells) s.X -= ShellSpeed * dt;
    }

    // ----- the sky ------------------------------------------------------------------------------

    private void UpdateSky(double dt)
    {
        int d = Difficulty;
        if (Phase == Phase.Playing && Ufos.Count == 0)
        {
            _ufoTimer -= dt;
            if (_ufoTimer <= 0) Arrive(d);
        }

        for (int i = 0; i < Ufos.Count; i++)
        {
            var u = Ufos[i];
            u.Age += dt;
            u.Phase += dt;
            if (u.Age > 17 || Phase == Phase.Dying) u.Leaving = true;
            if (u.Leaving)
            {
                u.Y -= 60 * dt;
                u.X += 30 * dt;
                if (u.Y < Field.PanelHeight - 12) u.Dead = true;
                continue;
            }
            double tx, ty;
            switch (u.Kind)
            {
                case UfoKind.Saucer:
                    u.BaseX = Math.Clamp(u.BaseX + 20 * Math.Sin(u.Phase * 0.4) * dt, 40, 216);
                    tx = u.BaseX + 34 * Math.Sin(u.Phase * 1.3 + i);
                    ty = u.BaseY + 12 * Math.Sin(u.Phase * 2.1 + i);
                    break;
                case UfoKind.Diver:
                    tx = u.BaseX + 40 * Math.Sin(u.Phase * 0.9 + i);
                    ty = u.BaseY + 40 * Math.Max(0, Math.Sin(u.Phase * 0.8 + i));
                    break;
                default:
                    tx = u.BaseX + 90 * Math.Sin(u.Phase * 1.7 + i * 2);
                    ty = u.BaseY + 6 * Math.Sin(u.Phase * 5);
                    break;
            }
            double ease = Math.Min(1, (u.Kind == UfoKind.Dart ? 5 : 2.5) * dt);
            u.X += (tx - u.X) * ease;
            u.Y += (ty - u.Y) * ease;

            u.BombTimer -= dt;
            if (u.BombTimer <= 0 && u.Age > 1.5 && u.X is > 16 and < 240 && Phase == Phase.Playing
                && Bombs.Count < 3 + d / 8)
            {
                Bombs.Add(new Bomb { X = u.X, Y = u.Y + 4, Vy = u.Kind == UfoKind.Dart ? 95 : 70, Blasts = u.Kind == UfoKind.Diver });
                u.BombTimer = (1.6 + _rng.NextDouble() * 1.6) * Math.Max(0.55, 1 - d * 0.015);
            }
        }
        Ufos.RemoveAll(u => u.Dead);

        foreach (var b in Bombs)
        {
            b.Y += b.Vy * dt;
            if (b.Y < Field.GroundY) continue;
            b.Dead = true;
            double at = CameraX + b.X;
            Burst(new Vec(at, Field.GroundY - 3));
            if (b.Blasts && at > BuggyX + 30 && !Hazards.Any(h => !h.Dead && h.Right > at - 26 && h.X < at + 34))
            {
                Hazards.Add(new Hazard { Kind = HazardKind.SmallCrater, X = at - 7, Blown = true });
                Play(Sfx.Crater);
            }
            else Play(Sfx.BombBlast);
        }
        Bombs.RemoveAll(b => b.Dead);
    }

    /// <summary>A new flight comes in from behind: saucers first, then divers, then darts as well.</summary>
    private void Arrive(int d)
    {
        int count = d < 4 ? 2 : 3;
        for (int i = 0; i < count; i++)
        {
            var kind = d < 6 ? UfoKind.Saucer
                : d < 14 ? (_rng.NextDouble() < 0.5 ? UfoKind.Saucer : UfoKind.Diver)
                : (UfoKind)_rng.Next(3);
            Ufos.Add(new Ufo
            {
                Kind = kind,
                X = -12 - i * 16,
                Y = 56 + i * 10,
                BaseX = 80 + i * 50,
                BaseY = kind == UfoKind.Dart ? 58 + i * 6 : 66 + i * 9,
                Phase = _rng.NextDouble() * 6,
                BombTimer = 1.5 + _rng.NextDouble() * 2,
            });
        }
        _ufoTimer = Math.Max(2, 6 - d * 0.12) + _rng.NextDouble() * 3;
        Play(Sfx.UfoArrive);
    }

    // ----- collisions ---------------------------------------------------------------------------

    private void Collide()
    {
        double left = BuggyX - HalfLength, right = BuggyX + HalfLength;
        foreach (var h in Hazards)
        {
            if (h.Dead) continue;
            bool over = right > h.X && left < h.Right;
            if (over && Airborne) h.JumpedOver = true;
            if (h.IsCrater)
            {
                if (!Airborne && BuggyX > h.X + 3 && BuggyX < h.Right - 3) { Crash("fell into a crater"); return; }
            }
            else if (over && BuggyY > Field.GroundY - h.Height + 1)
            {
                Crash($"hit a {h.Kind}");
                return;
            }
            if (!h.Passed && left > h.Right)
            {
                h.Passed = true;
                if (h.JumpedOver)
                {
                    int points = h.Kind switch { HazardKind.SmallCrater => 50, HazardKind.BigCrater or HazardKind.SmallRock => 80, _ => 100 };
                    AddScore(points);
                    Popup(new Vec(h.X + h.Width / 2, Field.GroundY - 14), points);
                    Play(Sfx.JumpPoints);
                }
            }
        }
        foreach (var s in Shells)
            if (!s.Dead && s.X > left && s.X < right && BuggyY > Field.GroundY - 7) { Crash("hit by a shell"); return; }
        foreach (var b in Bombs)
            if (!b.Dead && Math.Abs(b.X - ScreenX) < HalfLength && b.Y > BuggyY - BuggyHeight && b.Y < BuggyY + 2)
            {
                Crash("bombed");
                return;
            }
    }

    internal List<string> LossLog { get; } = [];

    private void Crash(string why)
    {
        LossLog.Add($"{Course.Letter(LetterIndex)}{CourseNumber}: {why} at {BuggyX:0}");
        Phase = Phase.Dying;
        PhaseTime = 0;
        Music(false);
        Play(Sfx.Crash);
        var at = new Vec(BuggyX, BuggyY - 6);
        Burst(at, big: true);
        double[] vx = [-60, -10, 45, -35, 70];
        for (int i = 0; i < vx.Length; i++)
            Effects.Add(new Effect
            {
                Kind = EffectKind.Debris, Pos = at + new Vec(i * 4 - 8, 0), Vel = new Vec(vx[i], -110 - i * 18),
                Life = 2.4, Text = i < 3 ? "wheel" : "panel",
            });
        ForwardShot = null;
        UpShots.Clear();
        Shells.Clear();
    }

    // ----- checkpoints --------------------------------------------------------------------------

    private void Checkpoints()
    {
        int letter = (int)Math.Floor(BuggyX / Course.SectionLength);
        if (letter <= LetterIndex || letter >= Course.Letters) return;
        LetterIndex = letter;
        char name = Course.Letter(letter);
        if (!Course.IsMajor(letter))
        {
            Play(Sfx.Checkpoint);
            ShowBanner(2, $"POINT {name}");
            return;
        }
        int time = (int)Math.Round(CourseTime);
        int average = AverageTime(_lastMajor, letter);
        int bonus = 1000 + Math.Max(0, average - time) * 100;
        AddScore(bonus);
        Play(Sfx.Bonus);
        CourseTime = 0;
        _lastMajor = letter;
        if (letter < Course.Letters - 1)
        {
            ShowBanner(4.5, $"POINT {name}", $"YOUR TIME {time,6}", $"AVERAGE TIME {average,3}", $"BONUS {bonus,10}");
            return;
        }
        // Z: the course is done; the road runs on into the next, harder one.
        ShowBanner(5, $"POINT {name}: COURSE COMPLETE", $"YOUR TIME {time,6}", $"BONUS {bonus,10}", "NOW THE CHAMPION COURSE");
        CourseNumber++;
        LetterIndex = 0;
        _lastMajor = 0;
        BuggyX -= Course.Length;
        foreach (var h in Hazards) h.X -= Course.Length;
        foreach (var s in Shells) s.X -= Course.Length;
        foreach (var e in Effects) e.Pos = e.Pos with { X = e.Pos.X - Course.Length };
        _laidOut.Clear();
        // Section 0 of the new course: everything already on screen came with it.
        Hazards.RemoveAll(h => h.X > BuggyX + 120);
    }

    /// <summary>The expected time between checkpoints at cruising speed, in whole seconds.</summary>
    public static int AverageTime(int fromLetter, int toLetter) =>
        (int)Math.Round((toLetter - fromLetter) * Course.SectionLength / CruiseSpeed);

    // ----- starting and restarting --------------------------------------------------------------

    /// <summary>A buggy set down at the last checkpoint passed, the road there laid out afresh.</summary>
    private void SetDown()
    {
        ClearField();
        Phase = Phase.Playing;
        PhaseTime = 0;
        BuggyX = LetterIndex * Course.SectionLength + 30;
        BuggyY = Field.GroundY;
        Vy = 0;
        Airborne = false;
        Speed = CruiseSpeed;
        ScreenX = TargetScreenX(Speed);
        _ufoTimer = LetterIndex == 0 && CourseNumber == 0 ? 6 : 3.5;
        _fireCooldown = 0.3;
        LayOut();
        Play(Sfx.CourseStart);
        _musicDue = 2.2;
        ShowBanner(2.2, CourseNumber == 0 ? "BEGINNER COURSE" : "CHAMPION COURSE", $"POINT {Letter}");
    }

    private void ClearField()
    {
        Hazards.Clear();
        _laidOut.Clear();
        Ufos.Clear();
        Bombs.Clear();
        Shells.Clear();
        UpShots.Clear();
        ForwardShot = null;
        Banner = [];
        BannerTime = 0;
        Phase = Phase.Playing;
        BuggyY = Field.GroundY;
        Airborne = false;
    }

    // ----- for the tests and screenshots --------------------------------------------------------

    internal void Idle(int steps)
    {
        for (int i = 0; i < steps; i++) Step(default);
    }

    /// <summary>Sets the buggy down at a checkpoint, as if it had driven there.</summary>
    internal void JumpToPoint(int letter, int course = 0)
    {
        CourseNumber = course;
        LetterIndex = letter;
        _lastMajor = letter;
        CourseTime = 0;
        SetDown();
    }

    /// <summary>Clears the road ahead and puts one thing on it, <paramref name="ahead"/> pixels in front of the buggy.</summary>
    internal Hazard Put(HazardKind kind, double ahead)
    {
        for (int s = 0; s < Course.Letters; s++) _laidOut.Add(s);
        Hazards.RemoveAll(h => h.Right > BuggyX - HalfLength);
        var h = new Hazard { Kind = kind, X = BuggyX + HalfLength + ahead, Hits = kind == HazardKind.BigRock ? 2 : 1 };
        Hazards.Add(h);
        return h;
    }

    /// <summary>An empty road and sky (no hazards will be laid out).</summary>
    internal void ClearRoad()
    {
        for (int s = 0; s < Course.Letters; s++) _laidOut.Add(s);
        Hazards.Clear();
        Ufos.Clear();
        Bombs.Clear();
        _ufoTimer = 1e9;
    }

    internal Ufo PutUfo(UfoKind kind, double x, double y)
    {
        var u = new Ufo { Kind = kind, X = x, Y = y, BaseX = x, BaseY = y, BombTimer = 1e9, Age = 2 };
        Ufos.Add(u);
        return u;
    }

    internal Bomb PutBomb(double x, double y, bool blasts = false)
    {
        var b = new Bomb { X = x, Y = y, Vy = 70, Blasts = blasts };
        Bombs.Add(b);
        return b;
    }

    internal void Arrivals(double seconds) => _ufoTimer = seconds;
}
