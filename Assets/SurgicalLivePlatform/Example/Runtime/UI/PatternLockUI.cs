using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SurgicalLivePlatform.Example.Runtime.UI
{
    /// <summary>
    /// 图案密码锁 UI 控制器 (Pattern Lock UI Controller)
    /// 专为 MR/XR 空间交互与 WorldSpace UI 设计的九宫格手势图案密码绘制组件。
    /// 负责节点状态跟踪、实时动态引线渲染、密码序列暂存（Caching）、对象池复用，
    /// 以及与 <see cref="UserLoginUIController"/> 之间的数据填充与面板显隐回退联动。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class PatternLockUI : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        /// <summary>
        /// 图案密码绘制提交事件，参数为 1-based 的节点索引拼接明文字符串（如 "12369"）
        /// </summary>
        public Action<string> OnPatternSubmit;

        [Header("UI References")]
        /// <summary>
        /// 九宫格中 9 个节点的 RectTransform 引用，按索引 0-8 依次对应九宫格位置
        /// </summary>
        [Tooltip("The 9 nodes in order 0-8")]
        public RectTransform[] nodes;
        
        /// <summary>
        /// 连线渲染模板图片（Pivot 必须预设为 (0, 0.5)），供对象池实例化复用
        /// </summary>
        [Tooltip("Line Renderer template for object pool")]
        public Image lineTemplate; // Used to draw connections, must have a Pivot of (0, 0.5)

        [Header("Control Buttons")]
        /// <summary>
        /// 确认按钮：触发将当前暂存的图案密码同步回填至 UserLoginUIController 并切回主登录面板
        /// </summary>
        public Button sureButton;

        /// <summary>
        /// 重置按钮：清空当前缓存密码与绘制面板，恢复至初始可输入状态
        /// </summary>
        public Button resetButton;

        /// <summary>
        /// 关闭按钮：清空缓存并退出图案锁面板，重新激活主登录面板
        /// </summary>
        public Button closeButton;

        /// <summary>
        /// 关联的主登录视图控制器引用，用于跨面板回传密码与显隐控制（支持运行时自愈寻路）
        /// </summary>
        public UserLoginUIController loginUIController;

        [Header("XR Interaction Settings")]
        /// <summary>
        /// 最小图案节点数要求（低于该长度抬起手势视为取消，防止超界伪提交，默认 4）
        /// </summary>
        [Tooltip("Minimum pattern node count required for a valid password")]
        [SerializeField] private int minPatternLength = 4;

        /// <summary>
        /// 节点距离吸附半径（本地空间像素），用于拖拽过程中的防穿透与跟手检测
        /// </summary>
        [Tooltip("Capture radius in Canvas local space units")]
        [SerializeField] private float captureRadius = 50f;

        public int MinPatternLength
        {
            get => minPatternLength;
            set => minPatternLength = value;
        }

        public float CaptureRadius
        {
            get => captureRadius;
            set => captureRadius = value;
        }

        /// <summary>
        /// 私有密码缓存变量：手势绘制抬起时实时暂存计算得到的密码明文字符串
        /// </summary>
        private string _cachedPassword = string.Empty;

        /// <summary>
        /// 获取当前暂存的图案密码明文字符串（只读暴露，供外部校验与单元测试断言）
        /// </summary>
        public string CachedPassword => _cachedPassword;

        /// <summary>
        /// 获取当前绘制路径中已选中的节点数量
        /// </summary>
        public int CurrentPatternCount => _currentPattern.Count;

        /// <summary>
        /// 暴露当前激活连线集合（只读，供单测验证）
        /// </summary>
        public IReadOnlyList<Image> ActiveLines => _activeLines;

        /// <summary>
        /// 暴露当前动态引导线（只读，供单测验证）
        /// </summary>
        public Image LiveLine => _liveLine;

        /// <summary>
        /// 当前已连结的节点索引序列容器
        /// </summary>
        private readonly List<int> _currentPattern = new List<int>(9);

        /// <summary>
        /// 当前场景中已激活展示的静态连线集合
        /// </summary>
        private readonly List<Image> _activeLines = new List<Image>(9);

        /// <summary>
        /// 连线对象池堆栈，避免在拖拽绘制过程中产生频繁的 GC 堆内存分配
        /// </summary>
        private readonly Stack<Image> _linePool = new Stack<Image>(9);
        
        /// <summary>
        /// 手指或射线正在拖拽时的动态实时连线实例
        /// </summary>
        private Image _liveLine;

        /// <summary>
        /// 父级 Canvas 引用，用于 WorldSpace 空间坐标换算与事件相机判定
        /// </summary>
        private Canvas _canvas;

        /// <summary>
        /// 控制按钮事件是否已绑定的防重标识
        /// </summary>
        private bool _isButtonEventsBound = false;

        /// <summary>
        /// 初始化组件依赖项，包含节点寻路、碰撞体挂载、连线对象池模板预设及按钮事件绑定。
        /// 具备自愈能力，杜绝因 Inspector 丢失拖拽导致的空引用。
        /// </summary>
        public void Initialize()
        {
            // 自动补齐缺失的 9 个节点引用（应对运行时 Inspector 拖拽丢失）
            if (nodes == null || nodes.Length == 0)
            {
                nodes = new RectTransform[9];
                for (int i = 0; i < 9; i++)
                {
                    Transform t = transform.Find($"Node_{i}");
                    if (t != null) nodes[i] = t.GetComponent<RectTransform>();
                }
            }

            // 为每个节点挂载碰撞代理以确保在 WorldSpace / XR 环境下拾取精准
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
                lineTemplate.raycastTarget = false;
                lineTemplate.gameObject.SetActive(false);
            }
            _canvas = GetComponentInParent<Canvas>();

            AutoBindButtons();
            BindButtonEvents();
        }

        /// <summary>
        /// 自动寻路并绑定确认、重置、关闭按钮及 UserLoginUIController 实例
        /// </summary>
        private void AutoBindButtons()
        {
            if (sureButton == null)
            {
                sureButton = FindButtonByNameOrKeyword("sure", "确定", "ok", "confirm");
            }
            if (resetButton == null)
            {
                resetButton = FindButtonByNameOrKeyword("reset", "重置", "clear");
            }
            if (closeButton == null)
            {
                closeButton = FindButtonByNameOrKeyword("close", "关闭", "back", "cancel");
            }
            if (loginUIController == null)
            {
                loginUIController = GetComponentInParent<UserLoginUIController>();
                if (loginUIController == null)
                {
                    loginUIController = FindObjectOfType<UserLoginUIController>();
                }
            }
        }

        /// <summary>
        /// 根据精确名称或模糊关键字在子级中检索目标按钮
        /// </summary>
        /// <param name="keywords">匹配关键字列表</param>
        /// <returns>匹配到的 Button 实例，未找到返回 null</returns>
        private Button FindButtonByNameOrKeyword(params string[] keywords)
        {
            var buttons = GetComponentsInChildren<Button>(true);
            foreach (var btn in buttons)
            {
                foreach (var kw in keywords)
                {
                    if (btn.gameObject.name.Equals(kw, StringComparison.OrdinalIgnoreCase))
                    {
                        return btn;
                    }
                }
            }

            foreach (var btn in buttons)
            {
                string objName = btn.gameObject.name;
                foreach (var kw in keywords)
                {
                    if (objName.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return btn;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// 注册确认、重置、关闭等按钮的点击监听事件
        /// </summary>
        public void BindButtonEvents()
        {
            if (_isButtonEventsBound) return;

            if (sureButton != null)
            {
                sureButton.onClick.AddListener(OnSureButtonClicked);
            }
            if (resetButton != null)
            {
                resetButton.onClick.AddListener(OnResetButtonClicked);
            }
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(OnCloseButtonClicked);
            }

            _isButtonEventsBound = true;
        }

        /// <summary>
        /// 注销各控制按钮的点击监听事件，防止内存泄漏或重复调用
        /// </summary>
        public void UnbindButtonEvents()
        {
            if (!_isButtonEventsBound) return;

            if (sureButton != null)
            {
                sureButton.onClick.RemoveListener(OnSureButtonClicked);
            }
            if (resetButton != null)
            {
                resetButton.onClick.RemoveListener(OnResetButtonClicked);
            }
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(OnCloseButtonClicked);
            }

            _isButtonEventsBound = false;
        }

        /// <summary>
        /// 销毁生命周期，清理注册的按钮事件监听
        /// </summary>
        private void OnDestroy()
        {
            UnbindButtonEvents();
        }

        /// <summary>
        /// 确认（sure）按钮点击事件处理逻辑
        /// 校验当前缓存的密码有效性，并将参数回填至 UserLoginUIController，随后平滑回退至主登录界面
        /// </summary>
        public void OnSureButtonClicked()
        {
            if (string.IsNullOrEmpty(_cachedPassword))
            {
                Debug.LogWarning("[PatternLockUI] 尚未绘制有效图案密码，拒绝确认");
                return;
            }

            if (loginUIController != null)
            {
                loginUIController.password = _cachedPassword;
            }

            // 完成填充后回退到原始登录面板并清空临时缓存
            OnCloseButtonClicked();
        }

        /// <summary>
        /// 重置（reset）按钮点击事件处理逻辑
        /// 清空密码缓存变量，并调用 ClearPattern 清除连线高亮，恢复至初始可输入状态
        /// </summary>
        public void OnResetButtonClicked()
        {
            _cachedPassword = string.Empty;
            ClearPattern();
        }

        /// <summary>
        /// 关闭（close）按钮点击事件处理逻辑
        /// 清空密码缓存与绘制面板，执行界面回退：隐藏图案锁面板，重新激活主登录面板
        /// </summary>
        public void OnCloseButtonClicked()
        {
            _cachedPassword = string.Empty;
            ClearPattern();

            if (loginUIController != null)
            {
                loginUIController.ShowLoginPanel();
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Unity 生命周期 Start，完成基础初始化并自适应修复 WorldSpace Canvas 的渲染相机
        /// </summary>
        private void Start()
        {
            Initialize();
            
            // 修复 WorldSpace 下若未绑定 Event Camera 导致的射线检测失效
            if (_canvas != null && _canvas.renderMode == RenderMode.WorldSpace && _canvas.worldCamera == null)
            {
                _canvas.worldCamera = Camera.main;
                if (_canvas.worldCamera == null)
                {
                    _canvas.worldCamera = FindObjectOfType<Camera>();
                }
            }
        }

        /// <summary>
        /// 指针按下事件处理，清空旧图案并检测是否直接点击到首个节点
        /// </summary>
        /// <param name="eventData">指针事件数据</param>
        public void OnPointerDown(PointerEventData eventData)
        {
            Debug.Log("[PatternLockUI] OnPointerDown triggered at " + eventData.position);
            ClearPattern();
            
            // 检查按下瞬间是否直接命中九宫格节点
            if (eventData.pointerCurrentRaycast.gameObject != null)
            {
                var hitNode = eventData.pointerCurrentRaycast.gameObject.GetComponent<PatternNodeHit>();
                if (hitNode != null)
                {
                    AddNode(hitNode.NodeIndex);
                }
            }

            // 若未直接命中节点，执行首触距离吸附
            if (_currentPattern.Count == 0 && eventData.pointerCurrentRaycast.worldPosition != Vector3.zero)
            {
                Vector3 wp = eventData.pointerCurrentRaycast.worldPosition;
                if (!float.IsNaN(wp.x) && !float.IsInfinity(wp.x))
                {
                    Transform parentTrans = lineTemplate != null && lineTemplate.transform.parent != null
                        ? lineTemplate.transform.parent
                        : transform;
                    Vector3 localPointer = parentTrans.InverseTransformPoint(wp);
                    CheckProximityCapture(localPointer, parentTrans);
                }
            }
        }

        /// <summary>
        /// 连续就近吸附检测：在本地空间比对射线当前位置与各节点间距，解决快速划过的漏触穿透
        /// </summary>
        /// <param name="localPointer">当前射线在父级局部平面的坐标</param>
        /// <param name="parentTrans">本地参考系 Transform</param>
        private void CheckProximityCapture(Vector3 localPointer, Transform parentTrans)
        {
            if (nodes == null) return;
            float sqrRadius = captureRadius * captureRadius;

            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i] == null || _currentPattern.Contains(i)) continue;

                Vector3 nodeLocal = nodes[i].parent == parentTrans
                    ? nodes[i].localPosition
                    : parentTrans.InverseTransformPoint(nodes[i].position);

                float dx = localPointer.x - nodeLocal.x;
                float dy = localPointer.y - nodeLocal.y;
                float sqrDist = dx * dx + dy * dy;

                if (sqrDist <= sqrRadius)
                {
                    AddNode(i);
                }
            }
        }

        /// <summary>
        /// 指针拖拽事件处理，驱动最后一节点至当前指针位置的动态引线渲染
        /// </summary>
        /// <param name="eventData">指针事件数据</param>
        public void OnDrag(PointerEventData eventData)
        {
            UpdateLiveLine(eventData);
        }

        /// <summary>
        /// 指针抬起事件处理，触发密码缓存与提交
        /// </summary>
        /// <param name="eventData">指针事件数据</param>
        public void OnPointerUp(PointerEventData eventData)
        {
            HandlePointerUp();
        }

        /// <summary>
        /// 外部测试钩子与节点按下转发回调
        /// </summary>
        /// <param name="nodeIndex">按下的节点索引 (0-8)</param>
        public void HandlePointerDown(int nodeIndex)
        {
            ClearPattern();
            AddNode(nodeIndex);
        }

        /// <summary>
        /// 节点悬停触碰回调，用于手势拖拽时连接新节点
        /// </summary>
        /// <param name="nodeIndex">进入的节点索引 (0-8)</param>
        public void HandleNodeEnter(int nodeIndex)
        {
            AddNode(nodeIndex);
        }

        /// <summary>
        /// 指针抬起核心逻辑：计算并缓存密码明文，保留图案连线供用户核对，触发提交通知。
        /// 具备超界追踪丢失保护，立即回收动态引导线，低于最小节点数时自动重置防伪提。
        /// </summary>
        public void HandlePointerUp()
        {
            // 立即回收动态引导线，防止超界/释放后残留
            if (_liveLine != null)
            {
                _liveLine.gameObject.SetActive(false);
                _linePool.Push(_liveLine);
                _liveLine = null;
            }

            if (_currentPattern.Count >= minPatternLength)
            {
                var sb = new System.Text.StringBuilder();
                foreach (var idx in _currentPattern) sb.Append(idx + 1); // 1-based index (1-9)
                _cachedPassword = sb.ToString();
                
                Debug.Log($"[PatternLockUI] 密码已缓存: {_cachedPassword}");
                OnPatternSubmit?.Invoke(_cachedPassword);
                // 保持图案连线显示供核对，不调用 ClearPattern()
            }
            else
            {
                if (_currentPattern.Count > 0)
                {
                    Debug.LogWarning($"[PatternLockUI] 手势节点数不足 ({_currentPattern.Count} < {minPatternLength})，已重置");
                }
                ClearPattern();
            }
        }

        /// <summary>
        /// 将指定索引的节点添加到当前图案序列中，更新高亮并绘制与上一节点的连接线段
        /// </summary>
        /// <param name="nodeIndex">待加入的节点索引 (0-8)</param>
        private void AddNode(int nodeIndex)
        {
            if (!_currentPattern.Contains(nodeIndex))
            {
                _currentPattern.Add(nodeIndex);
                
                // 节点视觉反馈（黄色高亮）
                if (nodes != null && nodeIndex >= 0 && nodeIndex < nodes.Length && nodes[nodeIndex] != null)
                {
                    var img = nodes[nodeIndex].GetComponent<Image>();
                    if (img != null) img.color = Color.yellow;
                }
                
                DrawLineToLastNode();
            }
        }

        /// <summary>
        /// 从对象池获取线段实例，绘制倒数第二个节点与当前节点之间的静态连线
        /// </summary>
        private void DrawLineToLastNode()
        {
            if (_currentPattern.Count < 2 || lineTemplate == null) return;
            
            int prevNodeIndex = _currentPattern[_currentPattern.Count - 2];
            int currNodeIndex = _currentPattern[_currentPattern.Count - 1];

            if (nodes == null || prevNodeIndex >= nodes.Length || currNodeIndex >= nodes.Length) return;
            RectTransform prevNode = nodes[prevNodeIndex];
            RectTransform currNode = nodes[currNodeIndex];
            if (prevNode == null || currNode == null) return;

            Image line = GetLineFromPool();
            line.gameObject.SetActive(true);
            
            ConnectToPoint(line.rectTransform, prevNode.position, currNode.position);
            _activeLines.Add(line);
        }

        /// <summary>
        /// 实时更新当前手指/射线拖拽时的动态引导线（Live Line），并驱动连续吸附
        /// </summary>
        /// <param name="eventData">射线拾取与屏幕指针事件数据</param>
        private void UpdateLiveLine(PointerEventData eventData)
        {
            if (lineTemplate == null) return;
            if (_canvas == null) _canvas = GetComponentInParent<Canvas>();

            Transform parentTrans = lineTemplate.transform.parent != null
                ? lineTemplate.transform.parent
                : transform;

            Vector3 worldPoint = Vector3.zero;
            bool hasPoint = false;

            // 优先采用 XR 物理射线检测返回的世界坐标
            if (eventData.pointerCurrentRaycast.worldPosition != Vector3.zero)
            {
                worldPoint = eventData.pointerCurrentRaycast.worldPosition;
                hasPoint = !float.IsNaN(worldPoint.x) && !float.IsInfinity(worldPoint.x);
            }
            else if (_canvas != null && RectTransformUtility.ScreenPointToWorldPointInRectangle(_canvas.transform as RectTransform, eventData.position, eventData.pressEventCamera, out Vector3 wp))
            {
                worldPoint = wp;
                hasPoint = !float.IsNaN(worldPoint.x) && !float.IsInfinity(worldPoint.x);
            }

            if (!hasPoint)
            {
                // 超界或坐标失效，临时隐藏引导线以防飞线
                if (_liveLine != null && _liveLine.gameObject.activeSelf)
                {
                    _liveLine.gameObject.SetActive(false);
                }
                return;
            }

            // 驱动局部距离吸附检测
            Vector3 localPointer = parentTrans.InverseTransformPoint(worldPoint);
            CheckProximityCapture(localPointer, parentTrans);

            if (_currentPattern.Count == 0)
            {
                if (_liveLine != null && _liveLine.gameObject.activeSelf)
                {
                    _liveLine.gameObject.SetActive(false);
                }
                return;
            }

            if (_liveLine == null)
            {
                _liveLine = GetLineFromPool();
            }
            _liveLine.gameObject.SetActive(true);

            int lastNodeIndex = _currentPattern[_currentPattern.Count - 1];
            if (nodes != null && lastNodeIndex >= 0 && lastNodeIndex < nodes.Length && nodes[lastNodeIndex] != null)
            {
                RectTransform lastNode = nodes[lastNodeIndex];
                ConnectToPoint(_liveLine.rectTransform, lastNode.position, worldPoint);
            }
        }

        /// <summary>
        /// 空间几何换算：在父级局部空间计算相对方向、旋转角度与跨度，彻底解决 WorldSpace Canvas 3D 朝向引起的线段脱节
        /// </summary>
        /// <param name="lineRect">线段的 RectTransform</param>
        /// <param name="startPos">起点世界坐标</param>
        /// <param name="endPos">终点世界坐标</param>
        private void ConnectToPoint(RectTransform lineRect, Vector3 startPos, Vector3 endPos)
        {
            Transform parentTrans = lineRect.parent != null ? lineRect.parent : transform;
            Vector3 localStart = parentTrans.InverseTransformPoint(startPos);
            Vector3 localEnd = parentTrans.InverseTransformPoint(endPos);

            // 锁定在局部 2D 平面，消除 Z 轴倾斜，保持深度贴合
            localStart.z = 0f;
            localEnd.z = 0f;

            Vector3 localDir = localEnd - localStart;
            float distance = localDir.magnitude;

            lineRect.localPosition = localStart;

            float angle = Mathf.Atan2(localDir.y, localDir.x) * Mathf.Rad2Deg;
            lineRect.localRotation = Quaternion.Euler(0f, 0f, angle);

            lineRect.sizeDelta = new Vector2(distance, lineRect.sizeDelta.y);
        }

        /// <summary>
        /// 对象池复用机制：获取或新建线段渲染 Image 实例，避免高频堆内存分配，并确保射线穿透 (raycastTarget = false)
        /// </summary>
        /// <returns>可用的 Image 连线实例</returns>
        private Image GetLineFromPool()
        {
            Image line;
            if (_linePool.Count > 0)
            {
                line = _linePool.Pop();
            }
            else
            {
                line = Instantiate(lineTemplate, lineTemplate.transform.parent);
            }
            line.raycastTarget = false;
            return line;
        }

        /// <summary>
        /// 重置图案锁面板：恢复所有节点默认颜色，回收激活连线及动态线段至对象池
        /// </summary>
        private void ClearPattern()
        {
            // 还原节点高亮颜色为白色
            if (nodes != null)
            {
                foreach (var index in _currentPattern)
                {
                    if (index >= 0 && index < nodes.Length && nodes[index] != null)
                    {
                        var img = nodes[index].GetComponent<Image>();
                        if (img != null) img.color = Color.white;
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
    /// 动态挂载于每个节点上的碰撞与事件穿透代理组件 (Pattern Node Hit Proxy)
    /// 解决在 XR WorldSpace 空间射线碰撞以及屏幕空间转换下的 PointerEnter 精度与事件冒泡问题。
    /// </summary>
    public class PatternNodeHit : MonoBehaviour, IPointerEnterHandler, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        /// <summary>
        /// 当前节点在九宫格中的索引 (0-8)
        /// </summary>
        public int NodeIndex;

        /// <summary>
        /// 所属的主 PatternLockUI 实例引用
        /// </summary>
        public PatternLockUI ParentUI;

        /// <summary>
        /// XR Physics Raycaster 需要 Collider 支持，缺失时动态增补 BoxCollider
        /// </summary>
        private void EnsureCollider()
        {
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
            // 拖拽激活中且射线扫过节点时，触发节点纳入图案序列
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
            // 转发拖拽事件至父级 UI，以保持动态实时连线跟随
            if (ParentUI != null) ParentUI.OnDrag(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (ParentUI != null) ParentUI.HandlePointerUp();
        }
    }
}
