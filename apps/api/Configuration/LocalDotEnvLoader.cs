namespace Api.Configuration;

public static class LocalDotEnvLoader
{
    public static void LoadIfPresent(
        string environmentName,
        string startDirectory)
    {
        if (!environmentName.Equals(
                "Development",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var repositoryRoot = FindRepositoryRoot(startDirectory);
        if (repositoryRoot is null)
        {
            return;
        }

        var envFilePath = Path.Combine(repositoryRoot, ".env");
        if (File.Exists(envFilePath))
        {
            DotNetEnv.Env.Load(envFilePath);
        }
    }

    private static string? FindRepositoryRoot(string startDirectory)
    {
        for (var directory = new DirectoryInfo(startDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var gitPath = Path.Combine(directory.FullName, ".git");
            if (Directory.Exists(gitPath) || File.Exists(gitPath))
            {
                return directory.FullName;
            }
        }

        return null;
    }
}