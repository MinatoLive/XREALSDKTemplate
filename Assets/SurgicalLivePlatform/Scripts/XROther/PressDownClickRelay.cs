using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace SurgicalLivePlatform
{
    /// <summary>
    /// Allows hand-ray UI buttons to fire Button.onClick on pointer down instead of release.
    /// It consumes the native UI press event; it does not modify pinch detection or pointer poses.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PressDownClickRelay : MonoBehaviour
    {
        private const int EdgePointerId = -1000;

        [SerializeField] private XRUIInputModule inputModule;
        [SerializeField] private UITargetLockController targetLock;
        [SerializeField] private bool excludeScrollRect = true;
        [SerializeField] private bool logHandledPresses = false;

        private struct PendingPress
        {
            public int pointerId;
            public Button button;
        }

        private readonly List<PendingPress> _pending = new List<PendingPress>(4);

        private void OnEnable()
        {
            if (inputModule == null)
            {
                inputModule = GetComponent<XRUIInputModule>();
            }

            if (inputModule == null && EventSystem.current != null)
            {
                inputModule = EventSystem.current.GetComponent<XRUIInputModule>();
            }

            if (targetLock == null)
            {
                targetLock = GetComponent<UITargetLockController>();
            }

            if (targetLock == null && EventSystem.current != null)
            {
                targetLock = EventSystem.current.GetComponent<UITargetLockController>();
            }

            if (inputModule != null)
            {
                inputModule.pointerDown += OnPointerDown;
                inputModule.pointerUp += OnPointerUp;
            }
            else
            {
                Debug.LogWarning("[PressDownClickRelay] No XRUIInputModule found; relay disabled.", this);
            }

            if (targetLock == null)
            {
                Debug.LogWarning("[PressDownClickRelay] No UITargetLockController found; target lock is disabled.", this);
            }
        }

        private void OnDisable()
        {
            if (inputModule != null)
            {
                inputModule.pointerDown -= OnPointerDown;
                inputModule.pointerUp -= OnPointerUp;
            }

            if (targetLock != null)
            {
                for (var i = 0; i < _pending.Count; i++)
                {
                    targetLock.Unlock(_pending[i].pointerId);
                }
            }

            _pending.Clear();
        }

        private void OnPointerDown(GameObject hoverTarget, PointerEventData eventData)
        {
            if (!(eventData is TrackedDeviceEventData tracked) ||
                !(tracked.interactor is NearFarInteractor))
            {
                return;
            }

            var button = UIPressTargetResolver.FindButton(hoverTarget);
            var hasButton = button != null;
            var buttonActive = hasButton && button.IsActive();
            var buttonInteractable = hasButton && button.IsInteractable();
            var insideScrollRect = hasButton && UIPressTargetResolver.IsInsideScrollRect(button);
            var isDraggable = hasButton && UIPressTargetResolver.IsDraggable(button);
            var shouldHandle = PressDownClickRules.ShouldHandle(
                true,
                hasButton,
                buttonActive,
                buttonInteractable,
                insideScrollRect,
                isDraggable,
                excludeScrollRect);
            var shouldTargetLock = UITargetLockRules.ShouldLock(
                true,
                hasButton,
                buttonActive,
                buttonInteractable,
                insideScrollRect,
                isDraggable,
                excludeScrollRect);

            if (!shouldHandle ||
                HasPending(eventData.pointerId) ||
                HasPendingButton(button))
            {
                return;
            }

            // Fire onClick on pointer down, then suppress the standard release click.
            eventData.eligibleForClick = false;
            if (shouldTargetLock && targetLock != null)
            {
                targetLock.Lock(eventData.pointerId, button);
            }

            _pending.Add(new PendingPress { pointerId = eventData.pointerId, button = button });

            if (logHandledPresses)
            {
                Debug.Log("[PressDownClickRelay] handled press on " + button.name + " pointer=" + eventData.pointerId, this);
            }
        }

        private void OnPointerUp(GameObject target, PointerEventData eventData)
        {
            if (targetLock != null)
            {
                targetLock.Unlock(eventData.pointerId);
            }
        }

        private void LateUpdate()
        {
            for (var i = 0; i < _pending.Count; i++)
            {
                var pending = _pending[i];
                var button = pending.button;
                if (button != null && button.IsActive() && button.IsInteractable())
                {
                    button.onClick.Invoke();
                }
            }

            _pending.Clear();
        }

        /// <summary>
        /// Queues a click for a button that was slightly outside the ray's UI hit
        /// but is still inside the configured edge tolerance. The normal pointer
        /// down path de-duplicates by button in the same frame.
        /// </summary>
        public bool TryHandleEdgePress(Button button)
        {
            if (button == null || !button.IsActive() || !button.IsInteractable())
            {
                return false;
            }

            if (UIPressTargetResolver.IsDraggable(button))
            {
                return false;
            }

            if (excludeScrollRect && UIPressTargetResolver.IsInsideScrollRect(button))
            {
                return false;
            }

            if (HasPendingButton(button))
            {
                return false;
            }

            _pending.Add(new PendingPress { pointerId = EdgePointerId, button = button });

            if (logHandledPresses)
            {
                Debug.Log("[PressDownClickRelay] handled edge press on " + button.name, this);
            }

            return true;
        }

        private bool HasPending(int pointerId)
        {
            for (var i = 0; i < _pending.Count; i++)
            {
                if (_pending[i].pointerId == pointerId)
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasPendingButton(Button button)
        {
            if (button == null)
            {
                return false;
            }

            for (var i = 0; i < _pending.Count; i++)
            {
                if (_pending[i].button == button)
                {
                    return true;
                }
            }

            return false;
        }

    }
}
