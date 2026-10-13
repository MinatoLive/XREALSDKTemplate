using NUnit.Framework;
using SurgicalLivePlatform.Example;
using UnityEngine;

namespace SurgicalLivePlatform.Tests.Runtime
{
    [TestFixture]
    public class LookAtCameraTests
    {
        private GameObject _cameraGo;
        private Camera _camera;
        private GameObject _lookAtCameraGo;
        private LookAtCamera _lookAtCamera;

        [SetUp]
        public void SetUp()
        {
            _cameraGo = new GameObject("TestCamera", typeof(Camera));
            _camera = _cameraGo.GetComponent<Camera>();
            _cameraGo.tag = "MainCamera";
            _cameraGo.transform.position = Vector3.zero;
            _cameraGo.transform.rotation = Quaternion.identity;

            _lookAtCameraGo = new GameObject("LookAtCameraHost", typeof(LookAtCamera));
            _lookAtCamera = _lookAtCameraGo.GetComponent<LookAtCamera>();
            _lookAtCamera.TargetCamera = _camera;
            _lookAtCamera.EnableSmooth = false; // 基础几何测试默认使用即时模式，专项测试验证平滑与死区
        }

        [TearDown]
        public void TearDown()
        {
            if (_cameraGo != null) Object.DestroyImmediate(_cameraGo);
            if (_lookAtCameraGo != null) Object.DestroyImmediate(_lookAtCameraGo);
        }

        [Test]
        public void LookAtCamera_Orientations_FacesCameraDirectlyWithoutReversal()
        {
            var targetGo = new GameObject("UITarget");
            targetGo.transform.position = new Vector3(0, 0, 5);
            targetGo.transform.rotation = Quaternion.Euler(0, 180, 0);

            try
            {
                _lookAtCamera.lookAtTar = new Transform[] { targetGo.transform };
                _lookAtCamera.LockYAxis = false;
                _lookAtCamera.EnableSmooth = false;

                // 执行朝向更新
                _lookAtCamera.UpdateOrientations();

                // 核心验证：
                // Unity Canvas 可见正面朝向其 -Z 方向（+Z 轴远离正面）。
                // 正向无镜像的 UI 其 +Z forward 向量必须与 (target - camera) 方向同向（远离摄像机），
                // 这样从摄像机看过去正好看到正面，Dot 积应接近 1.0f。
                Vector3 expectedForward = (targetGo.transform.position - _cameraGo.transform.position).normalized;
                float dot = Vector3.Dot(targetGo.transform.forward, expectedForward);

                Assert.Greater(dot, 0.999f, "UI 的 forward (+Z) 应背向摄像机指向远方，确保正面法线迎向摄像机（消除相反镜像）");

                // 对比：传统 naive LookAt(camera.position) 会导致 dot 接近 -1.0f（正面背对用户，文字相反）
                Vector3 naiveLookAtForward = (_cameraGo.transform.position - targetGo.transform.position).normalized;
                float naiveDot = Vector3.Dot(targetGo.transform.forward, naiveLookAtForward);
                Assert.Less(naiveDot, -0.99f, "UI 不得采用反向背对摄像机的错误朝向");
            }
            finally
            {
                Object.DestroyImmediate(targetGo);
            }
        }

        [Test]
        public void LookAtCamera_MovingCamera_CorrectlyTracksCameraPosition()
        {
            var targetGo = new GameObject("UITarget");
            targetGo.transform.position = new Vector3(2, 0, 4);

            try
            {
                _cameraGo.transform.position = new Vector3(-3, 1, -2);
                _lookAtCamera.lookAtTar = new Transform[] { targetGo.transform };
                _lookAtCamera.EnableSmooth = false;

                _lookAtCamera.UpdateOrientations();

                Vector3 expectedForward = (targetGo.transform.position - _cameraGo.transform.position).normalized;
                float dot = Vector3.Dot(targetGo.transform.forward, expectedForward);

                Assert.Greater(dot, 0.999f, "在任意三维摄像机位置下，UI 均应精准正面朝向摄像机");
            }
            finally
            {
                Object.DestroyImmediate(targetGo);
            }
        }

