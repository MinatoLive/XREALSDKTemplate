// HandFrustumMonitor.cs
// 位于: Assets/SurgicalLivePlatform/Scripts/HandFrustumMonitor.cs
// 该脚本使用 XREAL 官方 XRHands API（UnityEngine.XR.Hands）监测左右手相对于 Camera 的视锥体位置，
// 并通过左右两张 UI Image 进行平滑颜色/透明度渐变警示：
//   • 安全区域（未达临界阈值）  – 透明不显示 (Alpha = 0)
//   • 接近临界值 (NearEdge)    – 从透明向亮橙色连续渐变加深
//   • 超过临界值 (OutOfFrustum)– 显示为不透明红色 (Alpha = 1)
//   • 未被追踪 (NotTracked)    – 显示为橙色半透明警示态 (Alpha ≈ 0.6)
// 可选调试 Text 会输出左右手的视锥状态与手部模型状态（显示/隐藏）。

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Subsystems;
using UnityEngine.XR.Hands;

namespace SurgicalLivePlatform
{
    /// <summary>
    /// 监测左右手相对于 Camera 的视锥体位置并通过 Image 颜色渐变展示状态。
    /// 自动对齐 XREAL XR Tracking 空间坐标系，即时计算视口边缘距离，实现零 GC 性能开销。
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class HandFrustumMonitor : MonoBehaviour
    {
        public enum HandFrustumState
        {
            NotTracked,
            Inside,
            NearEdge,
            OutOfFrustum
        }

        // ---------- 配置项 ----------
        [Header("Detection Threshold")]
        [Tooltip("手部距离视口边缘的阈值（0~0.5），小于此值判定为 NearEdge（接近边缘）。")]
        [Range(0f, 0.5f)]
        [SerializeField] private float edgeDistThreshold = 0.1f;

        [Header("Tracking Space")]
        [Tooltip("XR Tracking Origin Transform（若留空则自动取 Camera 父级 Camera Offset）")]
        [SerializeField] private Transform trackingOrigin;

        [Header("Hand Model References (Optional)")]
        [Tooltip("左手 3D 模型根节点或掌心 Transform（若绑定则优先根据模型在世界空间的实际渲染位置判定视口）")]
        [SerializeField] private Transform leftHandModelTransform;

        [Tooltip("右手 3D 模型根节点或掌心 Transform（若绑定则优先根据模型在世界空间的实际渲染位置判定视口）")]
        [SerializeField] private Transform rightHandModelTransform;

        // ---------- 图像预警组件引用 ----------
        [Header("Image Warning UI References")]
        [Tooltip("左手状态预警 Image（若未绑定则不更新图片）")]
        [SerializeField] private Image leftHandWarningImage;
        [Tooltip("右手状态预警 Image（若未绑定则不更新图片）")]
        [SerializeField] private Image rightHandWarningImage;

        // ---------- 颜色渐变配置 ----------
        [Header("Warning Colors & Gradient")]
        [Tooltip("安全区内部颜色（完全透明）")]
        [SerializeField] private Color insideColor = new Color(1f, 0.55f, 0f, 0f);

        [Tooltip("接近临界值警示色（橙色）")]
        [SerializeField] private Color nearEdgeColor = new Color(1f, 0.55f, 0f, 1f);

        [Tooltip("超过临界值超界警示色（红色）")]
        [SerializeField] private Color outOfFrustumColor = new Color(1f, 0f, 0f, 1f);

        [Tooltip("传感器未追踪时的警示色（橙色半透明）")]
        [SerializeField] private Color notTrackedColor = new Color(1f, 0.55f, 0f, 0.6f);

        [Tooltip("颜色插值平滑阻尼速度（0 为直接切换不插值）")]
        [SerializeField] private float colorLerpSpeed = 10f;

        // ---------- 可选调试 Text 引用 ----------
        [Header("Optional Debug Text References")]
        [Tooltip("（可选调试备用）左手状态提示 Text（若与右手共用同一 Text 则合并输出）")]
        [SerializeField] private TextMeshProUGUI leftHandText;
        [Tooltip("（可选调试备用）右手状态提示 Text（若与左手共用同一 Text 则合并输出）")]
        [SerializeField] private TextMeshProUGUI rightHandText;

        // ---------- 调试文本常量 ----------
        private const string TextNotTracked = "未追踪";
        private const string TextInside = "正常视野";
        private const string TextNearEdge = "接近边缘";
        private const string TextOutOfFrustum = "超出视野";

        // ---------- 私有缓存与零 GC 管理 ----------
        private Camera _mainCamera;
        private XRHandSubsystem _handSubsystem;
        private readonly List<XRHandSubsystem> _subsystemCache = new List<XRHandSubsystem>();

        private Color _currentLeftColor;
        private Color _currentRightColor;

        private HandFrustumState _lastLeftState = (HandFrustumState)(-1);
        private HandFrustumState _lastRightState = (HandFrustumState)(-1);
        private bool _lastLeftModelVisible;
        private bool _lastRightModelVisible;

        public Transform TrackingOrigin
        {
            get => trackingOrigin;
            set => trackingOrigin = value;
        }

        public Transform LeftHandModelTransform
        {
            get => leftHandModelTransform;
            set => leftHandModelTransform = value;
        }

        public Transform RightHandModelTransform
        {
            get => rightHandModelTransform;
            set => rightHandModelTransform = value;
        }

        public Image LeftHandWarningImage
        {
            get => leftHandWarningImage;
            set => leftHandWarningImage = value;
        }

        public Image RightHandWarningImage
        {
            get => rightHandWarningImage;
            set => rightHandWarningImage = value;
        }

        public float EdgeDistThreshold
        {
            get => edgeDistThreshold;
            set => edgeDistThreshold = Mathf.Clamp(value, 0f, 0.5f);
        }

        private void Awake()
        {
            _mainCamera = GetComponent<Camera>();
            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;
            }

            if (trackingOrigin == null && _mainCamera != null && _mainCamera.transform.parent != null)
            {
                trackingOrigin = _mainCamera.transform.parent;
            }

            _currentLeftColor = insideColor;
            _currentRightColor = insideColor;

            if (leftHandWarningImage != null)
            {
                leftHandWarningImage.color = _currentLeftColor;
            }

            if (rightHandWarningImage != null)
            {
                rightHandWarningImage.color = _currentRightColor;
            }
        }

