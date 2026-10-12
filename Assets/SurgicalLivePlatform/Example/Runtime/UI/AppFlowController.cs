using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using SurgicalLivePlatform.Runtime.UI;
using SurgicalLivePlatform.Runtime;
using SurgicalLivePlatform.Runtime.Models;

namespace SurgicalLivePlatform.Example.Runtime.UI
{
    public enum AppUIState
    {
        Login,
        Lobby,
        InRoom
    }

    public class AppFlowController : MonoBehaviour
    {
        public static AppFlowController Instance { get; private set; }

        private Dictionary<string, GameObject> _uiNodes = new Dictionary<string, GameObject>();
        private Dictionary<string, Button> _uiButtons = new Dictionary<string, Button>();

        private AppUIState _currentState;

        // 约定的 UI Mark 名称
        private const string MARK_LOGIN_PANEL = "Panel_Login";
        private const string MARK_LOBBY_PANEL = "Panel_Lobby";
        private const string MARK_ROOM_PANEL = "Panel_Room";
        
        private const string MARK_LOBBY_BACK = "Btn_LobbyBack";
        private const string MARK_LOBBY_REFRESH = "Btn_LobbyRefresh";
        private const string MARK_ROOM_BACK = "Btn_RoomBack";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            
            FindAllUIMarks();
        }

        private void Start()
        {
            SurgicalLiveSDK.Initialize();
            SurgicalLiveSDK.StateManager.OnStateChanged += HandleSDKStateChanged;
            RoomJoinController.OnRoomJoinSuccess += HandleRoomJoinSuccess;

            BindEvents();

            // 初始状态检查
            HandleSDKStateChanged(UserAppState.Init, SurgicalLiveSDK.StateManager.CurrentState);
        }

        private void OnDestroy()
        {
            if (SurgicalLiveSDK.StateManager != null)
            {
                SurgicalLiveSDK.StateManager.OnStateChanged -= HandleSDKStateChanged;
            }
            RoomJoinController.OnRoomJoinSuccess -= HandleRoomJoinSuccess;
            UnbindEvents();
        }

        private void HandleRoomJoinSuccess(string url, string token)
        {
            JoinRoom();
        }

        /// <summary>
        /// 动态收集场景中所有挂载了 UIMark 的节点
        /// </summary>
        private void FindAllUIMarks()
        {
            _uiNodes.Clear();
            _uiButtons.Clear();

            // 查找场景中所有的 UIMark，包括未激活的对象
            UIMark[] marks = FindObjectsByType<UIMark>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var mark in marks)
            {
                if (string.IsNullOrEmpty(mark.markName)) continue;

                _uiNodes[mark.markName] = mark.gameObject;

                Button btn = mark.GetComponent<Button>();
                if (btn != null)
                {
                    _uiButtons[mark.markName] = btn;
                }
            }

