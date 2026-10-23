using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;

namespace SurgicalLivePlatform
{
    /// <summary>
    /// 双手射线 RayHit 标记：未按下为白色圆环，原生按下瞬间变为绿色实心小圆；
    /// 捏合量仍会连续收缩外径与孔径。未命中且未开启 showWhenNoHit 时隐藏。
    /// </summary>
    public class HandRayHitIndicator : MonoBehaviour
    {
        private const int DiscSegments = 48;
        private static readonly int HoleRadiusPropertyId = Shader.PropertyToID("_HoleRadius");
        private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");

        [Header("References")]
        [Tooltip("驱动射线端点与按压状态的手部交互器。")]
        [SerializeField] private NearFarInteractor interactor;
        [Tooltip("承载标记网格的物体（MeshFilter + MeshRenderer）。")]
        [SerializeField] private Transform indicator;
        [Tooltip("用于距离自适应缩放的相机，留空时取 Camera.main。")]
        [SerializeField] private Camera viewCamera;
        [Tooltip("按下锁定组件；锁定时标记固定在锁定落点并保持绿色。")]
        [SerializeField] private PinchAimPoseLock aimLock;

        [Header("Marker Size")]
        [Tooltip("标记尺寸随距离增长的系数（世界单位/米）。")]
        [SerializeField] private float scalePerMeter = 0.0025f;
        [Tooltip("标记最小基础尺寸。")]
        [SerializeField] private float minScale = 0.008f;
        [Tooltip("标记最大基础尺寸。")]
        [SerializeField] private float maxScale = 0.03f;
        [Tooltip("标记沿表面法线向相机方向的偏移，避免与表面重叠。")]
        [SerializeField] private float surfaceOffset = 0.005f;

        [Header("Pinch Shape")]
        [Tooltip("完全张开时的外径倍率（大圆环）。")]
        [SerializeField, Min(0f)] private float openMarkerScaleMultiplier = 1.4f;
        [Tooltip("完全捏合时的外径倍率（小实心圆）。")]
        [SerializeField, Min(0f)] private float pinchMarkerScaleMultiplier = 0.5f;
        [Tooltip("完全张开时的中心孔径，捏合到 1 时收缩为 0 即实心。")]
        [SerializeField, Range(0f, 0.5f)] private float openMarkerHoleRadius = 0.31f;

        [Header("Marker Color")]
        [Tooltip("未按下时的标记颜色。")]
        [SerializeField, FormerlySerializedAs("openColor")]
        private Color idleColor = Color.white;
        [Tooltip("原生按下或锁定时的标记颜色。")]
        [SerializeField, FormerlySerializedAs("pinchColor")]
        private Color pressedColor = new Color(0.2f, 1f, 0.53f, 1f);

        [Header("Visibility")]
        [Tooltip("无命中时是否仍在最后采样点显示标记。")]
        [SerializeField] private bool showWhenNoHit;

        [Header("Hover Lock")]
        [Tooltip("悬停在可锁定按钮上时固定投射点，手部抖动不影响其位置。")]
        [SerializeField] private bool stabilizeOnButtonHover = true;
        [Tooltip("进入锁定前需在按钮上停留的时长。")]
        [SerializeField, Min(0f)] private float hoverLockDwellSeconds = 0.15f;
        [Tooltip("保持锁定时允许射线超出按钮的比例（按按钮宽高向外扩展）。")]
        [SerializeField, Range(0f, 1f)] private float releaseEdgeTolerancePercent = 0.25f;
        [Tooltip("ScrollRect 内按钮不锁定，避免滚动时标记粘住。")]
        [SerializeField] private bool excludeScrollRect = true;

        private ICurveInteractionDataProvider _curveProvider;
        private HandRayLineStyleController _lineStyleController;
        private InteractionCasterBase _caster;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Mesh _markerMesh;
        private MaterialPropertyBlock _propertyBlock;
        private bool _isVisible;
        private bool _hasVisibilityState;
        private float _appliedHoleRadius = -1f;
        private Color _appliedColor;
        private bool _hasAppliedColor;

        private bool _hoverLocked;
        private Button _hoverCandidate;
        private bool _hoverCandidateEligible;
        private float _hoverCandidateSince = float.NegativeInfinity;
        private Button _lockedButton;
        private Vector3 _lockedPosition;

        /// <summary>未按下时的标记颜色。</summary>
        public Color IdleColor => idleColor;

