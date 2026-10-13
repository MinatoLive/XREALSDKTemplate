using System;
using System.Threading.Tasks;
using SurgicalLivePlatform.Example.Runtime.Event;
using SurgicalLivePlatform.Runtime;
using SurgicalLivePlatform.Runtime.Config;
using SurgicalLivePlatform.Runtime.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SurgicalLivePlatform.Example.Runtime.UI
{
    /// <summary>
    /// 用户登录 UI 视图控制器 (MVP Dumb View)
    /// 继承 BaseUIPanel，实现 IUserLoginView。
    /// 仅负责 UI 元素绑定、用户交互事件上报及状态展示，不直接承担底层 HTTP 网络请求。
    /// </summary>
    public class UserLoginUIController : BaseUIPanel, IUserLoginView
    {
        [Header("UI Elements")]
        public TMP_InputField accountInput;
        public TMP_InputField passwordInput;
        public Toggle showPasswordToggle;
        public Button loginButton;
        public Button exitButton;

        public event Action<string, string> OnLoginRequested;
        public event Action OnExitRequested;

        private LoginPresenter _presenter;
        private bool _isEventsBound = false;

        private void Awake()
        {
            InitializeView();
        }

        private void Start()
        {
            InitializeView();
        }

        protected override void OnInit()
        {
            SurgicalLiveSDK.EnsureInitialized();
            AutoBindUIElements();
            InitializeUI();
            BindEvents();

            // 自愈注入 Presenter
            if (_presenter == null)
            {
                _presenter = new LoginPresenter(this);
            }
        }

        protected override void OnShow()
        {
            InitializeUI();
        }

        protected override void OnHide()
        {
        }

        private void OnDestroy()
        {
            UnbindEvents();
            _presenter?.Dispose();
            _presenter = null;
        }

        public void AttachPresenter(LoginPresenter presenter)
        {
            _presenter?.Dispose();
            _presenter = presenter;
        }

        #region IUserLoginView Implementation

        public void SetLoading(bool isLoading)
        {
            if (loginButton != null)
            {
                loginButton.interactable = !isLoading;
            }
        }

        public void SetCredentials(string account, string password)
        {
            if (accountInput != null) accountInput.text = account;
            if (passwordInput != null) passwordInput.text = password;
        }

        public void ShowErrorMessage(string message)
        {
            Debug.LogError($"[UserLoginUIController] {message}");
            Toast.Show(message, 2.5f);
        }

        #endregion

        /// <summary>
        /// 自动绑定层级树中的 UI 元素，杜绝因 Inspector 序列化丢失或动态生成导致的引用为空
        /// </summary>
        public void AutoBindUIElements()
        {
            if (accountInput == null)
            {
                accountInput = FindComponentByNameOrKeyword<TMP_InputField>("AccountInput", "账号", "account");
            }

            if (passwordInput == null)
            {
                passwordInput = FindComponentByNameOrKeyword<TMP_InputField>("PasswordInput", "密码", "password");
            }

            if (showPasswordToggle == null)
            {
                showPasswordToggle = FindComponentByNameOrKeyword<Toggle>("ShowPasswordToggle", "显示密码", "toggle");
            }

            if (loginButton == null)
            {
                loginButton = FindButtonByNameOrText("LoginButton", "登录", "login");
            }

            if (exitButton == null)
            {
                exitButton = FindButtonByNameOrText("ExitButton", "退出", "exit");
            }
        }

        /// <summary>
        /// 初始化默认值与显示模式
        /// </summary>
        public void InitializeUI()
        {
            var (defaultAccount, defaultPassword) = AuthConfig.GetDefaultCredentials();

            if (accountInput != null)
            {
                accountInput.text = defaultAccount;
            }

            if (passwordInput != null)
            {
                passwordInput.text = defaultPassword;

                // 仅当在开发/测试模式且有预置测试账密时默认明文展示，生产环境留空并使用 Password 掩码
                bool isDevWithCredentials = AuthConfig.IsDevelopmentEnvironment && !string.IsNullOrEmpty(defaultPassword);
                passwordInput.contentType = isDevWithCredentials
                    ? TMP_InputField.ContentType.Standard
                    : TMP_InputField.ContentType.Password;

                passwordInput.ForceLabelUpdate();

                if (showPasswordToggle != null)
                {
                    showPasswordToggle.isOn = isDevWithCredentials;
                }
            }
        }

        /// <summary>
        /// 绑定 UI 事件
        /// </summary>
        public void BindEvents()
        {
            if (_isEventsBound)
            {
                UnbindEvents();
            }

            if (showPasswordToggle != null)
            {
                showPasswordToggle.onValueChanged.AddListener(OnShowPasswordToggled);
            }

            if (loginButton != null)
            {
                loginButton.onClick.AddListener(OnLoginButtonClicked);
            }

            if (exitButton != null)
            {
                exitButton.onClick.AddListener(OnExitButtonClicked);
            }

            EventManager.AddListener(EventId.OnLoginButtonClicked, HandleLoginEvent);
            EventManager.AddListener(EventId.OnExitButtonClicked, HandleExitEvent);

            _isEventsBound = true;
        }

        /// <summary>
        /// 注销 UI 事件
        /// </summary>
        public void UnbindEvents()
        {
            if (!_isEventsBound) return;

            if (showPasswordToggle != null)
            {
                showPasswordToggle.onValueChanged.RemoveListener(OnShowPasswordToggled);
            }

            if (loginButton != null)
            {
                loginButton.onClick.RemoveListener(OnLoginButtonClicked);
            }

            if (exitButton != null)
            {
                exitButton.onClick.RemoveListener(OnExitButtonClicked);
            }

            EventManager.RemoveListener(EventId.OnLoginButtonClicked, HandleLoginEvent);
            EventManager.RemoveListener(EventId.OnExitButtonClicked, HandleExitEvent);

            _isEventsBound = false;
        }

        public void OnShowPasswordToggled(bool isOn)
        {
            if (passwordInput != null)
            {
                passwordInput.contentType = isOn ? TMP_InputField.ContentType.Standard : TMP_InputField.ContentType.Password;
                passwordInput.ForceLabelUpdate();
            }
        }

        public void OnLoginButtonClicked()
        {
            HandleLoginEvent();
        }

        public void OnExitButtonClicked()
        {
            HandleExitEvent();
        }

        private void HandleLoginEvent()
        {
            if (accountInput == null || passwordInput == null)
            {
                AutoBindUIElements();
            }

            string account = accountInput != null && accountInput.text != null ? accountInput.text.Trim() : string.Empty;
            string password = passwordInput != null && passwordInput.text != null ? passwordInput.text.Trim() : string.Empty;

            OnLoginRequested?.Invoke(account, password);
        }

        private void HandleExitEvent()
        {
            OnExitRequested?.Invoke();
        }

        /// <summary>
        /// 兼容旧式调用契约，委托至 Presenter 执行
        /// </summary>
        public async Task<bool> PerformLoginAsync(string account, string password)
        {
            if (_presenter == null)
            {
                _presenter = new LoginPresenter(this);
            }
            return await _presenter.ExecuteLoginAsync(account, password);
        }

        #region Private Auto-Bind Helpers

        private T FindComponentByNameOrKeyword<T>(string exactName, params string[] keywords) where T : Component
        {
            var components = GetComponentsInChildren<T>(true);
            foreach (var comp in components)
            {
                if (comp.gameObject.name.Equals(exactName, StringComparison.OrdinalIgnoreCase))
                {
                    return comp;
                }
            }

            foreach (var comp in components)
            {
                string objName = comp.gameObject.name;
                foreach (var kw in keywords)
                {
                    if (objName.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return comp;
                    }
                }
            }

            return null;
        }

        private Button FindButtonByNameOrText(string exactName, params string[] keywords)
        {
            var buttons = GetComponentsInChildren<Button>(true);
            foreach (var btn in buttons)
            {
                if (btn.gameObject.name.Equals(exactName, StringComparison.OrdinalIgnoreCase))
                {
                    return btn;
                }
            }

            foreach (var btn in buttons)
            {
                string objName = btn.gameObject.name;
                foreach (var kw in keywords)
                {
                    if (objName.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return btn;
                    }
                }

                var tmpText = btn.GetComponentInChildren<TMP_Text>(true);
                if (tmpText != null && !string.IsNullOrEmpty(tmpText.text))
                {
                    foreach (var kw in keywords)
                    {
                        if (tmpText.text.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            return btn;
                        }
                    }
                }

                var uiText = btn.GetComponentInChildren<Text>(true);
                if (uiText != null && !string.IsNullOrEmpty(uiText.text))
                {
                    foreach (var kw in keywords)
                    {
                        if (uiText.text.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            return btn;
                        }
                    }
                }
            }

            return null;
        }

        #endregion
    }
}
