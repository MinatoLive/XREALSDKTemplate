using System;
using System.Threading.Tasks;
using SurgicalLivePlatform.Runtime.Auth;
using SurgicalLivePlatform.Runtime.Config;
using SurgicalLivePlatform.Runtime.Models;
using SurgicalLivePlatform.Runtime.Services;
using UnityEngine;

namespace SurgicalLivePlatform.Runtime.UI
{
    /// <summary>
    /// 登录业务中介 (Login Presenter)
    /// 纯 C# 实现，负责驱动登录业务流，实现 View 与网络/状态机彻底解耦。
    /// </summary>
    public class LoginPresenter : IDisposable
    {
        private readonly IUserLoginView _view;
        private readonly IAuthService _authService;
        private readonly AppStateManager _stateManager;

        public LoginPresenter(
            IUserLoginView view,
            IAuthService authService = null,
            AppStateManager stateManager = null)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _authService = authService ?? (SurgicalLiveSDK.Auth);
            _stateManager = stateManager ?? (SurgicalLiveSDK.StateManager);

            _view.OnLoginRequested += HandleLoginRequested;
            _view.OnExitRequested += HandleExitRequested;

            // 预填充默认凭据
            var (defaultAcc, defaultPass) = AuthConfig.GetDefaultCredentials();
            _view.SetCredentials(defaultAcc, defaultPass);
        }

        public void Dispose()
        {
            if (_view != null)
            {
                _view.OnLoginRequested -= HandleLoginRequested;
                _view.OnExitRequested -= HandleExitRequested;
            }
        }

        private async void HandleLoginRequested(string account, string password)
        {
            await ExecuteLoginAsync(account, password);
        }

        public async Task<bool> ExecuteLoginAsync(string account, string password)
        {
            if (string.IsNullOrWhiteSpace(account) || string.IsNullOrWhiteSpace(password))
            {
                _view.ShowErrorMessage("账号或密码不能为空");
                return false;
            }

            _view.SetLoading(true);

            try
            {
                var auth = _authService ?? SurgicalLiveSDK.Auth;
                if (auth == null)
                {
                    _view.ShowErrorMessage("系统认证服务未初始化");
                    return false;
                }

                await auth.LoginAsync(account.Trim(), password.Trim());

                var sm = _stateManager ?? SurgicalLiveSDK.StateManager;
                if (sm != null)
                {
                    sm.ChangeState(UserAppState.LoggedIn);
                }

                return true;
            }
            catch (Exception ex)
            {
                _view.ShowErrorMessage($"登录失败: {ex.Message}");
                return false;
            }
            finally
            {
                _view.SetLoading(false);
            }
        }

        private void HandleExitRequested()
        {
            Debug.Log("[LoginPresenter] 用户点击退出应用。");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
