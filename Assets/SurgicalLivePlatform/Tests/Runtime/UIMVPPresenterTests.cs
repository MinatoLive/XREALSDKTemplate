using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using SurgicalLivePlatform.Runtime.Auth;
using SurgicalLivePlatform.Runtime.DTOs;
using SurgicalLivePlatform.Runtime.Models;
using SurgicalLivePlatform.Runtime.Services;
using SurgicalLivePlatform.Runtime;
using SurgicalLivePlatform.Runtime.UI;

namespace SurgicalLivePlatform.Tests.Runtime
{
    [TestFixture]
    public class UIMVPPresenterTests
    {
        #region Mock Implementations

        private class FakeUserLoginView : IUserLoginView
        {
            public string ViewId => "FakeLoginView";
            public bool IsVisible { get; private set; } = true;
            public bool LoadingState { get; private set; }
            public string LastErrorMessage { get; private set; }
            public string CurrentAccount { get; private set; }
            public string CurrentPassword { get; private set; }

            public event Action<string, string> OnLoginRequested;
            public event Action OnExitRequested;

            public void InitializeView() { }
            public void Show() { IsVisible = true; }
            public void Hide() { IsVisible = false; }

            public void SetLoading(bool isLoading) => LoadingState = isLoading;
            public void SetCredentials(string account, string password)
            {
                CurrentAccount = account;
                CurrentPassword = password;
            }
            public void ShowErrorMessage(string message) => LastErrorMessage = message;

            public void TriggerLogin(string acc, string pass) => OnLoginRequested?.Invoke(acc, pass);
            public void TriggerExit() => OnExitRequested?.Invoke();
        }

        private class FakeRoomLobbyView : IRoomLobbyView
        {
            public string ViewId => "FakeLobbyView";
            public bool IsVisible { get; private set; } = true;
            public List<RoomItem> RenderedRooms { get; private set; } = new List<RoomItem>();
            public bool CanCreate { get; private set; }
            public bool LoadingState { get; private set; }
            public string LastErrorMessage { get; private set; }

            public int PageSize { get; set; } = 8;
            public int CurrentPage { get; private set; }
            public int TotalPages { get; private set; }
            public int TotalItemCount { get; private set; }

            public event Action OnRefreshRequested;
            public event Action OnPreviousPageRequested;
            public event Action OnNextPageRequested;
            public event Action OnImmediateLiveRequested;
            public event Action OnScheduledLiveRequested;
            public event Action<string> OnJoinRoomRequested;
            public event Action<string> OnFilterChanged;

            public void InitializeView() { }
            public void Show() { IsVisible = true; }
            public void Hide() { IsVisible = false; }

            public void SetRoomList(List<RoomItem> rooms, bool append)
            {
                if (!append) RenderedRooms.Clear();
                if (rooms != null) RenderedRooms.AddRange(rooms);
            }

            public void SetCanCreateRoom(bool canCreate) => CanCreate = canCreate;
            public void SetLoading(bool isLoading) => LoadingState = isLoading;
            public void SetPaginationState(int currentPage, int totalPages, int totalItemCount)
            {
                CurrentPage = currentPage;
                TotalPages = totalPages;
                TotalItemCount = totalItemCount;
            }
            public void ShowErrorMessage(string message) => LastErrorMessage = message;

            public void TriggerRefresh() => OnRefreshRequested?.Invoke();
            public void TriggerPreviousPage() => OnPreviousPageRequested?.Invoke();
            public void TriggerNextPage() => OnNextPageRequested?.Invoke();
            public void TriggerFilter(string status) => OnFilterChanged?.Invoke(status);
            public void TriggerImmediate() => OnImmediateLiveRequested?.Invoke();
            public void TriggerScheduled() => OnScheduledLiveRequested?.Invoke();
            public void TriggerJoin(string id) => OnJoinRoomRequested?.Invoke(id);
        }

