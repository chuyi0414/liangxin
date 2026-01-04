using CYFramework.Core.UI.MVVM; // ObservableProperty 引用
using TMPro; // TextMeshPro 引用
using UnityEngine; // Unity 引擎类型引用
using UnityEngine.UI; // UI 组件引用

/// <summary>
/// 单位血条 UI 项（绑定目标并由管理器驱动位置更新）。
/// </summary>
public sealed class UnitHpBarItem : MonoBehaviour // 血条 UI 组件
{
    /// <summary>血条填充图（Image 设置为 Filled）。</summary>
    [SerializeField] private Image _fillImage;
    /// <summary>血量文本（可选）。</summary>
    [SerializeField] private TMP_Text _hpText;
    /// <summary>可见性控制（可选）。</summary>
    [SerializeField] private CanvasGroup _canvasGroup;

    /// <summary>缓存 RectTransform。</summary>
    private RectTransform _rectTransform;
    /// <summary>绑定的单位实体。</summary>
    private UnitEntity _target;
    /// <summary>绑定单位的 Transform。</summary>
    private Transform _targetTransform;
    /// <summary>世界坐标偏移（用于显示在头顶）。</summary>
    private Vector2 _worldOffset;
    /// <summary>血条 ViewModel（Typed MVVM）。</summary>
    private UnitHpBarViewModel _viewModel;

    /// <summary>是否已完成初始化。</summary>
    private bool _initialized;
    /// <summary>当前可见状态。</summary>
    private bool _isVisible;
    /// <summary>血量文本是否需要刷新。</summary>
    private bool _hpTextDirty;

    /// <summary>当前绑定单位（只读）。</summary>
    public UnitEntity Target => _target;

    /// <summary>是否仍有有效目标。</summary>
    public bool HasTarget => _targetTransform != null;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>(); // 缓存 RectTransform
        if (_canvasGroup == null)
        {
            _canvasGroup = GetComponent<CanvasGroup>(); // 尝试自动缓存 CanvasGroup
            if (_canvasGroup == null)
            {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>(); // 运行时补充 CanvasGroup
            }
        }

        if (_canvasGroup != null)
        {
            _canvasGroup.blocksRaycasts = false; // 禁用射线阻挡
            _canvasGroup.interactable = false; // 禁用交互
        }

