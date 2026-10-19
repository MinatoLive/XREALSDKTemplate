using NUnit.Framework;
using UnityEngine;
using SurgicalLivePlatform;

namespace SurgicalLivePlatform.Tests
{
    [TestFixture]
    public class HandFrustumMonitorTests
    {
        private const float DefaultThreshold = 0.1f;

        [Test]
        public void EvaluateViewportPosition_Center_ReturnsInside()
        {
            Vector3 center = new Vector3(0.5f, 0.5f, 1.0f);
            var state = HandFrustumMonitor.EvaluateViewportPosition(center, DefaultThreshold);
            Assert.AreEqual(HandFrustumMonitor.HandFrustumState.Inside, state);
        }

        [Test]
        public void EvaluateViewportPosition_NearEdge_ReturnsNearEdge()
        {
            Vector3 nearEdge = new Vector3(0.05f, 0.5f, 1.0f);
            var state = HandFrustumMonitor.EvaluateViewportPosition(nearEdge, DefaultThreshold);
            Assert.AreEqual(HandFrustumMonitor.HandFrustumState.NearEdge, state);
        }

        [Test]
        public void EvaluateViewportPosition_OutOfFrustum_ReturnsOutOfFrustum()
        {
            Vector3 outOfFrustum = new Vector3(1.2f, 0.5f, 1.0f);
            var state = HandFrustumMonitor.EvaluateViewportPosition(outOfFrustum, DefaultThreshold);
            Assert.AreEqual(HandFrustumMonitor.HandFrustumState.OutOfFrustum, state);
        }

        [Test]
        public void EvaluateViewportPosition_BehindCamera_ReturnsOutOfFrustum()
        {
            Vector3 behind = new Vector3(0.5f, 0.5f, -1.0f);
            var state = HandFrustumMonitor.EvaluateViewportPosition(behind, DefaultThreshold);
            Assert.AreEqual(HandFrustumMonitor.HandFrustumState.OutOfFrustum, state);
        }

        [Test]
        public void CalculateHandWarningColor_NotTracked_ReturnsNotTrackedColor()
        {
            Color notTracked = new Color(1f, 0.55f, 0f, 0.6f);
            Color nearEdge = new Color(1f, 0.55f, 0f, 1f);
            Color outOfFrustum = new Color(1f, 0f, 0f, 1f);
            Color inside = new Color(1f, 0.55f, 0f, 0f);

            Color result = HandFrustumMonitor.CalculateHandWarningColor(
                Vector3.zero,
                isTracked: false,
                DefaultThreshold,
                inside,
                nearEdge,
                outOfFrustum,
                notTracked
            );

            Assert.AreEqual(notTracked.r, result.r, 0.01f);
            Assert.AreEqual(notTracked.g, result.g, 0.01f);
            Assert.AreEqual(notTracked.b, result.b, 0.01f);
            Assert.AreEqual(notTracked.a, result.a, 0.01f);
        }

        [Test]
        public void CalculateHandWarningColor_BehindCamera_ReturnsOutOfFrustumColor()
        {
            Color notTracked = new Color(1f, 0.55f, 0f, 0.6f);
            Color nearEdge = new Color(1f, 0.55f, 0f, 1f);
            Color outOfFrustum = new Color(1f, 0f, 0f, 1f);
            Color inside = new Color(1f, 0.55f, 0f, 0f);

            Vector3 behind = new Vector3(0.5f, 0.5f, -0.5f);
            Color result = HandFrustumMonitor.CalculateHandWarningColor(
                behind,
                isTracked: true,
                DefaultThreshold,
                inside,
                nearEdge,
                outOfFrustum,
                notTracked
            );

            Assert.AreEqual(outOfFrustum, result);
        }

        [Test]
        public void CalculateHandWarningColor_SafeInside_ReturnsInsideTransparentColor()
        {
            Color notTracked = new Color(1f, 0.55f, 0f, 0.6f);
            Color nearEdge = new Color(1f, 0.55f, 0f, 1f);
            Color outOfFrustum = new Color(1f, 0f, 0f, 1f);
            Color inside = new Color(1f, 0.55f, 0f, 0f);

            Vector3 center = new Vector3(0.5f, 0.5f, 1f);
            Color result = HandFrustumMonitor.CalculateHandWarningColor(
                center,
                isTracked: true,
                DefaultThreshold,
                inside,
                nearEdge,
                outOfFrustum,
                notTracked
            );

            Assert.AreEqual(0f, result.a, 0.01f);
        }

        [Test]
        public void CalculateHandWarningColor_NearEdge_ReturnsInterpolatedAlpha()
        {
            Color notTracked = new Color(1f, 0.55f, 0f, 0.6f);
            Color nearEdge = new Color(1f, 0.55f, 0f, 1f);
            Color outOfFrustum = new Color(1f, 0f, 0f, 1f);
            Color inside = new Color(1f, 0.55f, 0f, 0f);

            // edgeDist = 0.05 (halfway in threshold 0.1) -> t = 0.5
            Vector3 nearEdgePos = new Vector3(0.05f, 0.5f, 1f);
            Color result = HandFrustumMonitor.CalculateHandWarningColor(
                nearEdgePos,
                isTracked: true,
                DefaultThreshold,
                inside,
                nearEdge,
                outOfFrustum,
                notTracked
            );

            // Should be halfway between inside (a=0) and nearEdge (a=1)
            Assert.AreEqual(0.5f, result.a, 0.05f);
            Assert.AreEqual(nearEdge.r, result.r, 0.05f);
        }
    }
}
