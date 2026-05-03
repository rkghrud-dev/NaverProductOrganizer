namespace NaverProductOrganizer;

internal static class AppPaths
{
    public static string ProjectRoot { get; } = FindProjectRoot();
    public static string DataDirectory { get; } = Path.Combine(ProjectRoot, "data");
    public static string ExportDirectory { get; } = Path.Combine(DataDirectory, "exports");
    public static string DatabasePath { get; } = Path.Combine(DataDirectory, "products.sqlite");
    public static string DefaultKeyDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "key");

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(ExportDirectory);
    }

    private static string FindProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (directory.GetFiles("NaverProductOrganizer.csproj").Any())
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            "프로젝트",
            "NaverProductOrganizer");
    }
}
