using System.Text.RegularExpressions;

namespace Digital.Net.Lib.String;

public static partial class RegularExpressions
{
    [GeneratedRegex(UsernamePattern)]
    private static partial Regex UsernameRegex();
    public const string UsernamePattern = @"^[a-zA-Z0-9_-]{6,24}$";
    public static Regex Username => UsernameRegex();

    [GeneratedRegex(EmailPattern)]
    private static partial Regex EmailRegex();
    public const string EmailPattern = @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$";
    public static Regex Email => EmailRegex();

    [GeneratedRegex(PasswordPattern)]
    private static partial Regex PasswordRegex();
    public const string PasswordPattern = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^\da-zA-Z]).{12,128}$";

    public static Regex Password => PasswordRegex();

    [GeneratedRegex(ApiKeyNamePattern)]
    private static partial Regex ApiKeyNameRegex();
    public const string ApiKeyNamePattern = @"^[a-zA-Z0-9 _-]{1,64}$";
    public static Regex ApiKeyName => ApiKeyNameRegex();

    [GeneratedRegex(PagePathPattern)]
    private static partial Regex PagePathRegex();
    public const string PagePathPattern = @"^(/|(/(:[a-zA-Z_][a-zA-Z0-9_]*|[a-zA-Z0-9_\-.~]+))+)$";
    public static Regex PagePath => PagePathRegex();

    [GeneratedRegex(ArticleSlugPattern)]
    private static partial Regex ArticleSlugRegex();

    public const string ArticleSlugPattern = @"^[a-zA-Z0-9_\-~]+$";
    public static Regex ArticleSlug => ArticleSlugRegex();

    [GeneratedRegex(MediaLabelPattern)]
    private static partial Regex MediaLabelRegex();

    public const string MediaLabelPattern = @"^[a-z0-9-]+$";
    public static Regex MediaLabel => MediaLabelRegex();

    [GeneratedRegex(ConfigValueNamePattern)]
    private static partial Regex ConfigValueNameRegex();

    public const string ConfigValueNamePattern = @"^[a-z0-9:._-]{1,128}$";
    public static Regex ConfigValueName => ConfigValueNameRegex();

    [GeneratedRegex("(?<!^)([A-Z])")]
    public static partial Regex PascalCase();

    [GeneratedRegex("(?<!^)([.])")]
    public static partial Regex ObjectName();
}

