namespace Digital.Net.Lib.Environment;

public static class AspNetEnv
{
    public const string Test = "Test";
    public const string Development = "Development";

    /// <summary>
    ///     Get the current environment of the application.
    /// </summary>
    public static string Get =>
        System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? Development;

    /// <summary>
    ///     Set the current environment of the application.
    /// </summary>
    /// <param name="environment">The environment to set.</param>
    public static void Set(string environment) =>
        System.Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", environment);

    /// <summary>
    ///     Check if the current environment is "Test".
    /// </summary>
    public static bool IsTest => Get == Test;

    /// <summary>
    ///     Check if the current environment is "Development".
    /// </summary>
    public static bool IsDevelopment => Get == Development;
}