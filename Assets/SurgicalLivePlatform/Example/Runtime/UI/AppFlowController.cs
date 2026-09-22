using UnityEngine;
using System.Collections;
using SurgicalLivePlatform.Runtime.UI;

namespace SurgicalLivePlatform.Example.Runtime.UI
{
    public class AppFlowController : MonoBehaviour
    {
        public static AppFlowController Instance { get; private set; }

        [Header("Panels")]
        [SerializeField] private GameObject loginPanel;
        [SerializeField] private GameObject lobbyPanel;

        [Header("Controllers")]
        [SerializeField] private RoomLobbyView lobbyView;

        [Header("Transition Settings")]
        [SerializeField] private float fadeDuration = 0.5f;

        private CanvasGroup loginGroup;
        private CanvasGroup lobbyGroup;
        private Coroutine transitionCoroutine;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Ensure CanvasGroups exist
            if (loginPanel != null) loginGroup = loginPanel.GetComponent<CanvasGroup>() ?? loginPanel.AddComponent<CanvasGroup>();
            if (lobbyPanel != null) lobbyGroup = lobbyPanel.GetComponent<CanvasGroup>() ?? lobbyPanel.AddComponent<CanvasGroup>();
        }

        private void Start()
        {
            // Initial state: show login immediately, hide lobby
            if (loginPanel != null)
            {
                loginPanel.SetActive(true);
                if (loginGroup != null) loginGroup.alpha = 1f;
            }
            if (lobbyPanel != null)
            {
                lobbyPanel.SetActive(false);
                if (lobbyGroup != null) lobbyGroup.alpha = 0f;
            }
        }

        public void ShowLoginPanel()
        {
            if (transitionCoroutine != null) StopCoroutine(transitionCoroutine);
            transitionCoroutine = StartCoroutine(FadeTransition(lobbyGroup, lobbyPanel, loginGroup, loginPanel));
        }

        public void ShowLobbyPanel()
        {
            if (lobbyView != null)
            {
                // Trigger data refresh before showing
                lobbyView.RefreshRooms();
            }

            if (transitionCoroutine != null) StopCoroutine(transitionCoroutine);
            transitionCoroutine = StartCoroutine(FadeTransition(loginGroup, loginPanel, lobbyGroup, lobbyPanel));
        }

        private IEnumerator FadeTransition(CanvasGroup fromGroup, GameObject fromPanel, CanvasGroup toGroup, GameObject toPanel)
        {
            if (toPanel != null) toPanel.SetActive(true);
            if (toGroup != null) toGroup.alpha = 0f;

            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeDuration;

                // Smooth step for silky feel
                float smoothT = Mathf.SmoothStep(0, 1, t);

                if (fromGroup != null) fromGroup.alpha = 1f - smoothT;
                if (toGroup != null) toGroup.alpha = smoothT;

                yield return null;
            }

            if (fromGroup != null) fromGroup.alpha = 0f;
            if (fromPanel != null) fromPanel.SetActive(false);

            if (toGroup != null) toGroup.alpha = 1f;
        }
    }
}
