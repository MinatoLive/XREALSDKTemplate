namespace SurgicalLivePlatform.Runtime.Models
{
    /// <summary>
    /// 全局应用状态，代表用户在生命周期中的不同阶段
    /// </summary>
    public enum UserAppState
    {
        /// <summary>
        /// 初始化状态
        /// </summary>
        Init,
        
        /// <summary>
        /// 未登录状态
        /// </summary>
        LoggedOut,
        
        /// <summary>
        /// 登录中状态
        /// </summary>
        LoggingIn,
        
        /// <summary>
        /// 已登录状态
        /// </summary>
        LoggedIn,
        
        /// <summary>
        /// 异常状态
        /// </summary>
        Error
    }
}
