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

    /// <summary>当前绑定单位（只读）。</summary>
    public UnitEntity Target => _target;

    /// <summary>是否仍有有效目标。</summary>
    public bool HasTarget => _targetTransform != null;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        _viewModel = new UnitHpBarViewModel();
        _viewModel.Initialize();
        _viewModel.BindHpRatio(OnHpRatioChanged);
        _viewModel.BindCurrentHp(OnCurrentHpChanged);
        _viewModel.BindMaxHp(OnMaxHpChanged);
        _initialized = true;
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
        _target = unit;
        _targetTransform = unit != null ? unit.transform : null;
        _worldOffset = baseOffset;

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
        _target = null;
        _targetTransform = null;
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
            return;
        }

        var worldPos = _targetTransform.position;
        worldPos.x += _worldOffset.x;
        worldPos.y += _worldOffset.y;
        var screenPos = worldCamera.WorldToScreenPoint(worldPos);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screenPos, uiCamera, out var localPos))
        {
            _rectTransform.anchoredPosition = localPos;
        }
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
        RefreshHpText();
    }

    /// <summary>
    /// 最大生命变化回调。
    /// </summary>
    private void OnMaxHpChanged(ref ObservableProperty<int>.ChangedEventArgs args)
    {
        RefreshHpText();
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
            _hpText.SetText("--");
            return;
        }

        _hpText.SetText("{0}/{1}", _viewModel.CurrentHp.Value, maxHp);
    }
}
