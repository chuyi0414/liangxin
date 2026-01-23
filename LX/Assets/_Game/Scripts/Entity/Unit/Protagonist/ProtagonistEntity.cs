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
    /// 持续发射计时器句柄（为空表示未在持续发射）。
    /// </summary>
    private GameFramework.Timer.Timer _fireTimer;
    /// <summary>
    /// 初始化主角实体的运行时数据
    /// </summary>
    protected override void OnInit(object userData)
    {
        base.OnInit(userData);
        object[] os = userData as object[];
        _dRProtagonist = os[0] as DRProtagonist;

        _dRProjectile = GameEntry.StartGame.DRProjectiles.GetDataRow(_dRProtagonist.ProjectileId);
        _input = new ProtagonistActions();
        _input.ProtagonistNormal.Move2d.performed += Move;
        _input.ProtagonistNormal.Move2d.canceled += StopMove;
        _input.ProtagonistNormal.Attack.performed += Attack_performed;
        _input.ProtagonistNormal.Attack.canceled += Attack_canceled;
    }
    /// <summary>
    /// 松开按键后触发（停止持续发射）
    /// </summary>
    /// <param name="context"></param>
    private void Attack_canceled(InputAction.CallbackContext context)
    {
        StopFire();
    }

    /// <summary>
    /// 发射子弹
    /// </summary>
    /// <param name="obj"></param>
    private void Attack_performed(InputAction.CallbackContext obj)
    {
        StartFire();
    }
    /// <summary>
    /// 开始持续发射（先立即发一发，再进入循环）。
    /// </summary>
    private void StartFire()
    {
        // 已经在持续发射时不重复启动
        if (_fireTimer != null)
        {
            return;
        }

        // 先发一发，保证手感
        FireOnce();

        // 使用计时器循环发射
        _fireTimer = GameEntry.Timer.Loop(0.1f, FireOnce, false);
    }

    /// <summary>
    /// 停止持续发射（取消计时器）。
    /// </summary>
    private void StopFire()
    {
        if (_fireTimer == null)
        {
            return;
        }

        GameEntry.Timer.Cancel(_fireTimer);
        _fireTimer = null;
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
    /// 实体显示
    /// </summary>
    protected override void OnShow(object userData)
    {
        base.OnShow(userData);
        _input.ProtagonistNormal.Enable();
    }

    /// <summary>
    /// 每帧驱动刚体移动
    /// </summary>
    protected override void OnUpdate(float elapseSeconds, float realElapseSeconds)
    {
        base.OnUpdate(elapseSeconds, realElapseSeconds);

        _rigidbody2D.velocity = _moveInput.normalized * _dRProtagonist.MoveSeep;
    }

    /// <summary>
    /// 实体隐藏时关闭输入
    /// </summary>
    protected override void OnHide(bool isShutdown, object userData)
    {
        base.OnHide(isShutdown, userData);
        _input.ProtagonistNormal.Disable();
    }

   
}