        private class FakeRoomModalView : IRoomModalView
        {
            public string ViewId => "FakeModalView";
            public bool IsVisible { get; private set; } = true;
            public List<string> Departments { get; private set; } = new List<string>();
            public bool SubmittingState { get; private set; }
            public string LastErrorMessage { get; private set; }
            public bool FormCleared { get; private set; }

            public event Action<RoomFormData> OnSubmitRequested;
            public event Action OnCloseRequested;

            public void InitializeView() { }
            public void Show() { IsVisible = true; }
            public void Hide() { IsVisible = false; }

            public void SetDepartments(List<string> departments)
            {
                Departments.Clear();
                if (departments != null) Departments.AddRange(departments);
            }

            public void SetSubmitting(bool isSubmitting) => SubmittingState = isSubmitting;
            public void ShowErrorMessage(string message) => LastErrorMessage = message;
            public void ClearForm() => FormCleared = true;

            public void TriggerSubmit(RoomFormData data) => OnSubmitRequested?.Invoke(data);
            public void TriggerClose() => OnCloseRequested?.Invoke();
        }

        private class MockAuthService : IAuthService
        {
            public string CurrentUserId => "mock_user_1";
            public bool LoginCalled { get; private set; }
            public bool CreateRoomCalled { get; private set; }
            public bool UpdateRoomCalled { get; private set; }

            public List<RoomItem> MockRooms { get; set; } = new List<RoomItem>();
            public UserInfo MockUserInfo { get; set; } = new UserInfo
            {
                id = "mock_user_1",
                username = "admin",
                permissions = new string[] { "room.create" }
            };

            public Task LoginAsync(string username, string password)
            {
                if (username == "fail") throw new Exception("Invalid password");
                LoginCalled = true;
                return Task.CompletedTask;
            }

            public int GetRoomInfosCallCount { get; private set; } = 0;

            public Task<RoomList> GetRoomInfosAsync(int page = 1, int size = 10, string hospitalId = null, string departmentId = null, string status = null)
            {
                GetRoomInfosCallCount++;
                return Task.FromResult(new RoomList { list = MockRooms, total = MockRooms.Count });
            }

            public Task<UserInfo> GetUserInfoAsync() => Task.FromResult(MockUserInfo);

            public Task<RoomItem> CreateRoomAsync(CreateRoomRequest request)
            {
                CreateRoomCalled = true;
                return Task.FromResult(new RoomItem { id = "new_room_1", name = request.name });
            }

            public Task<RoomItem> UpdateRoomAsync(string roomId, UpdateRoomRequest request)
            {
                UpdateRoomCalled = true;
                return Task.FromResult(new RoomItem { id = roomId, name = request.name });
            }

