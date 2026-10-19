using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using SurgicalLivePlatform.Runtime.Auth;
using SurgicalLivePlatform.Runtime.Config;
using SurgicalLivePlatform.Runtime.DTOs;
using SurgicalLivePlatform.Runtime.Network;
using SurgicalLivePlatform.Runtime.Storage;
using SurgicalLivePlatform.Runtime.Services;

namespace SurgicalLivePlatform.Runtime
{
    /// <summary>
    /// 手术直播平台核心 SDK 统一入口 (Facade)
    /// <para>封装了底层的所有网络请求、事件分发、本地存储以及图片资源异步加载。使用该单例可以直接与平台服务端进行交互。</para>
    /// </summary>
    /// <example>
    /// <code>
    /// // 1. 初始化 SDK
    /// SurgicalLiveSDK.Initialize();
    /// 
    /// // 2. 监听事件 (可选，如 Token 过期)
    /// SurgicalLiveSDK.Events.OnAuthExpired += () => Debug.Log("登录已过期，请重新登录");
    /// 
    /// // 3. 登录账号
    /// await SurgicalLiveSDK.Auth.LoginAsync("admin", "123456");
    /// 
    /// // 4. 获取房间列表
    /// var rooms = await SurgicalLiveSDK.Auth.GetRoomInfosAsync(1, 20);
    /// 
    /// // 5. 加载图片
    /// Texture2D tex = await SurgicalLiveSDK.LoadImageWithFallbackAsync(primaryUrl, secondaryUrl);
    /// </code>
    /// </example>
    public static class SurgicalLiveSDK
    {
        private static bool _initialized = false;
        private static readonly Dictionary<string, Texture2D> _textureCache = new Dictionary<string, Texture2D>();

        #region 核心服务与事件 (Core Services & Events)

        /// <summary>
        /// 全局网络事件分发器，提供各类鉴权失败、超时等底层事件的挂载点
        /// </summary>
        public static INetworkEventDispatcher Events { get; private set; }
        
        /// <summary>
        /// 底层 API 客户端，负责处理带有 Token 的原生 HTTP 请求
        /// </summary>
        public static IApiClient ApiClient { get; private set; }
        
        /// <summary>
        /// 本地存储服务，负责 Token 和用户信息的持久化
        /// </summary>
        public static ILocalStorage Storage { get; private set; }
        
        /// <summary>
        /// 业务鉴权与核心操作代理，包含了绝大部分的 API (如登录、房间拉取、控制等)
        /// </summary>
        public static IAuthService Auth { get; private set; }

        /// <summary>
        /// 全局应用状态管理器，控制 UI 与应用状态的流转
        /// </summary>
        public static AppStateManager StateManager { get; private set; }

        #endregion

        #region 图片服务配置 (Image Service Configuration)

        /// <summary>
        /// 后端业务 API 服务器基地址，统一委托至 AuthConfig
        /// </summary>
        public static string BaseServerUrl
        {
            get => AuthConfig.ApiBaseUrl;
            set => AuthConfig.ApiBaseUrl = value;
        }

        /// <summary>
        /// 前端静态资源服务器基地址，统一委托至 AuthConfig
        /// </summary>
        public static string BaseStaticServerUrl
        {
            get => AuthConfig.StaticAssetBaseUrl;
            set => AuthConfig.StaticAssetBaseUrl = value;
        }

        /// <summary>
        /// 自签名 SSL 证书校验绕行 Handler (仅开发与内网环境使用)
        /// </summary>
        private class BypassCertificateHandler : CertificateHandler
        {
            protected override bool ValidateCertificate(byte[] certificateData) => true;
        }

        #endregion

        #region 初始化与生命周期 (Initialization & Lifecycle)

        /// <summary>
        /// 检查并保证 SDK 已初始化完成
        /// </summary>
        public static void EnsureInitialized()
        {
            if (!_initialized)
            {
                Initialize();
            }
        }

        /// <summary>
        /// 初始化整个 SDK。在调用任何网络请求前必须先调用此方法一次。
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;

            Events = new NetworkEventDispatcher();
            ApiClient = new HttpClientService(Events);
            Storage = new JsonLocalStorage();
            Auth = new AuthService(ApiClient, Storage, Events);
            StateManager = new AppStateManager();

            Events.OnAuthExpired += OnHandleAuthExpired;

            _initialized = true;
            UnityEngine.Debug.Log("[SurgicalLiveSDK] 初始化完成。");
        }

        private static void OnHandleAuthExpired()
        {
            if (StateManager != null && StateManager.CurrentState != Models.UserAppState.LoggedOut)
            {
                UnityEngine.Debug.LogWarning("[SurgicalLiveSDK] 接收到 OnAuthExpired，自动重置状态为 LoggedOut。");
                StateManager.ChangeState(Models.UserAppState.LoggedOut);
            }
        }
        
