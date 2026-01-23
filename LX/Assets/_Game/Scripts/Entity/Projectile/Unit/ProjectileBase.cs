using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityGameFramework.Runtime;

/// <summary>
/// 子弹实体基类
/// </summary>
public class ProjectileBase : EntityLogic
{
    /// <summary>
    /// 子弹方向
    /// </summary>
    private Vector2 _direction;
    /// <summary>
    /// 子弹速度
    /// </summary>
    private float _speed;
    /// <summary>
    /// 回收时间
    /// </summary>
    private float _lifeDuration = 5f;
    /// <summary>
    /// 回收流逝时间
    /// </summary>
    private float _lifeElapsed = 0f;
    /// <summary>
    /// 子弹阵营
    /// </summary>
    public CAMP Camp;

    protected override void OnInit(object userData)
    {
        base.OnInit(userData);
    }

    protected override void OnShow(object userData)
    {
        base.OnShow(userData);
        _lifeDuration = 5f;
        _lifeElapsed = 0f;
        object[] os = userData as object[];
        transform.position = (Vector2)os[0];
        Vector2 v2 = (Vector2)os[1];
        _speed = float.Parse(os[2].ToString());
        _direction = (v2 - (Vector2)transform.position).normalized;
        Camp = (CAMP)int.Parse(os[3].ToString());
    }

    protected override void OnUpdate(float elapseSeconds, float realElapseSeconds)
    {
        base.OnUpdate(elapseSeconds, realElapseSeconds);
        transform.position += (Vector3)(_direction * _speed * elapseSeconds);
        _lifeElapsed+=(float)elapseSeconds;
        if(_lifeElapsed>= _lifeDuration)
        {
            GameEntry.Entity.HideEntity(Entity.Id);
        }
    }

    protected override void OnRecycle()
    {
        base.OnRecycle();

    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        TriggerEnter2D(collision);
    }
    
    protected void TriggerEnter2D(Collider2D collision)
    {
        int layer = collision.gameObject.layer;
        if (Camp == CAMP.Protagonist)
        {
            if (layer == LayerMask.NameToLayer("Enemy"))
            {
                GameEntry.Entity.HideEntity(Entity.Id);
            }
        }
        else if (Camp == CAMP.Enemy)
        {

        }
        else if (Camp == CAMP.NPC)
        {

        }
    }
}