            public Task LoginWithDeviceAsync(string deviceSn, string deviceSecret) => Task.CompletedTask;
            public Task<string> SetupPatternAsync(string deviceSn, string patternHash) => Task.FromResult("ok");
            public Task VerifyPatternAsync(string userId, string deviceSn, string patternHash) => Task.CompletedTask;
            public Task RefreshTokenAsync() => Task.CompletedTask;
            public Task LogoutAsync() => Task.CompletedTask;
            public Task<RoomItem> GetRoomDetailAsync(string roomId) => Task.FromResult(new RoomItem { id = roomId });
            public Task<List<string>> GetRoomMembersAsync(string roomId) => Task.FromResult(new List<string>());
            public Task<string> InviteToRoomAsync(string roomId, InviteToRoomRequest request) => Task.FromResult("ok");
            public Task<JoinRoomResponse> JoinRoomAsync(string roomId) => Task.FromResult(new JoinRoomResponse());
            public Task<string> StartRoomAsync(string roomId) => Task.FromResult("ok");
            public Task<string> EndRoomAsync(string roomId) => Task.FromResult("ok");
            public Task<string> UpdateMemberPermissionAsync(string roomId, string userId, UpdatePermissionRequest request) => Task.FromResult("ok");
            public Task<string> KickMemberAsync(string roomId, KickMemberRequest request) => Task.FromResult("ok");
            public Task<List<AnnotationData>> GetHistoryAnnotationsAsync(string roomId) => Task.FromResult(new List<AnnotationData>());
            public Task<List<ChatMessage>> GetHistoryMessagesAsync(string roomId) => Task.FromResult(new List<ChatMessage>());
            public Task<string> SendMessageAsync(string roomId, SendMessageRequest request) => Task.FromResult("ok");
            public Task<string> SendAnnotationAsync(string roomId, SendAnnotationRequest request) => Task.FromResult("ok");
            public Task<string> UpdateProfileAsync(UpdateProfileRequest request) => Task.FromResult("ok");
            public Task<string> UpdatePasswordAsync(UpdatePasswordRequest request) => Task.FromResult("ok");
            public Task<string> UploadAvatarAsync(byte[] imageBytes, string fileName) => Task.FromResult("ok");
            public Task<UserList> GetUsersAsync(int page = 1, int size = 20, string departmentId = null, string role = null, string keyword = null) => Task.FromResult(new UserList());
            public Task<UserInfo> GetUserByIdAsync(string userId) => Task.FromResult(new UserInfo());
            public Task<string> CreateUserAsync(CreateUserRequest request) => Task.FromResult("ok");
            public Task<string> UpdateUserAsync(string userId, UpdateUserRequest request) => Task.FromResult("ok");
            public Task<string> UpdateUserStatusAsync(string userId, UpdateUserStatusRequest request) => Task.FromResult("ok");
            public Task<string> DeleteUserAsync(string userId) => Task.FromResult("ok");
            public Task<DeviceList> GetDevicesAsync(int page = 1, int size = 20, string hospitalId = null, string departmentId = null, string status = null, string keyword = null) => Task.FromResult(new DeviceList());
            public Task<DeviceInfo> GetDeviceByIdAsync(string deviceId) => Task.FromResult(new DeviceInfo());
            public Task<RegisterDeviceResponse> RegisterDeviceAsync(RegisterDeviceRequest request) => Task.FromResult(new RegisterDeviceResponse());
            public Task<string> DeviceHeartbeatAsync(string deviceId) => Task.FromResult("ok");
            public Task<string> UpdateDeviceCapabilitiesAsync(string deviceId, UpdateDeviceCapabilitiesRequest request) => Task.FromResult("ok");
            public Task<AuditLogList> GetAuditLogsAsync(int page = 1, int size = 20, string action = null, string resource = null, string targetId = null, string userId = null) => Task.FromResult(new AuditLogList());
        }

        #endregion

        [Test]
        public async Task LoginPresenter_EmptyCredentials_ShowsErrorAndDoesNotCallAuth()
        {
            var view = new FakeUserLoginView();
            var auth = new MockAuthService();
            var sm = new AppStateManager();
            var presenter = new LoginPresenter(view, auth, sm);

            bool success = await presenter.ExecuteLoginAsync("", "");

            Assert.IsFalse(success);
            Assert.IsFalse(auth.LoginCalled);
            Assert.IsNotNull(view.LastErrorMessage);
            Assert.AreEqual(UserAppState.Init, sm.CurrentState);
        }

        [Test]
        public async Task LoginPresenter_ValidCredentials_ExecutesLoginAndChangesState()
        {
            var view = new FakeUserLoginView();
            var auth = new MockAuthService();
            var sm = new AppStateManager();
            var presenter = new LoginPresenter(view, auth, sm);

            bool success = await presenter.ExecuteLoginAsync("admin", "123456");

            Assert.IsTrue(success);
            Assert.IsTrue(auth.LoginCalled);
            Assert.AreEqual(UserAppState.LoggedIn, sm.CurrentState);
            Assert.IsFalse(view.LoadingState, "完成后加载状态应被复原为 false");
        }

