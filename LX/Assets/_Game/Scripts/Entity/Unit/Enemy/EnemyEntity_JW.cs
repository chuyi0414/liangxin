using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyEntity_JW : EnemyBaseEneiey
{
    protected override void OnInit(object userData)
    {
        base.OnInit(userData);
        _aIPath.maxSpeed = _dREnemy.MoveSeep;
    }
}
