using CYFramework;
using CYFramework.Infrastructure;
using UnityEngine;

/// <summary>
/// 相机管理器
/// </summary>
public class CameraManager : MonoBehaviour, IInitializable
{
    [Header("Cameras")]
    [SerializeField] private Camera _mainCamera;
    [SerializeField] private Camera _uiCamera;

    /// <summary>
    /// 主场景相机
    /// </summary>
    public Camera MainCamera 
    {
        get 
        {
            if (_mainCamera == null) _mainCamera = Camera.main;
            return _mainCamera;
        }
    }

    /// <summary>
    /// UI 相机 (如果是 ScreenSpaceOverlay 则可能为 null)
    /// </summary>
    public Camera UICamera => _uiCamera;

    public int InitOrder => 10; // 较早初始化

    private void Awake()
    {
        // 自动注册
        if (!ServiceLocator.IsRegistered<CameraManager>())
        {
            ServiceLocator.RegisterInstance(this);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void Initialize()
    {
        // 尝试自动查找
        if (_mainCamera == null) _mainCamera = Camera.main;
        
        if (_uiCamera == null)
        {
            // 尝试找 Tag 为 UICamera 的
            var uiCamObj = GameObject.FindGameObjectWithTag("UICamera");
            if (uiCamObj) _uiCamera = uiCamObj.GetComponent<Camera>();
            
            // 或者找名字里带 UI 的相机
            if (_uiCamera == null)
            {
                // 简单的查找策略
                foreach (var cam in Camera.allCameras)
                {
                    if (cam.name.Contains("UI") && cam != _mainCamera)
                    {
                        _uiCamera = cam;
                        break;
                    }
                }
            }
        }

        CY.Log($"[CameraManager] Main:{(_mainCamera?_mainCamera.name:"null")} UI:{(_uiCamera?_uiCamera.name:"null")}");
    }

    /// <summary>
    /// 将世界坐标转换为 UI 本地坐标 (AnchoredPosition)
    /// </summary>
    /// <param name="worldPos">世界坐标</param>
    /// <param name="parentRect">UI 父节点的 RectTransform</param>
    /// <param name="uiPos">输出的 UI 坐标</param>
    /// <returns>是否在屏幕范围内 (z > 0)</returns>
    public bool WorldToUIPoint(Vector3 worldPos, RectTransform parentRect, out Vector2 uiPos)
    {
        uiPos = Vector2.zero;
        if (MainCamera == null) return false;

        // 1. 转屏幕坐标
        Vector3 screenPos = MainCamera.WorldToScreenPoint(worldPos);

        // 2. 背面剔除
        if (screenPos.z < 0) return false;

        // 3. 转 UI 坐标
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPos, _uiCamera, out  uiPos))
        {
            return true;
        }
        
        return false;
    }
}
