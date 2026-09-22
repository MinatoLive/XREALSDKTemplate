using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using SurgicalLivePlatform.Runtime.UI;

namespace SurgicalLivePlatform.Example.UI
{
    /// <summary>
    /// 简易的 Toast 管理器，用于在屏幕上方或中间显示短暂的提示信息。
    /// 满足房间加入等流程中的异常提示需求。
    /// </summary>
    public class ToastManager : MonoBehaviour
    {
        private static ToastManager _instance;
        public static ToastManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("ToastManager");
                    _instance = go.AddComponent<ToastManager>();
                    DontDestroyOnLoad(go);
                    _instance.Initialize();
                }
                return _instance;
            }
        }

        private Canvas _canvas;
        private GameObject _toastPrefab; // 若没有 prefab，自动生成一个简易 UI
        private Text _toastText;
        private Image _toastBg;
        private CanvasGroup _canvasGroup;

        private void Initialize()
        {
            // 自动生成 Canvas 和基础 UI，避免强依赖外部 Prefab
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 999;
            gameObject.AddComponent<CanvasScaler>();
            gameObject.AddComponent<GraphicRaycaster>();

            var toastGo = new GameObject("ToastPanel");
            toastGo.transform.SetParent(transform, false);
            _toastBg = toastGo.AddComponent<Image>();
            _toastBg.color = new Color(0, 0, 0, 0.8f);

            var rect = toastGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.1f);
            rect.anchorMax = new Vector2(0.5f, 0.1f);
            rect.sizeDelta = new Vector2(600, 100);

            _canvasGroup = toastGo.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 0;
            _canvasGroup.blocksRaycasts = false;

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(toastGo.transform, false);
            _toastText = textGo.AddComponent<Text>();
            _toastText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            _toastText.fontSize = 32;
            _toastText.color = Color.white;
            _toastText.alignment = TextAnchor.MiddleCenter;
            
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
        }

        private Coroutine _hideCoroutine;

        private void OnEnable()
        {
            Toast.OnShow += Show;
            Toast.OnShowPersistent += ShowPersistent;
            Toast.OnHide += Hide;
        }

        private void OnDisable()
        {
            Toast.OnShow -= Show;
            Toast.OnShowPersistent -= ShowPersistent;
            Toast.OnHide -= Hide;
        }

        public void Show(string message, float duration = 2.0f)
        {
            _toastText.text = message;
            _canvasGroup.alpha = 1;
            
            if (_hideCoroutine != null)
            {
                StopCoroutine(_hideCoroutine);
            }
            _hideCoroutine = StartCoroutine(HideCoroutine(duration));
        }

        public void ShowPersistent(string message)
        {
            _toastText.text = message;
            _canvasGroup.alpha = 1;
            if (_hideCoroutine != null)
            {
                StopCoroutine(_hideCoroutine);
                _hideCoroutine = null;
            }
        }

        public void Hide()
        {
            _canvasGroup.alpha = 0;
            if (_hideCoroutine != null)
            {
                StopCoroutine(_hideCoroutine);
                _hideCoroutine = null;
            }
        }

        private IEnumerator HideCoroutine(float delay)
        {
            yield return new WaitForSeconds(delay);
            float fadeTime = 0.5f;
            float t = 0;
            while (t < fadeTime)
            {
                t += Time.deltaTime;
                _canvasGroup.alpha = Mathf.Lerp(1, 0, t / fadeTime);
                yield return null;
            }
            _canvasGroup.alpha = 0;
        }
    }
}