        private void Update()
        {
            if (_mainCamera == null)
            {
                _mainCamera = GetComponent<Camera>();
                if (_mainCamera == null) _mainCamera = Camera.main;
                if (_mainCamera == null)
                {
                    ShowStatusMessage("[HandFrustumMonitor] 未找到 Camera 引用");
                    return;
                }
            }

            EnsureSubsystem();

            bool hasModels = (leftHandModelTransform != null && leftHandModelTransform.gameObject.activeInHierarchy) ||
                             (rightHandModelTransform != null && rightHandModelTransform.gameObject.activeInHierarchy);

            bool subsystemReady = _handSubsystem != null && _handSubsystem.running;

            if (!subsystemReady && !hasModels)
            {
                // 子系统与手部模型均未就绪时，按未追踪警示色处理
                UpdateWarningImage(leftHandWarningImage, notTrackedColor, ref _currentLeftColor);
                UpdateWarningImage(rightHandWarningImage, notTrackedColor, ref _currentRightColor);
                UpdateDebugText(HandFrustumState.NotTracked, HandFrustumState.NotTracked, false, false);
                return;
            }

            XRHand leftHand = subsystemReady ? _handSubsystem.leftHand : default;
            XRHand rightHand = subsystemReady ? _handSubsystem.rightHand : default;

            bool leftModelVisible = IsHandModelVisible(leftHand, leftHandModelTransform, subsystemReady);
            bool rightModelVisible = IsHandModelVisible(rightHand, rightHandModelTransform, subsystemReady);

            // 1. 评估左右手目标颜色并执行平滑插值渲染
            Color targetLeftColor = EvaluateHandColor(leftHand, leftHandModelTransform);
            Color targetRightColor = EvaluateHandColor(rightHand, rightHandModelTransform);

            UpdateWarningImage(leftHandWarningImage, targetLeftColor, ref _currentLeftColor);
            UpdateWarningImage(rightHandWarningImage, targetRightColor, ref _currentRightColor);

            // 2. 更新左右手状态文本（含手部模型状态）
            UpdateDebugText(
                EvaluateHandState(leftHand, leftHandModelTransform),
                EvaluateHandState(rightHand, rightHandModelTransform),
                leftModelVisible,
                rightModelVisible);
        }

