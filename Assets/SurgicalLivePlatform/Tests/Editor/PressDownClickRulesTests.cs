using NUnit.Framework;
using SurgicalLivePlatform;

namespace SurgicalLivePlatform.Tests
{
    [TestFixture]
    public class PressDownClickRulesTests
    {
        [Test]
        public void ShouldHandle_HandRayButtonReady_ReturnsTrue()
        {
            Assert.IsTrue(PressDownClickRules.ShouldHandle(true, true, true, true, false, false, true));
        }

        [Test]
        public void ShouldHandle_NonHandRay_ReturnsFalse()
        {
            Assert.IsFalse(PressDownClickRules.ShouldHandle(false, true, true, true, false, false, true));
        }

        [Test]
        public void ShouldHandle_NoButton_ReturnsFalse()
        {
            Assert.IsFalse(PressDownClickRules.ShouldHandle(true, false, true, true, false, false, true));
        }

        [Test]
        public void ShouldHandle_InactiveButton_ReturnsFalse()
        {
            Assert.IsFalse(PressDownClickRules.ShouldHandle(true, true, false, true, false, false, true));
        }

        [Test]
        public void ShouldHandle_NonInteractableButton_ReturnsFalse()
        {
            Assert.IsFalse(PressDownClickRules.ShouldHandle(true, true, true, false, false, false, true));
        }

        [Test]
        public void ShouldHandle_DraggableButton_ReturnsFalse()
        {
            Assert.IsFalse(PressDownClickRules.ShouldHandle(true, true, true, true, false, true, true));
        }

        [Test]
        public void ShouldHandle_InsideScrollRectAndExcluding_ReturnsFalse()
        {
            Assert.IsFalse(PressDownClickRules.ShouldHandle(true, true, true, true, true, false, true));
        }

        [Test]
        public void ShouldHandle_InsideScrollRectButNotExcluding_ReturnsTrue()
        {
            Assert.IsTrue(PressDownClickRules.ShouldHandle(true, true, true, true, true, false, false));
        }

        [Test]
        public void ShouldHandle_AllConditionsPass_ReturnsTrue()
        {
            Assert.IsTrue(PressDownClickRules.ShouldHandle(true, true, true, true, false, false, true));
        }
    }
}