        /// <summary>按下或锁定时的标记颜色。</summary>
        public Color PressedColor => pressedColor;

        /// <summary>距离自适应缩放系数。</summary>
        public float ScalePerMeter => scalePerMeter;

        /// <summary>最小基础尺寸。</summary>
        public float MinScale => minScale;

        /// <summary>最大基础尺寸。</summary>
        public float MaxScale => maxScale;

        /// <summary>沿法线的表面偏移。</summary>
        public float SurfaceOffset => surfaceOffset;

        /// <summary>张开状态的外径倍率。</summary>
        public float OpenMarkerScaleMultiplier => openMarkerScaleMultiplier;

        /// <summary>捏合状态的外径倍率。</summary>
        public float PinchMarkerScaleMultiplier => pinchMarkerScaleMultiplier;

        /// <summary>张开状态的中心孔径。</summary>
        public float OpenMarkerHoleRadius => openMarkerHoleRadius;

        /// <summary>无命中时是否显示标记。</summary>
        public bool ShowWhenNoHit => showWhenNoHit;

        private void Awake()
        {
            _curveProvider = interactor as ICurveInteractionDataProvider;
            if (_curveProvider == null || indicator == null)
            {
                Debug.LogError(
                    "[HandRayHitIndicator] Missing NearFarInteractor or indicator transform.",
                    this);
                enabled = false;
                return;
            }

            if (viewCamera == null)
            {
                viewCamera = Camera.main;
            }

            if (viewCamera == null)
            {
                Debug.LogError("[HandRayHitIndicator] No camera available for marker scaling.", this);
                enabled = false;
                return;
            }

            if (aimLock == null)
            {
                aimLock = GetComponentInParent<PinchAimPoseLock>();
            }

            if (aimLock == null)
            {
                aimLock = interactor.GetComponentInChildren<PinchAimPoseLock>(true);
            }

            if (aimLock == null)
            {
                Debug.LogWarning(
                    "[HandRayHitIndicator] No PinchAimPoseLock found; locked point feedback is disabled.",
                    this);
            }

            ResolveCaster();
            if (stabilizeOnButtonHover && _caster == null)
            {
                Debug.LogWarning(
                    "[HandRayHitIndicator] farInteractionCaster unavailable; hover lock is disabled.",
                    this);
            }

            _lineStyleController = GetComponent<HandRayLineStyleController>();
            _meshFilter = indicator.GetComponent<MeshFilter>();
            _meshRenderer = indicator.GetComponent<MeshRenderer>();
            if (_meshFilter == null || _meshRenderer == null)
            {
                Debug.LogError("[HandRayHitIndicator] Indicator requires MeshFilter and MeshRenderer.", this);
                enabled = false;
                return;
            }

            _markerMesh = CreateDiscMesh();
            _meshFilter.sharedMesh = _markerMesh;
            _propertyBlock = new MaterialPropertyBlock();
            SetVisible(false);
        }

        private void LateUpdate()
        {
            if (!_curveProvider.isActive)
            {
                ReleaseHoverLock();
                SetVisible(false);
                return;
            }

            bool pressed = ReadPressed();

            if (aimLock != null &&
                aimLock.isLocked &&
                aimLock.TryGetLockFeedback(out var lockPoint, out _))
            {
                ReleaseHoverLock();
                UpdateMarker(lockPoint, 1f, true);
                SetVisible(true);
                return;
            }

            RayHitKind hitKind = MapEndPointType(
                interactor.TryGetCurveEndPoint(out Vector3 curveEndPoint, false, false));

            bool hasUiHit = false;
            Vector3 uiWorldPosition = Vector3.zero;
            Button currentButton = null;
            if (hitKind == RayHitKind.Ui &&
                interactor.TryGetCurrentUIRaycastResult(out RaycastResult uiResult))
            {
                hasUiHit = true;
                uiWorldPosition = uiResult.worldPosition;
                currentButton = UIPressTargetResolver.FindButton(uiResult.gameObject);
            }

            if (stabilizeOnButtonHover &&
                _caster != null &&
                TryUpdateHoverLock(currentButton, uiWorldPosition, out var hoverPosition))
            {
                UpdateMarker(hoverPosition, ReadPinchVisualAmount(), pressed);
                SetVisible(true);
                return;
            }

            bool shouldShow = HandRayVisualRules.ShouldShowHitMarker(
                hitKind,
                _curveProvider.isActive,
                showWhenNoHit);

            if (!shouldShow ||
                !HandRayVisualRules.TryResolveHitMarkerPosition(
                    hitKind,
                    hasUiHit,
                    uiWorldPosition,
                    curveEndPoint,
                    _curveProvider.lastSamplePoint,
                    showWhenNoHit,
                    out Vector3 position))
            {
                SetVisible(false);
                return;
            }

            UpdateMarker(position, ReadPinchVisualAmount(), pressed);
            SetVisible(true);
        }

