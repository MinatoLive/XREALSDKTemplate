using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using SurgicalLivePlatform.Runtime.Auth;
using SurgicalLivePlatform.Runtime.DTOs;
using SurgicalLivePlatform.Runtime.Models;
using SurgicalLivePlatform.Runtime.Network;
using SurgicalLivePlatform.Runtime.Services;
using SurgicalLivePlatform.Runtime.Storage;

namespace SurgicalLivePlatform.Tests
{
    [TestFixture]
    public class AuthRoutingAndTokenRefreshTests
    {
        #region Mock Implementations

        private class MockStorage : ILocalStorage
        {
            public readonly Dictionary<string, UserLocalData> Users = new Dictionary<string, UserLocalData>();

            public Task InitializeAsync() => Task.CompletedTask;

            public Task SaveUserAsync(UserLocalData user)
            {
                Users[user.user_id] = user;
                return Task.CompletedTask;
            }

            public Task RemoveUserAsync(string userId)
            {
                Users.Remove(userId);
                return Task.CompletedTask;
            }

            public Task<UserLocalData> GetUserAsync(string userId)
            {
                Users.TryGetValue(userId, out var user);
                return Task.FromResult(user);
            }

            public Task<List<UserLocalData>> GetAllUsersAsync() => Task.FromResult(new List<UserLocalData>(Users.Values));

            public Task ClearAllAsync()
            {
                Users.Clear();
                return Task.CompletedTask;
            }
        }

        private class MockNetworkDispatcher : INetworkEventDispatcher
        {
            public event Action OnAuthExpired;
            public event Action OnAccountForbidden;
            public event Action OnNetworkTimeout;

            public void TriggerAuthExpired() => OnAuthExpired?.Invoke();
            public void TriggerAccountForbidden() => OnAccountForbidden?.Invoke();
            public void TriggerNetworkTimeout() => OnNetworkTimeout?.Invoke();
        }

        private class MockApiClient : IApiClient
        {
            public string AccessToken { get; private set; }
            public bool HasValidAccessToken => !string.IsNullOrEmpty(AccessToken);

            public int RefreshCallCount { get; private set; }
            public int GetRoomInfosCallCount { get; private set; }
            public bool FailRefreshWith401 { get; set; } = false;

            // 用于模拟 401 发生次数
            public int Remaining401ErrorsOnGetRoom = 0;

            public void SetAccessToken(string token)
            {
                AccessToken = token;
            }

            public Task<AuthResponse> LoginAsync(LoginRequest request)
            {
                return Task.FromResult(new AuthResponse
                {
                    access_token = "mock_access_token_1",
                    refresh_token = "mock_refresh_token_1",
                    user = new UserInfo { id = "user_1", username = request.username, name = "Test User" }
                });
            }

            public Task<AuthResponse> DeviceLoginAsync(DeviceLoginRequest request) => throw new NotImplementedException();
            public Task<string> SetupPatternAsync(PatternSetupRequest request) => throw new NotImplementedException();
            public Task<AuthResponse> VerifyPatternAsync(PatternVerifyRequest request) => throw new NotImplementedException();

            public async Task<AuthResponse> RefreshTokenAsync(RefreshRequest request)
            {
                RefreshCallCount++;
                // 模拟稍微延迟，方便并发测试
                await Task.Delay(20);

                if (FailRefreshWith401)
                {
                    throw new Exception("API Error: 401 Unauthorized - Refresh token expired");
                }

                return new AuthResponse
                {
                    access_token = $"mock_refreshed_access_{RefreshCallCount}",
                    refresh_token = $"mock_refreshed_refresh_{RefreshCallCount}",
                    user = new UserInfo { id = "user_1", username = "test", name = "Test User" }
                };
            }

            public Task<UserInfo> GetUserInfoAsync() => throw new NotImplementedException();

