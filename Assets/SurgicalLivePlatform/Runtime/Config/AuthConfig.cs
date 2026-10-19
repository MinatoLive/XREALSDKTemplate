using System;

namespace SurgicalLivePlatform.Runtime.Config
{
    /// <summary>
    /// 身份验证凭据及网络运行环境全局配置中心 (Single Source of Truth)
    /// <para>统一管理服务端基地址、默认凭据与发布环境权限控制。</para>
    /// </summary>
    public static class AuthConfig
    {
        #region 基础默认凭据 (Default Credentials)

        /// <summary>
        /// 预设测试账号 (仅供本地联调或快速开发模式使用)
        /// </summary>
        public const string DefaultTestAccount = "test1";

        /// <summary>
        /// 预设测试密码 (仅供本地联调或快速开发模式使用)
        /// </summary>
        public const string DefaultTestPassword = "12345678";

        #endregion

        #region 服务器基地址配置 (Server Base URL Configuration)

        /// <summary>
        /// 全局统一服务器默认根基地址 (唯一真实数据源)
        /// <para>包含协议与端口，不含尾部斜杠，例如：http://10.72.128.250:8081</para>
        /// </summary>
        public const string DefaultBaseUrl = "https://10.72.128.250:8444";

        private static string s_baseUrlOverride = null;
        private static string s_staticAssetBaseUrlOverride = null;

        /// <summary>
        /// 全局统一服务器基地址。
        /// <para>支持在运行时通过设置界面或业务配置进行动态覆盖与多环境无缝切换。</para>
        /// </summary>
        public static string BaseUrl
        {
            get => s_baseUrlOverride ?? DefaultBaseUrl;
            set => s_baseUrlOverride = value;
        }

        /// <summary>
        /// 业务 API 访问端点基地址。
        /// <para>自动在 <see cref="BaseUrl"/> 基础上智能挂载 /api/v1，内置前缀防重与斜杠自动修剪。</para>
        /// </summary>
        public static string ApiBaseUrl
        {
            get
            {
                string root = BaseUrl.TrimEnd('/');
                return root.EndsWith("/api/v1", StringComparison.OrdinalIgnoreCase)
                    ? root
                    : root + "/api/v1";
            }
            set
            {
                if (!string.IsNullOrEmpty(value) && value.EndsWith("/api/v1", StringComparison.OrdinalIgnoreCase))
                {
                    BaseUrl = value.Substring(0, value.Length - "/api/v1".Length).TrimEnd('/');
                }
                else
                {
                    BaseUrl = value;
                }
            }
        }

        /// <summary>
        /// 静态资源服务器访问端点基地址 (托管 /presets 高清原图等)。
        /// <para>默认直接继承 <see cref="BaseUrl"/>；同时支持设置特定独立 CDN 域名进行定制覆盖。</para>
        /// </summary>
        public static string StaticAssetBaseUrl
        {
            get => s_staticAssetBaseUrlOverride ?? BaseUrl.TrimEnd('/');
            set => s_staticAssetBaseUrlOverride = value;
        }

        #endregion

        #region 运行环境与安全策略 (Environment & Security Policy)

        /// <summary>
        /// 获取当前是否处于开发调试环境。
        /// <para>在 Unity Editor 或 Development Build 开启下为 true；在正式 Release 构建下严格为 false。</para>
        /// </summary>
        public static bool IsDevelopmentEnvironment
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return true;
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// 获取登录界面初始凭据。
        /// <para>在开发或联调环境下提供预设测试账号，减少重复输入开销；在正式发布环境中强制留空，杜绝安全合规隐患。</para>
        /// </summary>
        /// <returns>返回包含 (account, password) 的元组；生产环境下均为空字符串</returns>
        public static (string account, string password) GetDefaultCredentials()
        {
            if (IsDevelopmentEnvironment)
            {
                return (DefaultTestAccount, DefaultTestPassword);
            }

            return (string.Empty, string.Empty);
        }

        /// <summary>
        /// 重置所有动态覆盖配置，恢复至系统默认出厂基地址设置
        /// </summary>
        public static void ResetToDefault()
        {
            s_baseUrlOverride = null;
            s_staticAssetBaseUrlOverride = null;
        }

        #endregion
    }
}
