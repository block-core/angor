namespace Angor.Primitives;

public static class Guard
{
    public static void NotNull(object? value, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);
    }

    public static void NotEmpty(string? value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrEmpty(value, parameterName);
    }
}
