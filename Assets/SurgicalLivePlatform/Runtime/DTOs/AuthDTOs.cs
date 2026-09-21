using System;

namespace SurgicalLivePlatform.Runtime.DTOs
{
    #region 认证相关请求对象 (Authentication Request DTOs)

    /// <summary>
    /// 标准账号密码或设备登录请求
    /// </summary>
    [Serializable]
    public class LoginRequest
    {
        public string username;         // 用户名
        public string password;         // 密码
    }

    /// <summary>
    /// 针对 XR 等硬件设备的专用设备级登录请求
    /// </summary>
    [Serializable]
    public class DeviceLoginRequest
    {
        public string device_sn;        // 设备唯一序列号 (SN)
        public string device_secret;    // 设备密钥
    }

    /// <summary>
    /// 设置设备图案密码请求
    /// </summary>
    [Serializable]
    public class PatternSetupRequest
    {
        public string device_sn;        // 设备唯一序列号 (SN)
        public string pattern_hash;     // 图案密码的加密哈希值
    }

    /// <summary>
    /// XR设备图案密码验证请求
    /// </summary>
    [Serializable]
    public class PatternVerifyRequest
    {
        public string user_id;          // 用户唯一标识
        public string device_sn;        // 设备唯一序列号 (SN)
        public string pattern_hash;     // 图案密码的加密哈希值
    }

    /// <summary>
    /// 刷新访问令牌请求
    /// </summary>
    [Serializable]
    public class RefreshRequest
    {
        public string refresh_token;    // 刷新令牌
    }

    #endregion

    #region 认证相关响应与基础信息对象 (Authentication Response & Info DTOs)

    /// <summary>
    /// 当前登录用户信息
    /// </summary>
    [Serializable]
    public class UserInfo
    {
        public string id;               // 用户唯一ID
        public string username;         // 登录账号名
        public string name;             // 显示昵称/姓名
        public string avatar_url;       // 头像地址
        // 注意：角色与权限 (roles, permissions) 字段可在此处按需扩展
    }

    /// <summary>
    /// 登录/认证成功后的返回结果
    /// </summary>
    [Serializable]
    public class AuthResponse
    {
        public string access_token;     // 短期访问令牌 (Business JWT)
        public string refresh_token;    // 长期刷新令牌
        public UserInfo user;           // 基础用户信息
    }

    /// <summary>
    /// 存储于本地客户端的持久化用户档案结构
    /// 用于快速渲染大量用户列表和实现一键快捷登录
    /// </summary>
    [Serializable]
    public class UserLocalData
    {
        public string user_id;          // 用户唯一ID
        public string username;         // 登录账号名
        public string display_name;     // 显示昵称
        public string avatar_url;       // 头像本地或远程地址
        public string access_token;     // 缓存的短期令牌
        public string refresh_token;    // 缓存的刷新令牌
        public long last_login_time;    // 最后登录时间的时间戳（Tick），用于列表排序
    }

    #endregion
}
