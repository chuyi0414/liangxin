using CYFramework;
using CYFramework.Core.Entity;
using UnityEngine;

/// <summary>
/// 玩家（老板）实体
/// </summary>
public class PlayerEntity : EntityBase
{
    public PlayerRow Data { get; private set; }

    [Header("Movement")]
    private Rigidbody2D _rb;
    private Vector2 _inputDir;

    protected override void OnEntityInit(object userData)
    {
        base.OnEntityInit(userData);
        // 初始化组件引用等
        _rb = GetComponent<Rigidbody2D>();
        if (_rb == null)
        {
            CY.LogError("[PlayerEntity] 缺少 Rigidbody2D 组件，无法移动！");
        }
    }

    protected override void OnEntityShow(object userData)
    {
        base.OnEntityShow(userData);
        
        if (userData is PlayerRow data)
        {
            Data = data;
            // 根据数据初始化属性，例如血量、攻击力等
        }
        
        // 重置状态
        _inputDir = Vector2.zero;
        if (_rb != null) _rb.velocity = Vector2.zero;
        
        // 注册到 UnitManager
        if (CY.Unit != null)
        {
            CY.Unit.RegisterUnit(this);
        }
    }
    
    protected override void OnEntityHide()
    {
        base.OnEntityHide();
        // 从 UnitManager 注销
        if (CY.Unit != null)
        {
            CY.Unit.UnregisterUnit(this);
        }
    }

    protected override void OnEntityUpdate(float deltaTime)
    {
        base.OnEntityUpdate(deltaTime);
        
        // 1. 处理输入 (Input.GetAxisRaw 获取 -1, 0, 1)
        float x = Input.GetAxisRaw("Horizontal");
        float y = Input.GetAxisRaw("Vertical");
        
        _inputDir = new Vector2(x, y).normalized;
        
        // 2. 面朝向处理
        if (x != 0)
        {
            // 保持编辑器里设置的 Y/Z 缩放，只改变 X 的正负
            var scale = transform.localScale;
            scale.x = x > 0 ? Mathf.Abs(scale.x) : -Mathf.Abs(scale.x);
            transform.localScale = scale;
        }
    }

    protected override void OnEntityFixedUpdate(float deltaTime)
    {
        base.OnEntityFixedUpdate(deltaTime);

        // 3. 物理移动
        if (_rb != null && Data != null)
        {
            _rb.velocity = _inputDir * Data.MoveSpeed;
        }
    }
}
