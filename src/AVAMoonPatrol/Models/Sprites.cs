namespace AVAMoonPatrol.Models;

/// <summary>
/// The game's pixel art, this game's own drawings. A and B take a palette (the flying things come in
/// three colours); the other letters are fixed colours.
/// </summary>
public static class Sprites
{
    public sealed record Sprite(string[] Rows)
    {
        public int Width => Rows[0].Length;
        public int Height => Rows.Length;
    }

    private static Sprite S(params string[] rows)
    {
        if (rows.Any(r => r.Length != rows[0].Length))
            throw new ArgumentException("Sprite rows must be the same width: " + string.Join("|", rows));
        return new Sprite(rows);
    }

    /// <summary>Colours for A and B.</summary>
    public sealed record Palette(uint A, uint B);

    public static readonly Palette Buggy = new(0xFFC77CF2, 0xFF7339B0);
    public static readonly Palette Saucer = new(0xFFE6E6F2, 0xFF5A6CFF);
    public static readonly Palette Diver = new(0xFFFF5050, 0xFFFFC84A);
    public static readonly Palette Dart = new(0xFF5BE07A, 0xFF23804A);

    public static readonly IReadOnlyDictionary<char, uint> Fixed = new Dictionary<char, uint>
    {
        ['w'] = 0xFFF4F0E6, // white
        ['c'] = 0xFF8FE4FF, // glass
        ['k'] = 0xFF8C8C9C, // steel
        ['K'] = 0xFF4A4A58, // dark steel
        ['y'] = 0xFFFFE14A, // yellow
        ['r'] = 0xFFFF4040, // red
        ['o'] = 0xFFFF8A1E, // orange
        ['d'] = 0xFF6B4A2E, // dark rock
        ['m'] = 0xFFA0744A, // rock
        ['l'] = 0xFFD2A46E, // light rock
        ['t'] = 0xFF9DB04A, // tank
        ['T'] = 0xFF57661F, // dark tank
    };

    /// <summary>The moon buggy's body, facing right; the wheels go underneath.</summary>
    public static readonly Sprite Body = S(
        "........kk..............",
        "........kk..............",
        ".......kKKk.............",
        ".....BBBBBBBBBB.........",
        "....BAAccccAAAAB........",
        "...BAAAccccAAAAAB...kkkk",
        ".BBAAAAAAAAAAAAAABBBkK..",
        "BAAAAAAAAAAAAAAAAAAAAAy.",
        "BAAAAAAAAAAAAAAAAAAAAAB.",
        ".BBBBBBBBBBBBBBBBBBBBB..");

    /// <summary>A wheel, two frames as it turns.</summary>
    public static readonly Sprite[] Wheel =
    [
        S(".KKK.", "KkKkK", "KKwKK", "KkKkK", ".KKK."),
        S(".KKK.", "KKkKK", "KkwkK", "KKkKK", ".KKK."),
    ];

    public static readonly Sprite[] SaucerArt =
    [
        S("....wwww....", "..wwccccww..", ".AAAAAAAAAA.", "AByAAyAAyAAB", ".AAAAAAAAAA.", "...B....B..."),
        S("....wwww....", "..wwccccww..", ".AAAAAAAAAA.", "ABAAyAAyAAyB", ".AAAAAAAAAA.", "..B......B.."),
    ];

    public static readonly Sprite[] DiverArt =
    [
        S(".....BB.....", "....BccB....", "..AAAAAAAA..", "AAyAAAAAAyAA", "..AAAAAAAA..", "...A....A...", "..A......A.."),
        S(".....BB.....", "....BccB....", "..AAAAAAAA..", "AAAAyAAyAAAA", "..AAAAAAAA..", "...A....A...", "...A....A..."),
    ];

    public static readonly Sprite[] DartArt =
    [
        S("AA........AA", ".AAA.ww.AAA.", "..AAByyBAA..", ".AAA.ww.AAA.", "AA........AA"),
        S(".A........A.", "AAAA.ww.AAAA", "..AAByyBAA..", "AAAA.ww.AAAA", ".A........A."),
    ];

    public static readonly Sprite SmallRock = S(
        "..dmmd..",
        ".dmllmd.",
        "dmllmmmd",
        "dmlmmmmd",
        "dmmmmmdd",
        "dddddddd");

    public static readonly Sprite BigRock = S(
        "....ddmmd...",
        "..ddmllmmd..",
        ".dmmllllmmd.",
        ".dmllllmmmd.",
        "dmlllmmmmmmd",
        "dmllmmmmmmmd",
        "dmlmmmmmmmdd",
        "dmmmmmmmmddd",
        "ddmmmmmmdddd",
        "dddddddddddd");

    /// <summary>A big rock after its first hit: cracked.</summary>
    public static readonly Sprite CrackedRock = S(
        "....ddmmd...",
        "..ddmlKmmd..",
        ".dmmllKlmmd.",
        ".dmllKKmmmd.",
        "dmlllmKmmmmd",
        "dmllmmKKmmmd",
        "dmlmmmmKmmdd",
        "dmmmmmmKmddd",
        "ddmmmmmKdddd",
        "dddddddddddd");

    public static readonly Sprite[] Mine =
    [
        S("..rrr..", ".rKKKr.", "rKyyKKr", "KKKKKKK"),
        S("..ooo..", ".rKKKr.", "rKKyyKr", "KKKKKKK"),
    ];

    /// <summary>A tank, gun towards the oncoming buggy.</summary>
    public static readonly Sprite Tank = S(
        "........tt......",
        "......tttttt....",
        "kkkkkkTttttTt...",
        "....tttttttttt..",
        "..tttttttttttttt",
        ".TTTTTTTTTTTTTT.",
        "TKkKkKkKkKkKkKT.",
        "TkKkKkKkKkKkKkT.",
        ".TTTTTTTTTTTTT..");

    public static readonly Sprite Bomb = S(".r.", "rrr", "ryr", ".r.");
    public static readonly Sprite Shell = S("yyrr", "rrrr");
    public static readonly Sprite UpShot = S("w", "y", "y");
    public static readonly Sprite ForwardShot = S("yyyw");

    /// <summary>A burst, for anything shot or blown up.</summary>
    public static readonly Sprite[] Burst =
    [
        S("........", "...ww...", "..wyyw..", ".wyrryw.", ".wyrryw.", "..wyyw..", "...ww...", "........"),
        S("w..w..w.", ".y.y.y..", "..yry...", "wyr.ryw.", "..yry...", ".y.y.y..", "w..w..w.", "........"),
        S("o......o", "...o....", ".o...o..", "...r....", "..o...o.", "o.......", "...o..o.", "........"),
    ];

    public static Sprite[] Art(Engine.UfoKind kind) => kind switch
    {
        Engine.UfoKind.Saucer => SaucerArt,
        Engine.UfoKind.Diver => DiverArt,
        _ => DartArt,
    };

    public static Palette Colours(Engine.UfoKind kind) => kind switch
    {
        Engine.UfoKind.Saucer => Saucer,
        Engine.UfoKind.Diver => Diver,
        _ => Dart,
    };
}
