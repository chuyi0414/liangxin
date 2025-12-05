// ============================================================================
// CYFramework - 使用示例
// 演示如何使用框架创建一个简单的游戏（带简单可视化）
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using CYFramework.Runtime.Core;
using CYFramework.Runtime.Gameplay.Abstraction;
using CYFramework.Runtime.Gameplay.OOP;
using CYFramework.Runtime.Gameplay.OOP.Systems;

namespace CYFramework.Samples
{
    /// <summary>
    /// 示例游戏入口
    /// 演示 CYFramework 的基本使用方法（含简单的实体可视化）
    /// </summary>
    public class SampleGame : MonoBehaviour
    {
        [Header("游戏设置")]
        [SerializeField] private int _enemyCount = 5;
        [SerializeField] private float _spawnRadius = 10f;

        [Header("可视化预制体（可不填，默认使用 Primitive）")]
        [SerializeField] private GameObject _playerPrefab;
        [SerializeField] private GameObject _enemyPrefab;

        private int _playerId;

        // 玩法世界引用（OOP 实现）
        private GameplayWorldOOP _world;

        // 实体 ID -> 场景中可见物体 的映射
        private Dictionary<int, GameObject> _entityViews = new Dictionary<int, GameObject>();

        private void Start()
        {
            // 等待框架初始化
            if (!CYFrameworkEntry.Instance.IsInitialized)
            {
                Log.E("SampleGame", "CYFramework 未初始化！");
                return;
            }

            // 初始化游戏
            InitializeGame();
        }

        private void InitializeGame()
        {
            var fw = CYFrameworkEntry.Instance;
            _world = fw.GameplayWorld as GameplayWorldOOP;

            if (_world == null)
            {
                Log.E("SampleGame", "当前示例只支持 GameplayWorldOOP 实现");
                return;
            }

            // 生成玩家
            _playerId = _world.SpawnEntity(new EntitySpawnInfo
            {
                Type = EntityType.Player,
                ConfigId = 1,
                CampId = 1,  // 玩家阵营
                Position = Vector3.zero,
                Rotation = 0f
            });

            // 为玩家创建可见物体
            CreateOrUpdateView(_playerId);

            // 生成敌人
            for (int i = 0; i < _enemyCount; i++)
            {
                Vector3 pos = Random.insideUnitSphere * _spawnRadius;
                pos.y = 0;

                int enemyId = _world.SpawnEntity(new EntitySpawnInfo
                {
                    Type = EntityType.Enemy,
                    ConfigId = 100 + i,
                    CampId = 2,  // 敌人阵营
                    Position = pos,
                    Rotation = Random.Range(0f, 360f)
                });

                // 为敌人添加 AI
                var aiSystem = _world.GetSystem<AISystem>();
                aiSystem?.AddAI(enemyId);

                // 为敌人创建可见物体
                CreateOrUpdateView(enemyId);
            }

            Log.I("SampleGame", $"游戏初始化完成：玩家 ID={_playerId}，敌人数量={_enemyCount}");

            // 订阅伤害事件
            fw.Event.Subscribe<DamageEvent>(OnDamage);
        }

        private void Update()
        {
            if (!CYFrameworkEntry.Instance.IsInitialized) return;

            var world = CYFrameworkEntry.Instance.GameplayWorld;
            if (world == null || !world.IsRunning) return;

            // 处理输入
            HandlePlayerInput();

            // 检查战斗结束
            CheckBattleEnd();

            // 同步可视化物体与实体数据
            SyncViews();
        }

        private void HandlePlayerInput()
        {
            var world = CYFrameworkEntry.Instance.GameplayWorld;

            // 鼠标右键移动
            if (Input.GetMouseButtonDown(1))
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    world.HandleInput(new PlayerInput
                    {
                        Type = InputType.Move,
                        PlayerId = _playerId,
                        TargetPosition = hit.point,
                        Timestamp = Time.time
                    });
                }
            }

