using System;
using System.Buffers;

static class ValidationHelpers
{
    public static string SanitizeName(string source)
    {
        int length = source.Length;
        char[]? rentedFromPool = null;

        Span<char> buffer =
            length > 64
                ? (rentedFromPool = ArrayPool<char>.Shared.Rent(length))
                : stackalloc char[64];

        int index = 0;
        bool different = false;

        foreach (char c in source)
        {
            if (IsValid(c))
            {
                buffer[index] = c;
            }
            else
            {
                different = true;
                buffer[index] = '_';
            }

            index++;
        }

        if (!different)
        {
            if (rentedFromPool is not null)
                ArrayPool<char>.Shared.Return(rentedFromPool, clearArray: true);

            return source;
        }

        string data = buffer[..index].ToString();

        if (rentedFromPool is not null)
            ArrayPool<char>.Shared.Return(rentedFromPool, clearArray: true);

        return data;
    }

    public static bool IsValid(this char c) =>
        c switch
        {
            '.' or '`' or '/' or '|' or '+' or '<' or '>' or '$' => false,
            _ => true,
        };
}
