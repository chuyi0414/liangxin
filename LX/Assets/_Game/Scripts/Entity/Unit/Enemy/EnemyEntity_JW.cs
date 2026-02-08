using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 敌人 JW 实体：负责接收子物体触发器事件并进行区分处理。
/// </summary>
public class EnemyEntity_JW : EnemyBaseEneiey
{
    /// <summary>
    /// 实体初始化：在基类初始化完成后，可在此扩展敌人特有逻辑。
    /// </summary>
    /// <param name="userData">外部传入的初始化数据。</param>
    protected override void OnInit(object userData)
    {
        base.OnInit(userData);
    }

    protected override void OnShow(object userData)
    {
        base.OnShow(userData);

    }
}