        /// <summary>
        /// 手部模型状态：优先使用 XR Hands 官方追踪状态，未运行子系统时回退到模型节点激活状态。
        /// </summary>
        private static bool IsHandModelVisible(XRHand hand, Transform handModelTransform, bool subsystemReady)
        {
            if (subsystemReady)
            {
                return hand.isTracked;
            }

            return handModelTransform != null && handModelTransform.gameObject.activeInHierarchy;
        }

        /// <summary>
        /// 状态或模型显隐变化时才刷新文本，避免每帧字符串分配。
        /// </summary>
        private void UpdateDebugText(
            HandFrustumState leftState,
            HandFrustumState rightState,
            bool leftModelVisible,
            bool rightModelVisible)
        {
            if (leftHandText == null && rightHandText == null)
            {
                return;
            }

            if (leftState == _lastLeftState &&
                rightState == _lastRightState &&
                leftModelVisible == _lastLeftModelVisible &&
                rightModelVisible == _lastRightModelVisible)
            {
                return;
            }

            _lastLeftState = leftState;
            _lastRightState = rightState;
            _lastLeftModelVisible = leftModelVisible;
            _lastRightModelVisible = rightModelVisible;

            ApplyDebugTextUpdate(leftState, rightState, leftModelVisible, rightModelVisible);
        }

        /// <summary>
        /// 弹性检索与缓存 XRHandSubsystem，避免仅在 Awake 阶段检索导致的初始化竞争失效。
        /// </summary>
        private void EnsureSubsystem()
        {
            if (_handSubsystem != null && _handSubsystem.running)
            {
                return;
            }

            _subsystemCache.Clear();
            SubsystemManager.GetSubsystems(_subsystemCache);
            if (_subsystemCache.Count > 0)
            {
                _handSubsystem = _subsystemCache[0];
            }
        }

        /// <summary>
        /// 更新警告 Image 的颜色（支持平滑阻尼插值与零 GC 堆分配）。
        /// </summary>
        private void UpdateWarningImage(Image image, Color targetColor, ref Color currentColor)
        {
            if (image == null) return;

            if (colorLerpSpeed > 0f)
            {
                currentColor = Color.Lerp(currentColor, targetColor, Time.deltaTime * colorLerpSpeed);
            }
            else
            {
                currentColor = targetColor;
            }

            image.color = currentColor;
        }

        /// <summary>
        /// 纯逻辑计算：解析单手的目标世界坐标。
        /// 优先级：手部模型 Transform (若已激活) > 关节局部坐标转换世界坐标。
        /// </summary>
        public static bool TryResolveTargetWorldPosition(
            Transform handModelTransform,
            bool hasJointPose,
            Vector3 jointLocalPos,
            Transform origin,
            out Vector3 resolvedPos)
        {
            if (handModelTransform != null && handModelTransform.gameObject.activeInHierarchy)
            {
                resolvedPos = handModelTransform.position;
                return true;
            }

            if (hasJointPose)
            {
                resolvedPos = CalculateWorldPosition(jointLocalPos, origin);
                return true;
            }

            resolvedPos = Vector3.zero;
            return false;
        }

