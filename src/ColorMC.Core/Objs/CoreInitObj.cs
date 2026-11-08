namespace ColorMC.Core.Objs;

/// <summary>
/// 核心初始化参数
/// </summary>
public record CoreInitObj
{
    /// <summary>
    /// 运行的路径
    /// </summary>
    public required string Local;
    /// <summary>是否加载/保存核心自己的账户库。已有账户存储的嵌入方可关闭，避免重复保存登录凭据。</summary>
    public bool UseAuthDatabase = true;
    /// <summary>
    /// OAuth客户端密钥
    /// </summary>
    public string? OAuthKey;
    /// <summary>
    /// CurseForge客户端密钥
    /// </summary>
    public string? CurseForgeKey;
}
