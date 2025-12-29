using CYFramework;
using CYFramework.Core.Entity;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[EntityPrefab("Prefabs/Entities/Game/CompanyEntity", "CompanyEntity", "Scene")]
public class CompanyEntity : EntityBase
{
    protected override void OnEntityShow(object userData)
    {
        base.OnEntityShow(userData);

    }

    protected override void OnEntityRecycle()
    {
        base.OnEntityRecycle();
        
    }
}
