using SurgicalLivePlatform.Runtime.DTOs;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace SurgicalLivePlatform.Runtime.Network
{
    /// <summary>
    /// 基于原生 HttpClient 的网络请求客户端实现
    /// 统一管理鉴权令牌(Token)的附加、请求发出以及反序列化
    /// </summary>
    public class HttpClientService : IApiClient, IDisposable
    {
        #region 内部字段与常数 (Internal Fields & Constants)

        private readonly HttpClient _httpClient;
        private readonly INetworkEventDispatcher _dispatcher;
        private const string BaseUrl = "http://10.72.128.96:8081/api/v1";

        #endregion

        #region 生命周期 (Lifecycle)

        /// <summary>
        /// 构造 HttpClient 并配置默认超时
        /// </summary>
        public HttpClientService(INetworkEventDispatcher dispatcher = null, HttpMessageHandler handler = null)
        {
            if (handler != null)
            {
                _httpClient = new HttpClient(handler);
            }
            else
            {
                var defaultHandler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
                };
                _httpClient = new HttpClient(defaultHandler);
            }
                
            _httpClient.Timeout = TimeSpan.FromSeconds(15);
            _dispatcher = dispatcher;
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }

        #endregion

        #region 基础请求封装 (Base Request Wrappers)

        /// <summary>
        /// 设置或清除全局 HTTP 请求头中的 Bearer Token
        /// </summary>
        public void SetAccessToken(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization = null;
            }
            else
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }

        private async Task<string> ExecuteRequestAsync(Func<Task<HttpResponseMessage>> requestFunc, string method, string endpoint, string payload)
        {
            string url = $"{BaseUrl}{endpoint}";
            
            try
            {
                var response = await requestFunc();
                return await HandleResponseAsync(endpoint, response);
            }
            catch (TaskCanceledException ex)
            {
                _dispatcher?.TriggerNetworkTimeout();
                string sanitizedPayload = SanitizePayload(payload);
                throw new Exception($"网络连接超时:408:timeout | URL: {method} {url} | Param: {sanitizedPayload}", ex);
            }
            catch (HttpRequestException ex)
            {
                string sanitizedPayload = SanitizePayload(payload);
                throw new Exception($"网络请求失败:{ex.Message} | URL: {method} {url} | Param: {sanitizedPayload}", ex);
            }
        }

        private string SanitizePayload(string payload)
        {
            if (string.IsNullOrEmpty(payload) || payload == "{}") return "无参数";
            
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // 测试环境/编辑器阶段直接返回明文，方便调试
            return payload;
#else
            string sanitized = payload;
            string[] sensitiveKeys = { "password", "token", "device_secret", "refresh_token", "access_token" };
            foreach (var key in sensitiveKeys)
            {
                string pattern = $@"(""{key}""\s*:\s*"")[^""]*("")";
                sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, pattern, "$1***$2", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }
            return sanitized;
#endif
        }

        private Task<string> PostRawAsync(string endpoint, object payload)
        {
            string json = payload != null ? JsonUtility.ToJson(payload) : "{}";
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            return ExecuteRequestAsync(() => _httpClient.PostAsync($"{BaseUrl}{endpoint}", content), "POST", endpoint, json);
        }

        private Task<string> GetRawAsync(string endpoint)
        {
            return ExecuteRequestAsync(() => _httpClient.GetAsync($"{BaseUrl}{endpoint}"), "GET", endpoint, null);
        }

        private Task<string> PutRawAsync(string endpoint, object payload)
        {
            string json = payload != null ? JsonUtility.ToJson(payload) : "{}";
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            return ExecuteRequestAsync(() => _httpClient.PutAsync($"{BaseUrl}{endpoint}", content), "PUT", endpoint, json);
        }

        private Task<string> DeleteRawAsync(string endpoint)
        {
            return ExecuteRequestAsync(() => _httpClient.DeleteAsync($"{BaseUrl}{endpoint}"), "DELETE", endpoint, null);
        }

        private async Task<string> HandleResponseAsync(string endpoint, HttpResponseMessage response)
        {
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _dispatcher?.TriggerAuthExpired();
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    _dispatcher?.TriggerAccountForbidden();
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.GatewayTimeout || response.StatusCode == System.Net.HttpStatusCode.RequestTimeout)
                {
                    _dispatcher?.TriggerNetworkTimeout();
                }
                response.EnsureSuccessStatusCode();
            }

            string responseBody = await response.Content.ReadAsStringAsync();
            string method = response.RequestMessage?.Method?.Method ?? "UNKNOWN";
            string uri = response.RequestMessage?.RequestUri?.ToString() ?? "UNKNOWN_URI";
            UnityEngine.Debug.Log($"[HttpClientService] [{method}] [{uri}] : {responseBody}");
            return responseBody;
        }

        #endregion

        #region 泛型响应解析 (Response Parsing)

        // Unity 的 JsonUtility 限制必须通过非泛型 Wrapper 解析。我们定义通用的结构以复用解析。
        [Serializable]
        private class GenericResponseWrapper
        {
            public int code;
            public string message;
        }

        #endregion

        #region 模块 1: 认证与用户信息 (Auth & User)

        [Serializable]
        private class AuthResponseWrapper { public int code; public string message; public AuthResponse data; }
        
        [Serializable]
        private class UserInfoWrapper { public int code; public string message; public UserInfo data; }

        public async Task<AuthResponse> LoginAsync(LoginRequest request)
        {
            string json = await PostRawAsync("/auth/login", request);
            var wrapper = JsonUtility.FromJson<AuthResponseWrapper>(json);
            if (wrapper == null) throw new Exception("API Error: 返回的JSON数据为空或无法解析");
            if (wrapper.code != 200) throw new Exception($"API Error: {wrapper.message}");
            return wrapper.data;
        }

        public async Task<AuthResponse> DeviceLoginAsync(DeviceLoginRequest request)
        {
            string json = await PostRawAsync("/auth/device/login", request);
            var wrapper = JsonUtility.FromJson<AuthResponseWrapper>(json);
            if (wrapper == null) throw new Exception("API Error: 返回的JSON数据为空或无法解析");
            if (wrapper.code != 200) throw new Exception($"API Error: {wrapper.message}");
            return wrapper.data;
        }

        public async Task<string> SetupPatternAsync(PatternSetupRequest request)
        {
            string json = await PostRawAsync("/auth/pattern/setup", request);
            var wrapper = JsonUtility.FromJson<GenericResponseWrapper>(json);
            if (wrapper.code != 200) throw new Exception($"API Error: {wrapper.message}");
            return wrapper.message;
        }

        public async Task<AuthResponse> VerifyPatternAsync(PatternVerifyRequest request)
        {
            string json = await PostRawAsync("/auth/pattern/verify", request);
            var wrapper = JsonUtility.FromJson<AuthResponseWrapper>(json);
            if (wrapper.code != 200) throw new Exception($"API Error: {wrapper.message}");
            return wrapper.data;
        }

        public async Task<AuthResponse> RefreshTokenAsync(RefreshRequest request)
        {
            string json = await PostRawAsync("/auth/refresh", request);
            var wrapper = JsonUtility.FromJson<AuthResponseWrapper>(json);
            if (wrapper.code != 200) throw new Exception($"API Error: {wrapper.message}");
            return wrapper.data;
        }

        public async Task<UserInfo> GetUserInfoAsync()
        {
            string json = await GetRawAsync("/users/me");
            var wrapper = JsonUtility.FromJson<UserInfoWrapper>(json);
            if (wrapper.code != 200) throw new Exception($"API Error: {wrapper.message}");
            return wrapper.data;
        }

        #endregion

        #region 模块 2: 房间生命周期与入会 (Room)

        public async Task<RoomList> GetRoomInfosAsync(int page = 1, int size = 10, string hospitalId = null, string departmentId = null, string status = null)
        {
            string url = $"/rooms?page={page}&size={size}";
            if (!string.IsNullOrEmpty(hospitalId)) url += $"&hospital_id={hospitalId}";
            if (!string.IsNullOrEmpty(departmentId)) url += $"&department_id={departmentId}";
            if (!string.IsNullOrEmpty(status)) url += $"&status={status}";

            string json = await GetRawAsync(url);
            var wrapper = JsonUtility.FromJson<RoomListWrapper>(json);
            if (wrapper.code != 200) throw new Exception($"API Error: {wrapper.message}");
            return wrapper.data;
        }

        public async Task<RoomItem> GetRoomDetailAsync(string roomId)
        {
            string json = await GetRawAsync($"/rooms/{roomId}");
            var wrapper = JsonUtility.FromJson<RoomItemWrapper>(json);
            if (wrapper == null) throw new Exception("API Error: 返回的房间详情数据为空或无法解析");
            if (wrapper.code != 200) throw new Exception($"API Error: {wrapper.message}");
            return wrapper.data;
        }

        public async Task<RoomItem> CreateRoomAsync(CreateRoomRequest request)
        {
            string json = await PostRawAsync("/rooms", request);
            var wrapper = JsonUtility.FromJson<RoomItemWrapper>(json);
            if (wrapper.code != 200) throw new Exception($"API Error: {wrapper.message}");
            return wrapper.data;
        }

        public async Task<RoomItem> UpdateRoomAsync(string roomId, UpdateRoomRequest request)
        {
            string json = await PutRawAsync($"/rooms/{roomId}", request);
            var wrapper = JsonUtility.FromJson<RoomItemWrapper>(json);
            if (wrapper.code != 200) throw new Exception($"API Error: {wrapper.message}");
            return wrapper.data;
        }

        public async Task<List<string>> GetRoomMembersAsync(string roomId)
        {
            string json = await GetRawAsync($"/rooms/{roomId}/members");
            var wrapper = JsonUtility.FromJson<MemberListWrapper>(json);
            if (wrapper.code != 200) throw new Exception($"API Error: {wrapper.message}");
            return wrapper.data;
        }

        public async Task<string> InviteToRoomAsync(string roomId, InviteToRoomRequest request)
        {
            string json = await PostRawAsync($"/rooms/{roomId}/invite", request);
            var wrapper = JsonUtility.FromJson<GenericResponseWrapper>(json);
            if (wrapper.code != 200) throw new Exception($"API Error: {wrapper.message}");
            return wrapper.message;
        }

        public async Task<JoinRoomResponse> JoinRoomAsync(string roomId)
        {
            string json = await PostRawAsync($"/rooms/{roomId}/join", null);
            var wrapper = JsonUtility.FromJson<JoinRoomResponseWrapper>(json);
            if (wrapper == null) throw new Exception("API Error: 返回的入会响应数据为空或无法解析");
            if (wrapper.code != 200) throw new Exception($"API Error: {wrapper.message}");
            return wrapper.data;
        }

        public async Task<string> StartRoomAsync(string roomId)
        {
            string json = await PostRawAsync($"/rooms/{roomId}/start", null);
            var wrapper = JsonUtility.FromJson<GenericResponseWrapper>(json);
            if (wrapper.code != 200) throw new Exception($"API Error: {wrapper.message}");
            return wrapper.message;
        }

        public async Task<string> EndRoomAsync(string roomId)
        {
            string json = await PostRawAsync($"/rooms/{roomId}/end", null);
            var wrapper = JsonUtility.FromJson<GenericResponseWrapper>(json);
            if (wrapper.code != 200) throw new Exception($"API Error: {wrapper.message}");
            return wrapper.message;
        }

        #endregion

        #region 模块 3: 会中互动与信令管理 (In-Room Control)

        public async Task<string> UpdateMemberPermissionAsync(string roomId, string userId, UpdatePermissionRequest request)
        {
            string json = await PutRawAsync($"/rooms/{roomId}/members/{userId}/permission", request);
            var wrapper = JsonUtility.FromJson<GenericResponseWrapper>(json);
            if (wrapper.code != 200) throw new Exception($"API Error: {wrapper.message}");
            return wrapper.message;
        }

        public async Task<string> KickMemberAsync(string roomId, KickMemberRequest request)
        {
            string json = await PostRawAsync($"/rooms/{roomId}/kick", request);
            var wrapper = JsonUtility.FromJson<GenericResponseWrapper>(json);
            if (wrapper.code != 200) throw new Exception($"API Error: {wrapper.message}");
            return wrapper.message;
        }

        public async Task<List<AnnotationData>> GetHistoryAnnotationsAsync(string roomId)
        {
            string json = await GetRawAsync($"/rooms/{roomId}/annotations");
            var wrapper = JsonUtility.FromJson<AnnotationDataListWrapper>(json);
            if (wrapper.code != 200) throw new Exception($"API Error: {wrapper.message}");
            return wrapper.data;
        }

        public async Task<List<ChatMessage>> GetHistoryMessagesAsync(string roomId)
        {
            string json = await GetRawAsync($"/rooms/{roomId}/messages");
            var wrapper = JsonUtility.FromJson<ChatMessageListWrapper>(json);
            if (wrapper.code != 200) throw new Exception($"API Error: {wrapper.message}");
            return wrapper.data;
        }

        public async Task<string> SendMessageAsync(string roomId, SendMessageRequest request)
        {
            string json = await PostRawAsync($"/rooms/{roomId}/messages", request);
            var wrapper = JsonUtility.FromJson<GenericResponseWrapper>(json);
            if (wrapper.code != 200) throw new Exception($"API Error: {wrapper.message}");
            return wrapper.message;
        }

        public async Task<string> SendAnnotationAsync(string roomId, SendAnnotationRequest request)
        {
            string json = await PostRawAsync($"/rooms/{roomId}/annotations", request);
            var wrapper = JsonUtility.FromJson<GenericResponseWrapper>(json);
            if (wrapper.code != 200) throw new Exception($"API Error: {wrapper.message}");
            return wrapper.message;
        }

        #endregion

        #region 模块 5: 用户设置与账号管理 (User & Account Management)

        public async Task<string> UpdateProfileAsync(UpdateProfileRequest request)
        {
            string json = await PutRawAsync("/users/me/profile", request);
            var wrapper = JsonUtility.FromJson<GenericResponseWrapper>(json);
            if (wrapper.code != 200) throw new Exception($"API Error: {wrapper.message}");
            return wrapper.message;
        }

        public async Task<string> UpdatePasswordAsync(UpdatePasswordRequest request)
        {
            string json = await PutRawAsync("/users/me/password", request);
            var wrapper = JsonUtility.FromJson<GenericResponseWrapper>(json);
            if (wrapper.code != 200) throw new Exception($"API Error: {wrapper.message}");
            return wrapper.message;
        }

        public async Task<string> UploadAvatarAsync(byte[] imageBytes, string fileName)
        {
            var content = new MultipartFormDataContent();
            var imageContent = new ByteArrayContent(imageBytes);
            imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
            content.Add(imageContent, "file", fileName);

            var response = await _httpClient.PostAsync($"{BaseUrl}/users/me/avatar", content);
            string json = await HandleResponseAsync("/users/me/avatar", response);
            
            var wrapper = JsonUtility.FromJson<GenericResponseWrapper>(json);
            if (wrapper.code != 200) throw new Exception($"API Error: {wrapper.message}");
            return wrapper.message;
        }

        public async Task<UserList> GetUsersAsync(int page = 1, int size = 20, string departmentId = null, string role = null, string keyword = null)
        {
            string url = $"/users?page={page}&size={size}";
            if (!string.IsNullOrEmpty(departmentId)) url += $"&department_id={departmentId}";
            if (!string.IsNullOrEmpty(role)) url += $"&role={role}";
            if (!string.IsNullOrEmpty(keyword)) url += $"&keyword={keyword}";

            string json = await GetRawAsync(url);
            var wrapper = JsonUtility.FromJson<UserListWrapper>(json);
            if (wrapper.code != 200) throw new Exception($"API Error: {wrapper.message}");
            return wrapper.data;
        }

        public async Task<UserInfo> GetUserByIdAsync(string userId)
        {
            string json = await GetRawAsync($"/users/{userId}");
            var wrapper = JsonUtility.FromJson<UserInfoWrapper>(json);
            if (wrapper.code != 200) throw new Exception($"API Error: {wrapper.message}");
            return wrapper.data;
        }

        public async Task<string> CreateUserAsync(CreateUserRequest request)
        {
            string json = await PostRawAsync("/users", request);
            var wrapper = JsonUtility.FromJson<GenericResponseWrapper>(json);
            if (wrapper.code != 200) throw new Exception($"API Error: {wrapper.message}");
            return wrapper.message;
        }

        public async Task<string> UpdateUserAsync(string userId, UpdateUserRequest request)
        {
            string json = await PutRawAsync($"/users/{userId}", request);
            var wrapper = JsonUtility.FromJson<GenericResponseWrapper>(json);
            if (wrapper.code != 200) throw new Exception($"API Error: {wrapper.message}");
            return wrapper.message;
        }

        public async Task<string> UpdateUserStatusAsync(string userId, UpdateUserStatusRequest request)
        {
            string json = await PutRawAsync($"/users/{userId}/status", request);
            var wrapper = JsonUtility.FromJson<GenericResponseWrapper>(json);
            if (wrapper.code != 200) throw new Exception($"API Error: {wrapper.message}");
            return wrapper.message;
        }

        public async Task<string> DeleteUserAsync(string userId)
        {
            string json = await DeleteRawAsync($"/users/{userId}");
            var wrapper = JsonUtility.FromJson<GenericResponseWrapper>(json);
            if (wrapper.code != 200) throw new Exception($"API Error: {wrapper.message}");
            return wrapper.message;
        }

        #endregion

        #region 模块 6: 设备管理 (Device Management)

        public async Task<DeviceList> GetDevicesAsync(int page = 1, int size = 20, string hospitalId = null, string departmentId = null, string status = null, string keyword = null)
        {
            string url = $"/devices?page={page}&size={size}";
            if (!string.IsNullOrEmpty(hospitalId)) url += $"&hospital_id={hospitalId}";
            if (!string.IsNullOrEmpty(departmentId)) url += $"&department_id={departmentId}";
            if (!string.IsNullOrEmpty(status)) url += $"&status={status}";
            if (!string.IsNullOrEmpty(keyword)) url += $"&keyword={keyword}";

            string json = await GetRawAsync(url);
            var wrapper = JsonUtility.FromJson<DeviceListWrapper>(json);
            if (wrapper.code != 200) throw new Exception($"API Error: {wrapper.message}");
            return wrapper.data;
        }

        public async Task<DeviceInfo> GetDeviceByIdAsync(string deviceId)
        {
            string json = await GetRawAsync($"/devices/{deviceId}");
            var wrapper = JsonUtility.FromJson<DeviceInfoWrapper>(json);
            if (wrapper.code != 200) throw new Exception($"API Error: {wrapper.message}");
            return wrapper.data;
        }

        public async Task<RegisterDeviceResponse> RegisterDeviceAsync(RegisterDeviceRequest request)
        {
            string json = await PostRawAsync("/devices/register", request);
            var wrapper = JsonUtility.FromJson<RegisterDeviceResponseWrapper>(json);
            if (wrapper.code != 200) throw new Exception($"API Error: {wrapper.message}");
            return wrapper.data;
        }

        public async Task<string> DeviceHeartbeatAsync(string deviceId)
        {
            string json = await PutRawAsync($"/devices/{deviceId}/heartbeat", null);
            var wrapper = JsonUtility.FromJson<GenericResponseWrapper>(json);
            if (wrapper.code != 200) throw new Exception($"API Error: {wrapper.message}");
            return wrapper.message;
        }

        public async Task<string> UpdateDeviceCapabilitiesAsync(string deviceId, UpdateDeviceCapabilitiesRequest request)
        {
            string json = await PostRawAsync($"/devices/{deviceId}/capabilities", request);
            var wrapper = JsonUtility.FromJson<GenericResponseWrapper>(json);
            if (wrapper.code != 200) throw new Exception($"API Error: {wrapper.message}");
            return wrapper.message;
        }

        #endregion

        #region 模块 7: 审计日志 (Audit Logs)

        public async Task<AuditLogList> GetAuditLogsAsync(int page = 1, int size = 20, string action = null, string resource = null, string targetId = null, string userId = null)
        {
            string url = $"/audit/logs?page={page}&size={size}";
            if (!string.IsNullOrEmpty(action)) url += $"&action={action}";
            if (!string.IsNullOrEmpty(resource)) url += $"&resource={resource}";
            if (!string.IsNullOrEmpty(targetId)) url += $"&targetId={targetId}";
            if (!string.IsNullOrEmpty(userId)) url += $"&userId={userId}";

            string json = await GetRawAsync(url);
            var wrapper = JsonUtility.FromJson<AuditLogListWrapper>(json);
            if (wrapper.code != 200) throw new Exception($"API Error: {wrapper.message}");
            return wrapper.data;
        }

        #endregion
    }
}
