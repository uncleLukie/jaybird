namespace jaybird.Utils;

using System.Reflection;

/// <summary>
/// Utility class for retrieving application version information.
/// Version is automatically derived from git tags using MinVer at build time.
/// </summary>
public static class VersionHelper
{
    private static string? _cachedVersion;

    /// <summary>
    /// Gets the current application version as a formatted string (e.g., "v1.0.6").
    /// Returns "v1.0-dev" for local development builds without a git tag.
    /// </summary>
    public static string GetVersion()
    {
        if (_cachedVersion != null)
        {
            return _cachedVersion;
        }

        try
        {
            var assembly = Assembly.GetExecutingAssembly();

            // First try InformationalVersion (set by MinVer with full version info)
            // MinVer sets AssemblyVersion to Major.Minor.0.0 for binary compatibility
            // but puts the actual version in InformationalVersion
            var infoVersionAttr = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            if (infoVersionAttr != null && !string.IsNullOrEmpty(infoVersionAttr.InformationalVersion))
            {
                var infoVersion = infoVersionAttr.InformationalVersion;

                // MinVer format: "1.0.8" or "1.0.8+git-hash" or "1.0.9-dev.1+git-hash"
                // Strip git hash if present (e.g., "1.0.8+abc123" -> "1.0.8")
                if (infoVersion.Contains('+'))
                {
                    infoVersion = infoVersion.Substring(0, infoVersion.IndexOf('+'));
                }

                // Add 'v' prefix if not present
                _cachedVersion = infoVersion.StartsWith("v") ? infoVersion : $"v{infoVersion}";
                return _cachedVersion;
            }

            // Fallback to AssemblyVersion (older behavior, may not reflect patch version)
            var version = assembly.GetName().Version;

            if (version == null)
            {
                _cachedVersion = "v1.0-dev";
                return _cachedVersion;
            }

            // Check if this is a dev build (MinVer uses 0.0.0 or 1.0.0 for untagged commits)
            if (version.Major == 0 && version.Minor == 0)
            {
                _cachedVersion = "v1.0-dev";
                return _cachedVersion;
            }

            // Format version as "v1.0.6"
            // MinVer sets: Major.Minor.Patch.Height
            // We'll display Major.Minor.Patch
            if (version.Build > 0)
            {
                _cachedVersion = $"v{version.Major}.{version.Minor}.{version.Build}";
            }
            else
            {
                _cachedVersion = $"v{version.Major}.{version.Minor}.0";
            }

            return _cachedVersion;
        }
        catch (Exception ex)
        {
            DebugLogger.LogException(ex, "VersionHelper.GetVersion");
            _cachedVersion = "v1.0-dev";
            return _cachedVersion;
        }
    }

    /// <summary>
    /// Gets the current application version without the 'v' prefix (e.g., "1.0.6").
    /// Useful for numeric version comparisons or display formats that don't use 'v'.
    /// </summary>
    public static string GetVersionWithoutPrefix()
    {
        var version = GetVersion();
        return version.StartsWith("v") ? version.Substring(1) : version;
    }

    /// <summary>
    /// Gets the informational version string which may include git commit info.
    /// Falls back to GetVersion() if informational version is not available.
    /// </summary>
    public static string GetInformationalVersion()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var infoVersionAttr = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();

            if (infoVersionAttr != null && !string.IsNullOrEmpty(infoVersionAttr.InformationalVersion))
            {
                return infoVersionAttr.InformationalVersion;
            }
        }
        catch (Exception ex)
        {
            DebugLogger.LogException(ex, "VersionHelper.GetInformationalVersion");
        }

        return GetVersion();
    }
}