        [Test]
        public async Task LobbyPresenter_RefreshRooms_PopulatesViewAndChecksPermission()
        {
            var view = new FakeRoomLobbyView();
            var auth = new MockAuthService();
            auth.MockRooms.Add(new RoomItem { id = "room_1", name = "Test Room 1" });
            auth.MockRooms.Add(new RoomItem { id = "room_2", name = "Test Room 2" });

            // 预设已登录状态
            SurgicalLiveSDK.EnsureInitialized();
            SurgicalLiveSDK.StateManager.ChangeState(UserAppState.LoggedIn);

            var presenter = new LobbyPresenter(view, null, auth);

            await presenter.RefreshRoomsAsync();

            Assert.AreEqual(2, view.RenderedRooms.Count);
            Assert.IsTrue(view.CanCreate, "拥有 room.create 权限时应允许创建房间");
        }

        [Test]
        public async Task RoomModalPresenter_ValidateAndSubmit_TriggersSuccess()
        {
            var view = new FakeRoomModalView();
            var auth = new MockAuthService();
            var presenter = new RoomModalPresenter(view, auth);

            bool eventFired = false;
            presenter.OnRoomOperationSuccess += () => eventFired = true;

            var formData = new RoomFormData
            {
                mode = FormMode.Create,
                createMode = RoomCreateMode.Immediate,
                name = "急诊手术直播",
                description = "颅脑微创手术测试",
                department = "神经外科"
            };

            bool ok = await presenter.SubmitFormAsync(formData);

            Assert.IsTrue(ok);
            Assert.IsTrue(auth.CreateRoomCalled);
            Assert.IsTrue(eventFired);
            Assert.IsFalse(view.SubmittingState);
        }

        [Test]
        public async Task RoomModalPresenter_InvalidName_FailsValidation()
        {
            var view = new FakeRoomModalView();
            var auth = new MockAuthService();
            var presenter = new RoomModalPresenter(view, auth);

            var formData = new RoomFormData
            {
                mode = FormMode.Create,
                name = "" // 空名称
            };

            bool ok = await presenter.SubmitFormAsync(formData);

            Assert.IsFalse(ok);
            Assert.IsFalse(auth.CreateRoomCalled);
            Assert.IsNotNull(view.LastErrorMessage);
        }

        [Test]
        public void RoomCardItem_ResolveStreamTypeBadge_MapsCorrectly()
        {
            // 1. liveStereo -> 3D双目立体直播
            var (stereoText1, _, _) = RoomCardItem.ResolveStreamTypeBadge("liveStereo");
            Assert.AreEqual("3D双目立体直播", stereoText1);

            var (stereoText2, _, _) = RoomCardItem.ResolveStreamTypeBadge("LIVESTEREO");
            Assert.AreEqual("3D双目立体直播", stereoText2);

            var (stereoText3, _, _) = RoomCardItem.ResolveStreamTypeBadge("live_stereo");
            Assert.AreEqual("3D双目立体直播", stereoText3);

            // 2. RECORD -> 双目录制 3D (3D RECORD)
            var (recordText, _, _) = RoomCardItem.ResolveStreamTypeBadge("RECORD");
            Assert.AreEqual("双目录制 3D (3D RECORD)", recordText);

            // 3. LIVE -> 实时直播 (LIVE)
            var (liveText, _, _) = RoomCardItem.ResolveStreamTypeBadge("LIVE");
            Assert.AreEqual("实时直播 (LIVE)", liveText);

            // 4. 空值 / null 回退为默认实时直播
            var (defaultText, _, _) = RoomCardItem.ResolveStreamTypeBadge(null);
            Assert.AreEqual("实时直播 (LIVE)", defaultText);
        }

        #region Lobby Pagination & Circular Navigation Tests

