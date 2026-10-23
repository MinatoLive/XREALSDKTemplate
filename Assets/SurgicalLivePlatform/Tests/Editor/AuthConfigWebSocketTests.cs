using NUnit.Framework;
using SurgicalLivePlatform.Runtime.Config;

namespace SurgicalLivePlatform.Tests
{
    [TestFixture]
    public class AuthConfigWebSocketTests
    {
        [SetUp]
        public void SetUp()
        {
            AuthConfig.ResetToDefault();
        }

        [TearDown]
        public void TearDown()
        {
            AuthConfig.ResetToDefault();
        }

        [Test]
        public void WebSocketUrl_FromHttpsBase_UsesWssAndApiPrefix()
        {
            AuthConfig.BaseUrl = "https://example.com:8444";

            Assert.AreEqual("wss://example.com:8444/api/v1/ws", AuthConfig.WebSocketUrl);
        }

        [Test]
        public void WebSocketUrl_FromHttpBase_UsesWsAndApiPrefix()
        {
            AuthConfig.BaseUrl = "http://example.com:8081";

            Assert.AreEqual("ws://example.com:8081/api/v1/ws", AuthConfig.WebSocketUrl);
        }

        [Test]
        public void GetRoomWebSocketUrl_AppendsEscapedRoomId()
        {
            AuthConfig.BaseUrl = "https://example.com:8444";

            Assert.AreEqual(
                "wss://example.com:8444/api/v1/ws?roomId=room%201",
                AuthConfig.GetRoomWebSocketUrl("room 1"));
        }
    }
}
