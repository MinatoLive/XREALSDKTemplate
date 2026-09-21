using System.Collections.Generic;
using System.Threading.Tasks;
using SurgicalLivePlatform.Runtime.Auth;
using SurgicalLivePlatform.Runtime.DTOs;
using SurgicalLivePlatform.Runtime.Network;
using SurgicalLivePlatform.Runtime.Storage;

namespace SurgicalLivePlatform.Runtime
{
    /// <summary>
    /// 手术直播平台核心 SDK 统一入口 (Facade)
    /// <para>封装了底层的所有网络请求、事件分发及本地存储。使用该单例可以直接与平台服务端进行交互。</para>
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
    /// </code>
    /// </example>
    public static class SurgicalLiveSDK
    {
        private static bool _initialized = false;
        
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
        /// 初始化整个 SDK。在调用任何网络请求前必须先调用此方法一次。
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;

            Events = new NetworkEventDispatcher();
            ApiClient = new HttpClientService(Events);
            Storage = new JsonLocalStorage();
            Auth = new AuthService(ApiClient, Storage, Events);

            _initialized = true;
            UnityEngine.Debug.Log("[SurgicalLiveSDK] 初始化完成。");
        }
        
        /// <summary>
        /// 销毁 SDK 资源，关闭所有网络底层连接，释放句柄。
        /// </summary>
        public static void Dispose()
        {
            if (!_initialized) return;
            
            if (ApiClient is System.IDisposable disposableClient)
            {
                disposableClient.Dispose();
            }
            
            Events = null;
            ApiClient = null;
            Storage = null;
            Auth = null;
            
            _initialized = false;
            UnityEngine.Debug.Log("[SurgicalLiveSDK] 资源已释放。");
        }
    }
}
