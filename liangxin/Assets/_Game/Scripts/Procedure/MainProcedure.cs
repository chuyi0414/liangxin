using CYFramework;
using CYFramework.Core.Procedure;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[AutoRegisterProcedure(name: "Main", order: 10)]
public class MainProcedure : ProcedureBase
{
    protected override void OnEnter(ProcedureBase previousProcedure)
    {
        base.OnEnter(previousProcedure);
        CY.UI.Open<MainUIPanel>();
    }

    protected override void OnUpdate(float deltaTime)
    {
        base.OnUpdate(deltaTime);

    }

    protected override void OnLeave(ProcedureBase nextProcedure)
    {
        base.OnLeave(nextProcedure);
        CY.UI.Close<MainUIPanel>();
    }
}
