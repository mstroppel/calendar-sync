namespace Rafatz.CalendarSync.Extensions;

public static class ConfigurationManagerExtension
{
    public static T GetValueOrThrow<T>(this IConfiguration configuration, string key)
    {
        var value = configuration.GetValue<T>(key);
        if (value == null)
        {
            throw new InvalidOperationException($"Configuration value for key '{key}' is missing or invalid.");
        }

        return value;
    }
}