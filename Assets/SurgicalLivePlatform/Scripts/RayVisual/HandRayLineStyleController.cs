using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;

namespace SurgicalLivePlatform
{
    /// <summary>
    /// 双手射线样式控制：默认白色；未锁定时为虚线，锁定后为实线；
    /// 原始捏合值高于 maxValue 时线色变为蓝色。仅挂在手部对象上，不修改 XRI 预制体或包源码。
    /// </summary>
    public class HandRayLineStyleController : MonoBehaviour
    {
        private static readonly int SolidAmountPropertyId = Shader.PropertyToID("_SolidAmount");

        [Header("References")]
        [Tooltip("驱动捏合视觉量的手部交互器。")]
        [SerializeField] private NearFarInteractor interactor;
        [Tooltip("显示射线的 LineRenderer（LineVisual 子物体）。")]
        [SerializeField] private LineRenderer lineRenderer;
        [Tooltip("XRI 曲线视觉控制器，用于无命中时的线长控制。")]
        [SerializeField] private CurveVisualController visualController;
        [Tooltip("调试显示原始手指捏合值。")]
        public TMP_Text textmesh;

        [Header("Line Material")]
        [Tooltip("虚线材质，其 _SolidAmount 控制虚线间隙的透明度。")]
        [SerializeField, FormerlySerializedAs("dashedMaterial")]
        private Material lineMaterial;

        [Header("Pinch Presentation")]
        [Tooltip("完全张开时的线体实心量：0 为纯虚线，0.5 为半虚，1 为实线。")]
        [SerializeField, Range(0f, 1f)] private float openSolidAmount = 0.5f;
        [Tooltip("模拟捏合量映射起点，低于该值视为完全张开。")]
        [SerializeField, Range(0f, 1f)] private float pinchVisualStart = 0.5f;
        [Tooltip("模拟捏合量映射终点，高于该值视为完全捏合。")]
        [SerializeField, Range(0f, 1f)] private float pinchVisualFull = 1f;
        [Tooltip("视觉平滑速度（实心量每秒），0 表示不平滑。")]
        [SerializeField, Min(0f)] private float pinchSmoothingSpeed = 8f;

        [Header("Lock Presentation")]
        [Tooltip("锁定组件；设置后线型由锁定状态与模拟量阈值驱动。")]
        [SerializeField] private PinchAimPoseLock aimLock;
        [Tooltip("原始捏合值高于 maxValue 时的线色。")]
        [SerializeField] private Color overMaxColor = new Color(0.2f, 0.6f, 1f, 1f);

        [Header("No-Hit Presentation")]
        [Tooltip("无命中时使用的线体渐变。")]
        [SerializeField] private Gradient noHitGradient;
        [Tooltip("无命中时保持最大视觉长度。")]
        [SerializeField] private bool keepFullLengthWhenNoHit = true;

        private MaterialPropertyBlock _propertyBlock;
        private bool _isValid;
        private float _currentPinchAmount;
        private float _currentSolidAmount;
        private float _appliedSolidAmount = -1f;
        private float _originalRestingLength;
        private bool _isOverMax;
        private Gradient _whiteGradient;
        private Gradient _blueGradient;

        /// <summary>当前（已平滑、含按下钳制）的捏合视觉量。</summary>
        public float CurrentPinchAmount => _currentPinchAmount;

        /// <summary>原生 UI 按下是否生效。</summary>
        public bool CurrentPressed { get; private set; }

        /// <summary>完全张开时的线体实心量。</summary>
        public float OpenSolidAmount => openSolidAmount;

        /// <summary>模拟捏合量映射起点。</summary>
        public float PinchVisualStart => pinchVisualStart;

        /// <summary>模拟捏合量映射终点。</summary>
        public float PinchVisualFull => pinchVisualFull;

        /// <summary>视觉平滑速度。</summary>
        public float PinchSmoothingSpeed => pinchSmoothingSpeed;

        private void Awake()
        {
            bool hasLengthTarget = !keepFullLengthWhenNoHit || visualController != null;
            if (interactor == null || lineRenderer == null || lineMaterial == null || !hasLengthTarget)
            {
                Debug.LogError(
                    "[HandRayLineStyleController] Missing interactor/LineRenderer/line material or CurveVisualController.",
                    this);
                enabled = false;
                return;
            }

            if (aimLock == null)
            {
                aimLock = GetComponentInChildren<PinchAimPoseLock>(true);
            }

            _propertyBlock = new MaterialPropertyBlock();
            _isValid = true;
            _originalRestingLength = visualController != null
                ? visualController.restingVisualLineLength
                : 0f;
            _whiteGradient = CreateTintedGradient(lineRenderer.colorGradient, Color.white);
            _blueGradient = CreateTintedGradient(lineRenderer.colorGradient, overMaxColor);
        }

