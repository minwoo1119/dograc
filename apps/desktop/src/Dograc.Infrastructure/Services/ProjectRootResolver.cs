namespace Dograc.Infrastructure.Services;

/// <summary>
/// Resolves the dograc repository root directory across development and packaged execution modes.
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

        foreach (var startDir in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var found = SearchUpwards(startDir);
            if (found is not null)
            {
                return found;
            }
        }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var commonCandidates = new[]
        {
            Path.Combine(userProfile, "Documents", "Github", "dograc"),
            Path.Combine(userProfile, "Documents", "dograc"),
            Path.Combine(userProfile, "Desktop", "dograc"),
            Path.Combine(userProfile, "dograc")
        };

        foreach (var candidate in commonCandidates)
        {
            if (IsValidProjectRoot(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        return Directory.GetCurrentDirectory();
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
