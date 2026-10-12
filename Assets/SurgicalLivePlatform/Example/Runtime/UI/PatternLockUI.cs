using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SurgicalLivePlatform.Example.Runtime.UI
{
    [RequireComponent(typeof(RectTransform))]
    public class PatternLockUI : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public Action<string> OnPatternSubmit;

        [Header("UI References")]
        [Tooltip("The 9 nodes in order 0-8")]
        public RectTransform[] nodes;
        
        [Tooltip("Line Renderer template for object pool")]
        public Image lineTemplate; // Used to draw connections, must have a Pivot of (0, 0.5)

        private readonly List<int> _currentPattern = new List<int>(9);
        private readonly List<Image> _activeLines = new List<Image>(9);
        private readonly Stack<Image> _linePool = new Stack<Image>(9);
        
        private Image _liveLine;

        private Canvas _canvas;

        public void Initialize()
        {
            // Auto-assign missing references (useful if Inspector loses them during PlayMode transition)
            if (nodes == null || nodes.Length == 0)
            {
                nodes = new RectTransform[9];
                for (int i = 0; i < 9; i++)
                {
                    Transform t = transform.Find($"Node_{i}");
                    if (t != null) nodes[i] = t.GetComponent<RectTransform>();
                }
            }

            // Attach robust hit detectors to nodes for WorldSpace/XR
            if (nodes != null)
            {
                for (int i = 0; i < nodes.Length; i++)
                {
                    if (nodes[i] != null)
                    {
                        var hitNode = nodes[i].GetComponent<PatternNodeHit>();
                        if (hitNode == null) hitNode = nodes[i].gameObject.AddComponent<PatternNodeHit>();
                        hitNode.NodeIndex = i;
                        hitNode.ParentUI = this;
                    }
                }
            }

            if (lineTemplate == null)
            {
                Transform t = transform.Find("LineTemplate");
                if (t != null) lineTemplate = t.GetComponent<Image>();
            }

            if (lineTemplate != null)
            {
                lineTemplate.gameObject.SetActive(false);
            }
            _canvas = GetComponentInParent<Canvas>();
        }

        private void Start()
        {
            Initialize();
            
            // Fix raycaster in World Space without Event Camera
            if (_canvas != null && _canvas.renderMode == RenderMode.WorldSpace && _canvas.worldCamera == null)
            {
                _canvas.worldCamera = Camera.main;
                if (_canvas.worldCamera == null)
                {
                    _canvas.worldCamera = FindObjectOfType<Camera>();
                }
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            Debug.Log("[PatternLockUI] OnPointerDown triggered at " + eventData.position);
            ClearPattern();
            
            // Check if we directly clicked on a node
            if (eventData.pointerCurrentRaycast.gameObject != null)
            {
                var hitNode = eventData.pointerCurrentRaycast.gameObject.GetComponent<PatternNodeHit>();
                if (hitNode != null)
                {
                    AddNode(hitNode.NodeIndex);
                }
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            UpdateLiveLine(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (_currentPattern.Count > 0)
            {
                var sb = new System.Text.StringBuilder();
                foreach(var idx in _currentPattern) sb.Append(idx + 1); // 1-based index
                string hash = sb.ToString();
                
                Debug.Log($"[PatternLockUI] 密码明文: {hash}");
                OnPatternSubmit?.Invoke(hash);
                ClearPattern();
            }
        }

        // Test hooks and node callback
        public void HandlePointerDown(int nodeIndex)
        {
            ClearPattern();
            AddNode(nodeIndex);
        }

        public void HandleNodeEnter(int nodeIndex)
        {
            AddNode(nodeIndex);
        }

        public void HandlePointerUp()
        {
            if (_currentPattern.Count > 0)
            {
                var sb = new System.Text.StringBuilder();
                foreach(var idx in _currentPattern) sb.Append(idx + 1); // 1-based index
                string hash = sb.ToString();
                
                Debug.Log($"[PatternLockUI] 密码明文: {hash}");
                OnPatternSubmit?.Invoke(hash);
                ClearPattern();
            }
        }

        private void AddNode(int nodeIndex)
        {
            if (!_currentPattern.Contains(nodeIndex))
            {
                _currentPattern.Add(nodeIndex);
                
                // Visual feedback for the node
                if (nodes != null && nodeIndex >= 0 && nodeIndex < nodes.Length && nodes[nodeIndex] != null)
                {
                    var img = nodes[nodeIndex].GetComponent<Image>();
                    if (img != null) img.color = Color.yellow; // Highlight color
                }
                
                DrawLineToLastNode();
            }
        }

        private void DrawLineToLastNode()
        {
            if (_currentPattern.Count < 2 || lineTemplate == null) return;
            
            int prevNodeIndex = _currentPattern[_currentPattern.Count - 2];
            int currNodeIndex = _currentPattern[_currentPattern.Count - 1];

            RectTransform prevNode = nodes[prevNodeIndex];
            RectTransform currNode = nodes[currNodeIndex];

            Image line = GetLineFromPool();
            line.gameObject.SetActive(true);
            
            ConnectToPoint(line.rectTransform, prevNode.position, currNode.position);
            _activeLines.Add(line);
        }

        private void UpdateLiveLine(PointerEventData eventData)
        {
            if (_currentPattern.Count == 0 || lineTemplate == null || _canvas == null) return;
            
            if (_liveLine == null)
            {
                _liveLine = GetLineFromPool();
                _liveLine.gameObject.SetActive(true);
            }
            
            int lastNodeIndex = _currentPattern[_currentPattern.Count - 1];
            if (lastNodeIndex < 0 || lastNodeIndex >= nodes.Length || nodes[lastNodeIndex] == null) return;
            
            RectTransform lastNode = nodes[lastNodeIndex];
            Vector3 worldPoint = Vector3.zero;
            bool hasPoint = false;

            // Prefer world position from XR raycasts if available
            if (eventData.pointerCurrentRaycast.isValid && eventData.pointerCurrentRaycast.worldPosition != Vector3.zero)
            {
                worldPoint = eventData.pointerCurrentRaycast.worldPosition;
                hasPoint = true;
            }
            else if (RectTransformUtility.ScreenPointToWorldPointInRectangle(_canvas.transform as RectTransform, eventData.position, eventData.pressEventCamera, out Vector3 wp))
            {
                worldPoint = wp;
                hasPoint = true;
            }

            if (hasPoint)
            {
                ConnectToPoint(_liveLine.rectTransform, lastNode.position, worldPoint);
            }
        }

        private void ConnectToPoint(RectTransform lineRect, Vector3 startPos, Vector3 endPos)
        {
            
            Vector3 dir = endPos - startPos;
            float distance = 0;
            if (lineRect.lossyScale.x != 0) {
                distance = dir.magnitude / lineRect.lossyScale.x;
            }
            
            lineRect.position = startPos;
            
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            lineRect.rotation = Quaternion.Euler(0, 0, angle);
            
            lineRect.sizeDelta = new Vector2(distance, lineRect.sizeDelta.y);
        }

        private Image GetLineFromPool()
        {
            if (_linePool.Count > 0)
            {
                return _linePool.Pop();
            }
            return Instantiate(lineTemplate, lineTemplate.transform.parent);
        }

        private void ClearPattern()
        {
            // Reset node colors
            if (nodes != null)
            {
                foreach (var index in _currentPattern)
                {
                    if (index >= 0 && index < nodes.Length && nodes[index] != null)
                    {
                        var img = nodes[index].GetComponent<Image>();
                        if (img != null) img.color = Color.white; // Default color
                    }
                }
            }

            _currentPattern.Clear();
            foreach (var line in _activeLines)
            {
                line.gameObject.SetActive(false);
                _linePool.Push(line);
            }
            _activeLines.Clear();

            if (_liveLine != null)
            {
                _liveLine.gameObject.SetActive(false);
                _linePool.Push(_liveLine);
                _liveLine = null;
            }
        }
    }
    
    /// <summary>
    /// Helper component dynamically attached to each node to accurately catch Pointer Enter events
    /// in World Space and XR environments without relying on screen-space rectangle math.
    /// Added all interfaces to prevent bubbling issues in custom XR input modules.
    /// </summary>
    public class PatternNodeHit : MonoBehaviour, IPointerEnterHandler, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        public int NodeIndex;
        public PatternLockUI ParentUI;

        private void EnsureCollider()
        {
            // XR Physics Raycasters require colliders. Auto-add if missing.
            var col = GetComponent<BoxCollider>();
            if (col == null)
            {
                var rt = GetComponent<RectTransform>();
                if (rt != null)
                {
                    col = gameObject.AddComponent<BoxCollider>();
                    col.size = new Vector3(rt.rect.width, rt.rect.height, 0.1f);
                }
            }
        }

        private void Start()
        {
            EnsureCollider();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (ParentUI != null) ParentUI.HandlePointerDown(NodeIndex);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            // If dragging is active (pointerPress is set), add node to pattern
            if (eventData.pointerPress != null || eventData.dragging)
            {
                if (ParentUI != null)
                {
                    ParentUI.HandleNodeEnter(NodeIndex);
                }
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            // Forward drag to parent so live tracking line works
            if (ParentUI != null) ParentUI.OnDrag(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (ParentUI != null) ParentUI.HandlePointerUp();
        }
    }
}
