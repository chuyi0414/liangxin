using GameFramework.DataTable;
using Pathfinding;
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
    /// <summary>
    /// A* 自带的目标设置组件
    /// </summary>
    protected AIDestinationSetter _aIDestinationSetter;
    /// <summary>
    /// A*的寻路组件
    /// </summary>
    protected AIPath _aIPath;

    protected override void OnInit(object userData)
    {
        base.OnInit(userData);
    }
}

/// <summary>
/// 阵营
/// </summary>
public enum CAMP
{
    Protagonist = 1,
    Enemy = 2,
    NPC = 3
}