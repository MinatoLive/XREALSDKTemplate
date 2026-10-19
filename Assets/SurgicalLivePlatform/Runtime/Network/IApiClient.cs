using System.Threading.Tasks;
using SurgicalLivePlatform.Runtime.DTOs;
using System.Collections.Generic;

namespace SurgicalLivePlatform.Runtime.Network
{
    /// <summary>
    /// API 网络客户端统一接口
    /// 提供与业务后端交互的基础契约，覆盖所有 7 个模块的 HTTP 接口
    /// </summary>
    public interface IApiClient
    {
        /// <summary>
        /// 设置当前请求所需的鉴权 Token
        /// </summary>
        void SetAccessToken(string token);

        /// <summary>
        /// 当前是否持有有效的 Access Token
        /// </summary>
        bool HasValidAccessToken { get; }

        #region 模块 1: 认证与用户信息 (Auth & User)

        /// <summary>
        /// 使用账号密码或硬件密钥进行登录
        /// </summary>
        Task<AuthResponse> LoginAsync(LoginRequest request);

        /// <summary>
        /// 使用硬件设备的 SN 和 Secret 进行专用的设备级登录
        /// </summary>
        Task<AuthResponse> DeviceLoginAsync(DeviceLoginRequest request);

        /// <summary>
        /// 设置设备的九宫格图案密码
        /// </summary>
        Task<string> SetupPatternAsync(PatternSetupRequest request);

        /// <summary>
        /// 验证设备本地的图案密码（快捷登录）
        /// </summary>
        Task<AuthResponse> VerifyPatternAsync(PatternVerifyRequest request);

        /// <summary>
        /// 刷新短期访问令牌
        /// </summary>
        Task<AuthResponse> RefreshTokenAsync(RefreshRequest request);

        /// <summary>
        /// 获取当前登录用户的详细资料
        /// </summary>
        Task<UserInfo> GetUserInfoAsync();

        #endregion

        #region 模块 2: 房间生命周期与入会 (Room)

        /// <summary>
        /// 获取可加入的房间列表
        /// </summary>
        Task<RoomList> GetRoomInfosAsync(int page = 1, int size = 10, string hospitalId = null, string departmentId = null, string status = null);

        /// <summary>
        /// 获取手术室预设场景及预览图列表
        /// </summary>
        Task<List<RoomPresetItem>> GetRoomPresetsAsync();

        /// <summary>
        /// 获取房间直连预览缩略图二进制数据 (接口 2.1.2)
        /// </summary>
        Task<byte[]> GetRoomPreviewImageBytesAsync(string roomId);

        /// <summary>
        /// 获取目标房间的详细状态、属性和参与者数量等
        /// </summary>
        Task<RoomItem> GetRoomDetailAsync(string roomId);

        /// <summary>
        /// 创建房间
        /// </summary>
        Task<RoomItem> CreateRoomAsync(CreateRoomRequest request);

        /// <summary>
        /// 编辑房间元数据
        /// </summary>
        Task<RoomItem> UpdateRoomAsync(string roomId, UpdateRoomRequest request);

        /// <summary>
        /// 获取房间成员列表
        /// </summary>
        Task<List<string>> GetRoomMembersAsync(string roomId);

        /// <summary>
        /// 邀请用户/设备入会
        /// </summary>
        Task<string> InviteToRoomAsync(string roomId, InviteToRoomRequest request);

        /// <summary>
        /// 申请加入房间 (获取 LiveKit Token)
        /// </summary>
        Task<JoinRoomResponse> JoinRoomAsync(string roomId);

        /// <summary>
        /// 开始房间
        /// </summary>
        Task<string> StartRoomAsync(string roomId);

        /// <summary>
        /// 结束房间
        /// </summary>
        Task<string> EndRoomAsync(string roomId);

        #endregion

        #region 模块 3: 会中互动与信令管理 (In-Room Control)

        /// <summary>
        /// 动态修改成员权限
        /// </summary>
        Task<string> UpdateMemberPermissionAsync(string roomId, string userId, UpdatePermissionRequest request);

        /// <summary>
        /// 踢出用户
        /// </summary>
        Task<string> KickMemberAsync(string roomId, KickMemberRequest request);

        /// <summary>
        /// 拉取历史标注数据
        /// </summary>
        Task<List<AnnotationData>> GetHistoryAnnotationsAsync(string roomId);

        /// <summary>
        /// 拉取历史聊天记录
        /// </summary>
        Task<List<ChatMessage>> GetHistoryMessagesAsync(string roomId);

        /// <summary>
        /// 发送聊天
        /// </summary>
        Task<string> SendMessageAsync(string roomId, SendMessageRequest request);

        /// <summary>
        /// 发送标注
        /// </summary>
        Task<string> SendAnnotationAsync(string roomId, SendAnnotationRequest request);

        #endregion

        #region 模块 5: 用户设置与账号管理 (User & Account Management)

        /// <summary>
        /// 更新个人基本信息
        /// </summary>
        Task<string> UpdateProfileAsync(UpdateProfileRequest request);

        /// <summary>
        /// 修改登录密码
        /// </summary>
        Task<string> UpdatePasswordAsync(UpdatePasswordRequest request);

        /// <summary>
        /// 上传或更新个人头像 (需要 Multipart 处理)
        /// </summary>
        Task<string> UploadAvatarAsync(byte[] imageBytes, string fileName);

        /// <summary>
        /// 分页获取用户列表
        /// </summary>
        Task<UserList> GetUsersAsync(int page = 1, int size = 20, string departmentId = null, string role = null, string keyword = null);

        /// <summary>
        /// 获取指定用户的详细信息
        /// </summary>
        Task<UserInfo> GetUserByIdAsync(string userId);

        /// <summary>
        /// 后台管理员创建新用户账号
        /// </summary>
        Task<string> CreateUserAsync(CreateUserRequest request);

        /// <summary>
        /// 更新指定用户的信息
        /// </summary>
        Task<string> UpdateUserAsync(string userId, UpdateUserRequest request);

        /// <summary>
        /// 启用或停用用户账号
        /// </summary>
        Task<string> UpdateUserStatusAsync(string userId, UpdateUserStatusRequest request);

        /// <summary>
        /// 删除用户账号
        /// </summary>
        Task<string> DeleteUserAsync(string userId);

        #endregion

        #region 模块 6: 设备管理 (Device Management)

        /// <summary>
        /// 获取设备列表
        /// </summary>
        Task<DeviceList> GetDevicesAsync(int page = 1, int size = 20, string hospitalId = null, string departmentId = null, string status = null, string keyword = null);

        /// <summary>
        /// 获取设备详情
        /// </summary>
        Task<DeviceInfo> GetDeviceByIdAsync(string deviceId);

        /// <summary>
        /// 注册新硬件设备
        /// </summary>
        Task<RegisterDeviceResponse> RegisterDeviceAsync(RegisterDeviceRequest request);

        /// <summary>
        /// 设备心跳维持在线
        /// </summary>
        Task<string> DeviceHeartbeatAsync(string deviceId);

        /// <summary>
        /// 更新硬件音视频能力
        /// </summary>
        Task<string> UpdateDeviceCapabilitiesAsync(string deviceId, UpdateDeviceCapabilitiesRequest request);

        #endregion

        #region 模块 7: 审计日志 (Audit Logs)

        /// <summary>
        /// 获取系统审计日志
        /// </summary>
        Task<AuditLogList> GetAuditLogsAsync(int page = 1, int size = 20, string action = null, string resource = null, string targetId = null, string userId = null);

        #endregion
    }
}
