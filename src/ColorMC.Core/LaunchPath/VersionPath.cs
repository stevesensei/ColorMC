using System.Collections.Concurrent;
using ColorMC.Core.Config;
using ColorMC.Core.Helpers;
using ColorMC.Core.Net;
using ColorMC.Core.Net.Apis;
using ColorMC.Core.Objs;
using ColorMC.Core.Objs.Loader;
using ColorMC.Core.Objs.Minecraft;
using ColorMC.Core.Objs.OptiFine;
using ColorMC.Core.Utils;

namespace ColorMC.Core.LaunchPath;

/// <summary>
/// 版本路径
/// </summary>
public static class VersionPath
{
    private static readonly ConcurrentDictionary<string, Cache> s_caches = new(
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    private static Cache s_default = null!;
    private static Cache Current => MinecraftPath.Resources?.Versions ?? s_default;

    /// <summary>
    /// 版本缓存路径
    /// </summary>
    public static string BaseDir => Current.BaseDir;

    //加载器缓存路径
    public static string ForgeDir => Current.ForgeDir;
    public static string FabricDir => Current.FabricDir;
    public static string QuiltDir => Current.QuiltDir;
    public static string NeoForgeDir => Current.NeoForgeDir;

    public static VersionObj? Version => Current.Version;

    /// <summary>
    /// 获取游戏版本列表
    /// </summary>
    /// <returns></returns>
    public static async Task<VersionObj?> GetVersionsAsync()
    {
        if (Current.Version == null)
        {
            await ReadVersionsAsync();
        }
        return Current.Version;
    }

    /// <summary>
    /// 初始化
    /// </summary>
    /// <param name="dir">运行的路径</param>
    public static void Init(string dir) => s_default = GetCache(dir);

    internal static Cache GetCache(string directory) =>
        s_caches.GetOrAdd(Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory)), static path => new Cache(path));

    internal sealed class Cache
    {
        internal readonly Dictionary<string, GameArgObj> GameArgs = [];
        internal readonly Dictionary<string, ForgeInstallObj> ForgeInstalls = [];
        internal readonly Dictionary<string, ForgeInstallObj> NeoForgeInstalls = [];
        internal readonly Dictionary<string, ForgeLaunchObj> ForgeLaunches = [];
        internal readonly Dictionary<string, ForgeLaunchObj> NeoForgeLaunches = [];
        internal readonly Dictionary<string, FabricLoaderObj> FabricLoaders = [];
        internal readonly Dictionary<string, QuiltLoaderObj> QuiltLoaders = [];
        internal readonly Dictionary<Guid, CustomLoaderObj> CustomLoaders = [];
        internal readonly Dictionary<string, OptifineObj> OptifineLoaders = [];
        internal VersionObj? Version;

        internal string BaseDir { get; }
        internal string ForgeDir { get; }
        internal string FabricDir { get; }
        internal string QuiltDir { get; }
        internal string NeoForgeDir { get; }
        internal string OptifineFile { get; }

        internal Cache(string directory)
        {
            BaseDir = Path.Combine(directory, Names.NameVersionDir);
            ForgeDir = Directory.CreateDirectory(Path.Combine(BaseDir, Names.NameForgeKey)).FullName;
            NeoForgeDir = Directory.CreateDirectory(Path.Combine(BaseDir, Names.NameNeoForgedKey)).FullName;
            FabricDir = Directory.CreateDirectory(Path.Combine(BaseDir, Names.NameFabricKey)).FullName;
            QuiltDir = Directory.CreateDirectory(Path.Combine(BaseDir, Names.NameQuiltKey)).FullName;
            OptifineFile = Path.Combine(BaseDir, Names.NameOptifineFile);

            if (!File.Exists(OptifineFile))
                return;

            try
            {
                using var stream = PathHelper.OpenRead(OptifineFile);
                OptifineLoaders = JsonUtils.ToObj(stream, JsonType.DictionaryStringOptifineObj) ?? [];
            }
            catch
            {
            }
        }
    }

    /// <summary>
    /// 保存高清修复信息
    /// </summary>
    private static void SaveOptifine()
    {
        var file = Current.OptifineFile;
        ConfigSave.AddItem(ConfigSaveObj.Build(file, file, Current.OptifineLoaders, JsonType.DictionaryStringOptifineObj));
    }

    /// <summary>
    /// 从在线获取版本信息
    /// </summary>
    /// <returns></returns>
    public static async Task GetFromWebAsync()
    {
        using var res = await GameAPI.GetVersionsAsync();
        if (res != null && res.Version != null)
        {
            Current.Version = res.Version;
            SaveVersions(res.Text);
            return;
        }
        using var res1 = await GameAPI.GetVersionsAsync(SourceLocal.Offical);
        if (res1 != null && res1.Version != null)
        {
            Current.Version = res1.Version;
            SaveVersions(res1.Text);
            return;
        }

        ColorMCCore.OnError(new MojangGetVersionErrorEventArgs());
    }

    /// <summary>
    /// 是否存在版本信息
    /// </summary>
    /// <returns>结果</returns>
    public static async Task<bool> IsHaveVersionInfoAsync()
    {
        return await GetVersionsAsync() != null;
    }

    /// <summary>
    /// 读取版本信息
    /// </summary>
    private static async Task ReadVersionsAsync()
    {
        string file = Path.Combine(BaseDir, Names.NameVersionFile);
        if (File.Exists(file))
        {
            using var stream = PathHelper.OpenRead(file);
            Current.Version = JsonUtils.ToObj(stream, JsonType.VersionObj);
        }
        else
        {
            await GetFromWebAsync();
        }
    }

    /// <summary>
    /// 保存版本信息
    /// </summary>
    public static void SaveVersions(MemoryStream data)
    {
        var file = Path.Combine(BaseDir, Names.NameVersionFile);
        PathHelper.WriteBytes(file, data);
        data.Dispose();
    }

    /// <summary>
    /// 添加版本信息
    /// </summary>
    /// <param name="obj">游戏数据</param>
    public static async Task<GameArgObj?> AddGameAsync(VersionObj.VersionsObj obj)
    {
        var url = UrlHelper.DownloadSourceChange(obj.Url, CoreHttpClient.Source);
        using var res = await GameAPI.GetGameAsync(url);
        if (res == null || res.Arg == null)
        {
            return null;
        }
        PathHelper.WriteBytes(Path.Combine(BaseDir, $"{obj.Id}.json"), res.Text);
        return res.Arg;
    }

    /// <summary>
    /// 保存Fabric-Loader信息
    /// </summary>
    /// <param name="obj">信息</param>
    /// <param name="mc">游戏版本</param>
    /// <param name="version">加载器版本</param>
    public static void AddGame(FabricLoaderObj obj, Stream array, string mc, string version)
    {
        PathHelper.WriteBytes(Path.Combine(FabricDir, $"{obj.Id}.json"), array);

        var key = $"{mc}-{version}";
        if (!Current.FabricLoaders.TryAdd(key, obj))
        {
            Current.FabricLoaders[key] = obj;
        }
    }

    /// <summary>
    /// 添加Forge启动信息
    /// </summary>
    /// <param name="obj">信息</param>
    /// <param name="array">文本</param>
    /// <param name="mc">游戏版本</param>
    /// <param name="version">加载器版本</param>
    /// <param name="neo">是否为NeoForge</param>
    public static void AddGame(ForgeLaunchObj obj, Stream array, string mc, string version, bool neo)
    {
        var v2222 = CheckHelpers.IsGameVersion1202(mc);
        string name;
        if (neo && v2222)
        {
            name = $"neoforge-{version}";
        }
        else
        {
            name = $"forge-{mc}-{version}";
        }
        PathHelper.WriteBytes(Path.Combine(neo ? NeoForgeDir : ForgeDir, $"{name}.json"), array);

        var key = $"{mc}-{version}";
        if (neo)
        {
            if (!Current.NeoForgeLaunches.TryAdd(key, obj))
            {
                Current.NeoForgeLaunches[key] = obj;
            }
        }
        else
        {
            if (!Current.ForgeLaunches.TryAdd(key, obj))
            {
                Current.ForgeLaunches[key] = obj;
            }
        }
    }

    /// <summary>
    /// 添加Forge安装信息
    /// </summary>
    /// <param name="obj">信息</param>
    /// <param name="array">文本</param>
    /// <param name="mc">游戏版本</param>
    /// <param name="version">加载器版本</param>
    /// <param name="neo">是否为NeoForge</param>
    public static void AddGame(ForgeInstallObj obj, Stream array, string mc, string version, bool neo)
    {
        var v2222 = CheckHelpers.IsGameVersion1202(mc);
        string name;
        if (neo && v2222)
        {
            name = $"neoforge-{version}-install";
        }
        else
        {
            name = $"forge-{mc}-{version}-install";
        }

        PathHelper.WriteBytes(Path.Combine(neo ? NeoForgeDir : ForgeDir, $"{name}.json"), array);

        var key = $"{mc}-{version}";
        if (neo)
        {
            if (!Current.NeoForgeInstalls.TryAdd(key, obj))
            {
                Current.NeoForgeInstalls[key] = obj;
            }
        }
        else
        {
            if (!Current.ForgeInstalls.TryAdd(key, obj))
            {
                Current.ForgeInstalls[key] = obj;
            }
        }
    }

    /// <summary>
    /// 添加Quilt信息
    /// </summary>
    /// <param name="obj">Quilt加载器数据</param>
    /// <param name="data">数据内容</param>
    /// <param name="mc">游戏版本</param>
    /// <param name="version">加载器版本</param>
    public static void AddGame(QuiltLoaderObj obj, Stream data, string mc, string version)
    {
        PathHelper.WriteBytes(Path.Combine(QuiltDir, $"{obj.Id}.json"), data);

        var key = $"{mc}-{version}";
        if (!Current.QuiltLoaders.TryAdd(key, obj))
        {
            Current.QuiltLoaders[key] = obj;
        }
    }

    /// <summary>
    /// 添加自定义加载器信息
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="uuid"></param>
    public static void AddGame(CustomLoaderObj obj, Guid uuid)
    {
        if (!Current.CustomLoaders.TryAdd(uuid, obj))
        {
            Current.CustomLoaders[uuid] = obj;
        }
    }

    /// <summary>
    /// 添加高清修复信息
    /// </summary>
    /// <param name="obj">高清修复信息</param>
    public static void AddOptifine(OptifineObj obj)
    {
        if (!Current.OptifineLoaders.TryAdd(obj.Version, obj))
        {
            Current.OptifineLoaders[obj.Version] = obj;
        }
        SaveOptifine();
    }

    /// <summary>
    /// 获取版本信息
    /// </summary>
    /// <param name="version">游戏版本</param>
    /// <returns>游戏数据</returns>
    public static GameArgObj? GetVersion(string version)
    {
        if (Current.GameArgs.TryGetValue(version, out var temp))
        {
            return temp;
        }
        string file = Path.Combine(BaseDir, $"{version}.json");

        if (!File.Exists(file))
        {
            return null;
        }

        using var stream = PathHelper.OpenRead(file);
        var obj = JsonUtils.ToObj(stream, JsonType.GameArgObj);
        if (obj == null)
        {
            return null;
        }
        Current.GameArgs.Add(version, obj);
        return obj;
    }

    /// <summary>
    /// 更新json
    /// </summary>
    /// <param name="version">游戏版本</param>
    public static async Task<GameArgObj?> CheckUpdateAsync(string version)
    {
        var ver = await GetVersionsAsync();
        if (ver == null)
        {
            return null;
        }
        var data = ver.Versions.FirstOrDefault(a => a.Id == version);
        if (data != null)
        {
            var file = Path.Combine(BaseDir, $"{version}.json");
            var temp = PathHelper.OpenRead(file);
            if (temp == null)
            {
                return null;
            }
            var sha1 = await HashHelper.GenSha1Async(temp);
            temp.Close();
            if (sha1 != data.Sha1)
            {
                return await AddGameAsync(data);
            }

            return GetVersion(version);
        }

        return null;
    }

    /// <summary>
    /// 获取Forge安装数数据
    /// </summary>
    /// <param name="mc">游戏版本</param>
    /// <param name="version">forge版本</param>
    /// <returns>Forge安装数据</returns>
    public static ForgeInstallObj? GetNeoForgeInstallObj(string mc, string version)
    {
        var key = $"{mc}-{version}";
        if (Current.NeoForgeInstalls.TryGetValue(key, out var temp))
        {
            return temp;
        }

        var v2222 = CheckHelpers.IsGameVersion1202(mc);
        string file = v2222
            ? Path.Combine(NeoForgeDir, $"neoforge-{version}-install.json")
            : Path.Combine(NeoForgeDir, $"forge-{mc}-{version}-install.json");

        if (!File.Exists(file))
        {
            return null;
        }

        using var stream = PathHelper.OpenRead(file);
        var obj = JsonUtils.ToObj(stream, JsonType.ForgeInstallObj);
        if (obj == null)
        {
            return null;
        }
        Current.NeoForgeInstalls.Add(key, obj);
        return obj;
    }

    /// <summary>
    /// 获取Forge启动数据
    /// </summary>
    /// <param name="obj">游戏实例</param>
    /// <returns>启动数据</returns>
    public static ForgeLaunchObj? GetNeoForgeObj(this GameSettingObj obj)
    {
        return GetNeoForgeObj(obj.Version, obj.LoaderVersion!);
    }

    /// <summary>
    /// 获取Forge启动数据
    /// </summary>
    /// <param name="mc">游戏版本</param>
    /// <param name="version">forge版本</param>
    /// <returns>启动数据</returns>
    public static ForgeLaunchObj? GetNeoForgeObj(string mc, string version)
    {
        var key = $"{mc}-{version}";
        if (Current.NeoForgeLaunches.TryGetValue(key, out var temp))
        {
            return temp;
        }

        string file = CheckHelpers.IsGameVersion1202(mc)
            ? Path.Combine(NeoForgeDir, $"neoforge-{version}.json")
            : Path.Combine(NeoForgeDir, $"forge-{mc}-{version}.json");

        if (!File.Exists(file))
        {
            return null;
        }

        using var stream = PathHelper.OpenRead(file);
        var obj = JsonUtils.ToObj(stream, JsonType.ForgeLaunchObj);
        if (obj == null)
        {
            return null;
        }
        Current.NeoForgeLaunches.Add(key, obj);
        return obj;
    }

    /// <summary>
    /// 获取Forge安装数数据
    /// </summary>
    /// <param name="mc">游戏版本</param>
    /// <param name="version">forge版本</param>
    /// <returns>Forge安装数据</returns>
    public static ForgeInstallObj? GetForgeInstallObj(string mc, string version)
    {
        var key = $"{mc}-{version}";
        string file = Path.Combine(ForgeDir, $"forge-{mc}-{version}-install.json");
        if (Current.NeoForgeInstalls.TryGetValue(key, out var temp))
        {
            return temp;
        }

        if (!File.Exists(file))
        {
            return null;
        }

        using var stream = PathHelper.OpenRead(file);
        var obj = JsonUtils.ToObj(stream, JsonType.ForgeInstallObj);
        if (obj == null)
        {
            return null;
        }
        Current.NeoForgeInstalls.Add(key, obj);
        return obj;
    }

    /// <summary>
    /// 获取Forge启动数据
    /// </summary>
    /// <param name="obj">游戏实例</param>
    /// <returns>启动数据</returns>
    public static ForgeLaunchObj? GetForgeObj(this GameSettingObj obj)
    {
        return GetForgeObj(obj.Version, obj.LoaderVersion!);
    }

    /// <summary>
    /// 获取Forge启动数据
    /// </summary>
    /// <param name="mc">游戏版本</param>
    /// <param name="version">forge版本</param>
    /// <returns>启动数据</returns>
    public static ForgeLaunchObj? GetForgeObj(string mc, string version)
    {
        var key = $"{mc}-{version}";
        if (Current.ForgeLaunches.TryGetValue(key, out var temp))
        {
            return temp;
        }
        string file = Path.Combine(ForgeDir, $"forge-{mc}-{version}.json");

        if (!File.Exists(file))
        {
            return null;
        }

        using var stream = PathHelper.OpenRead(file);
        var obj = JsonUtils.ToObj(stream, JsonType.ForgeLaunchObj);
        if (obj == null)
        {
            return null;
        }
        Current.ForgeLaunches.Add(key, obj);
        return obj;
    }

    /// <summary>
    /// 获取Fabric数据
    /// </summary>
    /// <param name="obj">游戏实例</param>
    /// <returns>数据</returns>
    public static FabricLoaderObj? GetFabricObj(this GameSettingObj obj)
    {
        return GetFabricObj(obj.Version, obj.LoaderVersion!);
    }

    /// <summary>
    /// 获取Fabric数据
    /// </summary>
    /// <param name="mc">游戏版本</param>
    /// <param name="version">fabric版本</param>
    /// <returns>数据</returns>
    public static FabricLoaderObj? GetFabricObj(string mc, string version)
    {
        var key = $"{mc}-{version}";
        if (Current.FabricLoaders.TryGetValue(key, out var temp))
        {
            return temp;
        }
        string file = Path.Combine(FabricDir, $"fabric-loader-{version}-{mc}.json");

        if (!File.Exists(file))
        {
            return null;
        }

        using var stream = PathHelper.OpenRead(file);
        var obj = JsonUtils.ToObj(stream, JsonType.FabricLoaderObj);
        if (obj == null)
        {
            return null;
        }
        Current.FabricLoaders.Add(key, obj);
        return obj;
    }

    /// <summary>
    /// 获取Quilt数据
    /// </summary>
    /// <param name="obj">游戏实例</param>
    /// <returns>数据</returns>
    public static QuiltLoaderObj? GetQuiltObj(this GameSettingObj obj)
    {
        return GetQuiltObj(obj.Version, obj.LoaderVersion!);
    }

    /// <summary>
    /// 获取Quilt数据
    /// </summary>
    /// <param name="mc">游戏版本</param>
    /// <param name="version">quilt版本</param>
    /// <returns>数据</returns>
    public static QuiltLoaderObj? GetQuiltObj(string mc, string version)
    {
        var key = $"{mc}-{version}";
        if (Current.QuiltLoaders.TryGetValue(key, out var temp))
        {
            return temp;
        }
        string file = Path.Combine(QuiltDir, $"quilt-loader-{version}-{mc}.json");

        if (!File.Exists(file))
        {
            return null;
        }

        using var stream = PathHelper.OpenRead(file);
        var obj = JsonUtils.ToObj(stream, JsonType.QuiltLoaderObj);
        if (obj == null)
        {
            return null;
        }
        Current.QuiltLoaders.Add(key, obj);
        return obj;
    }

    /// <summary>
    /// 获取自定义加载器数据
    /// </summary>
    /// <param name="uuid">游戏实例</param>
    /// <returns></returns>
    public static CustomLoaderObj? GetCustomLoaderObj(this GameSettingObj obj)
    {
        if (Current.CustomLoaders.TryGetValue(obj.UUID, out var temp))
        {
            return temp;
        }

        return null;
    }

    /// <summary>
    /// 获取高清修复信息
    /// </summary>
    /// <param name="version">版本号</param>
    /// <returns></returns>
    public static OptifineObj? GetOptifine(string version)
    {
        if (Current.OptifineLoaders.TryGetValue(version, out var temp))
        {
            return temp;
        }

        return null;
    }

    /// <summary>
    /// 获取高清修复信息
    /// </summary>
    /// <param name="obj">游戏实例</param>
    /// <returns></returns>
    public static OptifineObj? GetOptifine(this GameSettingObj obj)
    {
        if (obj.LoaderVersion == null || obj.Loader != Loaders.OptiFine)
        {
            return null;
        }

        return GetOptifine(obj.LoaderVersion);
    }
}
