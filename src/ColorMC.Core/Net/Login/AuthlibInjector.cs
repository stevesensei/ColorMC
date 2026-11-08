using ColorMC.Core.Game;
using ColorMC.Core.GuiHandle;
using ColorMC.Core.Objs;
using ColorMC.Core.Objs.Login;

namespace ColorMC.Core.Net.Login;

/// <summary>
/// 外置登录
/// </summary>
public static class AuthlibInjector
{
    /// <summary>
    /// 外置登录
    /// </summary>
    /// <param name="clientToken">客户端代码</param>
    /// <param name="user">用户名</param>
    /// <param name="pass">密码</param>
    /// <param name="server">服务器地址</param>
    /// <returns></returns>
    public static async Task<LoginObj> AuthenticateAsync(string clientToken, string user, string pass, string server, ILoginGui select, CancellationToken token)
    {
        var obj = await LegacyLogin.AuthenticateAsync(server, clientToken, user, pass, true, token);

        obj.Auth.AuthType = AuthType.AuthlibInjector;
        obj.Auth.Text1 = server;

        bool needselect = false;
        if (obj.Logins != null)
        {
            if (select != null)
            {
                var index = await select.SelectAuth([.. obj.Logins.Select(item => item.UserName)]);
                if (index >= obj.Logins.Count || index < 0)
                {
                    throw new LoginException(LoginFailState.LoginAuthListEmpty, AuthState.Profile);
                }
                var item = obj.Logins[index];
                obj.Auth.UUID = item.UUID;
                obj.Auth.UserName = item.UserName;
            }
            else
            {
                var item = obj.Logins[0];
                obj.Auth.UUID = item.UUID;
                obj.Auth.UserName = item.UserName;
            }
            needselect = true;
        }

        return await LegacyLogin.RefreshAsync(server, obj.Auth, needselect, token);
    }

    /// <summary>
    /// 刷新登录
    /// </summary>
    /// <param name="obj">保存的账户</param>
    public static async Task<LoginObj> RefreshAsync(LoginObj obj, CancellationToken token)
    {
        // 暂时失效的令牌会被 validate 拒绝，但按 Yggdrasil 规范仍允许 refresh。
        // 直接刷新，由服务器决定令牌是否已彻底失效，不能用 validate 作为刷新前置条件。
        return await LegacyLogin.RefreshAsync(obj.Text1, obj, false, token);
    }
}
