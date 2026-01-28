using Pathfinding;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.SceneManagement;
using UnityEngine;

public class EnemyEntity : UnitBaseEntity
{
    protected override void OnInit(object userData)
    {
        base.OnInit(userData);
        _aIDestinationSetter = GetComponent<AIDestinationSetter>();
    }

    /*private void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            _aIDestinationSetter.target = GameEntry.StartGame.protagonistEntity.transform;
        }
    }*/

}
