using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace SurgicalLivePlatform
{
    /// <summary>
    /// Freezes the cast pose for a normal UI Button while the raw pinch value is inside the
    /// configured band, then clicks the locked Button once when the value crosses the upper
    /// bound. Native Button clicks are suppressed so the analog threshold is the only click
    /// source. A recently hovered Button may also be locked from just outside its bounds
    /// within a configurable edge tolerance.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public sealed class PinchAimPoseLock : MonoBehaviour
    {
        [SerializeField] private NearFarInteractor interactor;
        [SerializeField] private Transform aimPose;
        [SerializeField] private bool excludeScrollRect = true;
        [SerializeField] private bool logStateChanges = false;

        [Header("Analog Thresholds")]
        [Tooltip("Raw pinch value above which the cast pose locks.")]
        [SerializeField, Range(0f, 1f)] private float minValue = 0.9f;
        [Tooltip("Raw pinch value above which the locked Button is clicked once per lock.")]
        [SerializeField, Range(0f, 1f)] private float maxValue = 0.98f;

        [Header("Edge Target")]
        [SerializeField, Range(0f, 1f)] private float edgeTolerancePercent = 0.25f;
        [SerializeField, Min(0f)] private float lastHoverMemorySeconds = 0.3f;

        private GameObject _hoveredUI;
        private Button _lastHoveredButton;
        private float _lastHoverTime = float.NegativeInfinity;

        private bool _locked;
        private Button _lockedButton;
        private Vector3 _lockedPosition;
        private Quaternion _lockedRotation;
        private Vector3 _lockFeedbackPosition;
        private Vector3 _lockFeedbackNormal;
        private bool _hasLockFeedback;

        private InteractionCasterBase _caster;
        private XRUIInputModule _uiInputModule;
        private bool _clickedThisCycle;
        private bool _stabilizationCached;
        private float _savedPositionStabilization;
        private float _savedAngleStabilization;

        private bool _hasEdgeCandidateFeedback;
        private Vector3 _edgeCandidatePosition;
        private Vector3 _edgeCandidateNormal;

        /// <summary>Whether the cast pose is currently frozen.</summary>
        public bool isLocked => _locked;

        /// <summary>Raw pinch value above which the cast pose locks.</summary>
        public float MinValue => minValue;

        /// <summary>Raw pinch value above which the locked Button is clicked once.</summary>
        public float MaxValue => maxValue;

        private void Awake()
        {
            if (aimPose == null)
            {
                aimPose = transform;
            }
        }

        private void OnEnable()
        {
            ResolveInteractor();
            ResolveCaster();
            ResolveInputModule();
            if (interactor != null)
            {
                interactor.uiHoverEntered.AddListener(OnUIHoverEntered);
                interactor.uiHoverExited.AddListener(OnUIHoverExited);
            }

            if (_uiInputModule != null)
            {
                _uiInputModule.pointerDown += OnPointerDown;
            }
        }

        private void OnDisable()
        {
            if (interactor != null)
            {
                interactor.uiHoverEntered.RemoveListener(OnUIHoverEntered);
                interactor.uiHoverExited.RemoveListener(OnUIHoverExited);
            }

            if (_uiInputModule != null)
            {
                _uiInputModule.pointerDown -= OnPointerDown;
            }

            if (_locked)
            {
                Unlock("disable");
            }

            _hoveredUI = null;
            _lastHoveredButton = null;
            _hasLockFeedback = false;
        }

        private void Update()
        {
            EnsureInputModule();

            if (interactor == null || aimPose == null)
            {
                return;
            }

            var value = ReadPinchValue();

            if (value <= minValue)
            {
                _clickedThisCycle = false;
            }

            if (!_locked)
            {
                if (value > maxValue)
                {
                    // Fast path: a pinch that skips the lock band can still click,
                    // then freezes the cast pose at the clicked target.
                    if (!_clickedThisCycle &&
                        TryResolvePressTarget(value, out var fastButton, out var fastEdge))
                    {
                        Lock(fastButton, fastEdge);
                        _clickedThisCycle = true;
                        if (IsClickTargetValid())
                        {
                            ClickLockedButton();
                        }
                    }

                    return;
                }

                if (PinchAimLockRules.ShouldEnterLock(value, minValue) &&
                    TryResolvePressTarget(value, out var button, out var isEdge))
                {
                    Lock(button, isEdge);
                }

                return;
            }

            if (PinchAimLockRules.ShouldUnlock(value, minValue))
            {
                Unlock("analog-min");
                return;
            }

            if (PinchAimLockRules.ShouldClick(value, maxValue, _clickedThisCycle))
            {
                _clickedThisCycle = true;
                if (IsClickTargetValid())
                {
                    ClickLockedButton();
                }
            }

            ApplyLockedPose();
        }

        [BeforeRenderOrder(90)]
        private void OnBeforeRender()
        {
            if (_locked)
            {
                ApplyLockedPose();
            }
        }

        /// <summary>
        /// Returns the currently hovered Button, or a recently hovered Button whose expanded
        /// rect still contains the ray. The second case is an edge target and can only be
        /// clicked through onClick because the UI ray is outside the Button.
        /// </summary>
        public bool TryGetLockFeedback(out Vector3 position, out Vector3 normal)
        {
            if (_locked && _hasLockFeedback)
            {
                position = _lockFeedbackPosition;
                normal = _lockFeedbackNormal;
                return true;
            }

            position = default;
            normal = default;
            return false;
        }

        private bool TryResolvePressTarget(float value, out Button button, out bool isEdge)
        {
            button = null;
            isEdge = false;
            _hasEdgeCandidateFeedback = false;

            var currentButton = UIPressTargetResolver.FindButton(_hoveredUI);
            if (IsCandidateEligible(currentButton, value))
            {
                button = currentButton;
                return true;
            }

            if (_lastHoveredButton == null)
            {
                return false;
            }

            var withinTolerance = TryGetEdgeFeedback(
                _lastHoveredButton,
                out var edgePoint,
                out var edgeNormal);
            var eligible = IsCandidateEligible(_lastHoveredButton, value);
            var canUse = PinchAimLockRules.CanUseEdgeTarget(
                true,
                Time.unscaledTime - _lastHoverTime,
                lastHoverMemorySeconds,
                withinTolerance,
                eligible);

            if (!canUse)
            {
                return false;
            }

            button = _lastHoveredButton;
            isEdge = true;
            _hasEdgeCandidateFeedback = true;
            _edgeCandidatePosition = edgePoint;
            _edgeCandidateNormal = edgeNormal;
            return true;
        }

        private bool IsCandidateEligible(Button button, float value)
        {
            if (button == null || interactor == null)
            {
                return false;
            }

            return PinchAimLockRules.ShouldLock(
                value,
                minValue,
                true,
                button.IsActive(),
                button.IsInteractable(),
                UIPressTargetResolver.IsInsideScrollRect(button),
                UIPressTargetResolver.IsDraggable(button),
                interactor.hasSelection,
                excludeScrollRect);
        }

        private bool TryGetEdgeFeedback(Button button, out Vector3 point, out Vector3 normal)
        {
            point = default;
            normal = default;

            var rect = button != null ? button.transform as RectTransform : null;
            if (rect == null || !TryGetRaySource(out var origin, out var direction))
            {
                return false;
            }

            if (!UIPressTargetResolver.TryGetRayRectPoint(
                    origin,
                    direction,
                    rect,
                    out var worldPoint,
                    out var worldNormal))
            {
                return false;
            }

            if (!UIPressTargetResolver.IsWorldPointInsideExpandedRect(
                    rect,
                    worldPoint,
                    edgeTolerancePercent))
            {
                return false;
            }

            point = worldPoint;
            normal = worldNormal;
            return true;
        }

        private bool TryGetRaySource(out Vector3 origin, out Vector3 direction)
        {
            if (_caster != null && _caster.effectiveCastOrigin != null)
            {
                var castOrigin = _caster.effectiveCastOrigin;
                origin = castOrigin.position;
                direction = castOrigin.forward;
                return true;
            }

            if (aimPose != null)
            {
                origin = aimPose.position;
                direction = aimPose.forward;
                return true;
            }

            origin = default;
            direction = default;
            return false;
        }

        private void Lock(Button button, bool isEdge)
        {
            ResolveCaster();
            _locked = true;
            _lockedButton = button;

            var source = _caster != null ? _caster.effectiveCastOrigin : aimPose;
            _lockedPosition = source.position;
            _lockedRotation = source.rotation;

            if (_caster != null)
            {
                _savedPositionStabilization = _caster.positionStabilization;
                _savedAngleStabilization = _caster.angleStabilization;
                _stabilizationCached = true;
                _caster.positionStabilization = 0f;
                _caster.angleStabilization = 0f;
            }

            ApplyLockedPose();
            ResolveLockFeedback(button, isEdge);
            _clickedThisCycle = false;

            if (logStateChanges)
            {
                Debug.Log(
                    "[PinchAimPoseLock] locked on " +
                    (button != null ? button.name : "unknown") +
                    (isEdge ? " (edge)" : string.Empty) +
                    ", min=" + minValue.ToString("F3") +
                    ", max=" + maxValue.ToString("F3"),
                    this);
            }
        }

        private void Unlock(string reason)
        {
            _locked = false;

            if (_stabilizationCached && _caster != null)
            {
                _caster.positionStabilization = _savedPositionStabilization;
                _caster.angleStabilization = _savedAngleStabilization;
            }

            _stabilizationCached = false;
            _clickedThisCycle = false;
            _lockedButton = null;
            _hasLockFeedback = false;

            if (logStateChanges)
            {
                Debug.Log("[PinchAimPoseLock] unlocked (" + reason + ")", this);
            }
        }

        private void ApplyLockedPose()
        {
            if (aimPose == null)
            {
                return;
            }

            aimPose.SetPositionAndRotation(_lockedPosition, _lockedRotation);
        }

        private void ResolveLockFeedback(Button button, bool isEdge)
        {
            _hasLockFeedback = false;

            if (button == null)
            {
                return;
            }

            if (isEdge && _hasEdgeCandidateFeedback)
            {
                _lockFeedbackPosition = _edgeCandidatePosition;
                _lockFeedbackNormal = _edgeCandidateNormal;
                _hasLockFeedback = true;
                return;
            }

            var rect = button.transform as RectTransform;
            if (rect == null)
            {
                return;
            }

            if (interactor != null &&
                interactor.TryGetCurrentUIRaycastResult(out var uiResult) &&
                UIPressTargetResolver.FindButton(uiResult.gameObject) == button)
            {
                _lockFeedbackPosition = uiResult.worldPosition;
                _lockFeedbackNormal = uiResult.worldNormal.sqrMagnitude > 0.0001f
                    ? uiResult.worldNormal.normalized
                    : rect.forward;
                _hasLockFeedback = true;
                return;
            }

            if (TryGetRaySource(out var origin, out var direction) &&
                UIPressTargetResolver.TryGetRayRectPoint(
                    origin,
                    direction,
                    rect,
                    out var point,
                    out var normal))
            {
                _lockFeedbackPosition = point;
                _lockFeedbackNormal = normal;
                _hasLockFeedback = true;
                return;
            }

            _lockFeedbackPosition = rect.TransformPoint(rect.rect.center);
            _lockFeedbackNormal = rect.forward;
            _hasLockFeedback = true;
        }

        private void OnUIHoverEntered(UIHoverEventArgs args)
        {
            _hoveredUI = args != null ? args.uiObject : null;
            var button = UIPressTargetResolver.FindButton(_hoveredUI);
            if (button != null)
            {
                _lastHoveredButton = button;
                _lastHoverTime = Time.unscaledTime;
            }
        }

        private void OnUIHoverExited(UIHoverEventArgs args)
        {
            if (args != null && _hoveredUI == args.uiObject)
            {
                _hoveredUI = null;
            }
        }

        private void ResolveInteractor()
        {
            if (interactor != null || aimPose == null || aimPose.parent == null)
            {
                return;
            }

            interactor = aimPose.parent.GetComponentInChildren<NearFarInteractor>(true);
        }

        private void ResolveCaster()
        {
            if (_caster != null)
            {
                return;
            }

            ResolveInteractor();
            if (interactor == null)
            {
                return;
            }

            _caster = interactor.farInteractionCaster as InteractionCasterBase;
            if (_caster == null)
            {
                _caster = interactor.GetComponent<InteractionCasterBase>();
            }
        }

        private void EnsureInputModule()
        {
            if (_uiInputModule != null)
            {
                return;
            }

            ResolveInputModule();
            if (_uiInputModule != null)
            {
                _uiInputModule.pointerDown += OnPointerDown;
            }
        }

        private void ResolveInputModule()
        {
            if (_uiInputModule != null || EventSystem.current == null)
            {
                return;
            }

            _uiInputModule = EventSystem.current.GetComponent<XRUIInputModule>();
        }

        private float ReadPinchValue()
        {
            return interactor.selectInput != null ? interactor.selectInput.ReadValue() : 0f;
        }

        private bool IsClickTargetValid()
        {
            return _lockedButton != null &&
                   _lockedButton.IsActive() &&
                   _lockedButton.IsInteractable();
        }

        private void ClickLockedButton()
        {
            _lockedButton.onClick.Invoke();

            if (logStateChanges)
            {
                Debug.Log("[PinchAimPoseLock] clicked " + _lockedButton.name, this);
            }
        }

        /// <summary>
        /// Suppresses the native release click for Buttons that belong to the analog click path.
        /// Non-Button UI targets keep the native UI pointer semantics.
        /// </summary>
        private void OnPointerDown(GameObject hoverTarget, PointerEventData eventData)
        {
            if (!(eventData is TrackedDeviceEventData tracked) ||
                !ReferenceEquals(tracked.interactor, interactor))
            {
                return;
            }

            var button = UIPressTargetResolver.FindButton(hoverTarget);
            if (!PressDownClickRules.ShouldHandle(
                    true,
                    button != null,
                    button != null && button.IsActive(),
                    button != null && button.IsInteractable(),
                    button != null && UIPressTargetResolver.IsInsideScrollRect(button),
                    button != null && UIPressTargetResolver.IsDraggable(button),
                    excludeScrollRect))
            {
                return;
            }

            eventData.eligibleForClick = false;
        }
    }
}
