using System;
using SurgicalLivePlatform.Runtime.Models;

namespace SurgicalLivePlatform.Runtime.Services
{
    /// <summary>
    /// 应用状态管理器，负责维护全局的 UserAppState，并通过事件广播状态变化
    /// </summary>
    public class AppStateManager
    {
        /// <summary>
        /// 状态变化事件代理：参数1为旧状态，参数2为新状态
        /// </summary>
        public delegate void StateChangedHandler(UserAppState oldState, UserAppState newState);

        /// <summary>
        /// 当应用状态发生改变时触发
        /// </summary>
        public event StateChangedHandler OnStateChanged;

        /// <summary>
        /// 获取当前状态
        /// </summary>
        public UserAppState CurrentState { get; private set; }

        public AppStateManager()
        {
            // 初始状态设定
            CurrentState = UserAppState.Init;
        }

        /// <summary>
        /// 请求切换到新状态
        /// </summary>
        /// <param name="newState">目标状态</param>
        public void ChangeState(UserAppState newState)
        {
            if (CurrentState == newState) return;

            var oldState = CurrentState;
            CurrentState = newState;
            
            OnStateChanged?.Invoke(oldState, newState);
        }
    }
}
