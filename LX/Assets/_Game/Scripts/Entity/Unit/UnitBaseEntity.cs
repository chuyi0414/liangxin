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
    //当前子弹数据
    protected IDataTable<DRProjectile> _dRProjectiles;
    //当前子弹
    protected DRProjectile _dRProjectile;

    protected override void OnInit(object userData)
    {
        base.OnInit(userData);
        //获取子弹
        IDataTable<DRProjectile> dRProjectiles = GameEntry.DataTable.GetDataTable<DRProjectile>();
        _dRProjectiles = dRProjectiles;
    }
}
