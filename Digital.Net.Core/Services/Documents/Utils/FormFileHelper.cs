using Digital.Net.Lib.Random;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace Digital.Net.Core.Services.Documents.Utils;

public static class FormFileHelper
{
    public const int DefaultCompressionQuality = 75;

    public static JpegEncoder GetJpegEncoder(int? quality = null) =>
        new() { Quality = quality ?? DefaultCompressionQuality };

    public static string GenerateFileName() =>
        Randomizer.GenerateRandomString(Randomizer.AnyLetterOrNumber, 60);
}