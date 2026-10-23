using UnityEngine;

namespace SurgicalLivePlatform
{
    /// <summary>
    /// 项目自有的射线端点分类，避免测试程序集直接依赖 XRI 类型。
    /// </summary>
    public enum RayHitKind
    {
        None,
        EmptyHit,
        ValidHit,
        Ui,
        AttachPoint
    }

    /// <summary>
    /// 双手射线视觉规则：捏合映射、线体实心量与 RayHit 显隐/落点/缩放/两态颜色的纯函数。
    /// </summary>
    public static class HandRayVisualRules
    {
        public static float ComputePinchVisualAmount(
            float rawPinch,
            float startThreshold,
            float fullThreshold)
        {
            startThreshold = Mathf.Clamp01(startThreshold);
            fullThreshold = Mathf.Clamp01(fullThreshold);

            if (fullThreshold <= startThreshold)
            {
                return rawPinch >= startThreshold ? 1f : 0f;
            }

            return Mathf.Clamp01(Mathf.InverseLerp(startThreshold, fullThreshold, rawPinch));
        }

        /// <summary>
        /// 将捏合视觉量映射为线体实心量：完全张开时为 openSolidAmount，完全捏合时为 1。
        /// </summary>
        public static float ComputeLineSolidAmount(float visualAmount, float openSolidAmount)
        {
            return Mathf.Lerp(Mathf.Clamp01(openSolidAmount), 1f, Mathf.Clamp01(visualAmount));
        }

        /// <summary>
        /// 投射点形状驱动量：原生按下时立即取 1（小实心圆），否则沿用捏合视觉量。
        /// </summary>
        public static float ComputeMarkerVisualAmount(float visualAmount, bool pressed)
        {
            return pressed ? 1f : Mathf.Clamp01(visualAmount);
        }

        public static float ComputeMarkerScaleMultiplier(
            float visualAmount,
            float openMultiplier,
            float pinchMultiplier)
        {
            return Mathf.Lerp(openMultiplier, pinchMultiplier, Mathf.Clamp01(visualAmount));
        }

        public static float ComputeMarkerHoleRadius(float visualAmount, float openHoleRadius)
        {
            return Mathf.Lerp(Mathf.Max(0f, openHoleRadius), 0f, Mathf.Clamp01(visualAmount));
        }

        /// <summary>
        /// 投射点颜色：按下为绿色，未按下为白色，不随捏合量插值。
        /// </summary>
        public static Color ComputeMarkerColor(bool pressed, Color idleColor, Color pressedColor)
        {
            return pressed ? pressedColor : idleColor;
        }

        public static bool ShouldShowHitMarker(
            RayHitKind hitKind,
            bool isCurveActive,
            bool showWhenNoHit)
        {
            if (!isCurveActive)
            {
                return false;
            }

            return hitKind != RayHitKind.None || showWhenNoHit;
        }

        public static bool TryResolveHitMarkerPosition(
            RayHitKind hitKind,
            bool hasUiHit,
            Vector3 uiWorldPosition,
            Vector3 curveEndPoint,
            Vector3 lastSamplePoint,
            bool showWhenNoHit,
            out Vector3 position)
        {
            if (hitKind == RayHitKind.Ui && hasUiHit)
            {
                position = uiWorldPosition;
                return true;
            }

            if (hitKind == RayHitKind.None)
            {
                if (showWhenNoHit)
                {
                    position = lastSamplePoint;
                    return true;
                }

                position = Vector3.zero;
                return false;
            }

            position = curveEndPoint;
            return true;
        }

        public static float ComputeHitMarkerScale(
            float distanceToCamera,
            float scalePerMeter,
            float minScale,
            float maxScale)
        {
            if (distanceToCamera <= 0f)
            {
                return minScale;
            }

            return Mathf.Clamp(distanceToCamera * scalePerMeter, minScale, maxScale);
        }

        /// <summary>
        /// 是否进入悬停锁定：必须在可锁定按钮上停留足够时长，避免扫过误锁。
        /// </summary>
        public static bool ShouldBeginMarkerHold(
            bool isHoveringEligibleButton,
            float hoverSeconds,
            float dwellSeconds)
        {
            if (!isHoveringEligibleButton)
            {
                return false;
            }

            return hoverSeconds >= Mathf.Max(0f, dwellSeconds);
        }

        /// <summary>
        /// 是否释放悬停锁定：按钮失效、悬停到其他按钮，或射线超出按钮扩展区。
        /// </summary>
        public static bool ShouldReleaseMarkerHold(
            bool targetValid,
            bool hoveringOtherButton,
            bool rayInsideExpandedRect)
        {
            return !targetValid || hoveringOtherButton || !rayInsideExpandedRect;
        }
    }
}
