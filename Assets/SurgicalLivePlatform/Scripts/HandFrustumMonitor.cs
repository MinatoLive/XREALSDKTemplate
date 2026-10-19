// HandFrustumMonitor.cs
// 位于: Assets/SurgicalLivePlatform/Scripts/HandFrustumMonitor.cs
// 该脚本使用 XREAL 官方 XRHands API（UnityEngine.XR.Hands）监测左右手相对于 Camera 的视锥体位置，
// 并通过左右两张 UI Image 进行平滑颜色/透明度渐变警示：
//   • 安全区域（未达临界阈值）  – 透明不显示 (Alpha = 0)
//   • 接近临界值 (NearEdge)    – 从透明向亮橙色连续渐变加深
//   • 超过临界值 (OutOfFrustum)– 显示为不透明红色 (Alpha = 1)
//   • 未被追踪 (NotTracked)    – 显示为橙色半透明警示态 (Alpha ≈ 0.6)
// 同时支持保留 TextMeshProUGUI 引用作为可选调试载体。

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
        private bool _lastSubsystemReady = false;

        public Transform TrackingOrigin
        {
            get => trackingOrigin;
            set => trackingOrigin = value;
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

            if (_handSubsystem == null || !_handSubsystem.running)
            {
                if (_lastSubsystemReady)
                {
                    _lastSubsystemReady = false;
                    ShowStatusMessage("[HandFrustumMonitor] 等待 XRHandSubsystem 就绪...");
                }

                // 子系统未就绪时，按未追踪警示色处理
                UpdateWarningImage(leftHandWarningImage, notTrackedColor, ref _currentLeftColor);
                UpdateWarningImage(rightHandWarningImage, notTrackedColor, ref _currentRightColor);
                return;
            }

            _lastSubsystemReady = true;

            // 1. 评估左右手目标颜色并执行平滑插值渲染
            Color targetLeftColor = EvaluateHandColor(_handSubsystem.leftHand);
            Color targetRightColor = EvaluateHandColor(_handSubsystem.rightHand);

            UpdateWarningImage(leftHandWarningImage, targetLeftColor, ref _currentLeftColor);
            UpdateWarningImage(rightHandWarningImage, targetRightColor, ref _currentRightColor);

            // 2. 若存在可选调试 Text，基于状态脏标记执行状态文本更新
            if (leftHandText != null || rightHandText != null)
            {
                HandFrustumState curLeft = EvaluateHandState(_handSubsystem.leftHand);
                HandFrustumState curRight = EvaluateHandState(_handSubsystem.rightHand);

                if (curLeft != _lastLeftState || curRight != _lastRightState)
                {
                    _lastLeftState = curLeft;
                    _lastRightState = curRight;
                    ApplyDebugTextUpdate(curLeft, curRight);
                }
            }
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
        /// 计算单手相对于相机的当前目标预警颜色。
        /// </summary>
        private Color EvaluateHandColor(XRHand hand)
        {
            if (!hand.isTracked)
            {
                return notTrackedColor;
            }

            XRHandJoint wristJoint = hand.GetJoint(XRHandJointID.Wrist);
            if (!wristJoint.TryGetPose(out Pose wristPose))
            {
                return notTrackedColor;
            }

            Transform origin = ResolveTrackingOrigin();
            Vector3 worldPos = CalculateWorldPosition(wristPose.position, origin);
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
        private HandFrustumState EvaluateHandState(XRHand hand)
        {
            if (!hand.isTracked)
            {
                return HandFrustumState.NotTracked;
            }

            XRHandJoint wristJoint = hand.GetJoint(XRHandJointID.Wrist);
            if (!wristJoint.TryGetPose(out Pose wristPose))
            {
                return HandFrustumState.NotTracked;
            }

            Transform origin = ResolveTrackingOrigin();
            Vector3 worldPos = CalculateWorldPosition(wristPose.position, origin);
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
        /// （可选调试备用）更新调试文本。
        /// </summary>
        private void ApplyDebugTextUpdate(HandFrustumState leftState, HandFrustumState rightState)
        {
            bool isSharedText = leftHandText != null && leftHandText == rightHandText;

            if (isSharedText)
            {
                leftHandText.gameObject.SetActive(true);
                leftHandText.text = $"左手: {GetStateString(leftState)} | 右手: {GetStateString(rightState)}";
                leftHandText.color = GetStateColor(leftState == HandFrustumState.OutOfFrustum || rightState == HandFrustumState.OutOfFrustum ? HandFrustumState.OutOfFrustum : leftState);
                return;
            }

            if (leftHandText != null)
            {
                leftHandText.gameObject.SetActive(true);
                leftHandText.text = $"左手: {GetStateString(leftState)}";
                leftHandText.color = GetStateColor(leftState);
            }

            if (rightHandText != null)
            {
                rightHandText.gameObject.SetActive(true);
                rightHandText.text = $"右手: {GetStateString(rightState)}";
                rightHandText.color = GetStateColor(rightState);
            }
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
