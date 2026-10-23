namespace SurgicalLivePlatform
{
    /// <summary>
    /// Pure decision rules for the press-down UI adapter.
    /// </summary>
    public static class PressDownClickRules
    {
        public static bool ShouldHandle(
            bool isHandRay,
            bool hasButton,
            bool buttonActive,
            bool buttonInteractable,
            bool insideScrollRect,
            bool isDraggable,
            bool excludeScrollRect)
        {
            if (!isHandRay ||
                !hasButton ||
                !buttonActive ||
                !buttonInteractable ||
                isDraggable)
            {
                return false;
            }

            return !(excludeScrollRect && insideScrollRect);
        }
    }
}
