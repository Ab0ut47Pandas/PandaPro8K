using PandaPro8K;

var cases = new (string Name, ushort Input, bool Swap, ushort Expected)[]
{
    ("physical A", GameSirButtonMapping.B, true, GameSirButtonMapping.A),
    ("physical B", GameSirButtonMapping.A, true, GameSirButtonMapping.B),
    ("physical X", GameSirButtonMapping.Y, true, GameSirButtonMapping.X),
    ("physical Y", GameSirButtonMapping.X, true, GameSirButtonMapping.Y),
    ("raw A", GameSirButtonMapping.A, false, GameSirButtonMapping.A),
    ("raw B", GameSirButtonMapping.B, false, GameSirButtonMapping.B),
    ("non-face bits", 0x035F, true, 0x035F),
    ("A plus left bumper", 0x2100, true, 0x1100),
};

int failures = 0;
foreach (var test in cases)
{
    ushort actual = GameSirButtonMapping.NormalizePhysicalLabels(test.Input, test.Swap);
    bool passed = actual == test.Expected;
    Console.WriteLine($"{(passed ? "PASS" : "FAIL")} {test.Name}: 0x{test.Input:X4} -> 0x{actual:X4}");
    if (!passed) failures++;
}

Console.WriteLine(failures == 0
    ? $"All {cases.Length} face-button mapping checks passed."
    : $"{failures} face-button mapping check(s) failed.");
return failures == 0 ? 0 : 1;
