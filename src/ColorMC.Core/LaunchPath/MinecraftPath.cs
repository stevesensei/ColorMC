namespace ColorMC.Core.LaunchPath;

/// <summary>
/// 游戏路径
/// </summary>
public static class MinecraftPath
{
    private static readonly AsyncLocal<ResourcePaths?> s_resources = new();

    internal static ResourcePaths? Resources => s_resources.Value;

    /// <summary>
    /// 游戏路径
    /// </summary>
    public static string BaseDir { get; private set; }

    /// <summary>
    /// 初始化
    /// </summary>
    /// <param name="dir">运行的路径</param>
    public static void Init(string dir)
    {
        BaseDir = Path.Combine(dir, Names.NameMinecraftKey);

        Directory.CreateDirectory(BaseDir);

        AssetsPath.Init(BaseDir);
        LibrariesPath.Init(BaseDir);
        InstancesPath.Init(BaseDir);
        VersionPath.Init(BaseDir);
    }

    /// <summary>
    /// Selects the shared game files for this launch without reinitializing the core.
    /// </summary>
    public static IDisposable UseResourceDirectory(string directory)
    {
        if (!Path.IsPathFullyQualified(directory))
            throw new ArgumentException("Expected an absolute Minecraft resource directory.", nameof(directory));

        var scope = new ResourceScope(s_resources.Value);
        s_resources.Value = new ResourcePaths(
            new AssetsPath.Paths(directory), new LibrariesPath.Paths(directory), VersionPath.GetCache(directory));
        return scope;
    }

    internal sealed record ResourcePaths(AssetsPath.Paths Assets, LibrariesPath.Paths Libraries, VersionPath.Cache Versions);

    private sealed class ResourceScope(ResourcePaths? previous) : IDisposable
    {
        public void Dispose() => s_resources.Value = previous;
    }
}
