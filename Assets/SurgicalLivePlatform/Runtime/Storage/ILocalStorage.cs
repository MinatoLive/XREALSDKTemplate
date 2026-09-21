using System.Collections.Generic;
using System.Threading.Tasks;
using SurgicalLivePlatform.Runtime.DTOs;

namespace SurgicalLivePlatform.Runtime.Storage
{
    /// <summary>
    /// 本地存储服务接口
    /// 统一规划用户档案和鉴权 Token 的客户端离线存取能力
    /// </summary>
    public interface ILocalStorage
    {
        /// <summary>
        /// 异步初始化并加载磁盘缓存到内存
        /// </summary>
        Task InitializeAsync();

        /// <summary>
        /// 保存或更新单个本地用户数据
        /// </summary>
        Task SaveUserAsync(UserLocalData user);

        /// <summary>
        /// 移除指定用户的本地数据
        /// </summary>
        Task RemoveUserAsync(string userId);

        /// <summary>
        /// 获取指定用户的本地缓存档案
        /// </summary>
        Task<UserLocalData> GetUserAsync(string userId);

        /// <summary>
        /// 提取设备上所有曾经登录过的用户信息，用于构建历史登录列表
        /// </summary>
        Task<List<UserLocalData>> GetAllUsersAsync();

        /// <summary>
        /// 清除本地所有用户信息（如重置设备时调用）
        /// </summary>
        Task ClearAllAsync();
    }
}