            public async Task<RoomList> GetRoomInfosAsync(int page = 1, int size = 10, string hospitalId = null, string departmentId = null, string status = null)
            {
                GetRoomInfosCallCount++;
                
                // 网络守卫检测：若未设置 token，抛出未授权异常
                if (string.IsNullOrEmpty(AccessToken))
                {
                    throw new UnauthorizedAccessException("[路由守卫拦截] 未通过身份验证，禁止向受保护端点发起请求");
                }

                if (Remaining401ErrorsOnGetRoom > 0)
                {
                    Remaining401ErrorsOnGetRoom--;
                    throw new Exception("API Error: 401 Unauthorized - Token Expired");
                }

                await Task.Delay(10);
                return new RoomList
                {
                    total = 1,
                    list = new List<RoomItem>
                    {
                        new RoomItem { id = "room_1", name = "Surgery Room 1", status = "LIVE" }
                    }
                };
            }

            public Task<RoomItem> GetRoomDetailAsync(string roomId) => throw new NotImplementedException();
            public Task<RoomItem> CreateRoomAsync(CreateRoomRequest request) => throw new NotImplementedException();
            public Task<RoomItem> UpdateRoomAsync(string roomId, UpdateRoomRequest request) => throw new NotImplementedException();
            public Task<List<string>> GetRoomMembersAsync(string roomId) => throw new NotImplementedException();
            public Task<string> InviteToRoomAsync(string roomId, InviteToRoomRequest request) => throw new NotImplementedException();
            public Task<JoinRoomResponse> JoinRoomAsync(string roomId) => throw new NotImplementedException();
            public Task<string> StartRoomAsync(string roomId) => throw new NotImplementedException();
            public Task<string> EndRoomAsync(string roomId) => throw new NotImplementedException();
            public Task<string> UpdateMemberPermissionAsync(string roomId, string userId, UpdatePermissionRequest request) => throw new NotImplementedException();
            public Task<string> KickMemberAsync(string roomId, KickMemberRequest request) => throw new NotImplementedException();
            public Task<List<AnnotationData>> GetHistoryAnnotationsAsync(string roomId) => throw new NotImplementedException();
            public Task<List<ChatMessage>> GetHistoryMessagesAsync(string roomId) => throw new NotImplementedException();
            public Task<string> SendMessageAsync(string roomId, SendMessageRequest request) => throw new NotImplementedException();
            public Task<string> SendAnnotationAsync(string roomId, SendAnnotationRequest request) => throw new NotImplementedException();
            public Task<string> UpdateProfileAsync(UpdateProfileRequest request) => throw new NotImplementedException();
            public Task<string> UpdatePasswordAsync(UpdatePasswordRequest request) => throw new NotImplementedException();
            public Task<string> UploadAvatarAsync(byte[] imageBytes, string fileName) => throw new NotImplementedException();
            public Task<UserList> GetUsersAsync(int page = 1, int size = 20, string departmentId = null, string role = null, string keyword = null) => throw new NotImplementedException();
            public Task<UserInfo> GetUserByIdAsync(string userId) => throw new NotImplementedException();
            public Task<string> CreateUserAsync(CreateUserRequest request) => throw new NotImplementedException();
            public Task<string> UpdateUserAsync(string userId, UpdateUserRequest request) => throw new NotImplementedException();
            public Task<string> UpdateUserStatusAsync(string userId, UpdateUserStatusRequest request) => throw new NotImplementedException();
            public Task<string> DeleteUserAsync(string userId) => throw new NotImplementedException();
            public Task<DeviceList> GetDevicesAsync(int page = 1, int size = 20, string hospitalId = null, string departmentId = null, string status = null, string keyword = null) => throw new NotImplementedException();
            public Task<DeviceInfo> GetDeviceByIdAsync(string deviceId) => throw new NotImplementedException();
            public Task<RegisterDeviceResponse> RegisterDeviceAsync(RegisterDeviceRequest request) => throw new NotImplementedException();
            public Task<string> DeviceHeartbeatAsync(string deviceId) => throw new NotImplementedException();
            public Task<string> UpdateDeviceCapabilitiesAsync(string deviceId, UpdateDeviceCapabilitiesRequest request) => throw new NotImplementedException();
            public Task<AuditLogList> GetAuditLogsAsync(int page = 1, int size = 20, string action = null, string resource = null, string targetId = null, string userId = null) => throw new NotImplementedException();
        }

        #endregion

        private MockApiClient _mockApiClient;
        private MockStorage _mockStorage;
        private MockNetworkDispatcher _mockDispatcher;
        private AuthService _authService;

