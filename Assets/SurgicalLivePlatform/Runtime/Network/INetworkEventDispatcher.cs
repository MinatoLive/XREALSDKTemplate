using System;

namespace SurgicalLivePlatform.Runtime.Network
{
    /// <summary>
    /// 全局网络事件分发器接口
    /// 用于解耦底层网络异常与上层 UI 的异常处理
    /// </summary>
    public interface INetworkEventDispatcher
    {
        /// <summary>
        /// 当 Refresh Token 失效，或登录态完全过期时触发
        /// 前端订阅此事件后应强制跳转回登录页
        /// </summary>
        event Action OnAuthExpired;

        /// <summary>
        /// 当发生 403 Forbidden 错误（如账号被封禁或禁用）时触发
        /// </summary>
        event Action OnAccountForbidden;

        /// <summary>
        /// 当发生网络超时或 504 错误时触发
        /// </summary>
        event Action OnNetworkTimeout;

        void TriggerAuthExpired();
        void TriggerAccountForbidden();
        void TriggerNetworkTimeout();
    }
}
