using NUnit.Framework;
using SurgicalLivePlatform;

namespace SurgicalLivePlatform.Tests
{
    [TestFixture]
    public class UITargetLockRulesTests
    {
        [Test]
        public void ShouldLock_NormalButton_ReturnsTrue()
        {
            Assert.IsTrue(UITargetLockRules.ShouldLock(true, true, true, true, false, false, true));
        }

        [Test]
        public void ShouldLock_DraggableButton_ReturnsFalse()
        {
            Assert.IsFalse(UITargetLockRules.ShouldLock(true, true, true, true, false, true, true));
        }

        [Test]
        public void ShouldLock_InsideScrollRectAndExcluding_ReturnsFalse()
        {
            Assert.IsFalse(UITargetLockRules.ShouldLock(true, true, true, true, true, false, true));
        }

        [Test]
        public void ShouldLock_InsideScrollRectButNotExcluding_ReturnsTrue()
        {
            Assert.IsTrue(UITargetLockRules.ShouldLock(true, true, true, true, true, false, false));
        }

        [Test]
        public void ShouldLock_NonHandRay_ReturnsFalse()
        {
            Assert.IsFalse(UITargetLockRules.ShouldLock(false, true, true, true, false, false, true));
        }

        [Test]
        public void ShouldLock_NoButtonOrInactiveOrNonInteractable_ReturnsFalse()
        {
            Assert.IsFalse(UITargetLockRules.ShouldLock(true, false, true, true, false, false, true));
            Assert.IsFalse(UITargetLockRules.ShouldLock(true, true, false, true, false, false, true));
            Assert.IsFalse(UITargetLockRules.ShouldLock(true, true, true, false, false, false, true));
        }
    }
}
