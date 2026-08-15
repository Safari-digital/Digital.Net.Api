using System.Security.Cryptography;

namespace Digital.Net.Lib.Random;

public static class Randomizer
{
    public const string CapitalLetters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    public const string SmallLetters = "abcdefghijklmnopqrstuvwxyz";
    public const string Numbers = "0123456789";
    public const string SpecialCharacters = "!@#$%^&*()";

    public static string AnyLetter => CapitalLetters + SmallLetters;
    public static string AnyLetterOrNumber => CapitalLetters + SmallLetters + Numbers;
    public static string AnyCharacter => CapitalLetters + SmallLetters + Numbers + SpecialCharacters;

    /// <summary>
    ///     Generates a random string of the specified length using the specified characters.
    ///     Default length is 128 and default characters are Randomizer.AnyCharacter.
    /// </summary>
    /// <param name="chars">The characters to use for the random string.</param>
    /// <param name="length">The length of the random string.</param>
    public static string GenerateRandomString(string? chars = null, int? length = null)
    {
        chars ??= AnyCharacter;
        length ??= RandomNumberGenerator.GetInt32(1, 128);
        return new string(RandomNumberGenerator.GetItems<char>(chars, length.Value));
    }
}