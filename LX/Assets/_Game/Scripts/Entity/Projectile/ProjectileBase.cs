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
    /// 是否已经回收
    /// </summary>
    private bool _isHidden;
    /// <summary>
    /// 发射者
    /// </summary>
    private UnitBaseEntity _unitBaseEntity;
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
        _isHidden = false;
        _lifeDuration = 5f;
        _lifeElapsed = 0f;
        object[] os = userData as object[];
        transform.position = (Vector2)os[0];
        Vector2 v2 = (Vector2)os[1];
        _speed = float.Parse(os[2].ToString());
        _direction = (v2 - (Vector2)transform.position).normalized;
        _unitBaseEntity = (UnitBaseEntity)os[3];
        Camp = _unitBaseEntity.Camp;
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

        if (layer == LayerMask.NameToLayer("Company"))
        {
            MyHideEntity();
        }

        if (Camp == CAMP.Protagonist)
        {
            if (layer == LayerMask.NameToLayer("Enemy"))
            {
                MyHideEntity();
            }
        }
        else if (Camp == CAMP.Enemy)
        {

        }
        else if (Camp == CAMP.NPC)
        {

        }
    }

    
    private void MyHideEntity()
    {
        if(_isHidden)
        {
            return;
        }
        _isHidden = true;
        GameEntry.Entity.HideEntity(Entity.Id);
    }
}
