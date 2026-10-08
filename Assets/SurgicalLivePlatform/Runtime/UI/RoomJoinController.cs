using System;
using System.Threading.Tasks;
using UnityEngine;
using SurgicalLivePlatform.Runtime.Auth;
using SurgicalLivePlatform.Runtime.Services;

namespace SurgicalLivePlatform.Runtime.UI
{
    /// <summary>
    /// 处理点击直播中房间发起的入会流程逻辑。
    /// </summary>
    public class RoomJoinController : MonoBehaviour
    {
        public static event System.Action<string, string> OnRoomJoinSuccess;

        private static RoomJoinController _instance;
        public static RoomJoinController Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<RoomJoinController>();
                    if (_instance == null)
                    {
                        var go = new GameObject("RoomJoinController");
                        _instance = go.AddComponent<RoomJoinController>();
                    }
                }
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void OnEnable()
        {
            RoomConnectionManager.Instance.OnConnectionProgress += HandleConnectionProgress;
            RoomConnectionManager.Instance.OnConnectionError += HandleConnectionError;
            RoomConnectionManager.Instance.OnSignalingDegraded += HandleSignalingDegraded;
        }

        private void OnDisable()
        {
            if (RoomConnectionManager.Instance != null)
            {
                RoomConnectionManager.Instance.OnConnectionProgress -= HandleConnectionProgress;
                RoomConnectionManager.Instance.OnConnectionError -= HandleConnectionError;
                RoomConnectionManager.Instance.OnSignalingDegraded -= HandleSignalingDegraded;
            }
        }

        public async void JoinRoom(string roomId)
        {
            try
            {
                // 1. 获取房间详情 (模拟10s超时拦截应在 ApiClient 层实现，这里只做调用)
                Toast.Show("获取房间详情...", 1.0f);
                var fetchTask = SurgicalLiveSDK.Auth.GetRoomDetailAsync(roomId);
                var timeoutTask = Task.Delay(10000);
                if (await Task.WhenAny(fetchTask, timeoutTask) == timeoutTask)
                {
                    throw new Exception("获取房间信息失败，请稍后重试");
                }
                var roomDetail = await fetchTask;
                if (roomDetail == null)
                {
                    throw new Exception("服务器返回的房间信息为空");
                }
                
                // 2. 发起入会申请
                var joinResponse = await SurgicalLiveSDK.Auth.JoinRoomAsync(roomId);
                if (joinResponse == null)
                {
                    throw new Exception("入会申请失败，服务器返回空数据");
                }
                
                // 采用 C# 事件解耦：触发事件，让外层（如 MeetManager）自行监听，彻底避免反向引用和反射
                if (OnRoomJoinSuccess != null)
                {
                    OnRoomJoinSuccess.Invoke(joinResponse.livekit_url, joinResponse.livekit_token);
                }
                else
                {
                    Debug.LogWarning("[RoomJoinController] 入会申请成功，但目前没有任何对象监听 OnRoomJoinSuccess 事件！");
                }

                return;
            }
            catch (Exception ex)
            {
                HandleJoinException(ex);
            }
        }

        private async Task LoadHistoryDataQuietly(string roomId)
        {
            try
            {
                var messages = await SurgicalLiveSDK.Auth.GetHistoryMessagesAsync(roomId);
                var annotations = await SurgicalLiveSDK.Auth.GetHistoryAnnotationsAsync(roomId);
                // TODO: 渲染到对应 UI 模块
                Debug.Log($"[RoomJoinController] 拉取完成：{messages.Count} 条消息，{annotations.Count} 条批注。");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RoomJoinController] 拉取历史数据失败 (不阻塞主流程): {ex.Message}");
            }
        }

        private void HandleJoinException(Exception ex)
        {
            // 假设抛出的异常中包含特定的 HTTP 状态码信息，或者自定义的 ApiException
            string message = ex.Message.ToLower();
            
            if (message.Contains("403") || message.Contains("409"))
            {
                if (message.Contains("full") || message.Contains("409"))
                {
                    Toast.Show("房间人数已达上限", 2.0f);
                }
                else
                {
                    Toast.Show("您没有权限加入该房间", 2.0f);
                }
            }
            else
            {
                Toast.Show($"入会失败: {ex.Message}", 2.0f);
            }
            
            // 终止入会并返回列表页（通过场景切换或 UI 隐藏）
            Debug.LogError($"[RoomJoinController] Join exception: {ex.Message}");
        }

        // --- 主持人主动邀请功能 ---
        public async void InviteUserToRoom(string roomId, string userIdToInvite)
        {
            // 在实际 UI 侧应控制按钮仅对 Host 角色可见
            try
            {
                var request = new DTOs.InviteToRoomRequest { user_ids = new System.Collections.Generic.List<string> { userIdToInvite } };
                await SurgicalLiveSDK.Auth.InviteToRoomAsync(roomId, request);
                Toast.Show("邀请已发送", 1.5f);
            }
            catch (Exception ex)
            {
                Toast.Show($"邀请发送失败: {ex.Message}", 2.0f);
            }
        }

        private void HandleConnectionProgress(string msg)
        {
            // 可在此处将进度输出给加载面板
            Debug.Log($"[RoomJoinController] 进度: {msg}");
        }

        private void HandleConnectionError(string error)
        {
            Toast.Show(error, 3.0f);
        }

        private void HandleSignalingDegraded(string msg)
        {
            // 降级提示，持久化显示，直到重连成功
            Toast.ShowPersistent(msg);
        }
    }
}
