using UnityEngine;

/// <summary>
/// 员工实体基类。
/// 负责处理员工通用的运行时数据初始化与关键单位注册逻辑。
/// </summary>
public class EmployeeBaseEntity : UnitBaseEntity
{
    /// <summary>
    /// 员工运行时数据副本（用于独立血量等运行时变化，避免共享数据表）。
    /// </summary>
    private DREmployee _runtimeDRUnit;

    /// <summary>
    /// 实体初始化。
    /// 仅在初始化阶段创建运行时数据副本，避免显示阶段反复分配带来 GC。
    /// </summary>
    /// <param name="userData">外部传入的初始化数据。</param>
    protected override void OnInit(object userData)
    {
        base.OnInit(userData);
        _runtimeDRUnit = new DREmployee();
    }

    /// <summary>
    /// 实体显示。
    /// 负责注册关键单位、解析出生参数、初始化员工运行时数据。
    /// </summary>
    /// <param name="userData">显示参数，约定为 object[2]：位置(Vector3) + DREmployee。</param>
    protected override void OnShow(object userData)
    {
        base.OnShow(userData);
        GameEntry.GameManager.RegisterKeyUnit(this);

        object[] showParams = userData as object[];
        if (showParams == null || showParams.Length < 2)
        {
            return;
        }

        transform.position = (Vector3)showParams[0];
        _dRUnit = showParams[1] as DREmployee;
        if (_dRUnit == null)
        {
            return;
        }

        if (_runtimeDRUnit == null)
        {
            _runtimeDRUnit = new DREmployee();
        }

        _runtimeDRUnit.CopyFrom(_dRUnit as DREmployee);
        CurrentDRUnit = _runtimeDRUnit;
        _attackElapsedTime = 0f;
    }

    /// <summary>
    /// 实体隐藏。
    /// 负责注销关键单位，避免无效实体继续参与关键单位计算。
    /// </summary>
    /// <param name="isShutdown">是否为系统关闭触发。</param>
    /// <param name="userData">外部传入的隐藏数据。</param>
    protected override void OnHide(bool isShutdown, object userData)
    {
        GameEntry.GameManager.UnregisterKeyUnit(this);
        base.OnHide(isShutdown, userData);
    }
}
