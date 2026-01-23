using Pathfinding;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyEntity : UnitBaseEntity
{
    private void Start()
    {
        _aIDestinationSetter = GetComponent<AIDestinationSetter>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            _aIDestinationSetter.target = GameEntry.StartGame.protagonistEntity.transform;
        }
    }

}
