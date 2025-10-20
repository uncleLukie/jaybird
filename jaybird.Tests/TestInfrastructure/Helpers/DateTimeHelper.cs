namespace jaybird.Tests.TestInfrastructure.Helpers;

/// <summary>
/// Helper class for managing deterministic time in tests
/// </summary>
public static class DateTimeHelper
{
    private static DateTime? _fixedDateTime;

    /// <summary>
    /// Sets a fixed date time for all subsequent calls to Now
    /// </summary>
    public static void SetFixedDateTime(DateTime dateTime)
    {
        _fixedDateTime = dateTime;
    }

    /// <summary>
    /// Gets the current date time (fixed if set, otherwise actual)
    /// </summary>
    public static DateTime Now => _fixedDateTime ?? DateTime.UtcNow;

    /// <summary>
    /// Resets to using actual date time
    /// </summary>
    public static void Reset()
    {
        _fixedDateTime = null;
    }

    /// <summary>
    /// Creates a date time offset from the fixed time
    /// </summary>
    public static DateTime Offset(TimeSpan offset)
    {
        return Now.Add(offset);
    }
}