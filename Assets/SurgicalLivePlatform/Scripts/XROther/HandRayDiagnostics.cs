using System.Collections.Generic;
using System.Text;
using TMPro;
using Unity.XR.XREAL;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;

namespace SurgicalLivePlatform
{
    /// <summary>
    /// Phase 0 diagnostics for the Test_HandsDemo hand ray.
    /// It never writes interaction transforms, input actions, or existing UI state.
    /// The optional controlled-test prompt is a diagnostic-only world-space Canvas.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HandRayDiagnostics : MonoBehaviour
    {
        [Header("Logging")]
        [SerializeField] private bool enableLogging = true;
        [SerializeField, Min(0.05f)] private float logIntervalSeconds = 0.2f;

        [Header("Controlled Test")]
        [SerializeField] private bool enableControlledSequence = false;
        [SerializeField, Min(1f)] private float initialDelaySeconds = 5f;

        private struct TestPhase
        {
            public string name;
            public string instruction;
            public float duration;

            public TestPhase(string name, string instruction, float duration)
            {
                this.name = name;
                this.instruction = instruction;
                this.duration = duration;
            }
        }

        private static readonly TestPhase[] TestPhases =
        {
            new TestPhase("Prepare", "准备：保持双手在视野内，注视前方", 5f),
            new TestPhase("HeadYawLeft", "保持双手不动，头向左转约30°", 8f),
            new TestPhase("HeadYawRight", "保持双手不动，头向右转约30°", 8f),
            new TestPhase("HeadPitchUp", "保持双手不动，抬头约20°", 8f),
            new TestPhase("HeadPitchDown", "保持双手不动，低头约20°", 8f),
            new TestPhase("LeftPinchHold", "左手捏合并保持，手不要动", 12f),
            new TestPhase("RightPinchHold", "右手捏合并保持，手不要动", 12f),
            new TestPhase("PinchHandMove", "保持捏合，手缓慢水平移动", 12f),
        };

        [Header("Optional References")]
        [SerializeField] private NearFarInteractor leftInteractor;
        [SerializeField] private NearFarInteractor rightInteractor;
        [SerializeField] private Transform leftAimPose;
        [SerializeField] private Transform rightAimPose;
        [SerializeField] private Camera xrCamera;

        private readonly StringBuilder _builder = new StringBuilder(768);
        private readonly List<XRHandSubsystem> _subsystems = new List<XRHandSubsystem>(2);
        private XRHandSubsystem _handSubsystem;
        private float _nextLogTime;
        private bool _warnedMissing;
        private bool _sequenceActive;
        private int _phaseIndex = -1;
        private float _phaseStartTime;
        private Canvas _promptCanvas;
        private TextMeshProUGUI _promptText;
        private string _lastPromptText;
        private float _nextPromptUpdateTime;
        private const float PromptDistance = 1.2f;
        private const float PromptScale = 0.001f;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            RefreshHandSubsystem();
        }

        private void Start()
        {
            if (!enableControlledSequence)
            {
                return;
            }

            _sequenceActive = true;
            _phaseIndex = -1;
            _phaseStartTime = Time.unscaledTime + Mathf.Max(1f, initialDelaySeconds);
            CreatePrompt();
            Debug.Log("[HandRayDiag][PHASE] sequence scheduled in " + Mathf.Max(1f, initialDelaySeconds).ToString("F1") + "s");
        }

        private void Update()
        {
            UpdateControlledSequence();
            UpdatePrompt();

            if (!enableLogging || Time.unscaledTime < _nextLogTime)
            {
                return;
            }

            _nextLogTime = Time.unscaledTime + Mathf.Max(0.05f, logIntervalSeconds);
            if (_handSubsystem == null || !_handSubsystem.running)
            {
                RefreshHandSubsystem();
            }

            WriteSnapshot();
        }

        private void UpdateControlledSequence()
        {
            if (!_sequenceActive)
            {
                return;
            }

            if (_phaseIndex < 0)
            {
                if (Time.unscaledTime < _phaseStartTime)
                {
                    return;
                }

                BeginPhase(0);
                return;
            }

            if (Time.unscaledTime - _phaseStartTime < TestPhases[_phaseIndex].duration)
            {
                return;
            }

            EndPhase(_phaseIndex);
            var nextIndex = _phaseIndex + 1;
            if (nextIndex >= TestPhases.Length)
            {
                _sequenceActive = false;
                Debug.Log("[HandRayDiag][PHASE] complete");
                return;
            }

            BeginPhase(nextIndex);
        }

        private void BeginPhase(int index)
        {
            _phaseIndex = index;
            _phaseStartTime = Time.unscaledTime;
            Debug.Log("[HandRayDiag][PHASE] start index=" + index + " name=" + TestPhases[index].name);
        }

        private void EndPhase(int index)
        {
            Debug.Log("[HandRayDiag][PHASE] end index=" + index + " name=" + TestPhases[index].name);
        }

