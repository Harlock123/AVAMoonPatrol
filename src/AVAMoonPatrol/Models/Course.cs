namespace AVAMoonPatrol.Models;

public enum HazardKind { SmallCrater, BigCrater, SmallRock, BigRock, Mine, Tank }

/// <summary>Where the course puts something in the buggy's way.</summary>
public readonly record struct HazardSpec(HazardKind Kind, double X);

/// <summary>
/// The course: checkpoints A to Z, a stretch of road between each. The road is laid out from the seed,
/// a stretch at a time, so after losing a buggy the stretch comes back exactly as it was. It gets
/// harder as it goes: bigger craters and rocks, then mines, then tanks; the champion course after the
/// first starts harder still.
/// </summary>
public static class Course
{
    public const int Letters = 26;
    public const double SectionLength = 640;
    public const double Length = (Letters - 1) * SectionLength;
    /// <summary>No hazard closer than this to the start of a stretch: where a new buggy is set down.</summary>
    public const double SafeStart = 140;

    public static char Letter(int i) => (char)('A' + i);

    /// <summary>E, J, O, T and Z: the checkpoints with a time bonus.</summary>
    public static bool IsMajor(int letter) => letter % 5 == 4 || letter == Letters - 1;

    /// <summary>How hard a stretch is: its place on the course, plus ten for each course before.</summary>
    public static int Difficulty(int course, int section) => Math.Min(34, course * 10 + section);

    public static double Width(HazardKind k) => k switch
    {
        HazardKind.SmallCrater => 14,
        HazardKind.BigCrater => 24,
        HazardKind.SmallRock => 8,
        HazardKind.BigRock => 12,
        HazardKind.Mine => 7,
        _ => 16,
    };

    /// <summary>How tall it stands above the surface (craters: nothing).</summary>
    public static double Height(HazardKind k) => k switch
    {
        HazardKind.SmallRock => 6,
        HazardKind.BigRock => 10,
        HazardKind.Mine => 3,
        HazardKind.Tank => 9,
        _ => 0,
    };

    public static bool IsCrater(HazardKind k) => k is HazardKind.SmallCrater or HazardKind.BigCrater;

    /// <summary>The hazards on one stretch of one course.</summary>
    public static List<HazardSpec> Generate(int seed, int course, int section)
    {
        var rng = new Random(unchecked(seed * 1000003 + course * 7919 + section * 104729));
        int d = Difficulty(course, section);
        double minGap = Math.Max(72, 130 - d * 2.2);
        double x = section * SectionLength + SafeStart - minGap;
        double end = (section + 1) * SectionLength - 50;
        var list = new List<HazardSpec>();
        while (true)
        {
            x += minGap + rng.NextDouble() * minGap * 0.9;
            var kind = Pick(rng, d);
            if (x + Width(kind) > end) break;
            list.Add(new HazardSpec(kind, x));
            x += Width(kind);
        }
        return list;
    }

    private static HazardKind Pick(Random rng, int d)
    {
        var options = new List<(HazardKind Kind, double Weight)> { (HazardKind.SmallCrater, 3), (HazardKind.SmallRock, 3) };
        if (d >= 2) options.Add((HazardKind.BigCrater, 2));
        if (d >= 3) options.Add((HazardKind.BigRock, 2));
        if (d >= 10) options.Add((HazardKind.Mine, 2));
        if (d >= 15) options.Add((HazardKind.Tank, 1.3));
        double pick = rng.NextDouble() * options.Sum(o => o.Weight);
        foreach (var (kind, weight) in options)
        {
            pick -= weight;
            if (pick < 0) return kind;
        }
        return options[^1].Kind;
    }
}