        [Test]
        public void LookAtCamera_LockYAxis_ConstrainsPitchAndRoll()
        {
            var targetGo = new GameObject("UITarget");
            targetGo.transform.position = new Vector3(0, 0, 5);

            try
            {
                // 摄像机位于高空（俯视 UI）
                _cameraGo.transform.position = new Vector3(3, 8, 0);
                _lookAtCamera.lookAtTar = new Transform[] { targetGo.transform };
                _lookAtCamera.LockYAxis = true;
                _lookAtCamera.EnableSmooth = false;

                _lookAtCamera.UpdateOrientations();

                // 开启 lockYAxis 后，UI 的 forward 向量 y 分量必须强制归 0（保持水平），UI 垂直于地面
                Assert.AreEqual(0f, targetGo.transform.forward.y, 0.001f, "开启 LockYAxis 后，UI 必须保持水平无倾斜");
                Assert.AreEqual(1f, targetGo.transform.up.y, 0.001f, "开启 LockYAxis 后，UI 的 up 向量必须对齐 Vector3.up");
            }
            finally
            {
                Object.DestroyImmediate(targetGo);
            }
        }

        [Test]
        public void LookAtCamera_InactiveAndNullTargets_SafelyHandledWithoutException()
        {
            var activeGo = new GameObject("ActiveTarget");
            var inactiveGo = new GameObject("InactiveTarget");
            inactiveGo.SetActive(false);

            try
            {
                _lookAtCamera.lookAtTar = new Transform[] { null, inactiveGo.transform, activeGo.transform };
                _lookAtCamera.EnableSmooth = false;

                // 包含 null 与 inactive，不应抛出任何 NullReferenceException
                Assert.DoesNotThrow(() => _lookAtCamera.UpdateOrientations());

                Vector3 expectedForward = (activeGo.transform.position - _cameraGo.transform.position).normalized;
                float dot = Vector3.Dot(activeGo.transform.forward, expectedForward);
                Assert.Greater(dot, 0.999f, "活跃节点必须正常更新朝向");
            }
            finally
            {
                Object.DestroyImmediate(activeGo);
                Object.DestroyImmediate(inactiveGo);
            }
        }

        [Test]
        public void LookAtCamera_ResolveTargetsIfNeeded_AutoFindsSceneRootUIs()
        {
            var loginObj = new GameObject("Login");
            var roomListObj = new GameObject("RoomList");
            var roomObj = new GameObject("Room");

            try
            {
                _lookAtCamera.lookAtTar = null;
                _lookAtCamera.ResolveTargetsIfNeeded();

                Assert.IsNotNull(_lookAtCamera.lookAtTar);
                Assert.AreEqual(3, _lookAtCamera.lookAtTar.Length);
                Assert.AreEqual("Login", _lookAtCamera.lookAtTar[0].name);
                Assert.AreEqual("RoomList", _lookAtCamera.lookAtTar[1].name);
                Assert.AreEqual("Room", _lookAtCamera.lookAtTar[2].name);
            }
            finally
            {
                Object.DestroyImmediate(loginObj);
                Object.DestroyImmediate(roomListObj);
                Object.DestroyImmediate(roomObj);
            }
        }

        [Test]
        public void LookAtCamera_SnapImmediately_InstantlyAlignsWithoutLag()
        {
            var targetGo = new GameObject("UITarget");
            targetGo.transform.position = new Vector3(0, 0, 5);
            targetGo.transform.rotation = Quaternion.Euler(0, 90, 0); // 偏角 90 度

            try
            {
                _lookAtCamera.lookAtTar = new Transform[] { targetGo.transform };
                _lookAtCamera.EnableSmooth = true; // 即使启用了平滑，SnapImmediately 必须瞬间对齐

                _lookAtCamera.SnapImmediately();

                Vector3 expectedForward = (targetGo.transform.position - _cameraGo.transform.position).normalized;
                float dot = Vector3.Dot(targetGo.transform.forward, expectedForward);
                Assert.Greater(dot, 0.999f, "SnapImmediately 必须立即使 UI 100% 对准摄像机");
            }
            finally
            {
                Object.DestroyImmediate(targetGo);
            }
        }

