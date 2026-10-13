using System;

namespace SurgicalLivePlatform.Runtime.UI
{
    /// <summary>
    /// 用户登录视图抽象接口 (MVP 模式 View 契约)
    /// </summary>
    public interface IUserLoginView : IUIView
    {
        /// <summary>
        /// 用户点击登录触发
        /// </summary>
        event Action<string, string> OnLoginRequested;

        /// <summary>
        /// 用户点击退出触发
        /// </summary>
        event Action OnExitRequested;

        /// <summary>
        /// 设置加载/不可交互状态
        /// </summary>
        void SetLoading(bool isLoading);

        /// <summary>
        /// 预填充凭据
        /// </summary>
        void SetCredentials(string account, string password);

        /// <summary>
        /// 展示错误信息
        /// </summary>
        void ShowErrorMessage(string message);
    }
}
