using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using SurgicalLivePlatform.Example.Runtime.UI;

namespace SurgicalLivePlatform.Example.Tests
{
    public class PatternLockUITests
    {
        private GameObject _go;
        private PatternLockUI _patternLock;

        [SetUp]
        public void Setup()
        {
            _go = new GameObject("PatternLock", typeof(RectTransform), typeof(Canvas));
            _patternLock = _go.AddComponent<PatternLockUI>();
            _patternLock.Initialize();
        }

        [TearDown]
        public void Teardown()
        {
            if (_go != null)
                UnityEngine.Object.DestroyImmediate(_go);
        }

        [Test]
        public void PatternLock_GeneratesCorrectPathString_OnSubmit()
        {
            string submittedResult = null;
            _patternLock.OnPatternSubmit += (res) => {
                submittedResult = res;
            };

            // Simulate drawing a pattern: 0 -> 1 -> 2 -> 5 -> 8 (which produces hash 12369)
            _patternLock.HandlePointerDown(0);
            _patternLock.HandleNodeEnter(1);
            _patternLock.HandleNodeEnter(2);
            _patternLock.HandleNodeEnter(5);
            _patternLock.HandleNodeEnter(8);
            _patternLock.HandlePointerUp();

            Assert.AreEqual("12369", submittedResult);
        }

        [Test]
        public void PatternLock_DoesNotAddDuplicateNodes()
        {
            string submittedResult = null;
            _patternLock.OnPatternSubmit += (res) => {
                submittedResult = res;
            };

            _patternLock.HandlePointerDown(1);
            _patternLock.HandleNodeEnter(2);
            _patternLock.HandleNodeEnter(1); // Duplicate
            _patternLock.HandleNodeEnter(4);
            _patternLock.HandleNodeEnter(5);
            _patternLock.HandlePointerUp();

            Assert.AreEqual("2356", submittedResult);
        }

        [Test]
        public void PatternLock_CachesPassword_OnPointerUp()
        {
            _patternLock.HandlePointerDown(0);
            _patternLock.HandleNodeEnter(1);
            _patternLock.HandleNodeEnter(2);
            _patternLock.HandleNodeEnter(5);
            _patternLock.HandlePointerUp();

            Assert.AreEqual("1236", _patternLock.CachedPassword);
            Assert.AreEqual(4, _patternLock.CurrentPatternCount, "Nodes should remain drawn until reset or sure");
        }

        [Test]
        public void PatternLock_ResetButton_ClearsCacheAndPattern()
        {
            _patternLock.HandlePointerDown(0);
            _patternLock.HandleNodeEnter(1);
            _patternLock.HandleNodeEnter(2);
            _patternLock.HandleNodeEnter(5);
            _patternLock.HandlePointerUp();

            Assert.AreEqual("1236", _patternLock.CachedPassword);

            _patternLock.OnResetButtonClicked();

            Assert.IsEmpty(_patternLock.CachedPassword);
            Assert.AreEqual(0, _patternLock.CurrentPatternCount);
        }

        [Test]
        public void PatternLock_SureButton_FillsPasswordToLoginUIController()
        {
            var loginObj = new GameObject("LoginController");
            var loginController = loginObj.AddComponent<UserLoginUIController>();
            _patternLock.loginUIController = loginController;

            _patternLock.HandlePointerDown(2);
            _patternLock.HandleNodeEnter(4);
            _patternLock.HandleNodeEnter(6);
            _patternLock.HandleNodeEnter(7);
            _patternLock.HandlePointerUp();

            Assert.AreEqual("3578", _patternLock.CachedPassword);

            _patternLock.OnSureButtonClicked();

            Assert.AreEqual("3578", loginController.password);

            UnityEngine.Object.DestroyImmediate(loginObj);
        }

        [Test]
        public void PatternLock_CloseButton_ClearsCacheAndRestoresPanels()
        {
            var loginObj = new GameObject("LoginController");
            var loginController = loginObj.AddComponent<UserLoginUIController>();
            var loginPanel = new GameObject("LoginPanel");
            var patternPanel = new GameObject("PatternPanel");

            loginController.loginPanel = loginPanel;
            loginController.patternLockPanel = patternPanel;
            _patternLock.loginUIController = loginController;

            loginController.ShowPatternLockPanel();
            Assert.IsFalse(loginPanel.activeSelf);
            Assert.IsTrue(patternPanel.activeSelf);

            _patternLock.HandlePointerDown(0);
            _patternLock.HandleNodeEnter(1);
            _patternLock.HandleNodeEnter(2);
            _patternLock.HandleNodeEnter(5);
            _patternLock.HandlePointerUp();
            Assert.AreEqual("1236", _patternLock.CachedPassword);

            _patternLock.OnCloseButtonClicked();

            Assert.IsEmpty(_patternLock.CachedPassword);
            Assert.IsTrue(loginPanel.activeSelf);
            Assert.IsFalse(patternPanel.activeSelf);

            UnityEngine.Object.DestroyImmediate(loginObj);
            UnityEngine.Object.DestroyImmediate(loginPanel);
            UnityEngine.Object.DestroyImmediate(patternPanel);
        }

