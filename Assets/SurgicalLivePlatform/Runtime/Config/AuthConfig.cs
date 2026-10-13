using System;

namespace SurgicalLivePlatform.Runtime.Config
{
    /// <summary>
    /// 身份验证凭据及运行环境配置提供器
    /// 统一管理内置默认账号密码与发布环境权限控制。
    /// 默认账密仅供本地测试与开发用途；在正式发布环境中强制留空，要求用户手动输入。
    /// </summary>
    public static class AuthConfig
    {
        /// <summary>
        /// 预设测试账号 (以 UserLoginUIController 规范为准)
        /// </summary>
        public const string DefaultTestAccount = "test1";

        /// <summary>
        /// 预设测试密码 (以 UserLoginUIController 规范为准)
        /// </summary>
        public const string DefaultTestPassword = "12345678";

        private static bool? s_devModeOverride = null;

        /// <summary>
        /// 当前是否为开发/测试环境。
        /// 在 Unity Editor 或 Development Build 构建下为 true；Release 构建下为 false。
        /// 支持通过单元测试注入覆盖。
        /// </summary>
        public static bool IsDevelopmentEnvironment
        {
            get
            {
                if (s_devModeOverride.HasValue)
                {
                    return s_devModeOverride.Value;
                }

                return true;

            }
        }

        /// <summary>
        /// 获取默认凭据。
        /// 若处于开发/测试模式，返回预设的测试账号和密码；
        /// 若处于正式上线环境，严格返回空字符串，确保登录界面不预填任何敏感信息。
        /// </summary>
        public static (string account, string password) GetDefaultCredentials()
        {
            if (IsDevelopmentEnvironment)
            {
                return (DefaultTestAccount, DefaultTestPassword);
            }

            return (string.Empty, string.Empty);
        }

        /// <summary>
        /// 仅用于单元测试和调试：覆盖开发模式判定
        /// </summary>
        public static void SetDevelopmentModeForTesting(bool? isDev)
        {
            s_devModeOverride = isDev;
        }

        /// <summary>
        /// 重置所有测试覆盖
        /// </summary>
        public static void ResetTestingOverrides()
        {
            s_devModeOverride = null;
        }
    }
}
