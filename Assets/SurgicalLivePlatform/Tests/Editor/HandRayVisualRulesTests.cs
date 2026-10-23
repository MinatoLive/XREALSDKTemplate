using NUnit.Framework;
using UnityEngine;
using SurgicalLivePlatform;

namespace SurgicalLivePlatform.Tests
{
    [TestFixture]
    public class HandRayVisualRulesTests
    {
        private const float ScalePerMeter = 0.0025f;
        private const float MinScale = 0.008f;
        private const float MaxScale = 0.03f;

        [Test]
        public void ComputePinchVisualAmount_BelowStart_ReturnsZero()
        {
            float amount = HandRayVisualRules.ComputePinchVisualAmount(0.04f, 0.05f, 0.95f);

            Assert.AreEqual(0f, amount, 0.0001f);
        }

        [Test]
        public void ComputePinchVisualAmount_AboveFull_ReturnsOne()
        {
            float amount = HandRayVisualRules.ComputePinchVisualAmount(0.96f, 0.05f, 0.95f);

            Assert.AreEqual(1f, amount, 0.0001f);
        }

        [Test]
        public void ComputePinchVisualAmount_MidRange_ReturnsLinearNormalizedValue()
        {
            float amount = HandRayVisualRules.ComputePinchVisualAmount(0.5f, 0.2f, 0.8f);

            Assert.AreEqual(0.5f, amount, 0.0001f);
        }

        [Test]
        public void ComputePinchVisualAmount_OutOfRangeInput_ClampsToZeroOrOne()
        {
            Assert.AreEqual(0f, HandRayVisualRules.ComputePinchVisualAmount(-1f, 0.05f, 0.95f), 0.0001f);
            Assert.AreEqual(1f, HandRayVisualRules.ComputePinchVisualAmount(2f, 0.05f, 0.95f), 0.0001f);
        }

        [Test]
        public void ComputePinchVisualAmount_InvertedThresholds_UsesStep()
        {
            Assert.AreEqual(0f, HandRayVisualRules.ComputePinchVisualAmount(0.4f, 0.9f, 0.2f), 0.0001f);
            Assert.AreEqual(1f, HandRayVisualRules.ComputePinchVisualAmount(0.9f, 0.9f, 0.2f), 0.0001f);
        }

        [Test]
        public void ComputeMarkerScaleMultiplier_Open_ReturnsOpenMultiplier()
        {
            float multiplier = HandRayVisualRules.ComputeMarkerScaleMultiplier(0f, 1.4f, 0.5f);

            Assert.AreEqual(1.4f, multiplier, 0.0001f);
        }

        [Test]
        public void ComputeMarkerScaleMultiplier_Pinch_ReturnsPinchMultiplier()
        {
            float multiplier = HandRayVisualRules.ComputeMarkerScaleMultiplier(1f, 1.4f, 0.5f);

            Assert.AreEqual(0.5f, multiplier, 0.0001f);
        }

        [Test]
        public void ComputeMarkerScaleMultiplier_Mid_ReturnsLerp()
        {
            float multiplier = HandRayVisualRules.ComputeMarkerScaleMultiplier(0.5f, 1.4f, 0.5f);

            Assert.AreEqual(0.95f, multiplier, 0.0001f);
        }

        [Test]
        public void ComputeMarkerHoleRadius_Open_ReturnsOpenRadius()
        {
            float radius = HandRayVisualRules.ComputeMarkerHoleRadius(0f, 0.31f);

            Assert.AreEqual(0.31f, radius, 0.0001f);
        }

        [Test]
        public void ComputeMarkerHoleRadius_Pinch_ReturnsZero()
        {
            float radius = HandRayVisualRules.ComputeMarkerHoleRadius(1f, 0.31f);

            Assert.AreEqual(0f, radius, 0.0001f);
        }

        [Test]
        public void ComputeMarkerHoleRadius_Mid_ReturnsHalfOpenRadius()
        {
            float radius = HandRayVisualRules.ComputeMarkerHoleRadius(0.5f, 0.31f);

            Assert.AreEqual(0.155f, radius, 0.0001f);
        }

        [Test]
        public void ShouldShowHitMarker_Inactive_ReturnsFalse()
        {
            bool show = HandRayVisualRules.ShouldShowHitMarker(
                RayHitKind.ValidHit,
                isCurveActive: false,
                showWhenNoHit: true);

            Assert.IsFalse(show);
        }

