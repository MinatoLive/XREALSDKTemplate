using SurgicalLivePlatform.Example.Runtime.Event;
using SurgicalLivePlatform.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using SurgicalLivePlatform.Runtime.Models;
namespace SurgicalLivePlatform.Example.Runtime.UI
{
    /// <summary>
    /// 用户登录 UI 控制器
    /// 负责初始化 UI 默认值，并通过统一事件管理器(EventManager)分发交互事件。
    /// 严禁在 Inspector 中拖拽绑定 Button 的 OnClick。
    /// </summary>
    public class UserLoginUIController : MonoBehaviour
    {
        [Header("UI Elements")]
        public TMP_InputField accountInput;
        public TMP_InputField passwordInput;
        public Toggle showPasswordToggle;
        public Button loginButton;
        public Button exitButton;

        private void Start()
        {
            SurgicalLiveSDK.Initialize();
            InitializeUI();
            BindEvents();
        }

        private void OnDestroy()
        {
            UnbindEvents();
        }

        /// <summary>
        /// 初始化默认值
        /// </summary>
        private void InitializeUI()
        {
            if (accountInput != null)
            {
                accountInput.text = "test1";
            }
            
            if (passwordInput != null)
            {
                passwordInput.text = "12345678";
                passwordInput.contentType = TMP_InputField.ContentType.Standard; // 默认明文
            }

            if (showPasswordToggle != null)
            {
                showPasswordToggle.isOn = true;
            }
        }

        /// <summary>
        /// 绑定 UI 事件
        /// </summary>
        private void BindEvents()
        {
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

            // 向事件管理器注册自身感兴趣的事件，这里是用于响应自身触发后在控制台打印消息
            EventManager.AddListener(EventId.OnLoginButtonClicked, HandleLoginEvent);
            EventManager.AddListener(EventId.OnExitButtonClicked, HandleExitEvent);
        }

        /// <summary>
        /// 注销 UI 事件
        /// </summary>
        private void UnbindEvents()
        {
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
        }

        private void OnShowPasswordToggled(bool isOn)
        {
            if (passwordInput != null)
            {
                passwordInput.contentType = isOn ? TMP_InputField.ContentType.Standard : TMP_InputField.ContentType.Password;
                passwordInput.ForceLabelUpdate(); // 刷新显示
            }
        }

        private void OnLoginButtonClicked()
        {
            // 通过事件管理器分发事件
            EventManager.Broadcast(EventId.OnLoginButtonClicked);
        }

        private void OnExitButtonClicked()
        {
            // 通过事件管理器分发事件
            EventManager.Broadcast(EventId.OnExitButtonClicked);
        }

        // ------------------ 事件响应实现 ------------------ //
        private async void HandleLoginEvent()
        {
            Debug.Log("[UserLogin] 登录被触发 (Login Triggered)");
            if (accountInput == null || passwordInput == null) return;
            
            string account = accountInput.text.Trim();
            string password = passwordInput.text.Trim();
            
            if (string.IsNullOrEmpty(account) || string.IsNullOrEmpty(password))
            {
                Debug.LogError("[UserLogin] 账号或密码不能为空！");
                return;
            }
            
            if (account.Length < 3 || password.Length < 6)
            {
                Debug.LogError("[UserLogin] 账号或密码格式不正确 (账号至少3位，密码至少6位)！");
                return;
            }
            
            try
            {
                // 可选：在这里添加正在登录的UI提示/禁用按钮
                if (loginButton != null) loginButton.interactable = false;
                
                Debug.Log($"[UserLogin] 正在尝试登录账号: {account}");
                await SurgicalLiveSDK.Auth.LoginAsync(account, password);
                Debug.Log("[UserLogin] 登录成功！");
                
                // 获取当前用户信息并更新本地储存
                var userInfo = await SurgicalLiveSDK.Auth.GetUserInfoAsync();
                if (userInfo != null)
                {
                    var localUser = await SurgicalLiveSDK.Storage.GetUserAsync(userInfo.id);
                    if (localUser != null)
                    {
                        localUser.display_name = userInfo.name;
                        localUser.avatar_url = userInfo.avatar_url;
                        localUser.user = userInfo;

                        await SurgicalLiveSDK.Storage.SaveUserAsync(localUser);
                    }
                    Debug.Log($"[UserLogin] 已获取并储存当前用户信息: ID={userInfo.id}, Username={userInfo.username}, Name={userInfo.name}");
                }
                
                // 切换用户状态为已登录，UI 将通过事件驱动自动响应
                SurgicalLiveSDK.StateManager.ChangeState(UserAppState.LoggedIn);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[UserLogin] 登录失败: {ex.Message}");
            }
            finally
            {
                if (loginButton != null) loginButton.interactable = true;
            }
        }

        private async void HandleExitEvent()
        {
            Debug.Log("[UserLogin] 退出被触发 (Exit Triggered)");
            
            try
            {
                if (SurgicalLiveSDK.Auth != null)
                {
                    await SurgicalLiveSDK.Auth.LogoutAsync();
                    Debug.Log("[UserLogin] SDK 登出成功。");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[UserLogin] 登出时发生异常: {ex.Message}");
            }
            finally
            {
                SurgicalLiveSDK.Dispose();

#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#elif UNITY_ANDROID
                Application.Quit();
#else
                Application.Quit();
#endif
            }
        }
    }
}