        [Test]
        public async Task LobbyPresenter_Pagination_InitialLoad_CachesAllDataAndSlicesFirstPage()
        {
            var view = new FakeRoomLobbyView { PageSize = 6 };
            var auth = new MockAuthService();
            for (int i = 1; i <= 15; i++)
            {
                auth.MockRooms.Add(new RoomItem { id = $"room_{i}", name = $"手术直播间 {i}" });
            }

            var presenter = new LobbyPresenter(view, null, auth);
            await presenter.RefreshRoomsAsync();

            Assert.AreEqual(1, auth.GetRoomInfosCallCount, "大厅初始化应仅发起 1 次接口拉取");
            Assert.AreEqual(15, presenter.CachedRooms.Count, "全量缓存中应有 15 条数据");
            Assert.AreEqual(6, view.RenderedRooms.Count, "首页切片应展示配置的单页数量 6 条");
            Assert.AreEqual("room_1", view.RenderedRooms[0].id);
            Assert.AreEqual("room_6", view.RenderedRooms[5].id);
            Assert.AreEqual(1, view.CurrentPage, "当前页应为第 1 页");
            Assert.AreEqual(3, view.TotalPages, "15 条按每页 6 条计算总页数应为 3 页");
            Assert.AreEqual(15, view.TotalItemCount, "总条数应为 15");
        }

        [Test]
        public async Task LobbyPresenter_Pagination_NextPage_CircularLoopFromLastToFirst()
        {
            var view = new FakeRoomLobbyView { PageSize = 6 };
            var auth = new MockAuthService();
            for (int i = 1; i <= 15; i++)
            {
                auth.MockRooms.Add(new RoomItem { id = $"room_{i}", name = $"手术直播间 {i}" });
            }

            var presenter = new LobbyPresenter(view, null, auth);
            await presenter.RefreshRoomsAsync();

            // 第 1 页 -> 点击下一页 -> 进入第 2 页
            view.TriggerNextPage();
            Assert.AreEqual(2, view.CurrentPage);
            Assert.AreEqual(6, view.RenderedRooms.Count);
            Assert.AreEqual("room_7", view.RenderedRooms[0].id);
            Assert.AreEqual("room_12", view.RenderedRooms[5].id);

            // 第 2 页 -> 点击下一页 -> 进入第 3 页 (最后一页)
            view.TriggerNextPage();
            Assert.AreEqual(3, view.CurrentPage);
            Assert.AreEqual(3, view.RenderedRooms.Count, "最后一页剩余 3 条数据 (13..15)");
            Assert.AreEqual("room_13", view.RenderedRooms[0].id);
            Assert.AreEqual("room_15", view.RenderedRooms[2].id);

            // 最后一页第 3 页 -> 点击下一页 -> 循环回第 1 页 (1>2>3>1)
            view.TriggerNextPage();
            Assert.AreEqual(1, view.CurrentPage, "在最后一页右翻应循环跳回第 1 页");
            Assert.AreEqual(6, view.RenderedRooms.Count);
            Assert.AreEqual("room_1", view.RenderedRooms[0].id);
            Assert.AreEqual(1, auth.GetRoomInfosCallCount, "翻页全程应为纯内存切片，不产生重复网络请求");
        }

        [Test]
        public async Task LobbyPresenter_Pagination_PreviousPage_CircularLoopFromFirstToLast()
        {
            var view = new FakeRoomLobbyView { PageSize = 6 };
            var auth = new MockAuthService();
            for (int i = 1; i <= 15; i++)
            {
                auth.MockRooms.Add(new RoomItem { id = $"room_{i}", name = $"手术直播间 {i}" });
            }

            var presenter = new LobbyPresenter(view, null, auth);
            await presenter.RefreshRoomsAsync();

            // 当前在第 1 页 -> 点击上一页 -> 循环跳转至最后一页第 3 页 (1<3<2<1)
            view.TriggerPreviousPage();
            Assert.AreEqual(3, view.CurrentPage, "在第 1 页左翻应循环跳转至最后一页");
            Assert.AreEqual(3, view.RenderedRooms.Count);
            Assert.AreEqual("room_13", view.RenderedRooms[0].id);

            // 最后一页第 3 页 -> 点击上一页 -> 进入第 2 页
            view.TriggerPreviousPage();
            Assert.AreEqual(2, view.CurrentPage);
            Assert.AreEqual(6, view.RenderedRooms.Count);
            Assert.AreEqual("room_7", view.RenderedRooms[0].id);

            // 第 2 页 -> 点击上一页 -> 进入第 1 页
            view.TriggerPreviousPage();
            Assert.AreEqual(1, view.CurrentPage);
            Assert.AreEqual(6, view.RenderedRooms.Count);
            Assert.AreEqual("room_1", view.RenderedRooms[0].id);
            Assert.AreEqual(1, auth.GetRoomInfosCallCount, "左翻全程同样零网络请求");
        }

