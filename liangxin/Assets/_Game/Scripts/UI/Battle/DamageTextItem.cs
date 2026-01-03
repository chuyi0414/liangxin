using TMPro; // TextMeshPro 引用
using UnityEngine; // Unity 引擎类型引用

/// <summary>
/// 伤害飘字 UI 项（由管理器驱动刷新）。
/// </summary>
public sealed class DamageTextItem : MonoBehaviour // 伤害飘字组件
{
    /// <summary>伤害文本。</summary>
    [SerializeField] private TMP_Text _text;
    /// <summary>透明度控制（可选）。</summary>
    [SerializeField] private CanvasGroup _canvasGroup;
    /// <summary>生命周期（秒）。</summary>
    [SerializeField] private float _lifeTime = 0.8f;
    /// <summary>淡出时长（秒）。</summary>
    [SerializeField] private float _fadeDuration = 0.2f;
    /// <summary>上浮速度（世界单位/秒）。</summary>
    [SerializeField] private float _riseSpeed = 0.8f;
    /// <summary>普通伤害颜色。</summary>
    [SerializeField] private Color _normalColor = new Color(1f, 1f, 1f, 1f);
    /// <summary>暴击伤害颜色。</summary>
    [SerializeField] private Color _critColor = new Color(1f, 0.25f, 0.25f, 1f);

    /// <summary>缓存 RectTransform。</summary>
    private RectTransform _rectTransform;
    /// <summary>当前世界坐标。</summary>
    private Vector2 _worldPosition;
    /// <summary>已运行时间。</summary>
    private float _elapsed;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
    }

    /// <summary>
    /// 初始化并显示伤害飘字。
    /// </summary>
    /// <param name="worldPos">世界坐标。</param>
    /// <param name="damage">伤害值。</param>
    /// <param name="isCrit">是否暴击。</param>
    public void Show(Vector2 worldPos, int damage, bool isCrit)
    {
        _worldPosition = worldPos;
        _elapsed = 0f;

        if (_text != null)
        {
            _text.color = isCrit ? _critColor : _normalColor;
            _text.SetText("{0}", damage);
        }

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 1f;
        }
    }

    /// <summary>
    /// 刷新飘字（返回是否仍有效）。
    /// </summary>
    /// <param name="deltaTime">帧间隔。</param>
    /// <param name="root">UI 根节点。</param>
    /// <param name="worldCamera">世界相机。</param>
    /// <param name="uiCamera">UI 相机（Overlay 可为 null）。</param>
    public bool Tick(float deltaTime, RectTransform root, Camera worldCamera, Camera uiCamera)
    {
        _elapsed += deltaTime;
        if (_elapsed >= _lifeTime)
        {
            return false;
        }

        _worldPosition.y += _riseSpeed * deltaTime;
        UpdatePosition(root, worldCamera, uiCamera);
        UpdateAlpha();
        return true;
    }

    /// <summary>
    /// 更新 UI 位置。
    /// </summary>
    private void UpdatePosition(RectTransform root, Camera worldCamera, Camera uiCamera)
    {
        if (root == null || worldCamera == null || _rectTransform == null)
        {
            return;
        }

        var screenPos = worldCamera.WorldToScreenPoint(_worldPosition);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screenPos, uiCamera, out var localPos))
        {
            _rectTransform.anchoredPosition = localPos;
        }
    }

    /// <summary>
    /// 更新淡出透明度。
    /// </summary>
    private void UpdateAlpha()
    {
        if (_canvasGroup == null)
        {
            return;
        }

        if (_fadeDuration <= 0f)
        {
            _canvasGroup.alpha = 1f;
            return;
        }

        var fadeStart = _lifeTime - _fadeDuration;
        if (_elapsed <= fadeStart)
        {
            _canvasGroup.alpha = 1f;
            return;
        }

        var t = (_elapsed - fadeStart) / _fadeDuration;
        if (t < 0f) t = 0f;
        if (t > 1f) t = 1f;
        _canvasGroup.alpha = 1f - t;
    }
}
