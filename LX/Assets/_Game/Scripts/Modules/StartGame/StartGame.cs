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

    private IDataTable<DREnemy> _drEnemys;
    public IDataTable<DREnemy> DREnemies
    {
        get
        {
            return _drEnemys;
        }
        set
        {
            _drEnemys = value;
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
    /// <summary>
    /// 主角
    /// </summary>
    public ProtagonistEntity protagonistEntity;

    /// <summary>
    /// 在指定地方创建敌人
    /// </summary>
    public void TryCreationEnemy(string id,Vector3 v3)
    {
        DREnemy dREnemy = DREnemies.GetDataRow(1);
        GameEntry.Entity.ShowEntity<EnemyEntity_JW>(
            GameEntry.EntityIdPool.Acquire(),
            dREnemy.PrefabPath,
            "Enemy",
            new object[]
            {
                transform.position
            });
    }
}