        [Test]
        public async Task LobbyPresenter_Pagination_DynamicPageSize_AdaptsTotalPagesAndSlices()
        {
            var view = new FakeRoomLobbyView { PageSize = 6 };
            var auth = new MockAuthService();
            for (int i = 1; i <= 15; i++)
            {
                auth.MockRooms.Add(new RoomItem { id = $"room_{i}", name = $"手术直播间 {i}" });
            }

            var presenter = new LobbyPresenter(view, null, auth);
            await presenter.RefreshRoomsAsync();
            Assert.AreEqual(3, view.TotalPages);

            // 动态修改单页配置为 5
            presenter.PageSize = 5;
            Assert.AreEqual(3, view.TotalPages, "15/5 = 3 页");
            Assert.AreEqual(5, view.RenderedRooms.Count);
            Assert.AreEqual("room_1", view.RenderedRooms[0].id);
            Assert.AreEqual("room_5", view.RenderedRooms[4].id);

            // 动态修改单页配置为 20 (大于总条数)
            presenter.PageSize = 20;
            Assert.AreEqual(1, view.TotalPages, "总数 15 小于单页 20 时总页数为 1");
            Assert.AreEqual(15, view.RenderedRooms.Count);
            Assert.AreEqual("room_15", view.RenderedRooms[14].id);
        }

        [Test]
        public async Task LobbyPresenter_Pagination_FilterChange_RecalculatesPagesAndResetsToPageOne()
        {
            var view = new FakeRoomLobbyView { PageSize = 4 };
            var auth = new MockAuthService();
            // 7 个 live，5 个 idle
            for (int i = 1; i <= 7; i++)
            {
                auth.MockRooms.Add(new RoomItem { id = $"live_{i}", name = $"直播间 {i}", status = "live" });
            }
            for (int i = 1; i <= 5; i++)
            {
                auth.MockRooms.Add(new RoomItem { id = $"idle_{i}", name = $"预告间 {i}", status = "idle" });
            }

            var presenter = new LobbyPresenter(view, null, auth);
            await presenter.RefreshRoomsAsync();

            Assert.AreEqual(12, view.TotalItemCount);
            Assert.AreEqual(3, view.TotalPages, "12 条 / 4 = 3 页");

            // 翻到第 2 页
            view.TriggerNextPage();
            Assert.AreEqual(2, view.CurrentPage);

            // 切换筛选为 "live"
            view.TriggerFilter("live");
            Assert.AreEqual(1, view.CurrentPage, "筛选变动后应重置回第 1 页");
            Assert.AreEqual(7, view.TotalItemCount, "过滤后 live 房间为 7 条");
            Assert.AreEqual(2, view.TotalPages, "7 条 / 4 = 2 页");
            Assert.AreEqual(4, view.RenderedRooms.Count);
            Assert.IsTrue(view.RenderedRooms.TrueForAll(r => r.status == "live"));

            // 切换回全量
            view.TriggerFilter("");
            Assert.AreEqual(1, view.CurrentPage);
            Assert.AreEqual(12, view.TotalItemCount);
            Assert.AreEqual(3, view.TotalPages);
            Assert.AreEqual(1, auth.GetRoomInfosCallCount, "内存筛选与翻页均无网络请求");
        }