        private void CreatePrompt()
        {
            if (_promptCanvas != null)
            {
                return;
            }

            if (xrCamera == null)
            {
                xrCamera = Camera.main;
            }

            if (xrCamera == null)
            {
                Debug.LogWarning("[HandRayDiagnostics] No camera found; controlled-test prompt disabled.", this);
                return;
            }

            var canvasGo = new GameObject("HandRayPromptCanvas", typeof(RectTransform), typeof(Canvas));
            canvasGo.transform.SetParent(xrCamera.transform, false);

            var canvasRect = canvasGo.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(1000f, 300f);
            canvasRect.localPosition = new Vector3(0f, 0f, PromptDistance);
            canvasRect.localRotation = Quaternion.identity;
            canvasRect.localScale = Vector3.one * PromptScale;

            _promptCanvas = canvasGo.GetComponent<Canvas>();
            _promptCanvas.renderMode = RenderMode.WorldSpace;
            _promptCanvas.worldCamera = xrCamera;
            _promptCanvas.overrideSorting = true;
            _promptCanvas.sortingOrder = 30000;

            var backgroundGo = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            backgroundGo.transform.SetParent(canvasGo.transform, false);
            var backgroundRect = backgroundGo.GetComponent<RectTransform>();
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = Vector2.zero;
            backgroundRect.offsetMax = Vector2.zero;
            var background = backgroundGo.GetComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.72f);
            background.raycastTarget = false;

            var textGo = new GameObject("Prompt", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(canvasGo.transform, false);
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.05f, 0.1f);
            textRect.anchorMax = new Vector2(0.95f, 0.9f);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            _promptText = textGo.GetComponent<TextMeshProUGUI>();
            _promptText.fontSize = 46f;
            _promptText.alignment = TextAlignmentOptions.Center;
            _promptText.color = Color.white;
            _promptText.raycastTarget = false;
            _promptText.textWrappingMode = TextWrappingModes.Normal;