        private bool TryUpdateHoverLock(Button currentButton, Vector3 uiWorldPosition, out Vector3 position)
        {
            position = default;

            if (_hoverLocked)
            {
                if (ShouldReleaseHoverLock(currentButton))
                {
                    ReleaseHoverLock();
                    return false;
                }

                position = _lockedPosition;
                return true;
            }

            if (currentButton == null)
            {
                ResetHoverCandidate();
                return false;
            }

            var now = Time.unscaledTime;
            if (_hoverCandidate != currentButton)
            {
                _hoverCandidate = currentButton;
                _hoverCandidateSince = now;
                _hoverCandidateEligible = IsEligibleButton(currentButton);
            }

            if (!_hoverCandidateEligible)
            {
                return false;
            }

            if (HandRayVisualRules.ShouldBeginMarkerHold(
                    true,
                    now - _hoverCandidateSince,
                    hoverLockDwellSeconds))
            {
                _hoverLocked = true;
                _lockedButton = currentButton;
                _lockedPosition = uiWorldPosition;
                position = _lockedPosition;
                return true;
            }

            return false;
        }

        private bool IsEligibleButton(Button button)
        {
            if (button == null || !button.IsActive() || !button.IsInteractable())
            {
                return false;
            }

            if (UIPressTargetResolver.IsDraggable(button))
            {
                return false;
            }

            return !(excludeScrollRect && UIPressTargetResolver.IsInsideScrollRect(button));
        }

        private bool ShouldReleaseHoverLock(Button currentButton)
        {
            if (_lockedButton == null ||
                !_lockedButton.IsActive() ||
                !_lockedButton.IsInteractable())
            {
                return true;
            }

            bool hoveringOtherButton = currentButton != null && currentButton != _lockedButton;

            bool rayInsideExpandedRect = false;
            var rect = _lockedButton.transform as RectTransform;
            if (rect != null &&
                TryGetRaySource(out var origin, out var direction) &&
                UIPressTargetResolver.TryGetRayRectPoint(origin, direction, rect, out var point, out _))
            {
                rayInsideExpandedRect = UIPressTargetResolver.IsWorldPointInsideExpandedRect(
                    rect,
                    point,
                    releaseEdgeTolerancePercent);
            }

            return HandRayVisualRules.ShouldReleaseMarkerHold(
                true,
                hoveringOtherButton,
                rayInsideExpandedRect);
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

            origin = default;
            direction = default;
            return false;
        }

        private void ResolveCaster()
        {
            if (_caster != null || interactor == null)
            {
                return;
            }

            _caster = interactor.farInteractionCaster as InteractionCasterBase;
            if (_caster == null)
            {
                _caster = interactor.GetComponent<InteractionCasterBase>();
            }
        }

        private void ReleaseHoverLock()
        {
            _hoverLocked = false;
            _lockedButton = null;
            _lockedPosition = default;
            _hoverCandidate = null;
            _hoverCandidateEligible = false;
            _hoverCandidateSince = float.NegativeInfinity;
        }

        private void ResetHoverCandidate()
        {
            _hoverCandidate = null;
            _hoverCandidateEligible = false;
            _hoverCandidateSince = float.NegativeInfinity;
        }

        private void OnDisable()
        {
            ReleaseHoverLock();
        }

        private void OnDestroy()
        {
            if (_markerMesh == null)
            {
                return;
            }

            if (_meshFilter != null && _meshFilter.sharedMesh == _markerMesh)
            {
                _meshFilter.sharedMesh = null;
            }

            Destroy(_markerMesh);
            _markerMesh = null;
        }