        [Test]
        public async Task LobbyPresenter_Pagination_EmptyDataset_HandlesGracefullyWithoutErrors()
        {
            var view = new FakeRoomLobbyView { PageSize = 6 };
            var auth = new MockAuthService(); // 0 条数据

            var presenter = new LobbyPresenter(view, null, auth);
            await presenter.RefreshRoomsAsync();

            Assert.AreEqual(0, view.TotalItemCount);
            Assert.AreEqual(0, view.TotalPages);
            Assert.AreEqual(0, view.CurrentPage);
            Assert.AreEqual(0, view.RenderedRooms.Count);

            // 空数据下点击翻页不抛异常且保持 0 状态
            view.TriggerNextPage();
            Assert.AreEqual(0, view.CurrentPage);
            Assert.AreEqual(0, view.TotalPages);

            view.TriggerPreviousPage();
            Assert.AreEqual(0, view.CurrentPage);
            Assert.AreEqual(0, view.TotalPages);
        }

        [Test]
        public async Task LobbyPresenter_NoDragRefreshLoop_StableRenderOnPageTurning()
        {
            var view = new FakeRoomLobbyView { PageSize = 4 };
            var auth = new MockAuthService();
            for (int i = 1; i <= 10; i++)
            {
                auth.MockRooms.Add(new RoomItem { id = $"room_{i}", name = $"房间 {i}" });
            }

            var presenter = new LobbyPresenter(view, null, auth);
            await presenter.RefreshRoomsAsync();

            Assert.AreEqual(1, auth.GetRoomInfosCallCount, "初次加载应仅有 1 次网络调用");
            Assert.AreEqual(4, view.RenderedRooms.Count);

            // 连续执行多次翻页操作，确认没有产生任何多余的网络循环或状态异常
            for (int i = 0; i < 10; i++)
            {
                view.TriggerNextPage();
            }

            Assert.AreEqual(1, auth.GetRoomInfosCallCount, "移除拖动加载后，纯翻页流程绝对不产生网络递归");
            Assert.IsTrue(view.RenderedRooms.Count > 0, "每次切片均稳定保有对应页码数据");
        }

        [Test]
        public async Task LobbyPresenter_RefreshTriggerIsolation_OnlyExplicitRefresh_NoUnintendedCalls()
        {
            var view = new FakeRoomLobbyView { PageSize = 4 };
            var auth = new MockAuthService();
            for (int i = 1; i <= 6; i++)
            {
                auth.MockRooms.Add(new RoomItem { id = $"room_{i}", name = $"房间 {i}" });
            }

            SurgicalLiveSDK.EnsureInitialized();
            SurgicalLiveSDK.StateManager.ChangeState(UserAppState.LoggedIn);

            var presenter = new LobbyPresenter(view, null, auth);

            // 1. 初始化及 View 显隐切换不应触发任何网络请求
            Assert.AreEqual(0, auth.GetRoomInfosCallCount, "初始化 Presenter 时不应主动发起网络刷新");
            view.Show();
            view.Hide();
            view.Show();
            Assert.AreEqual(0, auth.GetRoomInfosCallCount, "单纯 View.Show/Hide 绝不发起网络请求");

            // 2. 手动点击/请求刷新触发 1 次网络调用
            view.TriggerRefresh();
            // 等待异步刷新完成
            await Task.Yield();
            Assert.AreEqual(1, auth.GetRoomInfosCallCount, "手动触发刷新后应执行 1 次网络数据拉取");
            Assert.AreEqual(4, view.RenderedRooms.Count, "渲染第一页数据");

            // 3. 翻页与筛选均基于本地缓存，不触发网络拉取
            view.TriggerNextPage();
            view.TriggerPreviousPage();
            view.TriggerFilter("live");
            view.TriggerFilter("");
            Assert.AreEqual(1, auth.GetRoomInfosCallCount, "翻页与筛选不应触发任何网络刷新");

            // 4. 再次显式调用 RefreshRoomsAsync，网络调用计数累加 1
            await presenter.RefreshRoomsAsync();
            Assert.AreEqual(2, auth.GetRoomInfosCallCount, "再次手动刷新应有且仅有 1 次新增网络调用");
        }

        #endregion
    }
}
