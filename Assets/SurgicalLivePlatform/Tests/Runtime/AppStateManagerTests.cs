using NUnit.Framework;
using SurgicalLivePlatform.Runtime.Models;
using SurgicalLivePlatform.Runtime.Services;

namespace SurgicalLivePlatform.Tests
{
    [TestFixture]
    public class AppStateManagerTests
    {
        private AppStateManager _stateManager;

        [SetUp]
        public void Setup()
        {
            _stateManager = new AppStateManager();
        }

        [Test]
        public void InitialState_ShouldBeInit()
        {
            Assert.AreEqual(UserAppState.Init, _stateManager.CurrentState);
        }

        [Test]
        public void ChangeState_ShouldUpdateCurrentState()
        {
            _stateManager.ChangeState(UserAppState.LoggedOut);
            Assert.AreEqual(UserAppState.LoggedOut, _stateManager.CurrentState);
        }

        [Test]
        public void ChangeState_ShouldTriggerOnStateChangedEvent()
        {
            bool eventTriggered = false;
            UserAppState capturedOldState = UserAppState.Error;
            UserAppState capturedNewState = UserAppState.Error;

            _stateManager.OnStateChanged += (oldState, newState) =>
            {
                eventTriggered = true;
                capturedOldState = oldState;
                capturedNewState = newState;
            };

            _stateManager.ChangeState(UserAppState.LoggingIn);

            Assert.IsTrue(eventTriggered);
            Assert.AreEqual(UserAppState.Init, capturedOldState);
            Assert.AreEqual(UserAppState.LoggingIn, capturedNewState);
        }

        [Test]
        public void ChangeState_ToSameState_ShouldNotTriggerEvent()
        {
            // Arrange
            _stateManager.ChangeState(UserAppState.LoggedOut);
            
            bool eventTriggered = false;
            _stateManager.OnStateChanged += (oldState, newState) =>
            {
                eventTriggered = true;
            };

            // Act
            _stateManager.ChangeState(UserAppState.LoggedOut);

            // Assert
            Assert.IsFalse(eventTriggered);
        }
    }
}
