using GameFramework.DataTable;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityGameFramework.Runtime;

/// <summary>
/// 单位实体基类
/// </summary>
public class UnitBaseEntity : EntityLogic
{
    /// <summary>
    /// 重力
    /// </summary>
    [SerializeField]
    protected Rigidbody2D _rigidbody2D;
    /// <summary>
    /// 移动向量
    /// </summary>
    protected Vector2 _moveInput;
    //当前子弹
    protected DRProjectile _dRProjectile;

    protected override void OnInit(object userData)
    {
        base.OnInit(userData);
    }
}

public enum CAMP
{
    Protagonist = 1,
    Enemy = 2,
    NPC = 3
}