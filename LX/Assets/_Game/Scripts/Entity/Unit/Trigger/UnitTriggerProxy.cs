using UnityEngine;

/// <summary>
/// 通用触发器代理：挂在子物体的 Collider2D 上，将触发事件转发给父物体。
/// </summary>
public class UnitTriggerProxy : MonoBehaviour
{
    /// <summary>
    /// 触发器标识，用于区分不同子物体的触发器用途（如：视觉范围、攻击范围等）。
    /// </summary>
    [SerializeField]
    private string _triggerId = "Default";

    /// <summary>
    /// 触发事件接收者（通常是父物体上的脚本），可在 Inspector 中手动指定。
    /// </summary>
    [SerializeField]
    private MonoBehaviour _receiverBehaviour;

    /// <summary>
    /// 运行时缓存的触发事件接收者接口引用，避免重复查找。
    /// </summary>
    private IUnitTriggerReceiver _receiver;

    /// <summary>
    /// 对外暴露的触发器标识，只读，用于接收者区分触发器类型。
    /// </summary>
    public string TriggerId
    {
        get { return _triggerId; }
    }

    /// <summary>
    /// 初始化：优先使用 Inspector 指定的接收者，未指定则从父级查找。
    /// </summary>
    private void Awake()
    {
        _receiver = _receiverBehaviour as IUnitTriggerReceiver;
        if (_receiver == null)
        {
            _receiver = GetComponentInParent<IUnitTriggerReceiver>();
        }
    }

    /// <summary>
    /// 2D 触发进入：将事件转发给接收者。
    /// </summary>
    /// <param name="other">进入触发器的对方 Collider2D。</param>
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_receiver != null)
        {
            _receiver.OnUnitTriggerEnter(this, other);
        }
    }

    /// <summary>
    /// 2D 触发离开：将事件转发给接收者。
    /// </summary>
    /// <param name="other">离开触发器的对方 Collider2D。</param>
    private void OnTriggerExit2D(Collider2D other)
    {
        if (_receiver != null)
        {
            _receiver.OnUnitTriggerExit(this, other);
        }
    }
}

/// <summary>
/// 触发器事件接收接口：由父物体实现以接收子物体触发事件。
/// </summary>
public interface IUnitTriggerReceiver
{
    /// <summary>
    /// 子物体触发器进入事件回调。
    /// </summary>
    /// <param name="proxy">触发器代理组件，用于识别触发器来源。</param>
    /// <param name="other">进入触发器的对方 Collider2D。</param>
    void OnUnitTriggerEnter(UnitTriggerProxy proxy, Collider2D other);

    /// <summary>
    /// 子物体触发器离开事件回调。
    /// </summary>
    /// <param name="proxy">触发器代理组件，用于识别触发器来源。</param>
    /// <param name="other">离开触发器的对方 Collider2D。</param>
    void OnUnitTriggerExit(UnitTriggerProxy proxy, Collider2D other);
}
