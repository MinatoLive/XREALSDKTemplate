namespace SurgicalLivePlatform
{
    /// <summary>
    /// Pure rules for the UI target-lock mode. Only normal hand-ray buttons are lockable;
    /// draggable UI and ScrollRect content keep the native drag path.
    /// </summary>
    public static class UITargetLockRules
    {
        public static bool ShouldLock(
            bool isHandRay,
            bool hasButton,
            bool buttonActive,
            bool buttonInteractable,
            bool insideScrollRect,
            bool isDraggable,
            bool excludeScrollRect)
        {
            if (!isHandRay || !hasButton || !buttonActive || !buttonInteractable || isDraggable)
            {
                return false;
            }

            return !(excludeScrollRect && insideScrollRect);
        }
    }
}
