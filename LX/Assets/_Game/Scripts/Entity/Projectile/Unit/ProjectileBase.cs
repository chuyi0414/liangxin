using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityGameFramework.Runtime;

/// <summary>
/// 子弹实体基类
/// </summary>
public class ProjectileBase : EntityLogic
{
    protected override void OnInit(object userData)
    {
        base.OnInit(userData);
        object[] os = userData as object[];
        transform.position = (Vector2)os[0];
    }
}