        /// <summary>
        /// 计算单手相对于相机的当前目标预警颜色。
        /// 优先采用手部模型 Transform，如未绑定则优先取 Palm（掌心）关节，避免手腕在下边界切边误报红色。
        /// </summary>
        private Color EvaluateHandColor(XRHand hand, Transform handModelTransform)
        {
            bool hasJoint = false;
            Vector3 jointLocalPos = Vector3.zero;

            if (hand != null && hand.isTracked)
            {
                XRHandJoint palmJoint = hand.GetJoint(XRHandJointID.Palm);
                if (palmJoint.TryGetPose(out Pose palmPose))
                {
                    hasJoint = true;
                    jointLocalPos = palmPose.position;
                }
                else
                {
                    XRHandJoint wristJoint = hand.GetJoint(XRHandJointID.Wrist);
                    if (wristJoint.TryGetPose(out Pose wristPose))
                    {
                        hasJoint = true;
                        jointLocalPos = wristPose.position;
                    }
                }
            }

            Transform origin = ResolveTrackingOrigin();
            if (!TryResolveTargetWorldPosition(handModelTransform, hasJoint, jointLocalPos, origin, out Vector3 worldPos))
            {
                return notTrackedColor;
            }

            Vector3 viewportPos = _mainCamera.WorldToViewportPoint(worldPos);

            return CalculateHandWarningColor(
                viewportPos,
                isTracked: true,
                edgeDistThreshold,
                insideColor,
                nearEdgeColor,
                outOfFrustumColor,
                notTrackedColor
            );
        }

        /// <summary>
        /// 评估单手相对于相机的视锥几何状态（用于调试文本）。
        /// </summary>
        private HandFrustumState EvaluateHandState(XRHand hand, Transform handModelTransform)
        {
            bool hasJoint = false;
            Vector3 jointLocalPos = Vector3.zero;

            if (hand != null && hand.isTracked)
            {
                XRHandJoint palmJoint = hand.GetJoint(XRHandJointID.Palm);
                if (palmJoint.TryGetPose(out Pose palmPose))
                {
                    hasJoint = true;
                    jointLocalPos = palmPose.position;
                }
                else
                {
                    XRHandJoint wristJoint = hand.GetJoint(XRHandJointID.Wrist);
                    if (wristJoint.TryGetPose(out Pose wristPose))
                    {
                        hasJoint = true;
                        jointLocalPos = wristPose.position;
                    }
                }
            }

            Transform origin = ResolveTrackingOrigin();
            if (!TryResolveTargetWorldPosition(handModelTransform, hasJoint, jointLocalPos, origin, out Vector3 worldPos))
            {
                return HandFrustumState.NotTracked;
            }

            Vector3 viewportPos = _mainCamera.WorldToViewportPoint(worldPos);

            return EvaluateViewportPosition(viewportPos, edgeDistThreshold);
        }

        private Transform ResolveTrackingOrigin()
        {
            if (trackingOrigin != null)
            {
                return trackingOrigin;
            }

            if (_mainCamera != null && _mainCamera.transform.parent != null)
            {
                return _mainCamera.transform.parent;
            }

            return _mainCamera != null ? _mainCamera.transform : null;
        }

        /// <summary>
        /// 将 XR Tracking 空间的局部坐标转换为世界坐标。
        /// </summary>
        public static Vector3 CalculateWorldPosition(Vector3 localTrackingPos, Transform origin)
        {
            return origin != null ? origin.TransformPoint(localTrackingPos) : localTrackingPos;
        }

        /// <summary>
        /// 纯数学判定视口坐标是否处于视锥内部、接近边缘或超出范围。
        /// </summary>
        public static HandFrustumState EvaluateViewportPosition(Vector3 viewportPos, float threshold)
        {
            // 摄像机背面 (z <= 0) 或超出 [0, 1] 视口区间
            if (viewportPos.z <= 0f ||
                viewportPos.x < 0f || viewportPos.x > 1f ||
                viewportPos.y < 0f || viewportPos.y > 1f)
            {
                return HandFrustumState.OutOfFrustum;
            }

            // 计算到 4 个视口边缘的最小归一化距离
            float edgeDist = Mathf.Min(
                viewportPos.x,
                1f - viewportPos.x,
                viewportPos.y,
                1f - viewportPos.y
            );

            return edgeDist < threshold ? HandFrustumState.NearEdge : HandFrustumState.Inside;
        }

