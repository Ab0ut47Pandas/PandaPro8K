namespace PandaPro8K;

public static class GameSirButtonMapping
{
    public const ushort A = 0x1000;
    public const ushort B = 0x2000;
    public const ushort X = 0x4000;
    public const ushort Y = 0x8000;

    private const ushort FaceMask = A | B | X | Y;

    public static ushort NormalizePhysicalLabels(ushort rawButtons, bool swapFacePairs)
    {
        if (!swapFacePairs) return rawButtons;

        ushort normalized = (ushort)(rawButtons & ~FaceMask);
        if ((rawButtons & B) != 0) normalized |= A;
        if ((rawButtons & A) != 0) normalized |= B;
        if ((rawButtons & Y) != 0) normalized |= X;
        if ((rawButtons & X) != 0) normalized |= Y;
        return normalized;
    }
}
