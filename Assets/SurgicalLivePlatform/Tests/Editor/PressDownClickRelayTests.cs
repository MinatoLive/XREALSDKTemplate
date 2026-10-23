using NUnit.Framework;
using SurgicalLivePlatform;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SurgicalLivePlatform.Tests
{
    [TestFixture]
    public class PressDownClickRelayTests
    {
        [Test]
        public void IsDraggable_EventTriggerWithoutDragEntries_ReturnsFalse()
        {
            var go = new GameObject("RelayDragProbe", typeof(RectTransform), typeof(Button), typeof(EventTrigger));
            try
            {
                var button = go.GetComponent<Button>();
                Assert.IsFalse(UIPressTargetResolver.IsDraggable(button));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void IsDraggable_EventTriggerWithDragEntry_ReturnsTrue()
        {
            var go = new GameObject("RelayDragProbe", typeof(RectTransform), typeof(Button), typeof(EventTrigger));
            try
            {
                var button = go.GetComponent<Button>();
                var trigger = go.GetComponent<EventTrigger>();
                trigger.triggers.Add(new EventTrigger.Entry { eventID = EventTriggerType.Drag });
                Assert.IsTrue(UIPressTargetResolver.IsDraggable(button));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void IsLocalPointInsideExpandedRect_Inside_ReturnsTrue()
        {
            var rect = new Rect(0f, 0f, 100f, 100f);

            Assert.IsTrue(UIPressTargetResolver.IsLocalPointInsideExpandedRect(new Vector2(50f, 50f), rect, 0.25f));
        }

        [Test]
        public void IsLocalPointInsideExpandedRect_WithinMargin_ReturnsTrue()
        {
            var rect = new Rect(0f, 0f, 100f, 100f);

            Assert.IsTrue(UIPressTargetResolver.IsLocalPointInsideExpandedRect(new Vector2(120f, 50f), rect, 0.25f));
            Assert.IsTrue(UIPressTargetResolver.IsLocalPointInsideExpandedRect(new Vector2(-20f, 50f), rect, 0.25f));
        }

        [Test]
        public void IsLocalPointInsideExpandedRect_OutsideMargin_ReturnsFalse()
        {
            var rect = new Rect(0f, 0f, 100f, 100f);

            Assert.IsFalse(UIPressTargetResolver.IsLocalPointInsideExpandedRect(new Vector2(126f, 50f), rect, 0.25f));
            Assert.IsFalse(UIPressTargetResolver.IsLocalPointInsideExpandedRect(new Vector2(50f, -26f), rect, 0.25f));
        }

        [Test]
        public void IsLocalPointInsideExpandedRect_CornerRespectsBothAxes()
        {
            var rect = new Rect(0f, 0f, 100f, 100f);

            Assert.IsTrue(UIPressTargetResolver.IsLocalPointInsideExpandedRect(new Vector2(120f, 120f), rect, 0.25f));
            Assert.IsFalse(UIPressTargetResolver.IsLocalPointInsideExpandedRect(new Vector2(120f, 130f), rect, 0.25f));
        }
    }
}
