namespace SurgicalLivePlatform
{
    /// <summary>
    /// Pure rules for the pinch Aim Pose lock. The lock is driven by the raw pinch analog
    /// value and is only allowed for a normal UI Button.
    /// </summary>
    public static class PinchAimLockRules
    {
        public static bool ShouldEnterLock(float value, float minValue)
        {
            return value > minValue;
        }

        public static bool ShouldUnlock(float value, float minValue)
        {
            return value <= minValue;
        }

        public static bool ShouldClick(float value, float maxValue, bool alreadyClicked)
        {
            return !alreadyClicked && value > maxValue;
        }

        public static bool ShouldLock(
            float value,
            float minValue,
            bool hasButton,
            bool buttonActive,
            bool buttonInteractable,
            bool insideScrollRect,
            bool isDraggable,
            bool hasSelection,
            bool excludeScrollRect)
        {
            if (!ShouldEnterLock(value, minValue) ||
                !hasButton ||
                !buttonActive ||
                !buttonInteractable ||
                isDraggable ||
                hasSelection)
            {
                return false;
            }

            return !(excludeScrollRect && insideScrollRect);
        }

        public static bool CanUseEdgeTarget(
            bool hasLastHoveredButton,
            float secondsSinceLastHover,
            float lastHoverMemorySeconds,
            bool withinTolerance,
            bool targetEligible)
        {
            return hasLastHoveredButton &&
                   withinTolerance &&
                   targetEligible &&
                   secondsSinceLastHover >= 0f &&
                   secondsSinceLastHover <= lastHoverMemorySeconds;
        }
    }
}
