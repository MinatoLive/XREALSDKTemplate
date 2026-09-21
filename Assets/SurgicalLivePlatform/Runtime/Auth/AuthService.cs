using SurgicalLivePlatform.Runtime.DTOs;
using SurgicalLivePlatform.Runtime.Network;
using SurgicalLivePlatform.Runtime.Storage;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SurgicalLivePlatform.Runtime.Auth
{
    /// <summary>
    /// 全局 API 业务代理服务类
    /// 统一对应 HttpClientService 的所有接口，并提供 401 Token 失效时的自动续期（重试）机制。
    /// </summary>
    public class AuthService : IAuthService
    {
        #region 内部字段与属性 (Internal Fields & Properties)

        private readonly IApiClient _apiClient;
        private readonly ILocalStorage _storage;
        private readonly INetworkEventDispatcher _dispatcher;
        
        // 信号量锁：用于 401 并发控制，确保只有一个线程执行 RefreshToken
        private readonly SemaphoreSlim _refreshLock = new SemaphoreSlim(1, 1);
        
        /// <summary>
        /// 当前登录的用户 ID
        /// </summary>
        public string CurrentUserId { get; set; }

        #endregion

        #region 构造函数 (Constructor)

        /// <summary>
        /// 依赖注入构造函数
        /// </summary>
        /// <param name="apiClient">API 网络客户端</param>
        /// <param name="storage">本地存储服务</param>
        /// <param name="dispatcher">网络事件分发器</param>
        public AuthService(IApiClient apiClient, ILocalStorage storage, INetworkEventDispatcher dispatcher = null)
        {
            _apiClient = apiClient;
            _storage = storage;
            _dispatcher = dispatcher;
        }

        #endregion

        #region 模块 1: 认证与用户信息 (Auth & User)

        /// <summary>
        /// 常规账号密码登录
        /// </summary>
        public async Task LoginAsync(string username, string password)
        {
            var request = new LoginRequest { username = username, password = password };
            var response = await _apiClient.LoginAsync(request);
            await HandleAuthSuccessAsync(response);
        }

        /// <summary>
        /// 硬件设备凭证登录
        /// </summary>
        public async Task LoginWithDeviceAsync(string deviceSn, string deviceSecret)
        {
            var request = new DeviceLoginRequest { device_sn = deviceSn, device_secret = deviceSecret };
            var response = await _apiClient.DeviceLoginAsync(request);
            await HandleAuthSuccessAsync(response);
        }

        /// <summary>
        /// 设置设备的九宫格图案密码
        /// </summary>
        public async Task<string> SetupPatternAsync(string deviceSn, string patternHash)
        {
            return await ExecuteWithRetryAsync(() => 
                _apiClient.SetupPatternAsync(new PatternSetupRequest { device_sn = deviceSn, pattern_hash = patternHash })
            );
        }

        /// <summary>
        /// XR 头显端图案密码快捷验证
        /// </summary>
        public async Task VerifyPatternAsync(string userId, string deviceSn, string patternHash)
        {
            var request = new PatternVerifyRequest { user_id = userId, device_sn = deviceSn, pattern_hash = patternHash };
            var response = await _apiClient.VerifyPatternAsync(request);
            await HandleAuthSuccessAsync(response);
        }

        /// <summary>
        /// 带有 401 自动重试机制的用户信息拉取
        /// </summary>
        public async Task<UserInfo> GetUserInfoAsync()
        {
            return await ExecuteWithRetryAsync(() => _apiClient.GetUserInfoAsync());
        }

        /// <summary>
        /// 统一处理认证成功后的回调
        /// 将 Token 写入 API 客户端，并保存用户基础信息到本地缓存
        /// </summary>
        private async Task HandleAuthSuccessAsync(AuthResponse response)
        {
            if (response == null || response.user == null) 
            {
                throw new Exception("API登录成功，但返回的用户数据解析失败 (Failed to parse AuthResponse.user)");
            }
            
            CurrentUserId = response.user.id;
            _apiClient.SetAccessToken(response.access_token);

            var userLocalData = new UserLocalData
            {
                user_id = response.user.id,
                username = response.user.username,
                display_name = response.user.name,
                avatar_url = response.user.avatar_url,
                access_token = response.access_token,
                refresh_token = response.refresh_token,
                last_login_time = DateTime.UtcNow.Ticks
            };

            await _storage.SaveUserAsync(userLocalData);
        }

        /// <summary>
        /// 静默刷新 Token
        /// 使用 SemaphoreSlim 保证多个并发失败的请求只会触发一次网络刷新
        /// </summary>
        public async Task RefreshTokenAsync()
        {
            await _refreshLock.WaitAsync();
            try
            {
                if (string.IsNullOrEmpty(CurrentUserId)) throw new Exception("当前无登录用户，无法刷新 Token (No user logged in)");

                var userData = await _storage.GetUserAsync(CurrentUserId);
                if (userData == null || string.IsNullOrEmpty(userData.refresh_token))
                {
                    throw new Exception("当前用户无本地刷新令牌 (No refresh token available)");
                }

                var request = new RefreshRequest { refresh_token = userData.refresh_token };
                try
                {
                    var response = await _apiClient.RefreshTokenAsync(request);
                    await HandleAuthSuccessAsync(response);
                }
                catch (Exception ex) when (ex.Message.Contains("401") || ex.Message.Contains("403"))
                {
                    // Refresh token is invalid/expired
                    await _storage.RemoveUserAsync(CurrentUserId);
                    CurrentUserId = null;
                    _apiClient.SetAccessToken(null);
                    _dispatcher?.TriggerAuthExpired();
                    throw new Exception("登录已过期，请重新登录 (Auth Expired)", ex);
                }
            }
            finally
            {
                _refreshLock.Release();
            }
        }

        /// <summary>
        /// 清除当前客户端登录状态
        /// </summary>
        public async Task LogoutAsync()
        {
            CurrentUserId = null;
            _apiClient.SetAccessToken(null);
        }

        #endregion

        #region 模块 2: 房间生命周期与入会 (Room)

        public async Task<RoomList> GetRoomInfosAsync(int page = 1, int size = 10, string hospitalId = null, string departmentId = null, string status = null)
        {
            return await ExecuteWithRetryAsync(() => _apiClient.GetRoomInfosAsync(page, size, hospitalId, departmentId, status));
        }

        public async Task<RoomItem> GetRoomDetailAsync(string roomId)
        {
            return await ExecuteWithRetryAsync(() => _apiClient.GetRoomDetailAsync(roomId));
        }

        public async Task<RoomItem> CreateRoomAsync(CreateRoomRequest request)
        {
            return await ExecuteWithRetryAsync(() => _apiClient.CreateRoomAsync(request));
        }

        public async Task<RoomItem> UpdateRoomAsync(string roomId, UpdateRoomRequest request)
        {
            return await ExecuteWithRetryAsync(() => _apiClient.UpdateRoomAsync(roomId, request));
        }

        public async Task<List<string>> GetRoomMembersAsync(string roomId)
        {
            return await ExecuteWithRetryAsync(() => _apiClient.GetRoomMembersAsync(roomId));
        }

        public async Task<string> InviteToRoomAsync(string roomId, InviteToRoomRequest request)
        {
            return await ExecuteWithRetryAsync(() => _apiClient.InviteToRoomAsync(roomId, request));
        }

        public async Task<JoinRoomResponse> JoinRoomAsync(string roomId)
        {
            return await ExecuteWithRetryAsync(() => _apiClient.JoinRoomAsync(roomId));
        }

        public async Task<string> StartRoomAsync(string roomId)
        {
            return await ExecuteWithRetryAsync(() => _apiClient.StartRoomAsync(roomId));
        }

        public async Task<string> EndRoomAsync(string roomId)
        {
            return await ExecuteWithRetryAsync(() => _apiClient.EndRoomAsync(roomId));
        }

        #endregion

        #region 模块 3: 会中互动与信令管理 (In-Room Control)

        public async Task<string> UpdateMemberPermissionAsync(string roomId, string userId, UpdatePermissionRequest request)
        {
            return await ExecuteWithRetryAsync(() => _apiClient.UpdateMemberPermissionAsync(roomId, userId, request));
        }

        public async Task<string> KickMemberAsync(string roomId, KickMemberRequest request)
        {
            return await ExecuteWithRetryAsync(() => _apiClient.KickMemberAsync(roomId, request));
        }

        public async Task<List<AnnotationData>> GetHistoryAnnotationsAsync(string roomId)
        {
            return await ExecuteWithRetryAsync(() => _apiClient.GetHistoryAnnotationsAsync(roomId));
        }

        public async Task<List<ChatMessage>> GetHistoryMessagesAsync(string roomId)
        {
            return await ExecuteWithRetryAsync(() => _apiClient.GetHistoryMessagesAsync(roomId));
        }

        public async Task<string> SendMessageAsync(string roomId, SendMessageRequest request)
        {
            return await ExecuteWithRetryAsync(() => _apiClient.SendMessageAsync(roomId, request));
        }

        public async Task<string> SendAnnotationAsync(string roomId, SendAnnotationRequest request)
        {
            return await ExecuteWithRetryAsync(() => _apiClient.SendAnnotationAsync(roomId, request));
        }

        #endregion

        #region 模块 5: 用户设置与账号管理 (User & Account Management)

        public async Task<string> UpdateProfileAsync(UpdateProfileRequest request)
        {
            return await ExecuteWithRetryAsync(() => _apiClient.UpdateProfileAsync(request));
        }

        public async Task<string> UpdatePasswordAsync(UpdatePasswordRequest request)
        {
            return await ExecuteWithRetryAsync(() => _apiClient.UpdatePasswordAsync(request));
        }

        public async Task<string> UploadAvatarAsync(byte[] imageBytes, string fileName)
        {
            return await ExecuteWithRetryAsync(() => _apiClient.UploadAvatarAsync(imageBytes, fileName));
        }

        public async Task<UserList> GetUsersAsync(int page = 1, int size = 20, string departmentId = null, string role = null, string keyword = null)
        {
            return await ExecuteWithRetryAsync(() => _apiClient.GetUsersAsync(page, size, departmentId, role, keyword));
        }

        public async Task<UserInfo> GetUserByIdAsync(string userId)
        {
            return await ExecuteWithRetryAsync(() => _apiClient.GetUserByIdAsync(userId));
        }

        public async Task<string> CreateUserAsync(CreateUserRequest request)
        {
            return await ExecuteWithRetryAsync(() => _apiClient.CreateUserAsync(request));
        }

        public async Task<string> UpdateUserAsync(string userId, UpdateUserRequest request)
        {
            return await ExecuteWithRetryAsync(() => _apiClient.UpdateUserAsync(userId, request));
        }

        public async Task<string> UpdateUserStatusAsync(string userId, UpdateUserStatusRequest request)
        {
            return await ExecuteWithRetryAsync(() => _apiClient.UpdateUserStatusAsync(userId, request));
        }

        public async Task<string> DeleteUserAsync(string userId)
        {
            return await ExecuteWithRetryAsync(() => _apiClient.DeleteUserAsync(userId));
        }

        #endregion

        #region 模块 6: 设备管理 (Device Management)

        public async Task<DeviceList> GetDevicesAsync(int page = 1, int size = 20, string hospitalId = null, string departmentId = null, string status = null, string keyword = null)
        {
            return await ExecuteWithRetryAsync(() => _apiClient.GetDevicesAsync(page, size, hospitalId, departmentId, status, keyword));
        }

        public async Task<DeviceInfo> GetDeviceByIdAsync(string deviceId)
        {
            return await ExecuteWithRetryAsync(() => _apiClient.GetDeviceByIdAsync(deviceId));
        }

        public async Task<RegisterDeviceResponse> RegisterDeviceAsync(RegisterDeviceRequest request)
        {
            return await ExecuteWithRetryAsync(() => _apiClient.RegisterDeviceAsync(request));
        }

        public async Task<string> DeviceHeartbeatAsync(string deviceId)
        {
            return await ExecuteWithRetryAsync(() => _apiClient.DeviceHeartbeatAsync(deviceId));
        }

        public async Task<string> UpdateDeviceCapabilitiesAsync(string deviceId, UpdateDeviceCapabilitiesRequest request)
        {
            return await ExecuteWithRetryAsync(() => _apiClient.UpdateDeviceCapabilitiesAsync(deviceId, request));
        }

        #endregion

        #region 模块 7: 审计日志 (Audit Logs)

        public async Task<AuditLogList> GetAuditLogsAsync(int page = 1, int size = 20, string action = null, string resource = null, string targetId = null, string userId = null)
        {
            return await ExecuteWithRetryAsync(() => _apiClient.GetAuditLogsAsync(page, size, action, resource, targetId, userId));
        }

        #endregion

        #region 401 自动重试核心逻辑 (401 Retry Core Logic)

        /// <summary>
        /// 通用的带 401 自动续期的 API 调用执行器
        /// </summary>
        /// <typeparam name="T">API 接口返回的数据类型</typeparam>
        /// <param name="action">实际的 API 调用方法</param>
        /// <returns>API 返回的结果</returns>
        private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> action)
        {
            try
            {
                return await action();
            }
            catch (Exception ex) when (ex.Message.Contains("401"))
            {
                // 如果发现 401，尝试刷新 Token 后再重试一次
                await RefreshTokenAsync();
                return await action();
            }
        }

        #endregion
    }
}
