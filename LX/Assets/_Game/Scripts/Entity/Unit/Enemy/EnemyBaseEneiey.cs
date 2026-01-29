using Pathfinding;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityGameFramework.Runtime;

public class EnemyBaseEneiey : UnitBaseEntity
{
    protected override void OnInit(object userData)
    {
        base.OnInit(userData);
        _aIDestinationSetter = GetComponent<AIDestinationSetter>();
    }
}
