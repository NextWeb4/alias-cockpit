namespace AliasCockpit.Core.Generation;

public static class EntropyMath
{
    public static double FromAlphabet(int length, int alphabetSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        ArgumentOutOfRangeException.ThrowIfLessThan(alphabetSize, 2);

        return length * Math.Log2(alphabetSize);
    }

    public static double FromProduct(params int[] choices)
    {
        if (choices.Length == 0)
        {
            return 0;
        }

        var bits = 0d;
        foreach (var choice in choices)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(choice, 2);
            bits += Math.Log2(choice);
        }

        return bits;
    }

    public static int RequiredCharacters(int minEntropyBits, int alphabetSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(minEntropyBits, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(alphabetSize, 2);

        return (int)Math.Ceiling(minEntropyBits / Math.Log2(alphabetSize));
    }
}

