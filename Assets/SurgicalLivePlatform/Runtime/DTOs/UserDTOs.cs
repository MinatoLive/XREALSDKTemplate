using System;
using System.Collections.Generic;

namespace SurgicalLivePlatform.Runtime.DTOs
{
    #region 请求 DTO (Request DTOs)

    /// <summary>
    /// 更新个人资料请求
    /// </summary>
    [Serializable]
    public class UpdateProfileRequest
    {
        public string name;
        public string phone;
        public string bio;
    }

    /// <summary>
    /// 更新密码请求
    /// </summary>
    [Serializable]
    public class UpdatePasswordRequest
    {
        public string old_password;
        public string new_password;
    }

    /// <summary>
    /// 后台创建用户请求
    /// </summary>
    [Serializable]
    public class CreateUserRequest
    {
        public string username;
        public string password;
        public string name;
        public string department_id;
        public List<string> roles;
    }

    /// <summary>
    /// 后台更新用户资料请求
    /// </summary>
    [Serializable]
    public class UpdateUserRequest
    {
        public string name;
        public string department_id;
        public List<string> roles;
    }

    /// <summary>
    /// 后台更新用户状态请求
    /// </summary>
    [Serializable]
    public class UpdateUserStatusRequest
    {
        public string status; // ACTIVE | INACTIVE | SUSPENDED
    }

    #endregion

    #region 响应与模型 DTO (Response DTOs)

    [Serializable]
    public class UserList
    {
        public List<UserInfo> list;
        public int total;
    }

    #endregion

    #region JSON 包装类 (Wrappers)

    [Serializable]
    public class UserListWrapper
    {
        public int code;
        public string message;
        public UserList data;
    }

    #endregion
}
