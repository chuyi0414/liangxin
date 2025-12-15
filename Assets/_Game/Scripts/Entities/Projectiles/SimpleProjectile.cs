using CYFramework.Core.Pool;
using UnityEngine;

/// <summary>
/// 简单的追踪投射物 (Pool 版本)
/// </summary>
public class SimpleProjectile : MonoBehaviour, IPoolable
{
    private Vector3 _direction;
    private float _damage;
    private float _speed;
    private float _lifeTime;
    private GameObjectPool _pool; // 归属的池

    public void SetPool(GameObjectPool pool)
    {
        _pool = pool;
    }

    public void Init(Vector3 direction, float damage, float speed = 10f)
    {
        _direction = direction.normalized;
        _damage = damage;
        _speed = speed;
        _lifeTime = 5f; // 5秒寿命

        // 面向飞行方向
        float angle = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
    }
    
    // IPoolable 接口实现
    public void OnSpawn() { }
    
    public void OnDespawn() 
    {
        // 重置状态
        _lifeTime = 0;
        _damage = 0;
        _speed = 0;
    }

    private void Update()
    {
        // 移动
        transform.Translate(Vector3.right * _speed * Time.deltaTime);
        
        // 寿命检测
        _lifeTime -= Time.deltaTime;
        if (_lifeTime <= 0)
        {
            Recycle();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 碰到敌人
        var enemy = other.GetComponent<EnemyEntity>();
        if (enemy != null)
        {
            enemy.TakeDamage(_damage);
            Recycle(); // 命中后回收
        }
    }
    
    private void Recycle()
    {
        if (_pool != null)
        {
            _pool.Return(gameObject);
        }
        else
        {
            Destroy(gameObject); // 兜底销毁
        }
    }
}