        [Test]
        public void ShouldShowHitMarker_NoneWithoutOption_ReturnsFalse()
        {
            bool show = HandRayVisualRules.ShouldShowHitMarker(
                RayHitKind.None,
                isCurveActive: true,
                showWhenNoHit: false);

            Assert.IsFalse(show);
        }

        [Test]
        public void ShouldShowHitMarker_NoneWithOption_ReturnsTrue()
        {
            bool show = HandRayVisualRules.ShouldShowHitMarker(
                RayHitKind.None,
                isCurveActive: true,
                showWhenNoHit: true);

            Assert.IsTrue(show);
        }

        [Test]
        public void ShouldShowHitMarker_EmptyHit_ReturnsTrue()
        {
            bool show = HandRayVisualRules.ShouldShowHitMarker(
                RayHitKind.EmptyHit,
                isCurveActive: true,
                showWhenNoHit: false);

            Assert.IsTrue(show);
        }

        [Test]
        public void ShouldShowHitMarker_UiHit_ReturnsTrue()
        {
            bool show = HandRayVisualRules.ShouldShowHitMarker(
                RayHitKind.Ui,
                isCurveActive: true,
                showWhenNoHit: false);

            Assert.IsTrue(show);
        }

        [Test]
        public void TryResolveHitMarkerPosition_UiWithHit_ReturnsUiPosition()
        {
            Vector3 uiPosition = new Vector3(1f, 2f, 3f);
            Vector3 curveEndPoint = new Vector3(4f, 5f, 6f);

            bool resolved = HandRayVisualRules.TryResolveHitMarkerPosition(
                RayHitKind.Ui,
                hasUiHit: true,
                uiWorldPosition: uiPosition,
                curveEndPoint: curveEndPoint,
                lastSamplePoint: Vector3.zero,
                showWhenNoHit: false,
                out Vector3 position);

            Assert.IsTrue(resolved);
            Assert.AreEqual(uiPosition, position);
        }

        [Test]
        public void TryResolveHitMarkerPosition_NoneWithoutOption_ReturnsFalse()
        {
            bool resolved = HandRayVisualRules.TryResolveHitMarkerPosition(
                RayHitKind.None,
                hasUiHit: false,
                uiWorldPosition: Vector3.zero,
                curveEndPoint: new Vector3(1f, 0f, 0f),
                lastSamplePoint: new Vector3(2f, 0f, 0f),
                showWhenNoHit: false,
                out Vector3 position);

            Assert.IsFalse(resolved);
            Assert.AreEqual(Vector3.zero, position);
        }

        [Test]
        public void TryResolveHitMarkerPosition_NoneWithOption_ReturnsLastSamplePoint()
        {
            Vector3 lastSamplePoint = new Vector3(9f, 8f, 7f);

            bool resolved = HandRayVisualRules.TryResolveHitMarkerPosition(
                RayHitKind.None,
                hasUiHit: false,
                uiWorldPosition: Vector3.zero,
                curveEndPoint: new Vector3(1f, 0f, 0f),
                lastSamplePoint: lastSamplePoint,
                showWhenNoHit: true,
                out Vector3 position);

            Assert.IsTrue(resolved);
            Assert.AreEqual(lastSamplePoint, position);
        }

        [Test]
        public void ComputeHitMarkerScale_NearDistance_ClampsToMin()
        {
            float scale = HandRayVisualRules.ComputeHitMarkerScale(
                0.5f,
                ScalePerMeter,
                MinScale,
                MaxScale);

            Assert.AreEqual(MinScale, scale, 0.0001f);
        }

        [Test]
        public void ComputeHitMarkerScale_FarDistance_ClampsToMax()
        {
            float scale = HandRayVisualRules.ComputeHitMarkerScale(
                20f,
                ScalePerMeter,
                MinScale,
                MaxScale);

            Assert.AreEqual(MaxScale, scale, 0.0001f);
        }

        [Test]
        public void ComputeHitMarkerScale_MidDistance_ReturnsDistanceTimesScalePerMeter()
        {
            float scale = HandRayVisualRules.ComputeHitMarkerScale(
                4f,
                ScalePerMeter,
                MinScale,
                MaxScale);

            Assert.AreEqual(0.01f, scale, 0.0001f);
        }