        private void UpdateMarker(Vector3 position, float visualAmount, bool pressed)
        {
            Vector3 cameraPosition = viewCamera.transform.position;
            float distance = Vector3.Distance(cameraPosition, position);
            float baseScale = HandRayVisualRules.ComputeHitMarkerScale(
                distance,
                scalePerMeter,
                minScale,
                maxScale);

            float markerAmount = HandRayVisualRules.ComputeMarkerVisualAmount(visualAmount, pressed);

            float scale = baseScale * HandRayVisualRules.ComputeMarkerScaleMultiplier(
                markerAmount,
                openMarkerScaleMultiplier,
                pinchMarkerScaleMultiplier);

            float holeRadius = HandRayVisualRules.ComputeMarkerHoleRadius(
                markerAmount,
                openMarkerHoleRadius);

            Color color = HandRayVisualRules.ComputeMarkerColor(
                pressed,
                idleColor,
                pressedColor);

            Vector3 toCamera = cameraPosition - position;
            if (toCamera.sqrMagnitude > 0.000001f)
            {
                Vector3 direction = toCamera.normalized;
                position += direction * surfaceOffset;
                indicator.rotation = Quaternion.LookRotation(direction, viewCamera.transform.up);
            }
            else
            {
                indicator.rotation = viewCamera.transform.rotation;
            }

            indicator.position = position;
            indicator.localScale = Vector3.one * scale;

            var changed = false;
            if (!Mathf.Approximately(_appliedHoleRadius, holeRadius))
            {
                _propertyBlock.SetFloat(HoleRadiusPropertyId, holeRadius);
                _appliedHoleRadius = holeRadius;
                changed = true;
            }

            if (!_hasAppliedColor || !ColorApproximately(_appliedColor, color))
            {
                _propertyBlock.SetColor(ColorPropertyId, color);
                _appliedColor = color;
                _hasAppliedColor = true;
                changed = true;
            }

            if (changed)
            {
                _meshRenderer.SetPropertyBlock(_propertyBlock);
            }
        }

        private static bool ColorApproximately(Color a, Color b)
        {
            return Mathf.Approximately(a.r, b.r) &&
                   Mathf.Approximately(a.g, b.g) &&
                   Mathf.Approximately(a.b, b.b) &&
                   Mathf.Approximately(a.a, b.a);
        }

        private bool ReadPressed()
        {
            if (interactor.uiPressInput != null && interactor.uiPressInput.ReadIsPerformed())
            {
                return true;
            }

            return aimLock != null && aimLock.isLocked;
        }

        private float ReadPinchVisualAmount()
        {
            if (_lineStyleController != null && _lineStyleController.isActiveAndEnabled)
            {
                return _lineStyleController.CurrentPinchAmount;
            }

            float rawPinch = interactor.selectInput != null
                ? interactor.selectInput.ReadValue()
                : 0f;

            return Mathf.Clamp01(rawPinch);
        }

        private void SetVisible(bool visible)
        {
            if (_hasVisibilityState && _isVisible == visible)
            {
                return;
            }

            _isVisible = visible;
            _hasVisibilityState = true;
            indicator.gameObject.SetActive(visible);
        }

        private static Mesh CreateDiscMesh()
        {
            var vertices = new Vector3[DiscSegments + 1];
            var triangles = new int[DiscSegments * 3];
            vertices[0] = Vector3.zero;

            for (int i = 0; i < DiscSegments; i++)
            {
                float angle = 2f * Mathf.PI * i / DiscSegments;
                vertices[i + 1] = new Vector3(
                    Mathf.Cos(angle) * 0.5f,
                    Mathf.Sin(angle) * 0.5f,
                    0f);
            }

            for (int i = 0; i < DiscSegments; i++)
            {
                int next = (i + 1) % DiscSegments;
                int triangle = i * 3;
                triangles[triangle] = 0;
                triangles[triangle + 1] = i + 1;
                triangles[triangle + 2] = next + 1;
            }

            var mesh = new Mesh
            {
                name = "HandRayMarkerDisc",
                vertices = vertices,
                triangles = triangles
            };
            mesh.RecalculateBounds();
            return mesh;
        }

        private static RayHitKind MapEndPointType(EndPointType endPointType)
        {
            switch (endPointType)
            {
                case EndPointType.EmptyCastHit:
                    return RayHitKind.EmptyHit;
                case EndPointType.ValidCastHit:
                    return RayHitKind.ValidHit;
                case EndPointType.AttachPoint:
                    return RayHitKind.AttachPoint;
                case EndPointType.UI:
                    return RayHitKind.Ui;
                case EndPointType.None:
                default:
                    return RayHitKind.None;
            }
        }
    }
}