            // 空格键攻击最近的敌人
            if (Input.GetKeyDown(KeyCode.Space))
            {
                int nearestEnemy = FindNearestEnemy();
                if (nearestEnemy >= 0)
                {
                    world.HandleInput(new PlayerInput
                    {
                        Type = InputType.Attack,
                        PlayerId = _playerId,
                        TargetEntityId = nearestEnemy,
                        Timestamp = Time.time
                    });
                }
            }
        }

        private int FindNearestEnemy()
        {
            var world = CYFrameworkEntry.Instance.GameplayWorld as GameplayWorldOOP;
            if (world == null) return -1;

            if (!world.TryGetEntity(_playerId, out EntityData player))
                return -1;

            var entities = world.GetAllEntities();
            float minDist = float.MaxValue;
            int nearestId = -1;

            for (int i = 0; i < entities.Count; i++)
            {
                var entity = entities[i];
                if (entity.Type != EntityType.Enemy || entity.State != EntityState.Active)
                    continue;

                float dist = Vector3.Distance(player.Position, entity.Position);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearestId = entity.Id;
                }
            }

            return nearestId;
        }

        private void CheckBattleEnd()
        {
            var world = CYFrameworkEntry.Instance.GameplayWorld as GameplayWorldOOP;
            if (world == null || world.IsBattleEnded) return;

            // 检查玩家是否死亡
            if (!world.TryGetEntity(_playerId, out EntityData player) || 
                player.State == EntityState.Dead)
            {
                world.EndBattle(BattleResultType.Defeat);
                ShowResult("失败！");
                return;
            }

            // 检查是否所有敌人都死亡
            var entities = world.GetAllEntities();
            bool hasEnemy = false;
            for (int i = 0; i < entities.Count; i++)
            {
                if (entities[i].Type == EntityType.Enemy && 
                    entities[i].State == EntityState.Active)
                {
                    hasEnemy = true;
                    break;
                }
            }

            if (!hasEnemy)
            {
                world.EndBattle(BattleResultType.Victory, 100);
                ShowResult("胜利！");
            }
        }

        private void OnDamage(DamageEvent evt)
        {
            Log.I("Damage", $"{evt.AttackerId} -> {evt.TargetId}: {evt.Damage} 点伤害" + (evt.IsCritical ? " (暴击)" : string.Empty));
        }

        private void ShowResult(string message)
        {
            var result = CYFrameworkEntry.Instance.GameplayWorld.GetBattleResult();
            Log.I("Result", "=== 战斗结束 ===");
            Log.I("Result", $"结果: {message}");
            Log.I("Result", $"用时: {result.Duration:F1} 秒");
            Log.I("Result", $"击杀: {result.KillCount}");
            Log.I("Result", $"死亡: {result.DeathCount}");
            Log.I("Result", $"得分: {result.Score}");
        }

        private void OnDestroy()
        {
            // 取消事件订阅
            CYFrameworkEntry.Instance?.Event?.Unsubscribe<DamageEvent>(OnDamage);

            // 清理场景中的可视化物体
            if (_entityViews != null)
            {
                foreach (var kvp in _entityViews)
                {
                    if (kvp.Value != null)
                    {
                        Destroy(kvp.Value);
                    }
                }
                _entityViews.Clear();
            }
        }

        // ====================================================================
        // 可视化辅助
        // ====================================================================

        /// <summary>
        /// 为指定实体创建或更新可见 GameObject
        /// </summary>
        private void CreateOrUpdateView(int entityId)
        {
            if (_world == null) return;
            if (!_world.TryGetEntity(entityId, out EntityData data)) return;

            // 已有视图则直接更新位置
            if (_entityViews.TryGetValue(entityId, out GameObject view) && view != null)
            {
                view.transform.position = data.Position;
                view.transform.rotation = Quaternion.Euler(0f, data.Rotation, 0f);
                return;
            }

            // 根据实体类型选择预制体，如未指定则使用内置 Primitive
            GameObject prefab = null;
            if (data.Type == EntityType.Player)
                prefab = _playerPrefab;
            else if (data.Type == EntityType.Enemy)
                prefab = _enemyPrefab;

            if (prefab != null)
            {
                view = Instantiate(prefab);
            }
            else
            {
                // 默认：玩家用 Capsule，敌人用 Sphere
                PrimitiveType primitiveType = data.Type == EntityType.Player
                    ? PrimitiveType.Capsule
                    : PrimitiveType.Sphere;
                view = GameObject.CreatePrimitive(primitiveType);
            }

            view.name = $"{data.Type}_{entityId}";
            view.transform.position = data.Position;
            view.transform.rotation = Quaternion.Euler(0f, data.Rotation, 0f);

            // 简单上色：玩家绿色，敌人红色
            var renderer = view.GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                if (data.Type == EntityType.Player)
                    renderer.material.color = Color.green;
                else if (data.Type == EntityType.Enemy)
                    renderer.material.color = Color.red;
            }

            _entityViews[entityId] = view;
        }

        /// <summary>
        /// 每帧根据玩法世界中的实体数据同步可见物体
        /// </summary>
        private void SyncViews()
        {
            if (_world == null) return;

            var entities = _world.GetAllEntities();

            // 更新或创建视图
            for (int i = 0; i < entities.Count; i++)
            {
                var entity = entities[i];

                // 跳过无效实体
                if (entity.State == EntityState.Invalid)
                    continue;

                // 死亡或待销毁：删除对应可见物体
                if (entity.State == EntityState.Dead || entity.State == EntityState.PendingDestroy)
                {
                    if (_entityViews.TryGetValue(entity.Id, out GameObject view) && view != null)
                    {
                        Destroy(view);
                    }
                    _entityViews.Remove(entity.Id);
                    continue;
                }

                // 活跃实体：创建或更新视图
                CreateOrUpdateView(entity.Id);
            }
        }
    }
}