        [Test]
        public void ComputeLineSolidAmount_Open_ReturnsOpenSolidAmount()
        {
            float solid = HandRayVisualRules.ComputeLineSolidAmount(0f, 0.5f);

            Assert.AreEqual(0.5f, solid, 0.0001f);
        }

        [Test]
        public void ComputeLineSolidAmount_MidPinch_ReturnsLerp()
        {
            float solid = HandRayVisualRules.ComputeLineSolidAmount(0.5f, 0.5f);

            Assert.AreEqual(0.75f, solid, 0.0001f);
        }

        [Test]
        public void ComputeLineSolidAmount_FullPinch_ReturnsOne()
        {
            float solid = HandRayVisualRules.ComputeLineSolidAmount(1f, 0.5f);

            Assert.AreEqual(1f, solid, 0.0001f);
        }

        [Test]
        public void ComputeLineSolidAmount_OpenSolidAboveRange_ClampsToOne()
        {
            float solid = HandRayVisualRules.ComputeLineSolidAmount(0f, 1.5f);

            Assert.AreEqual(1f, solid, 0.0001f);
        }

        [Test]
        public void ComputeMarkerVisualAmount_Pressed_ReturnsOne()
        {
            float amount = HandRayVisualRules.ComputeMarkerVisualAmount(0f, true);

            Assert.AreEqual(1f, amount, 0.0001f);
        }

        [Test]
        public void ComputeMarkerVisualAmount_NotPressed_ReturnsVisualAmount()
        {
            float amount = HandRayVisualRules.ComputeMarkerVisualAmount(0.35f, false);

            Assert.AreEqual(0.35f, amount, 0.0001f);
        }

        [Test]
        public void ComputeMarkerColor_Idle_ReturnsIdleColorRegardlessOfAmount()
        {
            Color result = HandRayVisualRules.ComputeMarkerColor(false, Color.white, Color.green);

            Assert.AreEqual(Color.white, result);
        }

        [Test]
        public void ComputeMarkerColor_Pressed_ReturnsPressedColor()
        {
            Color result = HandRayVisualRules.ComputeMarkerColor(true, Color.white, Color.green);

            Assert.AreEqual(Color.green, result);
        }

        [Test]
        public void ShouldBeginMarkerHold_BelowDwell_ReturnsFalse()
        {
            bool hold = HandRayVisualRules.ShouldBeginMarkerHold(true, 0.1f, 0.15f);

            Assert.IsFalse(hold);
        }

        [Test]
        public void ShouldBeginMarkerHold_AtDwell_ReturnsTrue()
        {
            bool hold = HandRayVisualRules.ShouldBeginMarkerHold(true, 0.15f, 0.15f);

            Assert.IsTrue(hold);
        }

        [Test]
        public void ShouldBeginMarkerHold_NoCandidate_ReturnsFalse()
        {
            bool hold = HandRayVisualRules.ShouldBeginMarkerHold(false, 10f, 0.15f);

            Assert.IsFalse(hold);
        }

        [Test]
        public void ShouldBeginMarkerHold_ZeroDwell_ReturnsTrue()
        {
            bool hold = HandRayVisualRules.ShouldBeginMarkerHold(true, 0f, 0f);

            Assert.IsTrue(hold);
        }

        [Test]
        public void ShouldReleaseMarkerHold_InsideExpandedRect_ReturnsFalse()
        {
            bool release = HandRayVisualRules.ShouldReleaseMarkerHold(true, false, true);

            Assert.IsFalse(release);
        }

        [Test]
        public void ShouldReleaseMarkerHold_OutsideExpandedRect_ReturnsTrue()
        {
            bool release = HandRayVisualRules.ShouldReleaseMarkerHold(true, false, false);

            Assert.IsTrue(release);
        }

        [Test]
        public void ShouldReleaseMarkerHold_TargetInvalid_ReturnsTrue()
        {
            bool release = HandRayVisualRules.ShouldReleaseMarkerHold(false, false, true);

            Assert.IsTrue(release);
        }

        [Test]
        public void ShouldReleaseMarkerHold_HoveringOtherButton_ReturnsTrue()
        {
            bool release = HandRayVisualRules.ShouldReleaseMarkerHold(true, true, true);

            Assert.IsTrue(release);
        }
    }
}