        private void OnEnable()
        {
            if (!_isValid)
            {
                return;
            }

            lineRenderer.textureMode = LineTextureMode.Tile;
            lineRenderer.sharedMaterial = lineMaterial;

            if (keepFullLengthWhenNoHit)
            {
                visualController.lineDynamicsMode = LineDynamicsMode.Traditional;
                visualController.extendLineToEmptyHit = true;

                if (noHitGradient != null && visualController.noValidHitProperties != null)
                {
                    visualController.noValidHitProperties.gradient = noHitGradient;
                }
            }

            CurrentPressed = ReadPressed();
            float rawPinch = ReadRawPinch();
            _isOverMax = aimLock != null && rawPinch > aimLock.MaxValue;
            _currentPinchAmount = CurrentPressed
                ? 1f
                : HandRayVisualRules.ComputePinchVisualAmount(rawPinch, pinchVisualStart, pinchVisualFull);
            _currentSolidAmount = ComputeSolidTarget();
            ApplySolidAmount(_currentSolidAmount);
        }

        private void Update()
        {
            if (!_isValid)
            {
                return;
            }

            CurrentPressed = ReadPressed();
            float rawPinch = ReadRawPinch();
            _isOverMax = aimLock != null && rawPinch > aimLock.MaxValue;

            float targetPinch = CurrentPressed
                ? 1f
                : HandRayVisualRules.ComputePinchVisualAmount(rawPinch, pinchVisualStart, pinchVisualFull);
            _currentPinchAmount = pinchSmoothingSpeed > 0f
                ? Mathf.MoveTowards(_currentPinchAmount, targetPinch, pinchSmoothingSpeed * Time.deltaTime)
                : targetPinch;

            float targetSolid = ComputeSolidTarget();
            _currentSolidAmount = pinchSmoothingSpeed > 0f
                ? Mathf.MoveTowards(_currentSolidAmount, targetSolid, pinchSmoothingSpeed * Time.deltaTime)
                : targetSolid;
        }

        private void LateUpdate()
        {
            if (!_isValid)
            {
                return;
            }

            if (!Mathf.Approximately(_appliedSolidAmount, _currentSolidAmount))
            {
                ApplySolidAmount(_currentSolidAmount);
            }

            if (keepFullLengthWhenNoHit)
            {
                UpdateRestingLength();
            }
        }

        private bool ReadPressed()
        {
            return interactor.uiPressInput != null && interactor.uiPressInput.ReadIsPerformed();
        }

        private float ReadRawPinch()
        {
            float rawPinch = interactor.selectInput != null
                ? interactor.selectInput.ReadValue()
                : 0f;

            if (textmesh != null)
            {
                textmesh.text = rawPinch.ToString("F3");
            }

            return rawPinch;
        }

        private float ComputeSolidTarget()
        {
            if (aimLock != null)
            {
                return aimLock.isLocked ? 1f : 0f;
            }

            return HandRayVisualRules.ComputeLineSolidAmount(_currentPinchAmount, openSolidAmount);
        }

        private static Gradient CreateTintedGradient(Gradient source, Color tint)
        {
            var colorKeys = source.colorKeys;
            var tintedKeys = new GradientColorKey[colorKeys.Length];
            for (var i = 0; i < colorKeys.Length; i++)
            {
                tintedKeys[i] = new GradientColorKey(tint, colorKeys[i].time);
            }

            var gradient = new Gradient();
            gradient.SetKeys(tintedKeys, source.alphaKeys);
            return gradient;
        }

        [BeforeRenderOrder(102)]
        private void OnBeforeRenderLineColor()
        {
            if (!_isValid || aimLock == null || lineRenderer == null)
            {
                return;
            }

            lineRenderer.colorGradient = _isOverMax ? _blueGradient : _whiteGradient;
        }

        private void ApplySolidAmount(float amount)
        {
            _propertyBlock.SetFloat(SolidAmountPropertyId, amount);
            lineRenderer.SetPropertyBlock(_propertyBlock);
            _appliedSolidAmount = amount;
        }

        private void UpdateRestingLength()
        {
            bool isNoHit = interactor.TryGetCurveEndPoint(out _, false, false) == EndPointType.None;
            float targetLength = isNoHit
                ? visualController.maxVisualCurveDistance
                : _originalRestingLength;

            if (!Mathf.Approximately(visualController.restingVisualLineLength, targetLength))
            {
                visualController.restingVisualLineLength = targetLength;
            }
        }
    }
}
