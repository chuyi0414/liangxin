using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityGameFramework.Runtime;

/// <summary>
/// 主角实体
/// </summary>
public class ProtagonistEntity : UnitBaseEntity
{
    /// <summary>
    /// 主角输入
    /// </summary>
    private ProtagonistActions _input;
    /// <summary>
    /// 主角运行时数据副本（用于独立血量等运行时变化，避免共享数据表）。
    /// </summary>
    private DRProtagonist _runtimeDRUnit;
    /// <summary>
    /// 初始化主角实体的运行时数据
    /// </summary>
    protected override void OnInit(object userData)
    {
        base.OnInit(userData);
        // 仅在初始化时创建运行时数据副本，避免频繁分配导致 GC。
        _runtimeDRUnit = new DRProtagonist();
    }

    /// <summary>
    /// 实体显示
    /// </summary>
    protected override void OnShow(object userData)
    {
        base.OnShow(userData);
        // 注册主角为关键单位（影响敌人AI分级）
        GameEntry.GameManager.RegisterKeyUnit(this);
        // 注册到空间网格（让敌人空间查询能发现主角）
        GameEntry.GameManager.UnitBatchUpdateManager.RegisterUnit(this);

        object[] os = userData as object[];
        _dRUnit = os[0] as DRProtagonist;
        if (_runtimeDRUnit == null)
        {
            _runtimeDRUnit = new DRProtagonist();
        }
        _runtimeDRUnit.CopyFrom(_dRUnit as DRProtagonist);
        CurrentDRUnit = _runtimeDRUnit;

        _dRProjectile = GameEntry.GameManager.DRProjectiles.GetDataRow(CurrentDRUnit.ProjectileId);
        _input = new ProtagonistActions();
        _input.ProtagonistNormal.Move2d.performed += Move;
        _input.ProtagonistNormal.Move2d.canceled += StopMove;
        _input.ProtagonistNormal.Attack.performed += Attack_performed;
        _input.ProtagonistNormal.Attack.canceled += Attack_canceled;

        transform.position = ((Transform)os[1]).position;
        _input.ProtagonistNormal.Enable();

        GameEntry.GameManager.ProtagonistEntity = this;
        _attackElapsedTime = 0;

        CameraFollowDriver follow = GameEntry.Camera.GetCamera("main").GetComponent<CameraFollowDriver>();
        follow.SetTarget(transform);
        follow.transform.position = transform.position;
    }

    /// <summary>
    /// 松开按键后触发（停止持续发射）
    /// </summary>
    /// <param name="context"></param>
    private void Attack_canceled(InputAction.CallbackContext context)
    {
        //StopFire();
        _isAttackTarget = false;
    }

    /// <summary>
    /// 发射子弹
    /// </summary>
    /// <param name="obj"></param>
    private void Attack_performed(InputAction.CallbackContext obj)
    {
        //StartFire();
        _isAttackTarget = true;
    }
    /// <summary>
    /// 发射一次子弹（你原来的鼠标取点逻辑放这里）。
    /// </summary>
    private void FireOnce()
    {
        Camera camera = GameEntry.Camera.GetCamera("main");
        //读取鼠标屏幕坐标
        Vector2 mouseScreen = Mouse.current.position.ReadValue();
        //计算从相机到角色所在平面的距离
        float depth = Mathf.Abs(camera.transform.position.z - transform.position.z);
        //把屏幕坐标转换成世界坐标
        Vector3 mouseWorld = camera.ScreenToWorldPoint(new Vector3(
          mouseScreen.x,
          mouseScreen.y,
          depth));
        //计算角色指向鼠标的方向向量，并归一化
        //Vector2 direction = ((Vector2)mouseWorld - (Vector2)transform.position).normalized;

        GameEntry.Entity.ShowEntity<ProjectileBase>(
            GameEntry.EntityIdPool.Acquire(),
            _dRProjectile.PrefabPath,
            "Projectile",
            new object[]
            {
                (Vector2)transform.position,
                (Vector2)mouseWorld,
                CurrentDRUnit.ProjectileSpeed,
                this,
            });
    }

	protected override void OnAttackDuring()
	{
        FireOnce();

        base.OnAttackDuring();
	}

    /// <summary>
    /// 移动
    /// </summary>
    /// <param name="context"></param>
    private void Move(InputAction.CallbackContext context)
    {
        _moveInput = context.ReadValue<Vector2>();
    }
    /// <summary>
    /// 停止移动
    /// </summary>
    /// <param name="context"></param>
    private void StopMove(InputAction.CallbackContext context)
    {
        _moveInput = Vector2.zero;
    }

    /// <summary>
    /// 每帧驱动刚体移动
    /// </summary>
    protected override void OnUpdate(float elapseSeconds, float realElapseSeconds)
    {
        base.OnUpdate(elapseSeconds, realElapseSeconds);

        _rigidbody2D.velocity = _moveInput.normalized * CurrentDRUnit.MoveSeep;
    }

    /// <summary>
    /// 实体隐藏时关闭输入
    /// </summary>
    protected override void OnHide(bool isShutdown, object userData)
    {
        base.OnHide(isShutdown, userData);
        _input.ProtagonistNormal.Disable();
        // 注销主角关键单位
        GameEntry.GameManager.UnregisterKeyUnit(this);
        // 从空间网格移除
        GameEntry.GameManager.UnitBatchUpdateManager.UnregisterUnit(this);

        CameraFollowDriver follow = GameEntry.Camera.GetCamera("main").GetComponent<CameraFollowDriver>();
        follow.ClearTarget();
    }

    public override void OnInjuried(float damage)
    {
        base.OnInjuried(damage);
        Log.Info("我是主角");
    }
   
}