        /// <summary>
        /// 纯函数：根据手部视口坐标、追踪状态及阈值计算警告颜色（支持连续物理距离与 Alpha 渐变）。
        /// 渐变规则：
        ///   • 未追踪：notTracked
        ///   • 相机后方或超出视口：outOfFrustum (红色)
        ///   • 安全区内部 (edgeDist >= threshold)：inside (完全透明)
        ///   • 接近临界值 (0 <= edgeDist < threshold)：从 inside 平滑渐变至 nearEdge (橙色加深)
        /// </summary>
        public static Color CalculateHandWarningColor(
            Vector3 viewportPos,
            bool isTracked,
            float threshold,
            Color inside,
            Color nearEdge,
            Color outOfFrustum,
            Color notTracked)
        {
            if (!isTracked)
            {
                return notTracked;
            }

            // 相机背面 (z <= 0) 或超出 [0, 1] 视口区间
            if (viewportPos.z <= 0f ||
                viewportPos.x < 0f || viewportPos.x > 1f ||
                viewportPos.y < 0f || viewportPos.y > 1f)
            {
                return outOfFrustum;
            }

            // 计算到 4 个视口边缘的最小归一化距离
            float edgeDist = Mathf.Min(
                viewportPos.x,
                1f - viewportPos.x,
                viewportPos.y,
                1f - viewportPos.y
            );

            // 小于等于 0：已越界（红色）
            if (edgeDist <= 0f)
            {
                return outOfFrustum;
            }

            // 安全区内部（距离 >= 阈值）：完全透明
            if (edgeDist >= threshold)
            {
                return inside;
            }

            // 边缘过渡区（0 < edgeDist < threshold）：
            // 归一化因子 t 从 0（阈值边界，完全透明）逐渐增加到 1（视口边缘，橙色不透明）
            float t = threshold > 0.0001f ? Mathf.Clamp01(1f - (edgeDist / threshold)) : 1f;

            return Color.Lerp(inside, nearEdge, t);
        }

        /// <summary>
        /// （可选调试备用）更新调试文本，输出视锥状态与手部模型状态。
        /// </summary>
        private void ApplyDebugTextUpdate(
            HandFrustumState leftState,
            HandFrustumState rightState,
            bool leftModelVisible,
            bool rightModelVisible)
        {
            string leftStateText = FormatHandState("左手", leftState, leftModelVisible);
            string rightStateText = FormatHandState("右手", rightState, rightModelVisible);
            bool isSharedText = leftHandText != null && leftHandText == rightHandText;

            if (isSharedText)
            {
                leftHandText.gameObject.SetActive(true);
                leftHandText.text = $"{leftStateText} | {rightStateText}";
                leftHandText.color = GetStateColor(leftState == HandFrustumState.OutOfFrustum || rightState == HandFrustumState.OutOfFrustum ? HandFrustumState.OutOfFrustum : leftState);
                return;
            }

            if (leftHandText != null)
            {
                leftHandText.gameObject.SetActive(true);
                leftHandText.text = leftStateText;
                leftHandText.color = GetStateColor(leftState);
            }

            if (rightHandText != null)
            {
                rightHandText.gameObject.SetActive(true);
                rightHandText.text = rightStateText;
                rightHandText.color = GetStateColor(rightState);
            }
        }

        private static string FormatHandState(string label, HandFrustumState state, bool modelVisible)
        {
            return $"{label}: {GetStateString(state)} | 模型: {(modelVisible ? "显示" : "隐藏")}";
        }

        private static string GetStateString(HandFrustumState state)
        {
            switch (state)
            {
                case HandFrustumState.NotTracked:
                    return TextNotTracked;
                case HandFrustumState.Inside:
                    return TextInside;
                case HandFrustumState.NearEdge:
                    return TextNearEdge;
                case HandFrustumState.OutOfFrustum:
                default:
                    return TextOutOfFrustum;
            }
        }

        private Color GetStateColor(HandFrustumState state)
        {
            switch (state)
            {
                case HandFrustumState.Inside:
                    return insideColor;
                case HandFrustumState.NearEdge:
                    return nearEdgeColor;
                case HandFrustumState.NotTracked:
                    return notTrackedColor;
                case HandFrustumState.OutOfFrustum:
                default:
                    return outOfFrustumColor;
            }
        }

        private void ShowStatusMessage(string message)
        {
            if (leftHandText != null)
            {
                leftHandText.gameObject.SetActive(true);
                leftHandText.text = message;
                leftHandText.color = nearEdgeColor;
            }
            else if (rightHandText != null)
            {
                rightHandText.gameObject.SetActive(true);
                rightHandText.text = message;
                rightHandText.color = nearEdgeColor;
            }
        }
    }
}
