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
    //主角数据表
    private DRProtagonist _dRProtagonist;
    /// <summary>
    /// 初始化主角实体的运行时数据
    /// </summary>
    protected override void OnInit(object userData)
    {
        base.OnInit(userData);
        
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
        _dRProtagonist = os[0] as DRProtagonist;
        Camp = _dRProtagonist.Camp;

        _dRProjectile = GameEntry.GameManager.DRProjectiles.GetDataRow(_dRProtagonist.ProjectileId);
        _input = new ProtagonistActions();
        _input.ProtagonistNormal.Move2d.performed += Move;
        _input.ProtagonistNormal.Move2d.canceled += StopMove;
        _input.ProtagonistNormal.Attack.performed += Attack_performed;
        _input.ProtagonistNormal.Attack.canceled += Attack_canceled;

        transform.position = ((Transform)os[1]).position;
        _input.ProtagonistNormal.Enable();

        GameEntry.GameManager.protagonistEntity = this;
        _attackElapsedTime = 0;
    }

    /// <summary>
    /// 松开按键后触发（停止持续发射）
    /// </summary>
    /// <param name="context"></param>
    private void Attack_canceled(InputAction.CallbackContext context)
    {
        //StopFire();
        _isAttacking = false;
    }

    /// <summary>
    /// 发射子弹
    /// </summary>
    /// <param name="obj"></param>
    private void Attack_performed(InputAction.CallbackContext obj)
    {
        //StartFire();
        _isAttacking = true;
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
                _dRProtagonist.ProjectileSpeed,
                _dRProtagonist.Camp
            });
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

        _rigidbody2D.velocity = _moveInput.normalized * _dRProtagonist.MoveSeep;

        if(_isAttacking && _isAttack)
        {
            FireOnce();
            _attackElapsedTime = 0;
            _isAttack = false;
        }
        else
        {
            _attackElapsedTime += elapseSeconds;
            if (_attackElapsedTime >= _dRProtagonist.AttackSpeed)
            {
                _isAttack = true;
                if (_isAttacking && _isAttack)
                {
                    FireOnce();
                    _attackElapsedTime = 0;
                    _isAttack = false;
                }
            }
        }
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
    }

   
}