            // 输出日志，方便排查由于拼写错误导致的查找失败
            LogMissingNode(MARK_LOGIN_PANEL);
            LogMissingNode(MARK_LOBBY_PANEL);
            LogMissingNode(MARK_ROOM_PANEL);
            LogMissingNode(MARK_LOBBY_BACK);
            LogMissingNode(MARK_LOBBY_REFRESH);
            LogMissingNode(MARK_ROOM_BACK);
        }

        private void LogMissingNode(string markName)
        {
            if (!_uiNodes.ContainsKey(markName))
            {
                Debug.LogWarning($"[AppFlowController] 场景中未找到 markName 为 '{markName}' 的 UIMark 节点！如果不需要请忽略。");
            }
        }

        private void BindEvents()
        {
            if (_uiButtons.TryGetValue(MARK_LOBBY_BACK, out Button lobbyBack))
            {
                lobbyBack.onClick.AddListener(OnLobbyBackClicked);
            }
            if (_uiButtons.TryGetValue(MARK_LOBBY_REFRESH, out Button lobbyRefresh))
            {
                lobbyRefresh.onClick.AddListener(OnLobbyRefreshClicked);
            }
            if (_uiButtons.TryGetValue(MARK_ROOM_BACK, out Button roomBack))
            {
                roomBack.onClick.AddListener(OnRoomBackClicked);
            }
        }

        private void UnbindEvents()
        {
            if (_uiButtons.TryGetValue(MARK_LOBBY_BACK, out Button lobbyBack) && lobbyBack != null)
            {
                lobbyBack.onClick.RemoveListener(OnLobbyBackClicked);
            }
            if (_uiButtons.TryGetValue(MARK_LOBBY_REFRESH, out Button lobbyRefresh) && lobbyRefresh != null)
            {
                lobbyRefresh.onClick.RemoveListener(OnLobbyRefreshClicked);
            }
            if (_uiButtons.TryGetValue(MARK_ROOM_BACK, out Button roomBack) && roomBack != null)
            {
                roomBack.onClick.RemoveListener(OnRoomBackClicked);
            }
        }

        private void HandleSDKStateChanged(UserAppState oldState, UserAppState newState)
        {
            // SDK 状态变动防呆与容错：断开连接、异常、登出，都返回登录界面
            if (newState == UserAppState.LoggedOut || newState == UserAppState.Error || newState == UserAppState.Init)
            {
                GoToLogin();
            }
            else if (newState == UserAppState.LoggedIn)
            {
                GoToLobby();
            }
        }

        public void GoToLogin()
        {
            _currentState = AppUIState.Login;
            SetPanelActive(MARK_LOGIN_PANEL, true);
            SetPanelActive(MARK_LOBBY_PANEL, false);
            SetPanelActive(MARK_ROOM_PANEL, false);
        }

        public void GoToLobby()
        {
            _currentState = AppUIState.Lobby;
            SetPanelActive(MARK_LOGIN_PANEL, false);
            SetPanelActive(MARK_LOBBY_PANEL, true);
            SetPanelActive(MARK_ROOM_PANEL, false);
        }

        public void JoinRoom()
        {
            _currentState = AppUIState.InRoom;
            SetPanelActive(MARK_LOGIN_PANEL, false);
            // 核心需求：进入房间后大厅面板保持不隐藏
            SetPanelActive(MARK_LOBBY_PANEL, true); 
            SetPanelActive(MARK_ROOM_PANEL, true);
        }

        public void LeaveRoom()
        {
            // 断开流媒体与信令连接
            if (SurgicalLivePlatform.Runtime.Services.RoomConnectionManager.Instance != null)
            {
                SurgicalLivePlatform.Runtime.Services.RoomConnectionManager.Instance.DisconnectAll();
            }

            // 触发事件通知 MeetManager 等底层模块断开具体流媒体连接
            RoomJoinController.TriggerRoomLeave();

            // 状态退回大厅
            GoToLobby();
        }

        private async void OnLobbyBackClicked()
        {
            // 防御性清理：如果在特殊情况下仍残留有房间连接，先将其清理干净
            if (SurgicalLivePlatform.Runtime.Services.RoomConnectionManager.Instance != null)
            {
                SurgicalLivePlatform.Runtime.Services.RoomConnectionManager.Instance.DisconnectAll();
            }
            // 通知底层的 MeetManager 断开具体的 LiveKit 视频流连接
            RoomJoinController.TriggerRoomLeave();

            // 点击大厅返回按钮，执行登出并退回登录页
            if (SurgicalLiveSDK.Auth != null)
            {
                try
                {
                    await SurgicalLiveSDK.Auth.LogoutAsync();
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[AppFlowController] 退出登录时发生异常: {ex.Message}");
                }
            }
            
            // 修复 BUG：必须重置底层 SDK 状态，否则下次登录时因为状态同为 LoggedIn，不会触发 OnStateChanged 导致无法跳转
            if (SurgicalLiveSDK.StateManager != null)
            {
                SurgicalLiveSDK.StateManager.ChangeState(UserAppState.LoggedOut);
            }
            else
            {
                GoToLogin();
            }
        }

        private void OnLobbyRefreshClicked()
        {
            // 刷新房间列表数据
            RoomLobbyView lobbyView = FindAnyObjectByType<RoomLobbyView>(FindObjectsInactive.Include);
            if (lobbyView != null)
            {
                lobbyView.RefreshRooms();
            }
            else
            {
                Debug.LogWarning("[AppFlowController] 找不到 RoomLobbyView 组件，无法刷新大厅列表！");
            }
        }

        private void OnRoomBackClicked()
        {
            LeaveRoom();
        }

        private void SetPanelActive(string markName, bool isActive)
        {
            if (_uiNodes.TryGetValue(markName, out GameObject panelObj) && panelObj != null)
            {
                panelObj.SetActive(isActive);
                
                // 兼容之前有 Rigidbody 物理节点包裹的情况
                if (panelObj.transform.parent != null && panelObj.transform.parent.parent != null)
                {
                    var rb = panelObj.transform.parent.parent.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.gameObject.SetActive(isActive);
                    }
                }
            }
        }
    }
}
