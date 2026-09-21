using SurgicalLivePlatform.Runtime.DTOs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace SurgicalLivePlatform.Runtime.Storage
{
    /// <summary>
    /// 基于本地 JSON 文件的持久化存储实现类
    /// 用于安全、高效地存储和读取多用户的凭证及档案信息
    /// 解决在 XR 头显端多医生切换登录的性能瓶颈
    /// </summary>
    public class JsonLocalStorage : ILocalStorage
    {
        #region 内部字段与包装类 (Internal Fields & Wrapper)

        private readonly string _filePath;
        private Dictionary<string, UserLocalData> _cache;

        /// <summary>
        /// 用于 JSON 序列化的包装类 (Unity JsonUtility 无法直接序列化 Dictionary)
        /// </summary>
        [Serializable]
        private class SerializationWrapper
        {
            public List<UserLocalData> users;
        }

        #endregion

        #region 构造函数与初始化 (Constructor & Initialization)
       
        /// <summary>
        /// 初始化本地存储
        /// </summary>
        /// <param name="filePath">本地 JSON 文件绝对路径 (建议使用 Application.persistentDataPath)</param>
        public JsonLocalStorage()
        {
            _filePath = Path.Combine(Application.persistentDataPath, "user_credentials.json"); ;

        }

        /// <summary>
        /// 异步加载 JSON 数据至内存字典中
        /// 时间复杂度 O(N)，后续查询 O(1)
        /// </summary>
        public async Task InitializeAsync()
        {
            if (!File.Exists(_filePath)) 
            {
                _cache = new Dictionary<string, UserLocalData>();
                return;
            }

            try
            {
                string json;
                using (var reader = new StreamReader(_filePath))
                {
                    json = await reader.ReadToEndAsync();
                }
                var wrapper = JsonUtility.FromJson<SerializationWrapper>(json);

                if (wrapper != null && wrapper.users != null)
                {
                    _cache = wrapper.users
                        .Where(item => !string.IsNullOrEmpty(item.user_id))
                        .ToDictionary(item => item.user_id, item => item);
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"加载本地用户缓存失败: {ex.Message}");
                _cache = new Dictionary<string, UserLocalData>();
            }
        }

        #endregion

        #region 核心存储操作 (Core Storage Operations)

        /// <summary>
        /// 将内存字典全量序列化并异步刷入磁盘
        /// </summary>
        private async Task SaveToDiskAsync()
        {
            try
            {
                var wrapper = new SerializationWrapper { users = new List<UserLocalData>(_cache.Values) };
                string json = JsonUtility.ToJson(wrapper, true);
                
                string dir = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                using (var writer = new StreamWriter(_filePath, false))
                {
                    await writer.WriteAsync(json);
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"保存本地用户缓存失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 保存或更新单一用户信息
        /// </summary>
        public async Task SaveUserAsync(UserLocalData user)
        {
            if (user == null || string.IsNullOrEmpty(user.user_id)) return;
            if (_cache == null) await InitializeAsync();
            _cache[user.user_id] = user;
            await SaveToDiskAsync();
        }

        /// <summary>
        /// 移除指定用户的本地数据（如取消授权）
        /// </summary>
        public async Task RemoveUserAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId)) return;
            if (_cache == null) await InitializeAsync();
            if (_cache.ContainsKey(userId))
            {
                _cache.Remove(userId);
                await SaveToDiskAsync();
            }
        }

        /// <summary>
        /// 根据用户ID获取对应的本地缓存信息
        /// </summary>
        public async Task<UserLocalData> GetUserAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId)) return null;
            if (_cache == null) await InitializeAsync();
            if (_cache.TryGetValue(userId, out var user))
            {
                return user;
            }
            return null;
        }

        /// <summary>
        /// 聚合获取所有本地用户信息，用于列表渲染
        /// </summary>
        public async Task<List<UserLocalData>> GetAllUsersAsync()
        {
            if (_cache == null) await InitializeAsync();
            return new List<UserLocalData>(_cache.Values);
        }

        /// <summary>
        /// 清空所有用户的本地数据
        /// </summary>
        public async Task ClearAllAsync()
        {
            _cache = new Dictionary<string, UserLocalData>();
            await SaveToDiskAsync();
        }

        #endregion
    }
}
