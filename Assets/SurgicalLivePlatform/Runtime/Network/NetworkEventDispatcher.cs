using System;

namespace SurgicalLivePlatform.Runtime.Network
{
    /// <summary>
    /// 默认的全局网络事件分发器实现
    /// 可作为单例或通过 DI 注入到 Service 层
    /// </summary>
    public class NetworkEventDispatcher : INetworkEventDispatcher
    {
        public event Action OnAuthExpired;
        public event Action OnAccountForbidden;
        public event Action OnNetworkTimeout;

        public void TriggerAuthExpired()
        {
            OnAuthExpired?.Invoke();
        }

        public void TriggerAccountForbidden()
        {
            OnAccountForbidden?.Invoke();
        }

        public void TriggerNetworkTimeout()
        {
            OnNetworkTimeout?.Invoke();
        }
    }
}