            UpdatePrompt(true);
        }

        private void UpdatePrompt(bool force = false)
        {
            if (_promptText == null)
            {
                return;
            }

            if (!force && Time.unscaledTime < _nextPromptUpdateTime)
            {
                return;
            }

            _nextPromptUpdateTime = Time.unscaledTime + 0.2f;

            string text;
            if (!_sequenceActive)
            {
                text = "HandRay 受控测试完成";
            }
            else if (_phaseIndex < 0)
            {
                var wait = Mathf.Max(0f, _phaseStartTime - Time.unscaledTime);
                text = "准备：保持双手在视野内，注视前方\n" + wait.ToString("F1") + "s 后开始";
            }
            else
            {
                var phase = TestPhases[_phaseIndex];
                var remaining = Mathf.Max(0f, phase.duration - (Time.unscaledTime - _phaseStartTime));
                text = phase.instruction + "\n" + remaining.ToString("F1") + "s";
            }

            if (force || text != _lastPromptText)
            {
                _lastPromptText = text;
                _promptText.text = text;
            }
        }

        private void DestroyPrompt()
        {
            if (_promptCanvas == null)
            {
                return;
            }

            Destroy(_promptCanvas.gameObject);
            _promptCanvas = null;
            _promptText = null;
        }

        private void OnDestroy()
        {
            DestroyPrompt();
        }

        private void ResolveReferences()
        {
            if (leftInteractor == null)
            {
                leftInteractor = FindInteractor("XR Origin Hands (XR Rig)/Camera Offset/Left Hand/Near-Far Interactor");
            }

            if (rightInteractor == null)
            {
                rightInteractor = FindInteractor("XR Origin Hands (XR Rig)/Camera Offset/Right Hand/Near-Far Interactor");
            }

            if (leftAimPose == null)
            {
                leftAimPose = FindTransform("XR Origin Hands (XR Rig)/Camera Offset/Left Hand/Aim Pose");
            }

            if (rightAimPose == null)
            {
                rightAimPose = FindTransform("XR Origin Hands (XR Rig)/Camera Offset/Right Hand/Aim Pose");
            }

            if (xrCamera == null)
            {
                xrCamera = Camera.main;
            }

            if (!_warnedMissing && (leftInteractor == null || rightInteractor == null))
            {
                _warnedMissing = true;
                Debug.LogWarning("[HandRayDiagnostics] Missing hand references. Verify the active scene is Test_HandsDemo or assign references manually.", this);
            }
        }

        private static NearFarInteractor FindInteractor(string path)
        {
            var go = GameObject.Find(path);
            return go != null ? go.GetComponent<NearFarInteractor>() : null;
        }

        private static Transform FindTransform(string path)
        {
            var go = GameObject.Find(path);
            return go != null ? go.transform : null;
        }

        private void RefreshHandSubsystem()
        {
            _subsystems.Clear();
            SubsystemManager.GetSubsystems(_subsystems);
            _handSubsystem = null;
            for (var i = 0; i < _subsystems.Count; i++)
            {
                if (_subsystems[i] != null && _subsystems[i].running)
                {
                    _handSubsystem = _subsystems[i];
                    break;
                }
            }
        }

        private void WriteSnapshot()
        {
            _builder.Clear();
            _builder.Append("[HandRayDiag] t=").Append(Time.unscaledTime.ToString("F2"));
            _builder.Append(" scene=").Append(SceneManager.GetActiveScene().name);
            _builder.Append(" tracking=").Append(ReadTrackingType());

            if (xrCamera != null)
            {
                AppendVector(" cam", xrCamera.transform.position);
                AppendQuaternion(" camRot", xrCamera.transform.rotation);
                var offset = xrCamera.transform.parent;
                if (offset != null)
                {
                    AppendVector(" offset", offset.position);
                }
            }

            AppendHand("L", leftInteractor, leftAimPose, Handedness.Left);
            AppendHand("R", rightInteractor, rightAimPose, Handedness.Right);
            Debug.Log(_builder.ToString());
        }

        private void AppendHand(string label, NearFarInteractor interactor, Transform aimPose, Handedness handedness)
        {
            _builder.Append('\n').Append("[HandRayDiag][").Append(label).Append(']');
            if (interactor == null)
            {
                _builder.Append(" interactor=null");
                return;
            }

            AppendReader(" sel", interactor.selectInput);
            AppendReader(" ui", interactor.uiPressInput);

            if (aimPose != null)
            {
                AppendVector(" aimL", aimPose.localPosition);
                AppendQuaternion(" aimLRot", aimPose.localRotation);
                AppendVector(" aimW", aimPose.position);
                AppendQuaternion(" aimWRot", aimPose.rotation);
            }

            var caster = interactor.GetComponentInChildren<CurveInteractionCaster>(true);
            if (caster != null)
            {
                _builder.Append(" stab=").Append(caster.enableStabilization ? 1 : 0)
                    .Append(",P=").Append(caster.positionStabilization.ToString("F3"))
                    .Append(",A=").Append(caster.angleStabilization.ToString("F1"));
                AppendVector(" castOrigin", caster.castOrigin != null ? caster.castOrigin.position : Vector3.zero);
                AppendVector(" effective", caster.effectiveCastOrigin != null ? caster.effectiveCastOrigin.position : Vector3.zero);
            }

            var endType = interactor.TryGetCurveEndPoint(out var endPoint, false, false);
            _builder.Append(" endType=").Append(endType);
            AppendVector(" end", endPoint);

            if (TryGetWristPose(handedness, out var wristPose))
            {
                AppendVector(" wrist", wristPose.position);
                AppendQuaternion(" wristRot", wristPose.rotation);
            }
            else
            {
                _builder.Append(" wrist=unavailable");
            }
        }

        private void AppendReader(string label, XRInputButtonReader reader)
        {
            if (reader == null)
            {
                _builder.Append(label).Append("=null");
                return;
            }

            _builder.Append(label)
                .Append(":held=").Append(reader.ReadIsPerformed() ? 1 : 0)
                .Append(",down=").Append(reader.ReadWasPerformedThisFrame() ? 1 : 0)
                .Append(",up=").Append(reader.ReadWasCompletedThisFrame() ? 1 : 0)
                .Append(",val=").Append(reader.ReadValue().ToString("F3"))
                .Append(",idx=").Append(ReadAction(reader.inputActionReferencePerformed).ToString("F3"))
                .Append(",pinch=").Append(ReadAction(reader.inputActionReferenceValue).ToString("F3"));
        }

        private static float ReadAction(InputActionReference reference)
        {
            var action = reference != null ? reference.action : null;
            if (action == null || !action.enabled)
            {
                return 0f;
            }

            try
            {
                return action.ReadValue<float>();
            }
            catch
            {
                return 0f;
            }
        }

        private bool TryGetWristPose(Handedness handedness, out Pose pose)
        {
            pose = default;
            if (_handSubsystem == null || !_handSubsystem.running)
            {
                return false;
            }

            var hand = handedness == Handedness.Left ? _handSubsystem.leftHand : _handSubsystem.rightHand;
            if (!hand.isTracked)
            {
                return false;
            }

            return hand.GetJoint(XRHandJointID.Wrist).TryGetPose(out pose);
        }

        private static string ReadTrackingType()
        {
            try
            {
                return XREALPlugin.GetTrackingType().ToString();
            }
            catch
            {
                return "unavailable";
            }
        }

        private void AppendVector(string label, Vector3 value)
        {
            _builder.Append(label).Append('=')
                .Append(value.x.ToString("F3")).Append(',')
                .Append(value.y.ToString("F3")).Append(',')
                .Append(value.z.ToString("F3"));
        }

        private void AppendQuaternion(string label, Quaternion value)
        {
            _builder.Append(label).Append('=')
                .Append(value.x.ToString("F3")).Append(',')
                .Append(value.y.ToString("F3")).Append(',')
                .Append(value.z.ToString("F3")).Append(',')
                .Append(value.w.ToString("F3"));
        }
    }
}
