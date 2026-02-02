using System.Collections;
using UnityEngine;

/// <summary>
/// 员工实体
/// </summary>
public class EmployeeEntity : UnitBaseEntity
{
    /// <summary>
    /// 实体显示时注册为关键单位。
    /// </summary>
    protected override void OnShow(object userData)
    {
        base.OnShow(userData);
        GameEntry.GameManager.RegisterKeyUnit(this);
    }

    /// <summary>
    /// 实体隐藏时注销关键单位。
    /// </summary>
    protected override void OnHide(bool isShutdown, object userData)
    {
        GameEntry.GameManager.UnregisterKeyUnit(this);
        base.OnHide(isShutdown, userData);
    }

}