        [SetUp]
        public void Setup()
        {
            _mockApiClient = new MockApiClient();
            _mockStorage = new MockStorage();
            _mockDispatcher = new MockNetworkDispatcher();
            _authService = new AuthService(_mockApiClient, _mockStorage, _mockDispatcher);
        }

        [Test]
        public void RouteGuard_WhenUnauthenticated_BlocksBusinessRequest()
        {
            // Arrange: 未登录，无 Token
            _mockApiClient.SetAccessToken(null);

            // Act & Assert: 发起获取房间列表请求应被守卫拦截
            var ex = Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            {
                await _authService.GetRoomInfosAsync(1, 10);
            });

            Assert.IsTrue(ex.Message.Contains("路由守卫拦截") || ex.Message.Contains("未通过身份验证"));
        }

        [Test]
        public async Task RouteGuard_WhenAuthenticated_AllowsBusinessRequest()
        {
            // Arrange: 登录
            await _authService.LoginAsync("user1", "pass123");

            // Act: 登录后发起业务请求
            var roomList = await _authService.GetRoomInfosAsync(1, 10);

            // Assert: 请求成功放行
            Assert.IsNotNull(roomList);
            Assert.AreEqual(1, roomList.total);
            Assert.AreEqual("mock_access_token_1", _mockApiClient.AccessToken);
        }

        [Test]
        public async Task TokenRefresh_On401_AutomaticallyRefreshesAndRetries()
        {
            // Arrange: 登录
            await _authService.LoginAsync("user1", "pass123");

            // 模拟下一次业务请求返回 401（Token过期）
            _mockApiClient.Remaining401ErrorsOnGetRoom = 1;

            // Act: 发起业务请求
            var roomList = await _authService.GetRoomInfosAsync(1, 10);

            // Assert: 应该自动刷新了 1 次 Token，并且原业务请求成功重试返回结果
            Assert.AreEqual(1, _mockApiClient.RefreshCallCount, "应当自动触发一次 RefreshToken");
            Assert.AreEqual(2, _mockApiClient.GetRoomInfosCallCount, "业务请求应在 401 后重试了一次 (总计调用2次)");
            Assert.IsNotNull(roomList);
            Assert.AreEqual("mock_refreshed_access_1", _mockApiClient.AccessToken);
        }

        [Test]
        public async Task TokenRefresh_Concurrent401_RefreshesOnlyOnce()
        {
            // Arrange: 登录
            await _authService.LoginAsync("user1", "pass123");

            // 设置两次 401 模拟并发请求
            _mockApiClient.Remaining401ErrorsOnGetRoom = 2;

            // Act: 并发触发 2 个业务请求
            var task1 = _authService.GetRoomInfosAsync(1, 10);
            var task2 = _authService.GetRoomInfosAsync(1, 10);

            var results = await Task.WhenAll(task1, task2);

            // Assert: 无论并发多少个，RefreshToken 只能被调用 1 次（防击穿）
            Assert.AreEqual(1, _mockApiClient.RefreshCallCount, "并发请求只应触发 1 次网络刷新");
            Assert.IsNotNull(results[0]);
            Assert.IsNotNull(results[1]);
        }

        [Test]
        public async Task TokenRefresh_WhenRefreshTokenExpired_TriggersLogout()
        {
            // Arrange: 登录
            await _authService.LoginAsync("user1", "pass123");

            // 模拟 RefreshToken 也过期返回 401
            _mockApiClient.Remaining401ErrorsOnGetRoom = 1;
            _mockApiClient.FailRefreshWith401 = true;

            bool authExpiredTriggered = false;
            _mockDispatcher.OnAuthExpired += () => authExpiredTriggered = true;

            // Act & Assert: 刷新失败应抛出登录过期异常
            var ex = Assert.ThrowsAsync<Exception>(async () =>
            {
                await _authService.GetRoomInfosAsync(1, 10);
            });

            Assert.IsTrue(ex.Message.Contains("登录已过期") || ex.Message.Contains("Auth Expired"));
            Assert.IsTrue(authExpiredTriggered, "必须广播 OnAuthExpired 事件");
            Assert.IsNull(_authService.CurrentUserId, "CurrentUserId 应当被清除");
            Assert.IsNull(_mockApiClient.AccessToken, "AccessToken 应当被清空");
        }
    }
}
