using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using SurgicalLivePlatform.Example.UI;
using SurgicalLivePlatform.Runtime.UI;

namespace SurgicalLivePlatform.Example.Tests
{
    public class ToastManagerTests
    {
        [SetUp]
        public void Setup()
        {
            // Ensure instance is cleared between tests if needed, though it's a singleton pattern.
        }

        [UnityTest]
        public IEnumerator Show_DisplaysMessage_AndFadesOut()
        {
            // Ensure instance is created
            var _ = ToastManager.Instance;
            Toast.Show("Test Message", 0.5f);
            
            // Wait for it to be visible
            yield return null;
            var text = ToastManager.Instance.GetComponentInChildren<UnityEngine.UI.Text>();
            var canvasGroup = ToastManager.Instance.GetComponentInChildren<CanvasGroup>();
            
            Assert.IsNotNull(text, "Text component should exist.");
            Assert.AreEqual("Test Message", text.text);
            Assert.AreEqual(1f, canvasGroup.alpha);

            // Wait for duration + fadeout time
            yield return new WaitForSeconds(1.1f);
            
            // Should be invisible now
            Assert.AreEqual(0f, canvasGroup.alpha);
        }

        [UnityTest]
        public IEnumerator ShowPersistent_StaysVisible()
        {
            // Ensure instance is created
            var _ = ToastManager.Instance;
            Toast.ShowPersistent("Persistent Message");
            
            yield return new WaitForSeconds(1.0f);
            
            var text = ToastManager.Instance.GetComponentInChildren<UnityEngine.UI.Text>();
            var canvasGroup = ToastManager.Instance.GetComponentInChildren<CanvasGroup>();
            
            Assert.AreEqual("Persistent Message", text.text);
            Assert.AreEqual(1f, canvasGroup.alpha); // Should remain visible
            
            Toast.Hide();
            Assert.AreEqual(0f, canvasGroup.alpha);
        }
    }
}
