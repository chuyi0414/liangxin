using GameFramework.DataTable;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityGameFramework.Runtime;

public class StartGame : GameFrameworkComponent
{
    private IDataTable<DRProjectile> _dRProjectiles;
    /// <summary>
    /// 子弹数据表
    /// </summary>
    public IDataTable<DRProjectile> DRProjectiles
    {
        get
        {
            return _dRProjectiles;
        }
        set
        {
            _dRProjectiles = value;
        }
    }

    private IDataTable<DRProtagonist> _dRProtagonists;
    /// <summary>
    /// 主角数据表
    /// </summary>
    public IDataTable<DRProtagonist> DRProtagonists
    {
        get
        {
            return _dRProtagonists;
        }
        set
        {
            _dRProtagonists = value;
        }
    }

    private IDataTable<DRBattleData> _dRBattleDatas;
    /// <summary>
    /// 战斗数据表
    /// </summary>
    public IDataTable<DRBattleData> DRBattleDatas
    {
        get
        {
            return _dRBattleDatas;
        }
        set
        {
            _dRBattleDatas = value;
        }
    }
}
