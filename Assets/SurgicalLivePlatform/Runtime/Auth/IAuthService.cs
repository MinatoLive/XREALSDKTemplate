using SurgicalLivePlatform.Runtime.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SurgicalLivePlatform.Runtime.Auth
{
    /// <summary>
    /// 全局 API 业务代理服务接口 (Facade)
    /// 统一代理所有的后台接口，并提供内置的 401 Token 失效自动重试机制
    /// </summary>
    public interface IAuthService
    {
        string CurrentUserId { get; }

        #region 模块 1: 认证与用户信息 (Auth & User)

        Task LoginAsync(string username, string password);
        Task LoginWithDeviceAsync(string deviceSn, string deviceSecret);
        Task<string> SetupPatternAsync(string deviceSn, string patternHash);
        Task VerifyPatternAsync(string userId, string deviceSn, string patternHash);
        Task<UserInfo> GetUserInfoAsync();
        Task RefreshTokenAsync();
        Task LogoutAsync();

        #endregion

        #region 模块 2: 房间生命周期与入会 (Room)

        Task<RoomList> GetRoomInfosAsync(int page = 1, int size = 10, string hospitalId = null, string departmentId = null, string status = null);
        Task<List<RoomPresetItem>> GetRoomPresetsAsync();
        Task<byte[]> GetRoomPreviewImageBytesAsync(string roomId);
        Task<RoomItem> GetRoomDetailAsync(string roomId);
        Task<RoomItem> CreateRoomAsync(CreateRoomRequest request);
        Task<RoomItem> UpdateRoomAsync(string roomId, UpdateRoomRequest request);
        Task<List<string>> GetRoomMembersAsync(string roomId);
        Task<string> InviteToRoomAsync(string roomId, InviteToRoomRequest request);
        Task<JoinRoomResponse> JoinRoomAsync(string roomId);
        Task<string> StartRoomAsync(string roomId);
        Task<string> EndRoomAsync(string roomId);

        #endregion

        #region 模块 3: 会中互动与信令管理 (In-Room Control)

        Task<string> UpdateMemberPermissionAsync(string roomId, string userId, UpdatePermissionRequest request);
        Task<string> KickMemberAsync(string roomId, KickMemberRequest request);
        Task<List<AnnotationData>> GetHistoryAnnotationsAsync(string roomId);
        Task<List<ChatMessage>> GetHistoryMessagesAsync(string roomId);
        Task<string> SendMessageAsync(string roomId, SendMessageRequest request);
        Task<string> SendAnnotationAsync(string roomId, SendAnnotationRequest request);

        #endregion

        #region 模块 5: 用户设置与账号管理 (User & Account Management)

        Task<string> UpdateProfileAsync(UpdateProfileRequest request);
        Task<string> UpdatePasswordAsync(UpdatePasswordRequest request);
        Task<string> UploadAvatarAsync(byte[] imageBytes, string fileName);
        Task<UserList> GetUsersAsync(int page = 1, int size = 20, string departmentId = null, string role = null, string keyword = null);
        Task<UserInfo> GetUserByIdAsync(string userId);
        Task<string> CreateUserAsync(CreateUserRequest request);
        Task<string> UpdateUserAsync(string userId, UpdateUserRequest request);
        Task<string> UpdateUserStatusAsync(string userId, UpdateUserStatusRequest request);
        Task<string> DeleteUserAsync(string userId);

        #endregion

        #region 模块 6: 设备管理 (Device Management)

        Task<DeviceList> GetDevicesAsync(int page = 1, int size = 20, string hospitalId = null, string departmentId = null, string status = null, string keyword = null);
        Task<DeviceInfo> GetDeviceByIdAsync(string deviceId);
        Task<RegisterDeviceResponse> RegisterDeviceAsync(RegisterDeviceRequest request);
        Task<string> DeviceHeartbeatAsync(string deviceId);
        Task<string> UpdateDeviceCapabilitiesAsync(string deviceId, UpdateDeviceCapabilitiesRequest request);

        #endregion

        #region 模块 7: 审计日志 (Audit Logs)

        Task<AuditLogList> GetAuditLogsAsync(int page = 1, int size = 20, string action = null, string resource = null, string targetId = null, string userId = null);

        #endregion
    }
}
