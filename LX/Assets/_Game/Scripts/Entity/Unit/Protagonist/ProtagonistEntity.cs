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
    private ProtagonistActions _input;

    //主角数据表
    private DRProtagonist _dRProtagonist;

    /// <summary>
    /// 初始化主角实体的运行时数据
    /// </summary>
    protected override void OnInit(object userData)
    {
        base.OnInit(userData);
        object[] os = userData as object[];
        _dRProtagonist = os[0] as DRProtagonist;

        _input = new ProtagonistActions();
        _input.ProtagonistNormal.Move2d.performed += Move;
        _input.ProtagonistNormal.Move2d.canceled += StopMove;
        _input.ProtagonistNormal.Attack.performed += Attack_performed;
    }

    private void Attack_performed(InputAction.CallbackContext obj)
    {
        
    }

    private void Move(InputAction.CallbackContext context)
    {
        _moveInput = context.ReadValue<Vector2>();
    }
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
