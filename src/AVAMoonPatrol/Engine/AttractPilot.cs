using AVAMoonPatrol.Models;

namespace AVAMoonPatrol.Engine;

/// <summary>
/// The computer player for attract mode and the tests: it keeps both cannons firing, jumps craters,
/// mines, shells and any rock it hasn't shot away, and changes speed to slip out from under bombs.
/// </summary>
public sealed class AttractPilot(int seed)
{
    private readonly Random _rng = new(seed);

    public Controls Decide(GameEngine g)
    {
        if (g.Phase != Phase.Playing) return default;
        double front = g.BuggyX + GameEngine.HalfLength;
        bool jump = false;

        // The first thing on the road still ahead of the buggy's middle.
        foreach (var h in g.Hazards.Where(h => !h.Dead && h.Right > g.BuggyX - GameEngine.HalfLength).OrderBy(h => h.X))
        {
            double gap = h.X - front;
            if (h.IsCrater)
                jump = h.X - g.BuggyX is > -4 and < 6;
            else if (h.Kind == HazardKind.Mine)
                jump = gap is > 2 and < 12;
            else
                // A rock or tank the cannon hasn't cleared in time: jump it.
                jump = gap is > 4 and < 13;
            break;
        }
        foreach (var s in g.Shells)
            if (!s.Dead && s.X - front is > 2 and < 16) jump = true;

        // A bomb coming down on the buggy: speed up past it or drop back behind it.
        int dx = 0;
        foreach (var b in g.Bombs)
        {
            double over = b.X - g.ScreenX;
            if (Math.Abs(over) > GameEngine.HalfLength + 10 || b.Y > g.BuggyY) continue;
            dx = over >= 0 ? -1 : 1;
            break;
        }
        // Mostly hold fire; now and then let go so the cannon fires fresh.
        bool fire = _rng.NextDouble() < 0.93;
        return new Controls(dx, jump, fire);
    }
}