        _viewModel = new UnitHpBarViewModel(); // 创建 ViewModel
        _viewModel.Initialize(); // 初始化 ViewModel
        _viewModel.BindHpRatio(OnHpRatioChanged); // 绑定血量比例更新
        _viewModel.BindCurrentHp(OnCurrentHpChanged); // 绑定当前血量更新
        _viewModel.BindMaxHp(OnMaxHpChanged); // 绑定最大血量更新
        _initialized = true; // 标记初始化完成
        _isVisible = true; // 初始化可见状态
        _hpTextDirty = true; // 初始化文本脏标记
    }

    private void OnDestroy()
    {
        _viewModel?.Dispose();
        _viewModel = null;
    }

    /// <summary>
    /// 绑定单位与偏移。
    /// </summary>
    /// <param name="unit">目标单位。</param>
    /// <param name="baseOffset">基础偏移（世界坐标）。</param>
    /// <param name="useColliderTop">是否使用碰撞体顶部作为额外偏移。</param>
    public void Bind(UnitEntity unit, Vector2 baseOffset, bool useColliderTop)
    {
        _target = unit; // 缓存目标实体
        _targetTransform = unit != null ? unit.transform : null; // 缓存目标 Transform
        _worldOffset = baseOffset; // 缓存基础偏移
        SetVisible(true); // 重置可见状态

        if (useColliderTop && _targetTransform != null)
        {
            var collider = unit.GetComponent<Collider2D>();
            if (collider != null && collider.enabled)
            {
                var topOffset = collider.bounds.max.y - _targetTransform.position.y;
                _worldOffset.y += topOffset;
            }
        }
    }

    /// <summary>
    /// 解除绑定。
    /// </summary>
    public void Unbind()
    {
        _target = null; // 清空目标实体
        _targetTransform = null; // 清空目标 Transform
    }

    /// <summary>
    /// 设置生命值（驱动 ViewModel）。
    /// </summary>
    /// <param name="current">当前生命。</param>
    /// <param name="max">最大生命。</param>
    public void SetHp(int current, int max)
    {
        if (!_initialized)
        {
            return;
        }

        _viewModel.SetHp(current, max);
    }

    /// <summary>
    /// 更新 UI 位置（由管理器每帧调用）。
    /// </summary>
    /// <param name="root">UI 根节点。</param>
    /// <param name="worldCamera">世界相机。</param>
    /// <param name="uiCamera">UI 相机（Overlay 可为 null）。</param>
    public void UpdatePosition(RectTransform root, Camera worldCamera, Camera uiCamera)
    {
        if (_targetTransform == null || root == null || worldCamera == null || _rectTransform == null)
        {
            SetVisible(false); // 依赖缺失时隐藏
            return; // 结束位置刷新
        }

        var worldPos = _targetTransform.position; // 获取目标世界坐标
        worldPos.x += _worldOffset.x; // 应用 X 偏移
        worldPos.y += _worldOffset.y; // 应用 Y 偏移
        var screenPos = worldCamera.WorldToScreenPoint(worldPos); // 世界转屏幕坐标
        if (screenPos.z <= 0f)
        {
            SetVisible(false); // 相机背面时隐藏
            return; // 结束位置刷新
        }

        if (!IsScreenPointVisible(screenPos, uiCamera))
        {
            SetVisible(false); // 不在 UI 相机视口时隐藏
            return; // 结束位置刷新
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screenPos, uiCamera, out var localPos))
        {
            SetVisible(false); // 转换失败时隐藏
            return; // 结束位置刷新
        }

        if (!root.rect.Contains(localPos))
        {
            SetVisible(false); // 不在 UI 根节点范围内
            return; // 结束位置刷新
        }

        _rectTransform.anchoredPosition = localPos; // 更新 UI 位置
        SetVisible(true); // 更新为可见状态
    }

    /// <summary>
    /// 血量比例变化回调。
    /// </summary>
    private void OnHpRatioChanged(ref ObservableProperty<float>.ChangedEventArgs args)
    {
        if (_fillImage == null)
        {
            return;
        }

        _fillImage.fillAmount = args.NewValue;
    }

    /// <summary>
    /// 当前生命变化回调。
    /// </summary>
    private void OnCurrentHpChanged(ref ObservableProperty<int>.ChangedEventArgs args)
    {
        MarkHpTextDirty(); // 标记血量文本需要刷新
    }

    /// <summary>
    /// 最大生命变化回调。
    /// </summary>
    private void OnMaxHpChanged(ref ObservableProperty<int>.ChangedEventArgs args)
    {
        MarkHpTextDirty(); // 标记血量文本需要刷新
    }

    /// <summary>
    /// 标记血量文本需要刷新。
    /// </summary>
    private void MarkHpTextDirty()
    {
        _hpTextDirty = true; // 标记文本脏
        if (_isVisible)
        {
            RefreshHpText(); // 可见时立即刷新
        }
    }

    /// <summary>
    /// 刷新血量文本显示。
    /// </summary>
    private void RefreshHpText()
    {
        if (_hpText == null || _viewModel == null)
        {
            return;
        }

        var maxHp = _viewModel.MaxHp.Value;
        if (maxHp <= 0)
        {
            _hpText.SetText("--"); // 最大生命无效时显示占位
            _hpTextDirty = false; // 清理脏标记
            return; // 结束文本刷新
        }

        _hpText.SetText("{0}/{1}", _viewModel.CurrentHp.Value, maxHp); // 更新血量文本
        _hpTextDirty = false; // 清理脏标记
    }

    /// <summary>
    /// 设置可见状态（离屏隐藏渲染）。
    /// </summary>
    /// <param name="visible">是否可见。</param>
    private void SetVisible(bool visible)
    {
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = visible ? 1f : 0f; // 使用 CanvasGroup 控制可见性
        }
        else
        {
            if (_fillImage != null)
            {
                _fillImage.enabled = visible; // 控制血条显示
            }

            if (_hpText != null)
            {
                _hpText.enabled = visible; // 控制文本显示
            }
        }

        _isVisible = visible; // 缓存可见状态
        if (_isVisible && _hpTextDirty)
        {
            RefreshHpText(); // 恢复可见时补一次文本刷新
        }
    }

    /// <summary>
    /// 判断屏幕点是否在 UI 相机视口内。
    /// </summary>
    /// <param name="screenPos">屏幕坐标。</param>
    /// <param name="uiCamera">UI 相机。</param>
    private bool IsScreenPointVisible(Vector3 screenPos, Camera uiCamera)
    {
        if (uiCamera != null)
        {
            var rect = uiCamera.pixelRect; // UI 相机视口范围
            return screenPos.x >= rect.xMin && screenPos.x <= rect.xMax // 视口 X 范围判断
                && screenPos.y >= rect.yMin && screenPos.y <= rect.yMax; // 视口 Y 范围判断
        }

        return screenPos.x >= 0f && screenPos.x <= Screen.width // 屏幕 X 范围判断
            && screenPos.y >= 0f && screenPos.y <= Screen.height; // 屏幕 Y 范围判断
    }
}