        /// <summary>
        /// 销毁 SDK 资源，关闭所有网络底层连接，释放图片缓存与句柄。
        /// </summary>
        public static void Dispose()
        {
            if (!_initialized) return;

            if (Events != null)
            {
                Events.OnAuthExpired -= OnHandleAuthExpired;
            }
            
            if (ApiClient is System.IDisposable disposableClient)
            {
                disposableClient.Dispose();
            }
            
            Events = null;
            ApiClient = null;
            Storage = null;
            Auth = null;
            StateManager = null;

            ClearImageCache();
            
            _initialized = false;
            UnityEngine.Debug.Log("[SurgicalLiveSDK] 资源已释放。");
        }

        #endregion

        #region 图片异步加载与路由解析 (Image Loading & Resolving)

        /// <summary>
        /// 智能解析完整的网络图片访问地址 (URL)。
        /// <para>解析流水线包含：</para>
        /// <list type="number">
        /// <item><description>映射历史简写预设 (如 preset:0, preset:laparoscopic) 至 /presets/*.png 资源；</description></item>
        /// <item><description>外部合法完整 URL (如第三方 CDN、图床) 自动原样保留直通；</description></item>
        /// <item><description>内部明文 HTTP 地址根据当前 <see cref="BaseStaticServerUrl"/> 规范化升级为配置端点；</description></item>
        /// <item><description>/presets/... 相对路径：精准路由至静态资源服务器 (<see cref="BaseStaticServerUrl"/>)；</description></item>
        /// <item><description>/rooms/... 或 /api/... 业务端点：精准路由至 API 代理服务器 (<see cref="BaseServerUrl"/>) 并智能保证 /api/v1 唯一前缀；</description></item>
        /// <item><description>其他未命中相对路径：默认兜底导向静态资源服务器。</description></item>
        /// </list>
        /// </summary>
        /// <param name="url">输入的图片原始路径、完整链接或预设标识字符串</param>
        /// <returns>返回可直接用于底层网络请求的规范化绝对 URL 地址；若输入为空则返回原始值</returns>
        public static string ResolveImageUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return url;

            // 1. 规范化映射历史/简写预设格式 preset:X
            if (url.StartsWith("preset:", StringComparison.OrdinalIgnoreCase))
            {
                string key = url.Substring("preset:".Length).Trim().ToLowerInvariant();
                switch (key)
                {
                    case "0":
                    case "laparoscopic":
                        url = "/presets/laparoscopic.png";
                        break;
                    case "1":
                    case "orthopedic":
                        url = "/presets/orthopedic.png";
                        break;
                    case "2":
                    case "neurosurgery":
                        url = "/presets/neurosurgery.png";
                        break;
                    case "3":
                    case "hybrid":
                    case "hybrid_angiography":
                        url = "/presets/hybrid_angiography.png";
                        break;
                    default:
                        url = "/presets/laparoscopic.png";
                        break;
                }
            }

            // 2. 绝对 URL 解析 (完全基于配置动态解析，杜绝硬编码任何 IP/域名)
            if (Uri.TryCreate(url, UriKind.Absolute, out Uri absoluteUri) &&
                (absoluteUri.Scheme == Uri.UriSchemeHttp || absoluteUri.Scheme == Uri.UriSchemeHttps))
            {
                string staticHost = !string.IsNullOrEmpty(BaseStaticServerUrl)
                    ? BaseStaticServerUrl.TrimEnd('/')
                    : AuthConfig.StaticAssetBaseUrl.TrimEnd('/');

                string apiHost = !string.IsNullOrEmpty(BaseServerUrl)
                    ? BaseServerUrl.TrimEnd('/')
                    : AuthConfig.ApiBaseUrl.TrimEnd('/');

                string configuredStaticAuthority = GetAuthority(staticHost);
                string configuredApiAuthority = GetAuthority(apiHost);

                // 匹配当前系统配置的主机 Authority (如本地测试域名或 API 服务器)
                bool isInternal = (!string.IsNullOrEmpty(configuredStaticAuthority) &&
                                   string.Equals(absoluteUri.Authority, configuredStaticAuthority, StringComparison.OrdinalIgnoreCase)) ||
                                  (!string.IsNullOrEmpty(configuredApiAuthority) &&
                                   string.Equals(absoluteUri.Authority, configuredApiAuthority, StringComparison.OrdinalIgnoreCase));

                if (isInternal)
                {
                    return staticHost + absoluteUri.PathAndQuery;
                }

                // 外部第三方合法完整 URL，保持原样返回
                return url;
            }

            // 3. 规范化相对路径
            string normalizedPath = url.StartsWith("/") ? url : "/" + url;

            // 4. 静态预设大图路由
            if (normalizedPath.StartsWith("/presets/", StringComparison.OrdinalIgnoreCase))
            {
                string staticHost = !string.IsNullOrEmpty(BaseStaticServerUrl)
                    ? BaseStaticServerUrl.TrimEnd('/')
                    : AuthConfig.StaticAssetBaseUrl.TrimEnd('/');
                return staticHost + normalizedPath;
            }

