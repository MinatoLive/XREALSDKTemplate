using NUnit.Framework;
using SurgicalLivePlatform;

namespace SurgicalLivePlatform.Tests
{
    [TestFixture]
    public class PinchAimLockRulesTests
    {
        [Test]
        public void ShouldLock_ValueAboveMinOnNormalButton_ReturnsTrue()
        {
            Assert.IsTrue(PinchAimLockRules.ShouldLock(0.95f, 0.9f, true, true, true, false, false, false, true));
        }

        [Test]
        public void ShouldLock_ValueAtOrBelowMin_ReturnsFalse()
        {
            Assert.IsFalse(PinchAimLockRules.ShouldLock(0.9f, 0.9f, true, true, true, false, false, false, true));
            Assert.IsFalse(PinchAimLockRules.ShouldLock(0.5f, 0.9f, true, true, true, false, false, false, true));
        }

        [Test]
        public void ShouldLock_DraggableOrSelected_ReturnsFalse()
        {
            Assert.IsFalse(PinchAimLockRules.ShouldLock(0.95f, 0.9f, true, true, true, false, true, false, true));
            Assert.IsFalse(PinchAimLockRules.ShouldLock(0.95f, 0.9f, true, true, true, false, false, true, true));
        }

        [Test]
        public void ShouldLock_ScrollRectExclusion_IsRespected()
        {
            Assert.IsFalse(PinchAimLockRules.ShouldLock(0.95f, 0.9f, true, true, true, true, false, false, true));
            Assert.IsTrue(PinchAimLockRules.ShouldLock(0.95f, 0.9f, true, true, true, true, false, false, false));
        }

        [Test]
        public void ShouldLock_MissingOrInactiveButton_ReturnsFalse()
        {
            Assert.IsFalse(PinchAimLockRules.ShouldLock(0.95f, 0.9f, false, true, true, false, false, false, true));
            Assert.IsFalse(PinchAimLockRules.ShouldLock(0.95f, 0.9f, true, false, true, false, false, false, true));
            Assert.IsFalse(PinchAimLockRules.ShouldLock(0.95f, 0.9f, true, true, false, false, false, false, true));
        }

        [Test]
        public void ShouldUnlock_OnlyAtOrBelowMin()
        {
            Assert.IsTrue(PinchAimLockRules.ShouldUnlock(0.5f, 0.9f));
            Assert.IsTrue(PinchAimLockRules.ShouldUnlock(0.9f, 0.9f));
            Assert.IsFalse(PinchAimLockRules.ShouldUnlock(0.91f, 0.9f));
        }

        [Test]
        public void ShouldClick_OnlyOnceAboveMax()
        {
            Assert.IsTrue(PinchAimLockRules.ShouldClick(0.99f, 0.98f, false));
            Assert.IsFalse(PinchAimLockRules.ShouldClick(0.98f, 0.98f, false));
            Assert.IsFalse(PinchAimLockRules.ShouldClick(0.99f, 0.98f, true));
        }

        [Test]
        public void CanUseEdgeTarget_RecentHoverWithinTolerance_ReturnsTrue()
        {
            Assert.IsTrue(PinchAimLockRules.CanUseEdgeTarget(true, 0.1f, 0.3f, true, true));
        }

        [Test]
        public void CanUseEdgeTarget_MissingOrExpiredHover_ReturnsFalse()
        {
            Assert.IsFalse(PinchAimLockRules.CanUseEdgeTarget(false, 0.1f, 0.3f, true, true));
            Assert.IsFalse(PinchAimLockRules.CanUseEdgeTarget(true, 0.31f, 0.3f, true, true));
        }

        [Test]
        public void CanUseEdgeTarget_OutsideToleranceOrIneligible_ReturnsFalse()
        {
            Assert.IsFalse(PinchAimLockRules.CanUseEdgeTarget(true, 0.1f, 0.3f, false, true));
            Assert.IsFalse(PinchAimLockRules.CanUseEdgeTarget(true, 0.1f, 0.3f, true, false));
        }
    }
}
