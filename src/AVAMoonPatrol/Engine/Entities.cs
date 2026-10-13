using AVAMoonPatrol.Models;

namespace AVAMoonPatrol.Engine;

/// <summary>A crater, rock, mine or tank on the road, in course coordinates (X is its left edge).</summary>
public sealed class Hazard
{
    public required HazardKind Kind { get; init; }
    public required double X { get; set; }
    public double Width => Course.Width(Kind);
    public double Right => X + Width;
    public double Height => Course.Height(Kind);
    /// <summary>Shots it takes: a big rock two, everything else that can be shot one.</summary>
    public int Hits { get; set; }
    public bool Dead { get; set; }
    /// <summary>The buggy was in the air above it.</summary>
    public bool JumpedOver { get; set; }
    /// <summary>The buggy is past it (jump points paid, if earned).</summary>
    public bool Passed { get; set; }
    /// <summary>A crater a bomb blew, not one the course laid out.</summary>
    public bool Blown { get; init; }
    /// <summary>A tank's time to its next shell.</summary>
    public double Reload { get; set; } = 1.2;
    public bool IsCrater => Course.IsCrater(Kind);
}

public enum UfoKind
{
    /// <summary>Drifts in loops and drops bombs.</summary>
    Saucer,
    /// <summary>Swoops low; its bombs blow new craters in the road ahead.</summary>
    Diver,
    /// <summary>Darts back and forth fast; its bombs fall fast.</summary>
    Dart,
}

/// <summary>Something in the sky, in screen coordinates (the patrol keeps pace with the buggy).</summary>
public sealed class Ufo
{
    public required UfoKind Kind { get; init; }
    public double X { get; set; }
    public double Y { get; set; }
    public double BaseX { get; set; }
    public double BaseY { get; set; }
    public double Phase { get; set; }
    public double Age { get; set; }
    public double BombTimer { get; set; }
    public bool Leaving { get; set; }
    public bool Dead { get; set; }
    public int Points => Kind switch { UfoKind.Saucer => 100, UfoKind.Diver => 200, _ => 300 };
}

/// <summary>A falling bomb, in screen coordinates.</summary>
public sealed class Bomb
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Vy { get; set; }
    public bool Blasts { get; init; }
    public bool Dead { get; set; }
}

/// <summary>A tank's shell, low along the road, in course coordinates.</summary>
public sealed class Shell
{
    public double X { get; set; }
    public bool Dead { get; set; }
}

/// <summary>A cannon shot. Up-shots are in screen coordinates; the forward shot in course coordinates.</summary>
public sealed class Shot
{
    public double X { get; set; }
    public double Y { get; set; }
    /// <summary>Forward shots only: where it was fired from (it has a short range).</summary>
    public double FromX { get; init; }
    public bool Dead { get; set; }
}

public enum EffectKind { Burst, BigBurst, Score, Debris }

/// <summary>Bursts, points and flying wreckage, in course coordinates.</summary>
public sealed class Effect
{
    public required EffectKind Kind { get; init; }
    public Vec Pos { get; set; }
    public Vec Vel { get; set; }
    public double Age { get; set; }
    public double Life { get; init; } = 0.4;
    public string Text { get; init; } = "";
    public bool Done => Age >= Life;
}

/// <summary>
/// The controls for one step: speed up or slow down (−1, 0 or +1), jump, and fire (both cannons at
/// once; held, it keeps firing).
/// </summary>
public record struct Controls(int Dx, bool Jump, bool Fire);
