using System.Collections.Generic;
using SurgicalLivePlatform.Runtime;
using SurgicalLivePlatform.Runtime.Models;
using SurgicalLivePlatform.Runtime.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SurgicalLivePlatform.Example.Runtime.UI
{
    public enum AppUIState
    {
        Login,
        Lobby,
        InRoom
    }

    /// <summary>
    /// 应用流与 UI 状态路由器 (UI Navigation Router)
    /// 遵循 MVP 架构规范与 UILayerManager 层级标准。
    /// 负责强类型组装各 Presenter、维护应用级全局状态机及路由守卫。
    /// </summary>
    public class AppFlowController : MonoBehaviour
    {
        public static AppFlowController Instance { get; private set; }

        public AppUIState CurrentState => _currentState;

        public const string MARK_LOGIN_PANEL = "Panel_Login";
        public const string MARK_LOBBY_PANEL = "Panel_Lobby";
        public const string MARK_ROOM_PANEL = "Panel_Room";
        public const string MARK_LOBBY_BACK = "Btn_LobbyBack";
        public const string MARK_LOBBY_REFRESH = "Btn_LobbyRefresh";
        public const string MARK_ROOM_BACK = "Btn_RoomBack";

        [Header("Views (强类型注入)")]
        [SerializeField] private UserLoginUIController loginView;
        [SerializeField] private RoomLobbyView lobbyView;
        [SerializeField] private RoomModalView roomModalView;
        [SerializeField] private GameObject roomPanel;

        [Header("3D Root Entities (空间实体根对象显隐)")]
        [SerializeField] private GameObject loginRoot;
        [SerializeField] private GameObject roomListRoot;
        [SerializeField] private GameObject roomRoot;

        [Header("Action Controls (可选按键直接绑定)")]
        [SerializeField] private Button lobbyBackButton;
        [SerializeField] private Button lobbyRefreshButton;
        [SerializeField] private Button roomBackButton;

        public GameObject LoginRoot => loginRoot;
        public GameObject RoomListRoot => roomListRoot;
        public GameObject RoomRoot => roomRoot;

#if UNITY_INCLUDE_TESTS || UNITY_EDITOR
        public void ConfigureRootEntitiesForTesting(GameObject login, GameObject roomList, GameObject room)
        {
            loginRoot = login;
            roomListRoot = roomList;
            roomRoot = room;
        }

        public void ConfigurePresentersForTesting(LobbyPresenter lobbyPresenter, RoomLobbyView lobbyView = null)
        {
            _lobbyPresenter = lobbyPresenter;
            this.lobbyView = lobbyView;
        }

        public void SimulateSDKStateChanged(UserAppState oldState, UserAppState newState)
        {
            HandleSDKStateChanged(oldState, newState);
        }

        public void SimulateLobbyRefreshClick()
        {
            OnLobbyRefreshClicked();
        }
#endif

        private LoginPresenter _loginPresenter;
        private LobbyPresenter _lobbyPresenter;
        private RoomModalPresenter _roomModalPresenter;

        private AppUIState _currentState = AppUIState.Login;

        // 声明式定义：底层 SDK 核心业务状态 -> 前端 UI 界面状态
        private static readonly Dictionary<UserAppState, AppUIState> s_sdkToUIStateMap = new Dictionary<UserAppState, AppUIState>
        {
            { UserAppState.Init,      AppUIState.Login },
            { UserAppState.LoggedOut, AppUIState.Login },
            { UserAppState.Error,     AppUIState.Login },
            { UserAppState.LoggedIn,  AppUIState.Lobby }
        };

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // 提前初始化底层核心 SDK
            SurgicalLiveSDK.EnsureInitialized();

            ResolveViews();
            AssemblePresenters();
            BindControlButtons();

            // 初始路由守卫
            EnforceInitialRouteGuard();
        }

        private void Start()
        {
            SurgicalLiveSDK.EnsureInitialized();
            if (SurgicalLiveSDK.StateManager != null)
            {
                SurgicalLiveSDK.StateManager.OnStateChanged += HandleSDKStateChanged;
            }
            if (SurgicalLiveSDK.Events != null)
            {
                SurgicalLiveSDK.Events.OnAuthExpired += HandleAuthExpired;
            }
            RoomJoinController.OnRoomJoinSuccess += HandleRoomJoinSuccess;

            // 初始状态检查
            if (SurgicalLiveSDK.StateManager != null)
            {
                HandleSDKStateChanged(UserAppState.Init, SurgicalLiveSDK.StateManager.CurrentState);
            }
        }

        private void OnDestroy()
        {
            if (SurgicalLiveSDK.StateManager != null)
            {
                SurgicalLiveSDK.StateManager.OnStateChanged -= HandleSDKStateChanged;
            }
            if (SurgicalLiveSDK.Events != null)
            {
                SurgicalLiveSDK.Events.OnAuthExpired -= HandleAuthExpired;
            }
            RoomJoinController.OnRoomJoinSuccess -= HandleRoomJoinSuccess;

            UnbindControlButtons();

            _loginPresenter?.Dispose();
            _lobbyPresenter?.Dispose();
            _roomModalPresenter?.Dispose();
            _loginPresenter = null;
            _lobbyPresenter = null;
            _roomModalPresenter = null;
        }

        private void ResolveViews()
        {
            if (loginView == null)
            {
                loginView = FindFirstObjectByType<UserLoginUIController>(FindObjectsInactive.Include);
            }
            if (lobbyView == null)
            {
                lobbyView = FindFirstObjectByType<RoomLobbyView>(FindObjectsInactive.Include);
            }
            if (roomModalView == null)
            {
                roomModalView = FindFirstObjectByType<RoomModalView>(FindObjectsInactive.Include);
            }

            if (roomPanel == null)
            {
                // 查找包含 Room 关键字或标记的节点
                var marks = FindObjectsByType<UIMark>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var mark in marks)
                {
                    if (mark.markName == "Panel_Room")
                    {
                        roomPanel = mark.gameObject;
                        break;
                    }
                }
            }

            // 兜底按钮查找
            if (lobbyBackButton == null || lobbyRefreshButton == null || roomBackButton == null)
            {
                var marks = FindObjectsByType<UIMark>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var mark in marks)
                {
                    if (lobbyBackButton == null && mark.markName == "Btn_LobbyBack")
                    {
                        lobbyBackButton = mark.GetComponent<Button>();
                    }
                    else if (lobbyRefreshButton == null && mark.markName == "Btn_LobbyRefresh")
                    {
                        lobbyRefreshButton = mark.GetComponent<Button>();
                    }
                    else if (roomBackButton == null && mark.markName == "Btn_RoomBack")
                    {
                        roomBackButton = mark.GetComponent<Button>();
                    }
                }
            }

            ResolveRootEntities();
        }

        private void ResolveRootEntities()
        {
            if (loginRoot == null)
            {
                if (loginView != null)
                {
                    loginRoot = FindRootNamed(loginView.transform, "Login");
                }
                if (loginRoot == null)
                {
                    loginRoot = GameObject.Find("Login");
                }
                if (loginRoot == null)
                {
                    var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                    if (activeScene.isLoaded)
                    {
                        var allRoots = activeScene.GetRootGameObjects();
                        foreach (var root in allRoots)
                        {
                            if (root.name == "Login") { loginRoot = root; break; }
                        }
                    }
                }
            }

            if (roomListRoot == null)
            {
                if (lobbyView != null)
                {
                    roomListRoot = FindRootNamed(lobbyView.transform, "RoomList");
                }
                if (roomListRoot == null)
                {
                    roomListRoot = GameObject.Find("RoomList");
                }
                if (roomListRoot == null)
                {
                    var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                    if (activeScene.isLoaded)
                    {
                        var allRoots = activeScene.GetRootGameObjects();
                        foreach (var root in allRoots)
                        {
                            if (root.name == "RoomList") { roomListRoot = root; break; }
                        }
                    }
                }
            }

            if (roomRoot == null)
            {
                if (roomPanel != null)
                {
                    roomRoot = FindRootNamed(roomPanel.transform, "Room");
                }
                if (roomRoot == null)
                {
                    roomRoot = GameObject.Find("Room");
                }
                if (roomRoot == null)
                {
                    var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                    if (activeScene.isLoaded)
                    {
                        var allRoots = activeScene.GetRootGameObjects();
                        foreach (var root in allRoots)
                        {
                            if (root.name == "Room") { roomRoot = root; break; }
                        }
                    }
                }
            }
        }

        private GameObject FindRootNamed(Transform current, string targetName)
        {
            var curr = current;
            while (curr != null)
            {
                if (curr.name == targetName) return curr.gameObject;
                curr = curr.parent;
            }
            return null;
        }

        private void AssemblePresenters()
        {
            if (loginView != null)
            {
                _loginPresenter = new LoginPresenter(loginView);
                loginView.AttachPresenter(_loginPresenter);
            }

            if (lobbyView != null)
            {
                _lobbyPresenter = new LobbyPresenter(lobbyView, roomModalView);
                lobbyView.AttachPresenter(_lobbyPresenter);
            }

            if (roomModalView != null)
            {
                _roomModalPresenter = new RoomModalPresenter(roomModalView);
                roomModalView.AttachPresenter(_roomModalPresenter);
            }
        }

        private void BindControlButtons()
        {
            if (lobbyBackButton == null)
            {
                if (roomListRoot != null)
                {
                    var buttons = roomListRoot.GetComponentsInChildren<Button>(true);
                    foreach (var b in buttons)
                    {
                        if (b.name == "Back" || b.name == "Btn_Back" || b.name == "Btn_LobbyBack")
                        {
                            lobbyBackButton = b;
                            break;
                        }
                    }
                }
                if (lobbyBackButton == null && lobbyView != null)
                {
                    var buttons = lobbyView.GetComponentsInChildren<Button>(true);
                    foreach (var b in buttons)
                    {
                        if (b.name == "Back" || b.name == "Btn_Back" || b.name == "Btn_LobbyBack")
                        {
                            lobbyBackButton = b;
                            break;
                        }
                    }
                }
            }

            if (lobbyBackButton != null)
            {
                lobbyBackButton.onClick.RemoveAllListeners();
                lobbyBackButton.onClick.AddListener(OnLobbyBackClicked);
            }
            if (lobbyRefreshButton != null)
            {
                lobbyRefreshButton.onClick.AddListener(OnLobbyRefreshClicked);
            }
            if (roomBackButton != null)
            {
                roomBackButton.onClick.AddListener(OnRoomBackClicked);
            }
        }

        private void UnbindControlButtons()
        {
            if (lobbyBackButton != null)
            {
                lobbyBackButton.onClick.RemoveListener(OnLobbyBackClicked);
            }
            if (lobbyRefreshButton != null)
            {
                lobbyRefreshButton.onClick.RemoveListener(OnLobbyRefreshClicked);
            }
            if (roomBackButton != null)
            {
                roomBackButton.onClick.RemoveListener(OnRoomBackClicked);
            }
        }

        private void EnforceInitialRouteGuard()
        {
            SwitchTo(AppUIState.Login);
        }

        /// <summary>
        /// 彻底断开流媒体 (LiveKit) 与信令服务 (WebSocket) 连接
        /// </summary>
        public void DisconnectLiveKitAndSignaling()
        {
            try
            {
                if (SurgicalLivePlatform.Runtime.Services.RoomConnectionManager.Instance != null)
                {
                    SurgicalLivePlatform.Runtime.Services.RoomConnectionManager.Instance.DisconnectAll();
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[AppFlowController] 断开信令服务时异常: {ex.Message}");
            }

            try
            {
                // 触发事件通知底层 MeetManager 断开并清理 LiveKit 房间连接与音视频 Track 资源
                RoomJoinController.TriggerRoomLeave();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[AppFlowController] 触发 LiveKit 离会断开时异常: {ex.Message}");
            }
        }

        private void HandleAuthExpired()
        {
            Debug.LogWarning("[AppFlowController] 接收到 OnAuthExpired 事件，断开流媒体并重定向至登录界面。");
            DisconnectLiveKitAndSignaling();
            GoToLogin();
        }

        private void HandleRoomJoinSuccess(string url, string token)
        {
            JoinRoom();
        }

        private void HandleSDKStateChanged(UserAppState oldState, UserAppState newState)
        {
            if (s_sdkToUIStateMap.TryGetValue(newState, out var targetUIState))
            {
                SwitchTo(targetUIState);
            }

            // 仅在用户登录成功后自动执行一次房间列表全量刷新
            if (newState == UserAppState.LoggedIn && oldState != UserAppState.LoggedIn)
            {
                if (_lobbyPresenter != null)
                {
                    _lobbyPresenter.RefreshRoomsAsync();
                }
                else if (lobbyView != null)
                {
                    lobbyView.RefreshRooms();
                }
            }
        }

        /// <summary>
        /// 声明式统一状态路由核心切换方法
        /// </summary>
        public void SwitchTo(AppUIState targetState)
        {
            // 路由守卫：只有已登录状态才允许进入大厅或房间
            if (targetState != AppUIState.Login)
            {
                if (SurgicalLiveSDK.StateManager == null || SurgicalLiveSDK.StateManager.CurrentState != UserAppState.LoggedIn)
                {
                    Debug.LogWarning($"[AppFlowController] 路由守卫拦截：未登录尝试跳转至 {targetState} 状态！重定向至登录界面。");
                    targetState = AppUIState.Login;
                }
            }

            // 无论从何处返回登录，必须彻底断开 LiveKit 与信令服务
            if (targetState == AppUIState.Login)
            {
                DisconnectLiveKitAndSignaling();
            }

            _currentState = targetState;

            // 切换页面时，关闭所有残留的模态弹窗
            if (UILayerManager.Instance != null && targetState != AppUIState.InRoom)
            {
                UILayerManager.Instance.CloseAllModals();
            }

            // 1. 3D 实体根对象显隐控制 (整机激活/失活)
            // - 默认进入 Login：只显示 loginRoot
            // - 登录成功 Lobby：loginRoot 隐藏，roomListRoot 显示，roomRoot 隐藏
            // - 进入房间 InRoom：loginRoot 隐藏，roomListRoot 保留不消失，roomRoot 显示
            if (loginRoot != null)
            {
                loginRoot.SetActive(targetState == AppUIState.Login);
            }

            if (roomListRoot != null)
            {
                roomListRoot.SetActive(targetState == AppUIState.Lobby || targetState == AppUIState.InRoom);
            }

            if (roomRoot != null)
            {
                roomRoot.SetActive(targetState == AppUIState.InRoom);
            }

            // 2. 内部 UI 视图与生命周期同步
            if (loginView != null)
            {
                if (targetState == AppUIState.Login) loginView.Show();
                else loginView.Hide();
            }

            if (lobbyView != null)
            {
                if (targetState == AppUIState.Lobby || targetState == AppUIState.InRoom) lobbyView.Show();
                else lobbyView.Hide();
            }

            if (roomPanel != null)
            {
                roomPanel.SetActive(targetState == AppUIState.InRoom);
            }
        }

        public void GoToLogin()
        {
            SwitchTo(AppUIState.Login);
        }

        public void GoToLobby()
        {
            SwitchTo(AppUIState.Lobby);
        }

        public void JoinRoom()
        {
            SwitchTo(AppUIState.InRoom);
        }

        public void LeaveRoom()
        {
            // 退出房间时，严格断开 LiveKit 流媒体与信令通道
            DisconnectLiveKitAndSignaling();

            // 状态退回大厅
            GoToLobby();
        }

        private async void OnLobbyBackClicked()
        {
            // 退出登录时，严格断开 LiveKit 流媒体与信令通道
            DisconnectLiveKitAndSignaling();

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
            if (_lobbyPresenter != null)
            {
                _lobbyPresenter.RefreshRoomsAsync();
            }
            else if (lobbyView != null)
            {
                lobbyView.RefreshRooms();
            }
        }

        private void OnRoomBackClicked()
        {
            LeaveRoom();
        }
    }
}
