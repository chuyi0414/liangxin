using Pathfinding;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityGameFramework.Runtime;
using static UnityEngine.GraphicsBuffer;

public class EnemyBaseEneiey : UnitBaseEntity, IUnitTriggerReceiver
{
    /// <summary>
    /// 敌人数据
    /// </summary>
    protected DREnemy _dREnemy;

    protected override void OnInit(object userData)
    {
        base.OnInit(userData);

        _aIDestinationSetter = GetComponent<AIDestinationSetter>();
        _aIPath = GetComponent<AIPath>();
        ApplyConstantSpeedSettings();
    }

    protected override void OnShow(object userData)
    {
        base.OnShow(userData);

        object[] os = userData as object[];
        transform.position = (Vector3)os[0];
        _dREnemy = (DREnemy)os[1];
        Camp = _dREnemy.Camp;
        _aIPath.maxSpeed = _dREnemy.MoveSeep;
        _visualScopeCollider.radius = _dREnemy.VisualScope;
        _attackRangeCollider.radius = _dREnemy.AttackRange;
        StartAIMove(GameEntry.StartGame.companyEntity.transform);
    }

    /// <summary>
    /// 子物体触发器进入事件：根据触发器标识区分视觉/攻击等逻辑。
    /// </summary>
    /// <param name="proxy">触发器代理组件。</param>
    /// <param name="other">进入触发器的对方 Collider2D。</param>
    public void OnUnitTriggerEnter(UnitTriggerProxy proxy, Collider2D other)
    {
        if (proxy == null)
        {
            return;
        }
        UnitBaseEntity unitBaseEntity = other.gameObject.GetComponent<UnitBaseEntity>();
        if (unitBaseEntity == null)
        {
            return; 
        }

        if (proxy.TriggerId == _visualScopeTriggerId)
        {
            if (_dREnemy.Camp == CAMP.Protagonist)
            {

            }else if(_dREnemy.Camp == CAMP.Enemy)
            {
                if(unitBaseEntity.Camp == CAMP.Protagonist)
                {
                    _visualScopeUnitList.Add(unitBaseEntity);
                    if(_targetTransform == null )
                    {
                        _targetTransform = unitBaseEntity.transform;
                        StartAIMove(unitBaseEntity.transform);
                    }
                }
            }
        }
        else if (proxy.TriggerId == _attackRangeTriggerId)
        {
            if (_dREnemy.Camp == CAMP.Protagonist)
            {

            }
            else if (_dREnemy.Camp == CAMP.Enemy)
            {
                if (unitBaseEntity.gameObject == _targetTransform.gameObject)
                {
                    StopAIMove();
                }
            }
        }
    }

    /// <summary>
    /// 子物体触发器离开事件：根据触发器标识区分视觉/攻击等逻辑。
    /// </summary>
    /// <param name="proxy">触发器代理组件。</param>
    /// <param name="other">离开触发器的对方 Collider2D。</param>
    public void OnUnitTriggerExit(UnitTriggerProxy proxy, Collider2D other)
    {
        if (proxy == null)
        {
            return;
        }
        UnitBaseEntity unitBaseEntity = other.gameObject.GetComponent<UnitBaseEntity>();
        if (unitBaseEntity == null)
        {
            return;
        }
        if (proxy.TriggerId == _visualScopeTriggerId)
        {
            if (_dREnemy.Camp == CAMP.Protagonist)
            {

            }
            else if (_dREnemy.Camp == CAMP.Enemy)
            {
                if (unitBaseEntity.Camp == CAMP.Protagonist)
                {
                    if(_visualScopeUnitList.Contains(unitBaseEntity))
                    {
                        _visualScopeUnitList.Remove(unitBaseEntity);
                    }
                    if (_targetTransform == unitBaseEntity.transform)
                    {
                        _targetTransform = null;
                        if(_visualScopeUnitList.Count > 0)
                        {
                            UnitBaseEntity target = GetNearestUnit(_visualScopeUnitList,transform.position);
                            _targetTransform = target.transform;
                            StartAIMove(target.transform);
                        }
                        else
                        {
                            StartAIMove(GameEntry.StartGame.companyEntity.transform);
                        }
                    }
                }
            }
        }
        else if (proxy.TriggerId == _attackRangeTriggerId)
        {
            if (_dREnemy.Camp == CAMP.Protagonist)
            {

            }
            else if (_dREnemy.Camp == CAMP.Enemy)
            {
                if (unitBaseEntity.gameObject == _targetTransform.gameObject)
                {
                    StartAIMove(unitBaseEntity.transform);
                }
            }
        }
    }
}
