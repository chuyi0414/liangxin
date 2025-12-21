using CYFramework;
using CYFramework.Core.Procedure;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[AutoRegisterProcedure(name: "Game", order: 20)]
public class GameProcedure : ProcedureBase
{
    protected override void OnEnter(ProcedureBase previousProcedure)
    {
        base.OnEnter(previousProcedure);
        CY.UI.Open<GameUIPanel>();
        CY.Entity.SpawnEntity<>
    }

    protected override void OnUpdate(float deltaTime)
    {
        base.OnUpdate(deltaTime);

    }

    protected override void OnLeave(ProcedureBase nextProcedure)
    {
        base.OnLeave(nextProcedure);
        CY.UI.Close<GameUIPanel>();
    }
}
