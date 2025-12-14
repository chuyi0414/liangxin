using UnityEngine;
using CYFramework;

/// <summary>
/// 基础投射物/子弹脚本
/// </summary>
public class ProjectileBase : MonoBehaviour
{
    private float _speed = 10f;
    private float _damage = 0;
    private string _targetTag;
    private float _lifeTime = 5f;

    private Vector3 _direction;
    private bool _isLaunched = false;

    public void Init(Vector3 direction, float speed, float damage, string targetTag)
    {
        _direction = direction;
        _speed = speed;
        _damage = damage;
        _targetTag = targetTag;
        _lifeTime = 5f;
        _isLaunched = true;
        
        // 修正朝向 (假设子弹素材原本朝右)
        float angle = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);
    }

    private void Update()
    {
        if (!_isLaunched) return;

        // 移动
        transform.position += _direction * _speed * Time.deltaTime;

        // 生命周期销毁
        _lifeTime -= Time.deltaTime;
        if (_lifeTime <= 0)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!_isLaunched) return;
        
        // 撞到目标
        if (other.CompareTag(_targetTag))
        {
            // TODO: 造成伤害
            // other.GetComponent<IDamageable>()?.TakeDamage(_damage);
            CY.Log($"子弹命中 {_targetTag} ({other.name})，造成 {_damage} 点伤害");
            
            // 撞击特效?
            
            Destroy(gameObject);
        }
        else if (other.gameObject.layer == LayerMask.NameToLayer("Obstacle")) // 撞墙
        {
             Destroy(gameObject);
        }
    }
}
