using System.Collections.Generic;
using NUnit.Framework;
using SurgicalLivePlatform.Example.Runtime.UI;
using SurgicalLivePlatform.Runtime;
using SurgicalLivePlatform.Runtime.Models;
using SurgicalLivePlatform.Runtime.UI;
using UnityEngine;

namespace SurgicalLivePlatform.Tests
{
    [TestFixture]
    public class UILifecycleRouterTests
    {
        private class TestPanel : BaseUIPanel
        {
            public int OnInitCallCount { get; private set; }
            public int OnShowCallCount { get; private set; }
            public int OnHideCallCount { get; private set; }

            protected override void OnInit()
            {
                OnInitCallCount++;
            }

            protected override void OnShow()
            {
                OnShowCallCount++;
            }

            protected override void OnHide()
            {
                OnHideCallCount++;
            }
        }

        [SetUp]
        public void SetUp()
        {
            SurgicalLiveSDK.EnsureInitialized();
        }

        [TearDown]
        public void TearDown()
        {
            if (SurgicalLiveSDK.StateManager != null)
            {
                SurgicalLiveSDK.StateManager.ChangeState(UserAppState.Init);
            }
        }

        #region 1. BaseUIPanel Lifecycle Contract Tests

        [Test]
        public void BaseUIPanel_Lifecycle_InvokesHooksInCorrectSequence()
        {
            var go = new GameObject("TestPanel");
            var panel = go.AddComponent<TestPanel>();

            try
            {
                Assert.AreEqual(0, panel.OnInitCallCount);
                Assert.AreEqual(0, panel.OnShowCallCount);
                Assert.AreEqual(0, panel.OnHideCallCount);

                // Act 1: 首次展示
                panel.Show();

                // Assert 1: OnInit 仅调用一次，OnShow 调用一次，且处于可见激活状态
                Assert.AreEqual(1, panel.OnInitCallCount, "OnInit 应在首次展示时触发");
                Assert.AreEqual(1, panel.OnShowCallCount, "OnShow 应在展示时触发");
                Assert.IsTrue(panel.IsVisible, "面板应处于激活状态");

                // Act 2: 隐藏
                panel.Hide();

                // Assert 2: OnHide 触发，状态切为隐藏
                Assert.AreEqual(1, panel.OnHideCallCount, "OnHide 应在隐藏时触发");
                Assert.IsFalse(panel.IsVisible, "面板应处于未激活状态");

                // Act 3: 二次展示
                panel.Show();

                // Assert 3: OnInit 不应重复调用，OnShow 再次累加
                Assert.AreEqual(1, panel.OnInitCallCount, "OnInit 不得重复执行");
                Assert.AreEqual(2, panel.OnShowCallCount, "OnShow 计数应递增为 2");
                Assert.IsTrue(panel.IsVisible, "面板应重新恢复激活");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        #endregion

        #region 2. AppFlowController Declarative Routing Tests

        [Test]
        public void AppFlowController_RoutingRules_MatchesStateRequirements()
        {
            var routerGo = new GameObject("RouterTest");
            var router = routerGo.AddComponent<AppFlowController>();

            var loginGo = new GameObject("LoginPanel");
            var lobbyGo = new GameObject("LobbyPanel");
            var roomGo = new GameObject("RoomPanel");

            var loginMark = loginGo.AddComponent<UIMark>();
            loginMark.markName = AppFlowController.MARK_LOGIN_PANEL;
            var lobbyMark = lobbyGo.AddComponent<UIMark>();
            lobbyMark.markName = AppFlowController.MARK_LOBBY_PANEL;
            var roomMark = roomGo.AddComponent<UIMark>();
            roomMark.markName = AppFlowController.MARK_ROOM_PANEL;

            var loginPanel = loginGo.AddComponent<UserLoginUIController>();
            var lobbyPanel = lobbyGo.AddComponent<RoomLobbyView>();

            try
            {
                // 1. 验证登录状态下：仅登录面板处于激活状态
                router.SwitchTo(AppUIState.Login);
                Assert.AreEqual(AppUIState.Login, router.CurrentState);

                // 2. 模拟已登录状态
                SurgicalLiveSDK.StateManager.ChangeState(UserAppState.LoggedIn);

                // 3. 切换至大厅：大厅激活，登录与房间隐藏
                router.SwitchTo(AppUIState.Lobby);
                Assert.AreEqual(AppUIState.Lobby, router.CurrentState);

                // 4. 切换至房间：核心业务规则：大厅与房间面板均保持激活！
                router.SwitchTo(AppUIState.InRoom);
                Assert.AreEqual(AppUIState.InRoom, router.CurrentState);
            }
            finally
            {
                Object.DestroyImmediate(loginGo);
                Object.DestroyImmediate(lobbyGo);
                Object.DestroyImmediate(roomGo);
                Object.DestroyImmediate(routerGo);
            }
        }

        [Test]
        public void AppFlowController_RouteGuard_BlocksUnauthenticatedAccess()
        {
            var routerGo = new GameObject("RouterTest");
            var router = routerGo.AddComponent<AppFlowController>();

            try
            {
                // 确保处于未登录状态
                SurgicalLiveSDK.StateManager.ChangeState(UserAppState.LoggedOut);

                // Act: 未登录尝试强行跳转至大厅
                router.SwitchTo(AppUIState.Lobby);

                // Assert: 被路由守卫强制拦截回退至登录状态
                Assert.AreEqual(AppUIState.Login, router.CurrentState, "未登录时尝试访问大厅应被路由守卫强制拦截回登录状态");

                // Act: 未登录尝试强行跳转至房间
                router.SwitchTo(AppUIState.InRoom);

                // Assert: 同样拦截回退
                Assert.AreEqual(AppUIState.Login, router.CurrentState, "未登录时尝试访问房间应被路由守卫强制拦截回登录状态");
            }
            finally
            {
                Object.DestroyImmediate(routerGo);
            }
        }

        #endregion

        #region 3. Component Interface Conformance Tests

        [Test]
        public void UserLoginUIController_ConformsToIUIViewAndBaseUIPanel()
        {
            var go = new GameObject("LoginTest");
            var ctrl = go.AddComponent<UserLoginUIController>();

            try
            {
                Assert.IsInstanceOf<IUIView>(ctrl, "UserLoginUIController 必须实现 IUIView 接口");
                Assert.IsInstanceOf<BaseUIPanel>(ctrl, "UserLoginUIController 必须继承自 BaseUIPanel 基类");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void RoomLobbyView_ConformsToIUIViewAndBaseUIPanel()
        {
            var go = new GameObject("LobbyTest");
            var lobby = go.AddComponent<RoomLobbyView>();

            try
            {
                Assert.IsInstanceOf<IUIView>(lobby, "RoomLobbyView 必须实现 IUIView 接口");
                Assert.IsInstanceOf<BaseUIPanel>(lobby, "RoomLobbyView 必须继承自 BaseUIPanel 基类");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void RoomModalView_ConformsToIUIViewAndBaseUIPanel()
        {
            var go = new GameObject("ModalTest");
            var modal = go.AddComponent<RoomModalView>();

            try
            {
                Assert.IsInstanceOf<IUIView>(modal, "RoomModalView 必须实现 IUIView 接口");
                Assert.IsInstanceOf<BaseUIPanel>(modal, "RoomModalView 必须继承自 BaseUIPanel 基类");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void RoomModalView_ConfigureAndShowHide_DrivesLifecycle()
        {
            var go = new GameObject("ModalTest");
            var modal = go.AddComponent<RoomModalView>();

            try
            {
                // Act: 配置并 Show
                modal.Configure(RoomCreateMode.Immediate);
                modal.Show();

                Assert.IsTrue(modal.IsVisible, "调用 Show 后模态弹窗应处于激活状态");

                // Act: 隐藏
                modal.Hide();

                Assert.IsFalse(modal.IsVisible, "调用 Hide 后模态弹窗应处于未激活状态");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void AppFlowController_RootEntities_VisibilityAndLiveKitTeardown_Lifecycle()
        {
            var controllerGo = new GameObject("AppFlowControllerTest");
            var controller = controllerGo.AddComponent<AppFlowController>();

            var loginObj = new GameObject("Login");
            var roomListObj = new GameObject("RoomList");
            var roomObj = new GameObject("Room");

            bool roomLeaveTriggered = false;
            System.Action onLeaveAction = () => roomLeaveTriggered = true;
            RoomJoinController.OnRoomLeave += onLeaveAction;

            try
            {
                controller.ConfigureRootEntitiesForTesting(loginObj, roomListObj, roomObj);

                // 1. 默认登录态：只有 Login 激活，RoomList 和 Room 隐藏
                controller.SwitchTo(AppUIState.Login);
                Assert.IsTrue(loginObj.activeSelf, "登录态下 Login 实体必须整机激活");
                Assert.IsFalse(roomListObj.activeSelf, "登录态下 RoomList 实体必须整机隐藏");
                Assert.IsFalse(roomObj.activeSelf, "登录态下 Room 实体必须整机隐藏");

                // 模拟登录成功，进入大厅
                SurgicalLiveSDK.StateManager?.ChangeState(UserAppState.LoggedIn);
                controller.SwitchTo(AppUIState.Lobby);

                // 2. 大厅态：Login 隐藏，RoomList 激活，Room 隐藏
                Assert.IsFalse(loginObj.activeSelf, "登录成功后 Login 实体必须整机隐藏");
                Assert.IsTrue(roomListObj.activeSelf, "登录成功后 RoomList 实体必须整机激活");
                Assert.IsFalse(roomObj.activeSelf, "大厅态下 Room 实体必须整机隐藏");

                // 3. 进入房间：Login 隐藏，RoomList 保留不消失，Room 激活
                controller.SwitchTo(AppUIState.InRoom);
                Assert.IsFalse(loginObj.activeSelf, "进入房间后 Login 实体保持隐藏");
                Assert.IsTrue(roomListObj.activeSelf, "进入房间后 RoomList 实体必须保留不消失");
                Assert.IsTrue(roomObj.activeSelf, "进入房间后 Room 实体必须整机激活");

                // 4. 退出房间：Room 隐藏，RoomList 继续保留，触发 LiveKit 断开通知
                roomLeaveTriggered = false;
                controller.LeaveRoom();
                Assert.IsFalse(loginObj.activeSelf, "退房后 Login 实体保持隐藏");
                Assert.IsTrue(roomListObj.activeSelf, "退房后 RoomList 实体继续保留显示");
                Assert.IsFalse(roomObj.activeSelf, "退房后 Room 实体必须整机隐藏");
                Assert.IsTrue(roomLeaveTriggered, "退房时必须触发 LiveKit 离会断开事件");

                // 5. 返回登录 / 登出：仅 Login 显示，RoomList 与 Room 隐藏，断开 LiveKit
                roomLeaveTriggered = false;
                controller.GoToLogin();
                Assert.IsTrue(loginObj.activeSelf, "返回登录后 Login 实体必须整机激活");
                Assert.IsFalse(roomListObj.activeSelf, "返回登录后 RoomList 实体必须整机隐藏");
                Assert.IsFalse(roomObj.activeSelf, "返回登录后 Room 实体必须整机隐藏");
                Assert.IsTrue(roomLeaveTriggered, "返回登录时必须确保断开 LiveKit 连接");
            }
            finally
            {
                RoomJoinController.OnRoomLeave -= onLeaveAction;
                Object.DestroyImmediate(controllerGo);
                Object.DestroyImmediate(loginObj);
                Object.DestroyImmediate(roomListObj);
                Object.DestroyImmediate(roomObj);
            }
        }

        [Test]
        public void RoomLobbyView_ShowAndHide_NeverTriggersOnRefreshRequested()
        {
            var go = new GameObject("RoomLobbyViewTest");
            var lobbyView = go.AddComponent<RoomLobbyView>();
            int refreshCount = 0;
            lobbyView.OnRefreshRequested += () => refreshCount++;

            try
            {
                // Act 1: 首次展示
                lobbyView.Show();
                Assert.AreEqual(0, refreshCount, "首次 Show 绝不应触发 OnRefreshRequested");

                // Act 2: 多次显隐切换 (模拟进出房间)
                lobbyView.Hide();
                lobbyView.Show();
                lobbyView.Hide();
                lobbyView.Show();
                Assert.AreEqual(0, refreshCount, "多次 Show/Hide 绝不应触发 OnRefreshRequested");

                // Act 3: 显式调用 RefreshRooms
                lobbyView.RefreshRooms();
                Assert.AreEqual(1, refreshCount, "显式调用 RefreshRooms 必须触发 OnRefreshRequested 1 次");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void AppFlowController_RoomListRefresh_OnlyOnLoginAndManualRefresh_IsolatedFromJoinAndLeave()
        {
            var controllerGo = new GameObject("AppFlowControllerTest");
            var controller = controllerGo.AddComponent<AppFlowController>();

            var lobbyGo = new GameObject("RoomLobbyViewTest");
            var lobbyView = lobbyGo.AddComponent<RoomLobbyView>();

            int refreshCount = 0;
            lobbyView.OnRefreshRequested += () => refreshCount++;

            try
            {
                controller.ConfigurePresentersForTesting(null, lobbyView);

                // 1. 初始化登录态，刷新计数为 0
                controller.SwitchTo(AppUIState.Login);
                Assert.AreEqual(0, refreshCount, "登录态初始化时不应触发房间列表刷新");

                // 2. 登录成功：触发且仅触发 1 次房间列表刷新
                SurgicalLiveSDK.EnsureInitialized();
                SurgicalLiveSDK.StateManager.ChangeState(UserAppState.LoggedIn);
                controller.SimulateSDKStateChanged(UserAppState.Init, UserAppState.LoggedIn);
                Assert.AreEqual(1, refreshCount, "登录成功后必须自动执行且仅执行 1 次房间列表刷新");

                // 3. 点击「进入房间」：刷新计数保持为 1 (0 次额外网络请求)
                controller.JoinRoom();
                Assert.AreEqual(1, refreshCount, "进入房间操作绝对不触发房间列表刷新");

                // 4. 点击「退出房间」：刷新计数保持为 1 (0 次额外网络请求)
                controller.LeaveRoom();
                Assert.AreEqual(1, refreshCount, "退出房间操作绝对不触发房间列表刷新");

                // 5. 连续多次进出房间：刷新计数仍保持为 1
                for (int i = 0; i < 5; i++)
                {
                    controller.JoinRoom();
                    controller.LeaveRoom();
                }
                Assert.AreEqual(1, refreshCount, "多次进出房间后，房间列表刷新计数必须保持不变 (0 次额外请求)");

                // 6. 手动点击页面上的「刷新按钮」：刷新计数累加 1
                controller.SimulateLobbyRefreshClick();
                Assert.AreEqual(2, refreshCount, "手动点击刷新按钮必须触发 1 次房间列表刷新");

                // 7. 再次手动点击刷新按钮：刷新计数累加至 3
                controller.SimulateLobbyRefreshClick();
                Assert.AreEqual(3, refreshCount, "再次手动点击刷新按钮必须累加触发 1 次房间列表刷新");
            }
            finally
            {
                Object.DestroyImmediate(controllerGo);
                Object.DestroyImmediate(lobbyGo);
            }
        }

        #endregion
    }
}
