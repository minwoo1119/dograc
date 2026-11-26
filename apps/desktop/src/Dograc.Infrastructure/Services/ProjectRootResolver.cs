namespace Dograc.Infrastructure.Services;

/// <summary>
/// Resolves the dograc runtime root directory across packaged end-user installs and development checkouts.
/// Packaged installs bundle apps/api directly inside AppContext.BaseDirectory (zero Git repo dependency).
/// </summary>
public static class ProjectRootResolver
{
    public static string ResolveProjectRoot()
    {
        var envRoot = Environment.GetEnvironmentVariable("DOGRAC_ROOT");
        if (!string.IsNullOrWhiteSpace(envRoot) && IsValidProjectRoot(envRoot))
        {
            return Path.GetFullPath(envRoot);
        }

        // 1. Packaged end-user install: apps/api is bundled right next to dograc.exe
        if (IsValidProjectRoot(AppContext.BaseDirectory))
        {
            return Path.GetFullPath(AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        // 2. Development checkout: walk up from BaseDirectory or CWD
        foreach (var startDir in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var found = SearchUpwards(startDir);
            if (found is not null)
            {
                return found;
            }
        }

        return Path.GetFullPath(AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    public static bool IsPackagedInstall(string projectRoot)
    {
        if (string.IsNullOrWhiteSpace(projectRoot))
        {
            return false;
        }

        var fullRoot = Path.GetFullPath(projectRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var baseDir = Path.GetFullPath(AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(fullRoot, baseDir, StringComparison.OrdinalIgnoreCase) &&
               !Directory.Exists(Path.Combine(fullRoot, ".git"));
    }

    public static bool IsValidProjectRoot(string directory)
        => !string.IsNullOrWhiteSpace(directory) &&
           Directory.Exists(Path.Combine(directory, "apps", "api"));

    private static string? SearchUpwards(string startDirectory)
    {
        if (string.IsNullOrWhiteSpace(startDirectory) || !Directory.Exists(startDirectory))
        {
            return null;
        }

        var current = new DirectoryInfo(startDirectory);
        for (var depth = 0; depth < 10 && current is not null; depth++)
        {
            if (IsValidProjectRoot(current.FullName))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return null;
    }
}
