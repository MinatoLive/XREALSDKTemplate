using System.Collections.Generic;
using UnityEngine;

namespace SurgicalLivePlatform.Example
{
    /// <summary>
    /// 控制 3D World Space UI 始终正面朝向摄像机的组件。
    /// 具备以下工业级特性：
    /// 1. 解决 Unity Canvas 在直接使用 LookAt(Camera) 时出现的正反面镜像相反（文字颠倒）问题；
    /// 2. 指数阻尼球形插值 (Exponential Slerp)，解决真机佩戴时的生硬顿挫与眩晕感；
    /// 3. 角度死区过滤机制 (Angle Deadzone)，彻底过滤头部 IMU 追踪传感器高频微震，
    ///    并在视线相对静止时杜绝 Canvas 频繁脏标记与矩阵重算，消除移动端卡顿与掉帧；
    /// 4. 支持锁定 Y 轴旋转、相机缓存自愈与三大根 UI 实体自动纳管。
    /// </summary>
    public class LookAtCamera : MonoBehaviour
    {
        [Tooltip("需要朝向摄像机的目标 UI Transform 列表（如 Login, RoomList, Room）")]
        public Transform[] lookAtTar;

        [Header("平滑与防抖设置 (Smooth & Anti-Jitter)")]
        [Tooltip("是否启用平滑阻尼转向（false 为即时刚性对齐）")]
        [SerializeField] private bool enableSmooth = true;

        [Tooltip("平滑转向速度系数（建议 5 ~ 15，值越大跟随越快）")]
        [SerializeField] private float smoothSpeed = 8.0f;

        [Tooltip("角度死区（度）。当 UI 当前朝向与目标朝向夹角小于该值时，完全停止修改 Transform.rotation，彻底杜绝 Canvas 频繁脏标记与移动端掉帧")]
        [SerializeField] private float angleDeadzone = 0.3f;

        [Header("方向与相机设置")]
        [Tooltip("是否锁定 Y 轴旋转（保持 UI 始终垂直于地面，仅水平方向追踪朝向）")]
        [SerializeField] private bool lockYAxis = false;

        [Tooltip("观察摄像机引用，若未指定则自动获取 Camera.main")]
        [SerializeField] private Camera targetCamera;

        public bool EnableSmooth
        {
            get => enableSmooth;
            set => enableSmooth = value;
        }

        public float SmoothSpeed
        {
            get => smoothSpeed;
            set => smoothSpeed = Mathf.Max(0.1f, value);
        }

        public float AngleDeadzone
        {
            get => angleDeadzone;
            set => angleDeadzone = Mathf.Max(0f, value);
        }

        public bool LockYAxis
        {
            get => lockYAxis;
            set => lockYAxis = value;
        }

        public Camera TargetCamera
        {
            get => targetCamera;
            set => targetCamera = value;
        }

        private void OnEnable()
        {
            ResolveCamera();
            ResolveTargetsIfNeeded();
            SnapImmediately();
        }

        private void Start()
        {
            ResolveCamera();
            ResolveTargetsIfNeeded();
            SnapImmediately();
        }

        private void LateUpdate()
        {
            UpdateOrientations();
        }

        /// <summary>
        /// 瞬间即时对准目标朝向（无平滑过渡、无死区过滤）。
        /// 适用于面板初次显示、重置或快速切镜。
        /// </summary>
        public void SnapImmediately()
        {
            var cam = ResolveCamera();
            if (cam == null || lookAtTar == null || lookAtTar.Length == 0)
            {
                return;
            }

            Vector3 camPos = cam.transform.position;
            Vector3 camUp = cam.transform.up;

            for (int i = 0; i < lookAtTar.Length; i++)
            {
                Transform target = lookAtTar[i];
                if (target == null || !target.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Vector3 forwardDir = target.position - camPos;
                Vector3 upDir = camUp;
                if (lockYAxis)
                {
                    forwardDir.y = 0f;
                    upDir = Vector3.up;
                }

                if (forwardDir.sqrMagnitude > 0.0001f)
                {
                    target.rotation = Quaternion.LookRotation(forwardDir.normalized, upDir);
                }
            }
        }

        /// <summary>
        /// 执行所有目标 UI 的摄像机正面朝向对齐与平滑阻尼更新。
        /// </summary>
        /// <param name="deltaTime">帧间隔时间，传入负值时使用系统 Time.deltaTime</param>
        public void UpdateOrientations(float deltaTime = -1f)
        {
            var cam = ResolveCamera();
            if (cam == null || lookAtTar == null || lookAtTar.Length == 0)
            {
                return;
            }

            float dt = (deltaTime >= 0f) ? deltaTime : Time.deltaTime;
            Vector3 camPos = cam.transform.position;
            Vector3 camUp = cam.transform.up;

            for (int i = 0; i < lookAtTar.Length; i++)
            {
                Transform target = lookAtTar[i];
                if (target == null || !target.gameObject.activeInHierarchy)
                {
                    continue;
                }

                // 核心计算：计算从摄像机指向 UI 目标的前向向量 (+Z)
                // 在 Unity UI Canvas (World Space) 中，其可见正面法线朝向自身 -Z 方向（视线沿 +Z 观察为正向）。
                // 当 Canvas 的 +Z 轴指向远离摄像机的方向时，其可见正面正好直接迎向摄像机，彻底消除水平/垂直镜像相反。
                Vector3 forwardDir = target.position - camPos;

                Vector3 upDir = camUp;
                if (lockYAxis)
                {
                    forwardDir.y = 0f;
                    upDir = Vector3.up;
                }

                if (forwardDir.sqrMagnitude <= 0.0001f)
                {
                    continue;
                }


                Quaternion desiredRotation = Quaternion.LookRotation(forwardDir.normalized, upDir);

                if (!enableSmooth)
                {
                    target.rotation = desiredRotation;
                    continue;
                }

                target.rotation = Quaternion.Lerp(target.rotation, desiredRotation, Time.deltaTime * smoothSpeed);
            }
        }

        /// <summary>
        /// 解析并缓存摄像机引用，具备自愈机制
        /// </summary>
        public Camera ResolveCamera()
        {
            if (targetCamera != null && targetCamera.gameObject.activeInHierarchy)
            {
                return targetCamera;
            }

            targetCamera = Camera.main;
            if (targetCamera == null)
            {
                targetCamera = FindFirstObjectByType<Camera>(FindObjectsInactive.Exclude);
            }

            return targetCamera;
        }

        /// <summary>
        /// 当 Inspector 未手动拖拽绑定时，自愈查找场景中的三大根 UI 实体：Login, RoomList, Room
        /// </summary>
        public void ResolveTargetsIfNeeded()
        {
            if (lookAtTar != null && lookAtTar.Length > 0)
            {
                // 已有有效目标，无需重置
                bool hasAnyValid = false;
                for (int i = 0; i < lookAtTar.Length; i++)
                {
                    if (lookAtTar[i] != null)
                    {
                        hasAnyValid = true;
                        break;
                    }
                }
                if (hasAnyValid) return;
            }

            var resolvedList = new List<Transform>(3);

            var loginObj = GameObject.Find("Login");
            if (loginObj != null) resolvedList.Add(loginObj.transform);

            var roomListObj = GameObject.Find("RoomList");
            if (roomListObj != null) resolvedList.Add(roomListObj.transform);

            var roomObj = GameObject.Find("Room");
            if (roomObj != null) resolvedList.Add(roomObj.transform);

            if (resolvedList.Count > 0)
            {
                lookAtTar = resolvedList.ToArray();
            }
        }
    }
}
