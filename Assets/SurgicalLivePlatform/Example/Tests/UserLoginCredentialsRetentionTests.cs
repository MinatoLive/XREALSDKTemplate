using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SurgicalLivePlatform.Example.Runtime.UI;

namespace SurgicalLivePlatform.Example.Tests
{
    [TestFixture]
    public class UserLoginCredentialsRetentionTests
    {
        private GameObject _go;
        private UserLoginUIController _controller;
        private TMP_InputField _accountInput;
        private TMP_InputField _passwordInput;
        private Button _loginButton;

        [SetUp]
        public void Setup()
        {
            _go = new GameObject("LoginUI", typeof(RectTransform), typeof(Canvas));
            _controller = _go.AddComponent<UserLoginUIController>();

            var accGo = new GameObject("AccountInput", typeof(RectTransform), typeof(TMP_InputField));
            accGo.transform.SetParent(_go.transform, false);
            _accountInput = accGo.GetComponent<TMP_InputField>();
            var accTextGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            accTextGo.transform.SetParent(accGo.transform, false);
            _accountInput.textComponent = accTextGo.GetComponent<TextMeshProUGUI>();

            var pwdGo = new GameObject("PasswordInput", typeof(RectTransform), typeof(TMP_InputField));
            pwdGo.transform.SetParent(_go.transform, false);
            _passwordInput = pwdGo.GetComponent<TMP_InputField>();
            var pwdTextGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            pwdTextGo.transform.SetParent(pwdGo.transform, false);
            _passwordInput.textComponent = pwdTextGo.GetComponent<TextMeshProUGUI>();

            var btnGo = new GameObject("LoginButton", typeof(RectTransform), typeof(Button));
            btnGo.transform.SetParent(_go.transform, false);
            _loginButton = btnGo.GetComponent<Button>();

            _controller.accountInput = _accountInput;
            _controller.passwordInput = _passwordInput;
            _controller.loginButton = _loginButton;

            _controller.InitializeView();
        }

        [TearDown]
        public void Teardown()
        {
            if (_go != null)
                Object.DestroyImmediate(_go);
        }

        [Test]
        public void InitializeUI_WhenEmpty_PopulatesDefaultCredentials()
        {
            _controller.InitializeUI();
            Assert.IsFalse(string.IsNullOrEmpty(_accountInput.text), "AccountInput should have default credentials when empty.");
        }

        [Test]
        public void InitializeUI_WhenUserEnteredCredentials_RetainsUserCredentials()
        {
            _accountInput.text = "custom_doctor";
            _passwordInput.text = "secret_pass_123";

            _controller.InitializeUI();

            Assert.AreEqual("custom_doctor", _accountInput.text);
            Assert.AreEqual("secret_pass_123", _passwordInput.text);
        }

        [Test]
        public void OnShow_AfterLobbyReturn_RetainsCredentialsAndEnablesLoginButton()
        {
            _accountInput.text = "custom_doctor";
            _passwordInput.text = "secret_pass_123";

            // 模拟在登录前点击了登录按钮触发 loading 禁用状态
            _controller.OnLoginButtonClicked();
            _controller.SetLoading(true);
            Assert.IsFalse(_loginButton.interactable);

            // 模拟大厅返回登录界面：Show() 触发 OnShow()
            _controller.Show();

            Assert.AreEqual("custom_doctor", _accountInput.text, "返回登录界面时账号应被完整保留。");
            Assert.AreEqual("secret_pass_123", _passwordInput.text, "返回登录界面时密码应被完整保留。");
            Assert.IsTrue(_loginButton.interactable, "返回登录界面后登录按钮必须处于可交互状态。");
        }

        [Test]
        public void OnLoginButtonClicked_TriggersLoginWithRetainedCredentials()
        {
            _accountInput.text = "retained_user";
            _passwordInput.text = "retained_pwd";

            string reportedAccount = null;
            string reportedPassword = null;
            _controller.OnLoginRequested += (acc, pwd) => {
                reportedAccount = acc;
                reportedPassword = pwd;
            };

            _controller.OnLoginButtonClicked();

            Assert.AreEqual("retained_user", reportedAccount);
            Assert.AreEqual("retained_pwd", reportedPassword);
        }
    }
}
