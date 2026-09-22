using System;
using System.Threading.Tasks;
using UnityEngine;
using SurgicalLivePlatform.Runtime.Network;

namespace SurgicalLivePlatform.Runtime.Services
{
    /// <summary>
    /// 管理入会后的双通道连接：流媒体 (LiveKit) 与 信令 (WebSocket)。
    /// </summary>
    public class RoomConnectionManager : MonoBehaviour
    {
        private static RoomConnectionManager _instance;
        public static RoomConnectionManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("RoomConnectionManager");
                    _instance = go.AddComponent<RoomConnectionManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        private WebSocketService _webSocketService;
        
        // 状态事件
        public event Action<string> OnConnectionProgress;
        public event Action<string> OnConnectionError;
        public event Action<string> OnSignalingDegraded; // 信令降级提示

        private bool _isLiveKitConnected = false;
        private bool _isWebSocketConnected = false;
        private bool _isConnecting = false;

        private void Awake()
        {
            _webSocketService = new WebSocketService();
            // 配置 WebSocket 指数退避重连参数
            _webSocketService.MaxRetryCount = 10;
            _webSocketService.MaxBackoffMs = 30000;
            
            _webSocketService.OnConnected += HandleWebSocketConnected;
            _webSocketService.OnDisconnected += HandleWebSocketDisconnected;
            _webSocketService.OnError += HandleWebSocketError;
        }

        public async Task StartConnectionAsync(string roomId, string liveKitUrl, string liveKitToken, string wsToken)
        {
            if (_isConnecting) return;
            _isConnecting = true;
            
            OnConnectionProgress?.Invoke("正在连接音视频与信令服务...");

            // 1. 异步启动信令连接
            // 注意: 生产环境应从统一的配置或环境变量中获取 WS 的基准地址
            string wsUrl = $"ws://10.72.128.96:8081/api/v1/ws?roomId={roomId}";
            _ = _webSocketService.ConnectAsync(wsUrl, wsToken);

            // 2. 启动流媒体连接
            bool lkSuccess = await ConnectLiveKitWithFallbackAsync(liveKitUrl, liveKitToken);
            if (!lkSuccess)
            {
                OnConnectionError?.Invoke("音视频服务连接失败，请检查网络环境后重新加入");
                _isConnecting = false;
                return;
            }

            _isConnecting = false;
        }

        /// <summary>
        /// 包含 TURN fallback 和最多3次重试的 LiveKit 连接逻辑
        /// </summary>
        private async Task<bool> ConnectLiveKitWithFallbackAsync(string url, string token)
        {
            int maxRetries = 3;
            int[] retryDelays = new int[] { 1000, 2000, 3000 };

            for (int i = 0; i <= maxRetries; i++)
            {
                try
                {
                    if (i > 0)
                    {
                        OnConnectionProgress?.Invoke($"音视频连接重试 ({i}/{maxRetries})...");
                    }

                    // ==========================================
                    // TODO: 接入正式的 LiveKit SDK (LiveKit.Room)
                    // ==========================================
                    // var room = new LiveKit.Room();
                    // await room.Connect(url, token);
                    
                    // 【Mock】模拟连接成功或失败
                    await Task.Delay(500); 
                    // 假设这里如果出现异常会 throw
                    _isLiveKitConnected = true;
                    OnConnectionProgress?.Invoke("音视频连接成功");
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[LiveKit] Connection failed on attempt {i}: {ex.Message}");
                    if (i < maxRetries)
                    {
                        // TURN fallback 以及延迟
                        await Task.Delay(retryDelays[i]);
                    }
                }
            }

            _isLiveKitConnected = false;
            return false;
        }

        private void HandleWebSocketConnected()
        {
            _isWebSocketConnected = true;
            OnConnectionProgress?.Invoke("信令服务已连接");
        }

        private void HandleWebSocketDisconnected(string reason)
        {
            _isWebSocketConnected = false;
            // 抛出降级事件，通知 UI
            OnSignalingDegraded?.Invoke("信令服务断开，部分互动功能受限");
        }

        private void HandleWebSocketError(string error)
        {
            Debug.LogError($"[RoomConnectionManager] WS Error: {error}");
        }

        public void DisconnectAll()
        {
            _isConnecting = false;
            _ = _webSocketService?.DisconnectAsync();
            
            // TODO: LiveKit 资源释放
            // _room?.Disconnect();

            _isLiveKitConnected = false;
            _isWebSocketConnected = false;
        }

        private void OnDestroy()
        {
            if (_webSocketService != null)
            {
                _webSocketService.OnConnected -= HandleWebSocketConnected;
                _webSocketService.OnDisconnected -= HandleWebSocketDisconnected;
                _webSocketService.OnError -= HandleWebSocketError;
                _webSocketService.Dispose();
            }
        }
    }
}