            // 5. 业务直连接口路由 (如 /rooms/{id}/preview 或 /api/v1/...)
            if (normalizedPath.StartsWith("/rooms/", StringComparison.OrdinalIgnoreCase) ||
                normalizedPath.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
            {
                string apiHost = !string.IsNullOrEmpty(BaseServerUrl)
                    ? BaseServerUrl.TrimEnd('/')
                    : AuthConfig.ApiBaseUrl.TrimEnd('/');

                if (normalizedPath.StartsWith("/rooms/", StringComparison.OrdinalIgnoreCase))
                {
                    if (!apiHost.EndsWith("/api/v1", StringComparison.OrdinalIgnoreCase))
                    {
                        normalizedPath = "/api/v1" + normalizedPath;
                    }
                }
                else if (normalizedPath.StartsWith("/api/v1/", StringComparison.OrdinalIgnoreCase))
                {
                    if (apiHost.EndsWith("/api/v1", StringComparison.OrdinalIgnoreCase))
                    {
                        normalizedPath = normalizedPath.Substring("/api/v1".Length);
                    }
                }

                return apiHost + normalizedPath;
            }

            // 6. 兜底导向静态服务器
            string defaultHost = !string.IsNullOrEmpty(BaseStaticServerUrl)
                ? BaseStaticServerUrl.TrimEnd('/')
                : AuthConfig.StaticAssetBaseUrl.TrimEnd('/');
            return defaultHost + normalizedPath;
        }

        private static string GetAuthority(string url)
        {
            if (string.IsNullOrEmpty(url)) return null;
            if (Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
            {
                return uri.Authority;
            }
            return null;
        }

        /// <summary>
        /// 异步加载指定 URL 的图片资源并解码为 Unity <see cref="Texture2D"/>。
        /// <para>具备内存缓存加速机制：已下载成功的纹理将自动加入 SDK 缓存，后续请求直接瞬间返回。</para>
        /// </summary>
        /// <param name="url">图片相对路径、绝对 URL 或预设代号</param>
        /// <param name="timeoutSeconds">网络请求超时上限时间 (秒)，默认为 5 秒</param>
        /// <returns>成功返回下载解码后的 Texture2D 实例，失败或超时返回 null</returns>
        public static async Task<Texture2D> LoadImageAsync(string url, int timeoutSeconds = 5)
        {
            if (string.IsNullOrEmpty(url))
            {
                return null;
            }

            string fullUrl = ResolveImageUrl(url);
            if (_textureCache.TryGetValue(fullUrl, out var cachedTexture))
            {
                if (cachedTexture != null)
                {
                    return cachedTexture;
                }
                else
                {
                    _textureCache.Remove(fullUrl);
                }
            }

            try
            {
                using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(fullUrl))
                {
                    request.timeout = timeoutSeconds;

                    // 开发测试模式下兼容内网自签名证书
                    if (AuthConfig.IsDevelopmentEnvironment)
                    {
                        request.certificateHandler = new BypassCertificateHandler();
                    }

                    var asyncOp = request.SendWebRequest();

                    while (!asyncOp.isDone)
                    {
                        await Task.Yield();
                    }

                    if (request.result == UnityWebRequest.Result.ConnectionError ||
                        request.result == UnityWebRequest.Result.DataProcessingError ||
                        request.result == UnityWebRequest.Result.ProtocolError)
                    {
                        Debug.LogWarning($"[SurgicalLiveSDK] Failed to load image from {fullUrl} (orig: {url}): {request.error}");
                        return null;
                    }

                    Texture2D texture = DownloadHandlerTexture.GetContent(request);
                    if (texture != null)
                    {
                        _textureCache[fullUrl] = texture;
                    }
                    return texture;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SurgicalLiveSDK] Exception loading image from {fullUrl} (orig: {url}): {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 多级降级容灾异步加载图片。
        /// <para>优先尝试从首选主地址 (如高精度预设原图) 加载；若主地址不可达或发生网络错误，自动无缝降级加载备用地址 (如房间缩略图直连接口)；若均失败返回 null。</para>
        /// </summary>
        /// <param name="primaryUrl">首选优先级图片 URL (通常为静态原图)</param>
        /// <param name="secondaryUrl">次选降级图片 URL (通常为后端房间缩略图)</param>
        /// <param name="timeoutSeconds">单次尝试超时时间 (秒)，默认 3 秒</param>
        /// <returns>任意一级加载成功的 Texture2D；均失败则返回 null</returns>
        public static async Task<Texture2D> LoadImageWithFallbackAsync(string primaryUrl, string secondaryUrl, int timeoutSeconds = 3)
        {
            if (!string.IsNullOrEmpty(primaryUrl))
            {
                var primaryTex = await LoadImageAsync(primaryUrl, timeoutSeconds);
                if (primaryTex != null)
                {
                    return primaryTex;
                }
            }

            if (!string.IsNullOrEmpty(secondaryUrl))
            {
                var secondaryTex = await LoadImageAsync(secondaryUrl, timeoutSeconds);
                if (secondaryTex != null)
                {
                    return secondaryTex;
                }
            }

            return null;
        }

        /// <summary>
        /// 清理图片缓存，释放所有 Texture2D 资源
        /// </summary>
        public static void ClearImageCache()
        {
            foreach (var kvp in _textureCache)
            {
                if (kvp.Value != null)
                {
                    UnityEngine.Object.Destroy(kvp.Value);
                }
            }
            _textureCache.Clear();
        }

        #endregion
    }
}