        [Test]
        public void LookAtCamera_SmoothSlerp_ProgressivelyConvergesTowardTarget()
        {
            var targetGo = new GameObject("UITarget");
            targetGo.transform.position = new Vector3(0, 0, 5);
            targetGo.transform.rotation = Quaternion.Euler(0, 90, 0); // 偏角 90 度

            try
            {
                _lookAtCamera.lookAtTar = new Transform[] { targetGo.transform };
                _lookAtCamera.EnableSmooth = true;
                _lookAtCamera.SmoothSpeed = 10f;
                _lookAtCamera.AngleDeadzone = 0.01f;

                Quaternion desiredRotation = Quaternion.LookRotation(
                    (targetGo.transform.position - _cameraGo.transform.position).normalized,
                    Vector3.up);

                float initialAngle = Quaternion.Angle(targetGo.transform.rotation, desiredRotation);
                Assert.Greater(initialAngle, 80f, "初始角偏差应接近 90 度");

                // 执行一次模拟小步长 (0.05s) 更新
                _lookAtCamera.UpdateOrientations(0.05f);
                float angleStep1 = Quaternion.Angle(targetGo.transform.rotation, desiredRotation);
                Assert.Less(angleStep1, initialAngle, "第 1 帧更新后角度偏差应显著减小");

                // 再次执行一步
                _lookAtCamera.UpdateOrientations(0.05f);
                float angleStep2 = Quaternion.Angle(targetGo.transform.rotation, desiredRotation);
                Assert.Less(angleStep2, angleStep1, "第 2 帧更新后应继续单调平滑收敛");

                // 持续更新 20 步，验证最终完全平滑收敛到目标方向
                for (int i = 0; i < 20; i++)
                {
                    _lookAtCamera.UpdateOrientations(0.05f);
                }

                float finalAngle = Quaternion.Angle(targetGo.transform.rotation, desiredRotation);
                Assert.Less(finalAngle, 1f, "经过平滑阻尼插值后，角度偏差应接近收敛于 0");
            }
            finally
            {
                Object.DestroyImmediate(targetGo);
            }
        }

        [Test]
        public void LookAtCamera_AngleDeadzone_FiltersOutMicroJitterWithoutModifyingTransform()
        {
            var targetGo = new GameObject("UITarget");
            targetGo.transform.position = new Vector3(0, 0, 5);

            try
            {
                _lookAtCamera.lookAtTar = new Transform[] { targetGo.transform };
                _lookAtCamera.EnableSmooth = true;
                _lookAtCamera.AngleDeadzone = 0.5f; // 死区设为 0.5 度

                // 先瞬时对准
                _lookAtCamera.SnapImmediately();
                Quaternion initialAlignedRotation = targetGo.transform.rotation;

                // 模拟 IMU 高频极微弱微震（微调 0.1 度，低于 0.5 度死区）
                targetGo.transform.rotation = initialAlignedRotation * Quaternion.Euler(0.05f, 0.05f, 0f);
                Quaternion beforeTickRotation = targetGo.transform.rotation;

                // 执行更新
                _lookAtCamera.UpdateOrientations(0.02f);

                // 关键断言：因为角偏差处于死区内，Transform.rotation 绝不应被重写修改！
                // 这从根本上避免了 Canvas Transform 脏标记与 RectTransform 重排计算
                Assert.AreEqual(beforeTickRotation.x, targetGo.transform.rotation.x, 0.0001f);
                Assert.AreEqual(beforeTickRotation.y, targetGo.transform.rotation.y, 0.0001f);
                Assert.AreEqual(beforeTickRotation.z, targetGo.transform.rotation.z, 0.0001f);
                Assert.AreEqual(beforeTickRotation.w, targetGo.transform.rotation.w, 0.0001f);
            }
            finally
            {
                Object.DestroyImmediate(targetGo);
            }
        }
    }
}
