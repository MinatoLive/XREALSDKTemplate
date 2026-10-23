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

        [Header("Panel Switch Elements")]
        public Button switchPatternLockButton;
        public GameObject loginPanel;
        public GameObject patternLockPanel;

        private string _password = string.Empty;
        private string _cachedAccount = string.Empty;
        private string _cachedPassword = string.Empty;

        public string CachedAccount => _cachedAccount;
        public string CachedPassword => _cachedPassword;

        public string password
        {
            get => passwordInput != null ? passwordInput.text : _password;
            set
            {
                _password = value;
                _cachedPassword = value;
                if (passwordInput != null)
                {
                    passwordInput.text = value;
                }
            }
        }

        public void ShowPatternLockPanel()
        {
            if (loginPanel != null)
            {
                loginPanel.SetActive(false);
            }
            if (patternLockPanel != null)
            {
                patternLockPanel.SetActive(true);
            }
        }

        public void ShowLoginPanel()
        {
            if (patternLockPanel != null)
            {
                patternLockPanel.SetActive(false);
            }
            if (loginPanel != null)
            {
                loginPanel.SetActive(true);
            }
        }

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
            ShowLoginPanel();
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
            ShowLoginPanel();
            SetLoading(false); // 确保从大厅返回时恢复登录按钮 interactable = true，点击登录按钮仍然可以进行登录
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
            _cachedAccount = account;
            _cachedPassword = password;
            if (accountInput != null) accountInput.text = account;
            if (passwordInput != null) passwordInput.text = password;
        }

        public void ClearCredentials()
        {
            _cachedAccount = string.Empty;
            _cachedPassword = string.Empty;
            if (accountInput != null) accountInput.text = string.Empty;
            if (passwordInput != null) passwordInput.text = string.Empty;
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

            if (switchPatternLockButton == null)
            {
                switchPatternLockButton = FindButtonByNameOrText("PatternLockPanelView", "viewPsd", "切换图案密码", "pattern");
            }

            if (loginPanel == null)
            {
                Transform t = transform.Find("UI Poke Components/Login");
                if (t != null)
                {
                    loginPanel = t.gameObject;
                }
                else
                {
                    var rects = GetComponentsInChildren<RectTransform>(true);
                    foreach (var r in rects)
                    {
                        if (r.gameObject != gameObject && r.gameObject.name.Equals("Login", StringComparison.OrdinalIgnoreCase))
                        {
                            loginPanel = r.gameObject;
                            break;
                        }
                    }
                }
            }

            if (patternLockPanel == null)
            {
                Transform t = transform.Find("UI Poke Components/PatternLockPanel");
                if (t != null)
                {
                    patternLockPanel = t.gameObject;
                }
                else
                {
                    var lockUI = GetComponentInChildren<PatternLockUI>(true);
                    if (lockUI != null)
                    {
                        patternLockPanel = lockUI.gameObject;
                    }
                }
            }

            if (patternLockPanel != null)
            {
                var lockUI = patternLockPanel.GetComponent<PatternLockUI>();
                if (lockUI != null && lockUI.loginUIController == null)
                {
                    lockUI.loginUIController = this;
                }
            }
        }

        /// <summary>
        /// 初始化默认值与显示模式。
        /// 遵循保留上次账号密码原则：若输入框或缓存已有内容，则予以保留，仅在首次为空时注入配置默认账密。
        /// </summary>
        public void InitializeUI()
        {
            var (defaultAccount, defaultPassword) = AuthConfig.GetDefaultCredentials();

            if (accountInput != null)
            {
                if (string.IsNullOrEmpty(accountInput.text))
                {
                    accountInput.text = !string.IsNullOrEmpty(_cachedAccount) ? _cachedAccount : defaultAccount;
                }
                else
                {
                    _cachedAccount = accountInput.text;
                }
            }

            if (passwordInput != null)
            {
                if (string.IsNullOrEmpty(passwordInput.text))
                {
                    passwordInput.text = !string.IsNullOrEmpty(_cachedPassword) ? _cachedPassword : defaultPassword;
                }
                else
                {
                    _cachedPassword = passwordInput.text;
                }

                // 仅当在开发/测试模式且有预置测试账密时默认明文展示，生产环境留空并使用 Password 掩码
                bool isDevWithCredentials = AuthConfig.IsDevelopmentEnvironment && !string.IsNullOrEmpty(passwordInput.text);
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

            if (accountInput != null)
            {
                accountInput.onValueChanged.AddListener(OnAccountInputChanged);
            }

            if (passwordInput != null)
            {
                passwordInput.onValueChanged.AddListener(OnPasswordInputChanged);
            }

            if (loginButton != null)
            {
                loginButton.onClick.AddListener(OnLoginButtonClicked);
            }

            if (exitButton != null)
            {
                exitButton.onClick.AddListener(OnExitButtonClicked);
            }

            if (switchPatternLockButton != null)
            {
                switchPatternLockButton.onClick.AddListener(ShowPatternLockPanel);
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

            if (accountInput != null)
            {
                accountInput.onValueChanged.RemoveListener(OnAccountInputChanged);
            }

            if (passwordInput != null)
            {
                passwordInput.onValueChanged.RemoveListener(OnPasswordInputChanged);
            }

            if (loginButton != null)
            {
                loginButton.onClick.RemoveListener(OnLoginButtonClicked);
            }

            if (exitButton != null)
            {
                exitButton.onClick.RemoveListener(OnExitButtonClicked);
            }

            if (switchPatternLockButton != null)
            {
                switchPatternLockButton.onClick.RemoveListener(ShowPatternLockPanel);
            }

            EventManager.RemoveListener(EventId.OnLoginButtonClicked, HandleLoginEvent);
            EventManager.RemoveListener(EventId.OnExitButtonClicked, HandleExitEvent);

            _isEventsBound = false;
        }

        private void OnAccountInputChanged(string value)
        {
            _cachedAccount = value;
        }

        private void OnPasswordInputChanged(string value)
        {
            _cachedPassword = value;
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

            _cachedAccount = account;
            _cachedPassword = password;

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
