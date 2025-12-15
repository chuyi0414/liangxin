using CYFramework.Core.Pool;
using UnityEngine;

/// <summary>
/// 简单的追踪投射物 (Pool 版本)
/// </summary>
public class SimpleProjectile : MonoBehaviour, IPoolable
{
    private const string TargetEnemy = "Enemy";
    private const string TargetPlayer = "Player";

    private Vector3 _direction;
    private float _damage;
    private float _speed;
    private float _lifeTime;
    private string _targetTag; // 目标类型 (Enemy / Player)
    private GameObjectPool _pool; // 归属的池

    public void SetPool(GameObjectPool pool)
    {
        _pool = pool;
    }

    /// <summary>
    /// 初始化
    /// </summary>
    /// <param name="direction">飞行方向</param>
    /// <param name="damage">伤害值</param>
    /// <param name="speed">速度</param>
    /// <param name="targetTag">目标类型(例如 "Enemy" 或 "Player")</param>
    public void Init(Vector3 direction, float damage, float speed, string targetTag)
    {
        _direction = direction.normalized;
        _damage = damage;
        _speed = speed;
        _targetTag = targetTag;
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
        _targetTag = null;
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
        if (string.IsNullOrEmpty(_targetTag))
        {
            return;
        }

        // 目标单位检测：避免依赖 Unity Tag（未定义 Tag 会直接报错）
        // 理想情况下应该有一个 IDamageable 接口，这里先根据 Entity 类型区分
        if (string.Equals(_targetTag, TargetEnemy, System.StringComparison.Ordinal))
        {
            if (other.TryGetComponent(out EnemyEntity enemy))
            {
                enemy.TakeDamage(_damage);
                Recycle();
            }
            return;
        }

        if (string.Equals(_targetTag, TargetPlayer, System.StringComparison.Ordinal))
        {
            if (other.TryGetComponent(out PlayerEntity player))
            {
                // 假设 PlayerEntity 有 TakeDamage（如果没有，需要补全）
                // player.TakeDamage(_damage);
                Recycle();
            }
            return;
        }

        // 兜底：对于非 Enemy/Player 类型，使用 string compare 避免 CompareTag 对未定义 Tag 直接报错
        if (other.tag == _targetTag)
        {
            Recycle();
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