        [Test]
        public void UserLoginUIController_ShowPanels_TogglesVisibilityCorrectly()
        {
            var loginObj = new GameObject("LoginController");
            var loginController = loginObj.AddComponent<UserLoginUIController>();
            var loginPanel = new GameObject("LoginPanel");
            var patternPanel = new GameObject("PatternPanel");

            loginController.loginPanel = loginPanel;
            loginController.patternLockPanel = patternPanel;

            loginController.ShowPatternLockPanel();
            Assert.IsFalse(loginPanel.activeSelf);
            Assert.IsTrue(patternPanel.activeSelf);

            loginController.ShowLoginPanel();
            Assert.IsTrue(loginPanel.activeSelf);
            Assert.IsFalse(patternPanel.activeSelf);

            UnityEngine.Object.DestroyImmediate(loginObj);
            UnityEngine.Object.DestroyImmediate(loginPanel);
            UnityEngine.Object.DestroyImmediate(patternPanel);
        }

        private void SetupDummyNodesAndLineTemplate()
        {
            var templateObj = new GameObject("LineTemplate", typeof(RectTransform));
            templateObj.transform.SetParent(_go.transform, false);
            var templateRt = templateObj.GetComponent<RectTransform>();
            templateRt.sizeDelta = new Vector2(100f, 4f);
            var templateImg = templateObj.AddComponent<Image>();
            templateImg.raycastTarget = false;
            _patternLock.lineTemplate = templateImg;

            _patternLock.nodes = new RectTransform[9];
            for (int i = 0; i < 9; i++)
            {
                var nodeObj = new GameObject($"Node_{i}", typeof(RectTransform));
                nodeObj.transform.SetParent(_go.transform, false);
                var rt = nodeObj.GetComponent<RectTransform>();
                rt.localPosition = new Vector3((i % 3) * 120f, (i / 3) * -120f, 0f);
                rt.anchoredPosition = new Vector2((i % 3) * 120f, (i / 3) * -120f);
                nodeObj.AddComponent<Image>();
                _patternLock.nodes[i] = rt;
            }
            _patternLock.Initialize();
        }

        [Test]
        public void PatternLock_WhenCanvasRotated_LineRemainsInLocalPlane()
        {
            SetupDummyNodesAndLineTemplate();

            // Simulate WorldSpace UI rotated in 3D facing camera (LookAtCamera)
            _go.transform.rotation = Quaternion.Euler(30f, 45f, 0f);
            _patternLock.MinPatternLength = 1;

            _patternLock.HandlePointerDown(0);
            _patternLock.HandleNodeEnter(1);

            var activeLines = _patternLock.ActiveLines;
            Assert.IsNotNull(activeLines);
            Assert.AreEqual(1, activeLines.Count);

            Transform lineTrans = activeLines[0].transform;
            // Line should be in local 2D plane (Z == 0 or near 0 in parent space)
            Assert.AreEqual(0f, lineTrans.localPosition.z, 0.05f, "Line must be aligned on local Z plane");
            // Local rotation X and Y must be 0 (no tilt relative to Canvas surface)
            Assert.AreEqual(0f, lineTrans.localRotation.x, 0.01f, "Line local rotation X must be 0");
            Assert.AreEqual(0f, lineTrans.localRotation.y, 0.01f, "Line local rotation Y must be 0");
        }

        [Test]
        public void PatternLock_WhenPatternShorterThanMinimum_PointerUpClears()
        {
            SetupDummyNodesAndLineTemplate();
            _patternLock.MinPatternLength = 4;
            bool submitted = false;
            _patternLock.OnPatternSubmit += (res) => submitted = true;

            _patternLock.HandlePointerDown(0);
            _patternLock.HandleNodeEnter(1);
            _patternLock.HandlePointerUp();

            Assert.IsFalse(submitted, "Should not submit pattern when count < MinPatternLength");
            Assert.IsEmpty(_patternLock.CachedPassword, "Cached password should be empty when below minimum length");
            Assert.AreEqual(0, _patternLock.CurrentPatternCount, "Should clear pattern when below minimum length");
        }

        [Test]
        public void PatternLock_LineTemplate_RaycastTargetIsFalse()
        {
            SetupDummyNodesAndLineTemplate();
            Assert.IsNotNull(_patternLock.lineTemplate);
            Assert.IsFalse(_patternLock.lineTemplate.raycastTarget, "LineTemplate raycastTarget must be false to avoid blocking rays");
        }

        [Test]
        public void PatternLock_ProximityDetection_CapturesNodeWithinRadius()
        {
            SetupDummyNodesAndLineTemplate();
            _patternLock.MinPatternLength = 4;
            _patternLock.CaptureRadius = 50f;

            // Start by touching node 0
            _patternLock.HandlePointerDown(0);
            Assert.AreEqual(1, _patternLock.CurrentPatternCount);

            // Node 1 is located at (120, 0).
            // Simulate pointer dragging to (110, 5), well within captureRadius (50) of Node 1
            var eventData = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
            {
                pointerCurrentRaycast = new UnityEngine.EventSystems.RaycastResult
                {
                    gameObject = _patternLock.gameObject,
                    worldPosition = new Vector3(110f, 5f, 0f)
                }
            };

            _patternLock.OnDrag(eventData);

            Assert.AreEqual(2, _patternLock.CurrentPatternCount, "Node 1 should be captured by proximity detection");
        }
    }
}
