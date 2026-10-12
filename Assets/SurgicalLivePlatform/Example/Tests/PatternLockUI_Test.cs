using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
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
            _go = new GameObject("PatternLock");
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
            _patternLock.HandlePointerUp();

            Assert.AreEqual("23", submittedResult);
        }
    }
}
