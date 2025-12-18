using CYFramework.Core.UI;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CYFramework;

// 独立的血条组件，挂在每一个血条 Prefab 上
public class UIUnitHPBar : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Slider _hpSlider;
    [SerializeField] private TMP_Text _damageText; // 可选，如果用来飘字
    [SerializeField] private CanvasGroup _canvasGroup;

    [Header("Texts（可选）")]
    [SerializeField] private UITmpValueText _hpFractionText; // 显示“100/100”
    [SerializeField] private UITmpValueText _hpPercentText;  // 显示“100%”

    private Transform _targetTransform;
    private Vector3 _offset;
    private int _ownerUnitID;
    private RectTransform _rectTransform;

    public int OwnerUnitID => _ownerUnitID;
    public bool IsActive => gameObject.activeSelf;

    public void Init(int unitID, Transform target, Vector3 offset)
    {
        _ownerUnitID = unitID;
        _targetTransform = target;
        _offset = offset;
        _rectTransform = GetComponent<RectTransform>();
        
        // [Fix] 强制设置锚点为中心，对应 UpdatePosition 里的计算逻辑
        if (_rectTransform != null)
        {
            _rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            _rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _rectTransform.localScale = Vector3.one; // [Fix] 重置缩放
        }
        
        // 重置状态
        _hpSlider.value = 1f;
        _canvasGroup.alpha = 1f;
        if (_damageText) _damageText.text = "";

        // 对象池复用：避免上一次显示内容“残留”到下一次使用。
        if (_hpFractionText) _hpFractionText.Clear();
        if (_hpPercentText) _hpPercentText.Clear();
    }
    
    private float _lastLogTime;

    /// <summary>
    /// 由 Manager 驱动更新，传入必要的相机参数，避免每个 Bar 自己去 GetComponent
    /// </summary>
    public void UpdatePosition(Camera mainCamera, Camera uiCamera, RectTransform parentRect)
    {
        if (_targetTransform == null || mainCamera == null || parentRect == null) return;

        // 1. 世界坐标转屏幕坐标
        Vector3 worldPos = _targetTransform.position + _offset;
        Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPos);

        // 2. 处理相机背面剔除
        if (screenPos.z < 0)
        {
            _rectTransform.anchoredPosition = new Vector2(-10000, -10000);
            return;
        }

        // 3. 屏幕坐标转 UI 局部坐标
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPos, uiCamera, out Vector2 localPos))
        {
            _rectTransform.anchoredPosition = localPos;
        }
    }

    public void UpdateHP(float current, float max)
    {
        // 防御：避免 max 为 0 导致除零；同时 clamp 防止血量越界导致 UI 抖动
        if (max <= 0f)
        {
            _hpSlider.value = 0f;

            // 边界：最大值不合法时，文本按 0/0 与 0% 处理（避免出现 NaN/Infinity）。
            if (_hpFractionText) _hpFractionText.SetFraction(0, 0);
            if (_hpPercentText) _hpPercentText.SetPercent(0);
            return;
        }

        float normalized = current / max;
        if (normalized < 0f) normalized = 0f;
        else if (normalized > 1f) normalized = 1f;
        _hpSlider.value = normalized;

        // 文本显示：使用 TMP.SetText 内部格式化，避免字符串拼接产生 GC。
        // 说明：事件触发频率一般低于每帧，但依然建议走零 GC 路径，避免战斗高压场景产生抖动。
        if (_hpFractionText)
        {
            // 这里按“整数血量”显示；若你的设计是小数血量，可在 UITmpValueText 中扩展 float 格式化。
            _hpFractionText.SetFraction(Mathf.RoundToInt(current), Mathf.RoundToInt(max));
        }

        if (_hpPercentText)
        {
            _hpPercentText.SetPercentFromFraction(current, max);
        }
        
        // 简单的受击反馈动画
        // 可以用 DOTween 或者简单的 Coroutine，这里为了 0GC 暂略
    }
}
