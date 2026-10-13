using System.Threading.Tasks;
using NUnit.Framework;
using SurgicalLivePlatform.Example.Runtime.Event;
using SurgicalLivePlatform.Example.Runtime.UI;
using SurgicalLivePlatform.Runtime.Config;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SurgicalLivePlatform.Tests
{
    [TestFixture]
    public class UserLoginFlowTests
    {
        [TearDown]
        public void TearDown()
        {
            AuthConfig.ResetTestingOverrides();
        }

        #region 1. AuthConfig Tests

        [Test]
        public void AuthConfig_DevelopmentMode_ReturnsDefaultCredentials()
        {
            // Arrange
            AuthConfig.SetDevelopmentModeForTesting(true);

            // Act
            bool isDev = AuthConfig.IsDevelopmentEnvironment;
            var (account, password) = AuthConfig.GetDefaultCredentials();

            // Assert
            Assert.IsTrue(isDev, "开发模式判定应为 true");
            Assert.AreEqual("test1", account, "默认测试账号必须为 test1");
            Assert.AreEqual("12345678", password, "默认测试密码必须为 12345678");
        }

        [Test]
        public void AuthConfig_ReleaseMode_ReturnsEmptyCredentials()
        {
            // Arrange (模拟正式上线/Release环境)
            AuthConfig.SetDevelopmentModeForTesting(false);

            // Act
            bool isDev = AuthConfig.IsDevelopmentEnvironment;
            var (account, password) = AuthConfig.GetDefaultCredentials();

            // Assert
            Assert.IsFalse(isDev, "生产发布模式判定应为 false");
            Assert.IsEmpty(account, "生产发布模式下默认账号必须严格留空");
            Assert.IsEmpty(password, "生产发布模式下默认密码必须严格留空");
        }

        #endregion

        #region 2. UserLoginUIController Auto-Bind & Initialization Tests

        [Test]
        public void UserLoginUIController_AutoBind_FindsChildrenComponents()
        {
            // Arrange: 构造层级树模拟场景
            var rootGo = new GameObject("LoginPanel");
            var bgGo = new GameObject("BackgroundPanel");
            bgGo.transform.SetParent(rootGo.transform);

            var accountGo = new GameObject("AccountInput");
            accountGo.transform.SetParent(bgGo.transform);
            var accountInput = accountGo.AddComponent<TMP_InputField>();

            var passGo = new GameObject("PasswordInput");
            passGo.transform.SetParent(bgGo.transform);
            var passInput = passGo.AddComponent<TMP_InputField>();

            var toggleGo = new GameObject("ShowPasswordToggle");
            toggleGo.transform.SetParent(bgGo.transform);
            var toggle = toggleGo.AddComponent<Toggle>();

            var loginBtnGo = new GameObject("LoginButton");
            loginBtnGo.transform.SetParent(bgGo.transform);
            var loginBtn = loginBtnGo.AddComponent<Button>();

            var exitBtnGo = new GameObject("ExitButton");
            exitBtnGo.transform.SetParent(bgGo.transform);
            var exitBtn = exitBtnGo.AddComponent<Button>();

            var ctrl = rootGo.AddComponent<UserLoginUIController>();

            try
            {
                // Act: 执行自动绑定
                ctrl.AutoBindUIElements();

                // Assert
                Assert.IsNotNull(ctrl.accountInput, "accountInput 应该被自动绑定");
                Assert.IsNotNull(ctrl.passwordInput, "passwordInput 应该被自动绑定");
                Assert.IsNotNull(ctrl.showPasswordToggle, "showPasswordToggle 应该被自动绑定");
                Assert.IsNotNull(ctrl.loginButton, "loginButton 应该被自动绑定");
                Assert.IsNotNull(ctrl.exitButton, "exitButton 应该被自动绑定");
                Assert.AreEqual(accountInput, ctrl.accountInput);
                Assert.AreEqual(passInput, ctrl.passwordInput);
                Assert.AreEqual(toggle, ctrl.showPasswordToggle);
                Assert.AreEqual(loginBtn, ctrl.loginButton);
                Assert.AreEqual(exitBtn, ctrl.exitButton);
            }
            finally
            {
                Object.DestroyImmediate(rootGo);
            }
        }

        [Test]
        public void UserLoginUIController_InitializeUI_DevMode_PopulatesCredentials()
        {
            // Arrange
            AuthConfig.SetDevelopmentModeForTesting(true);

            var rootGo = new GameObject("LoginPanel");
            var accountInput = rootGo.AddComponent<TMP_InputField>();
            var passInput = rootGo.AddComponent<TMP_InputField>();
            var toggle = rootGo.AddComponent<Toggle>();

            var ctrl = rootGo.AddComponent<UserLoginUIController>();
            ctrl.accountInput = accountInput;
            ctrl.passwordInput = passInput;
            ctrl.showPasswordToggle = toggle;

            try
            {
                // Act
                ctrl.InitializeUI();

                // Assert
                Assert.AreEqual("test1", ctrl.accountInput.text, "测试模式下应预填 test1");
                Assert.AreEqual("12345678", ctrl.passwordInput.text, "测试模式下应预填 12345678");
                Assert.AreEqual(TMP_InputField.ContentType.Standard, ctrl.passwordInput.contentType, "测试模式下密码应默认明文展示");
                Assert.IsTrue(ctrl.showPasswordToggle.isOn, "测试模式下显示密码 Toggle 应默认激活");
            }
            finally
            {
                Object.DestroyImmediate(rootGo);
            }
        }

        [Test]
        public void UserLoginUIController_InitializeUI_ReleaseMode_LeavesCredentialsEmpty()
        {
            // Arrange
            AuthConfig.SetDevelopmentModeForTesting(false);

            var rootGo = new GameObject("LoginPanel");
            var accountInput = rootGo.AddComponent<TMP_InputField>();
            var passInput = rootGo.AddComponent<TMP_InputField>();
            var toggle = rootGo.AddComponent<Toggle>();

            var ctrl = rootGo.AddComponent<UserLoginUIController>();
            ctrl.accountInput = accountInput;
            ctrl.passwordInput = passInput;
            ctrl.showPasswordToggle = toggle;

            try
            {
                // Act
                ctrl.InitializeUI();

                // Assert
                Assert.IsEmpty(ctrl.accountInput.text, "上线模式下账号必须留空");
                Assert.IsEmpty(ctrl.passwordInput.text, "上线模式下密码必须留空");
                Assert.AreEqual(TMP_InputField.ContentType.Password, ctrl.passwordInput.contentType, "上线模式下必须为 Password 掩码类型");
                Assert.IsFalse(ctrl.showPasswordToggle.isOn, "上线模式下显示密码 Toggle 应默认未激活");
            }
            finally
            {
                Object.DestroyImmediate(rootGo);
            }
        }

        #endregion

        #region 3. Input Validation & Form Tests

        [Test]
        public async Task UserLoginUIController_PerformLogin_EmptyInput_FailsValidation()
        {
            var rootGo = new GameObject("LoginTest");
            var ctrl = rootGo.AddComponent<UserLoginUIController>();

            try
            {
                bool emptyResult = await ctrl.PerformLoginAsync("", "");
                Assert.IsFalse(emptyResult, "空账号密码应校验失败");

                bool whitespaceResult = await ctrl.PerformLoginAsync("   ", "   ");
                Assert.IsFalse(whitespaceResult, "空白字符账号密码应校验失败");
            }
            finally
            {
                Object.DestroyImmediate(rootGo);
            }
        }

        [Test]
        public async Task UserLoginUIController_PerformLogin_ShortLength_FailsValidation()
        {
            var rootGo = new GameObject("LoginTest");
            var ctrl = rootGo.AddComponent<UserLoginUIController>();

            try
            {
                // 账号至少3位，密码至少6位
                bool shortAccountResult = await ctrl.PerformLoginAsync("ab", "12345678");
                Assert.IsFalse(shortAccountResult, "账号小于3位应校验失败");

                bool shortPasswordResult = await ctrl.PerformLoginAsync("test1", "12345");
                Assert.IsFalse(shortPasswordResult, "密码小于6位应校验失败");
            }
            finally
            {
                Object.DestroyImmediate(rootGo);
            }
        }

        [Test]
        public void UserLoginUIController_TogglePassword_SwitchesContentType()
        {
            var rootGo = new GameObject("LoginTest");
            var passInput = rootGo.AddComponent<TMP_InputField>();
            var ctrl = rootGo.AddComponent<UserLoginUIController>();
            ctrl.passwordInput = passInput;

            try
            {
                ctrl.OnShowPasswordToggled(true);
                Assert.AreEqual(TMP_InputField.ContentType.Standard, ctrl.passwordInput.contentType, "Toggle 勾选时应为 Standard 明文");

                ctrl.OnShowPasswordToggled(false);
                Assert.AreEqual(TMP_InputField.ContentType.Password, ctrl.passwordInput.contentType, "Toggle 取消时应为 Password 密文");
            }
            finally
            {
                Object.DestroyImmediate(rootGo);
            }
        }

        [Test]
        public void UserLoginUIController_LoginButton_BroadcastsEvent()
        {
            var rootGo = new GameObject("LoginTest");
            var ctrl = rootGo.AddComponent<UserLoginUIController>();

            bool eventFired = false;
            void OnLoginClick()
            {
                eventFired = true;
            }

            try
            {
                EventManager.AddListener(EventId.OnLoginButtonClicked, OnLoginClick);
                ctrl.OnLoginButtonClicked();

                Assert.IsTrue(eventFired, "点击登录按钮时应成功广播 OnLoginButtonClicked 事件");
            }
            finally
            {
                EventManager.RemoveListener(EventId.OnLoginButtonClicked, OnLoginClick);
                Object.DestroyImmediate(rootGo);
            }
        }

        #endregion
    }
}
