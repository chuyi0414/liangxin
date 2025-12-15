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
        _hpSlider.value = current / max;
        
        // 简单的受击反馈动画
        // 可以用 DOTween 或者简单的 Coroutine，这里为了 0GC 暂略
    }
}
