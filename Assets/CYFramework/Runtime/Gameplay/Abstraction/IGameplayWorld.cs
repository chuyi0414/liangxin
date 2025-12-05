// ============================================================================
// CYFramework - 玩法世界接口
// 定义玩法核心运行时的统一接口，对上层（UI、流程、网络）屏蔽实现细节
// 
// 说明：
// - 以下接口示例是按"战斗/单位管理"类型的玩法场景设计的
// - 实际项目中应根据具体玩法类型（如卡牌、解谜、模拟经营等）定制接口方法
// - 不同平台可以有不同的实现（OOP / DOTS），但对外接口保持一致
// ============================================================================

namespace CYFramework.Runtime.Gameplay.Abstraction
{
    /// <summary>
    /// 玩法世界接口
    /// UI、流程等上层模块只依赖此接口，不直接依赖具体实现
    /// </summary>
    public interface IGameplayWorld
    {
        /// <summary>
        /// 玩法世界是否已初始化
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// 玩法世界是否正在运行
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// 初始化玩法世界
        /// </summary>
        /// <param name="config">玩法配置（可为 null，使用默认配置）</param>
        void Initialize(GameplayConfig config);

        /// <summary>
        /// 帧更新
        /// 内部负责调用各子系统（移动、AI、战斗、Buff 等）的更新逻辑
        /// </summary>
        /// <param name="deltaTime">距离上一帧的时间（秒）</param>
        void Tick(float deltaTime);

        /// <summary>
        /// 关闭玩法世界
        /// 清理所有实体与内部资源
        /// </summary>
        void Shutdown();

        /// <summary>
        /// 暂停玩法世界
        /// </summary>
        void Pause();

        /// <summary>
        /// 恢复玩法世界
        /// </summary>
        void Resume();

        /// <summary>
        /// 重置玩法世界
        /// 清理当前状态，准备开始新一局
        /// </summary>
        void Reset();

        // ====================================================================
        // 输入与命令
        // ====================================================================

        /// <summary>
        /// 处理玩家输入
        /// </summary>
        /// <param name="input">玩家输入</param>
        void HandleInput(PlayerInput input);

        /// <summary>
        /// 执行游戏命令
        /// </summary>
        /// <param name="command">游戏命令</param>
        void ExecuteCommand(GameplayCommand command);

        // ====================================================================
        // 状态查询
        // ====================================================================

        /// <summary>
        /// 获取战斗结果
        /// </summary>
        /// <returns>战斗结果</returns>
        BattleResult GetBattleResult();

        /// <summary>
        /// 获取战斗是否已结束
        /// </summary>
        bool IsBattleEnded { get; }

        // ====================================================================
        // 实体操作
        // ====================================================================

        /// <summary>
        /// 生成实体
        /// </summary>
        int SpawnEntity(EntitySpawnInfo spawnInfo);

        /// <summary>
        /// 销毁实体
        /// </summary>
        void DestroyEntity(int entityId);

        /// <summary>
        /// 获取实体数据
        /// </summary>
        bool TryGetEntity(int entityId, out EntityData data);

        /// <summary>
        /// 判断实体是否存活
        /// </summary>
        bool IsEntityAlive(int entityId);

        /// <summary>
        /// 移动实体到目标位置
        /// </summary>
        void MoveEntityTo(int entityId, UnityEngine.Vector3 targetPosition);

        /// <summary>
        /// 对实体造成伤害
        /// </summary>
        void DamageEntity(int entityId, int damage);

        /// <summary>
        /// 治疗实体
        /// </summary>
        void HealEntity(int entityId, int amount);

        /// <summary>
        /// 获取游戏时间
        /// </summary>
        float GetGameTime();

        /// <summary>
        /// 设置时间缩放
        /// </summary>
        void SetTimeScale(float scale);

        /// <summary>
        /// 获取所有实体
        /// </summary>
        System.Collections.Generic.List<EntityData> GetAllEntities();

        /// <summary>
        /// 获取实体索引
        /// </summary>
        int GetEntityIndex(int entityId);

        /// <summary>
        /// 更新实体数据
        /// </summary>
        void UpdateEntity(int index, EntityData data);

        /// <summary>
        /// 清理待销毁的实体
        /// </summary>
        void CleanupPendingDestroyEntities();

        // ====================================================================
        // 子系统
        // ====================================================================

        /// <summary>
        /// 获取子系统
        /// </summary>
        T GetSystem<T>() where T : class, IGameSystem;

        /// <summary>
        /// 注册子系统
        /// </summary>
        void RegisterSystem<T>(T system) where T : class, IGameSystem;
    }

    // ========================================================================
    // 子系统接口
    // ========================================================================

    /// <summary>
    /// 游戏子系统接口
    /// </summary>
    public interface IGameSystem
    {
        /// <summary>
        /// 优先级（数值越小越先更新）
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// 初始化
        /// </summary>
        void Initialize(IGameplayWorld world);

        /// <summary>
        /// 帧更新
        /// </summary>
        void Update(float deltaTime);

        /// <summary>
        /// 关闭
        /// </summary>
        void Shutdown();

        /// <summary>
        /// 重置
        /// </summary>
        void Reset();
    }

    // ========================================================================
    // 实体数据结构
    // ========================================================================

    /// <summary>
    /// 实体类型
    /// </summary>
    public enum EntityType
    {
        Unknown = 0,
        Player = 1,
        Enemy = 2,
        Npc = 3,
        Projectile = 4,
        Item = 5,
        Effect = 6
    }

    /// <summary>
    /// 实体状态
    /// </summary>
    public enum EntityState
    {
        Invalid = 0,
        Active = 1,
        Dead = 2,
        PendingDestroy = 3
    }

    /// <summary>
    /// 实体数据（共用结构）
    /// </summary>
    public struct EntityData
    {
        public int Id;
        public EntityType Type;
        public EntityState State;
        public int ConfigId;
        public int CampId;
        public UnityEngine.Vector3 Position;
        public float Rotation;
        public UnityEngine.Vector3 TargetPosition;
        public bool IsMoving;
        public float MoveSpeed;
        public int Hp;
        public int MaxHp;
        public int Attack;
        public int Defense;
        public float CreateTime;
        public float LifeTime;
        public float MaxLifeTime;
    }

    /// <summary>
    /// 实体生成信息
    /// </summary>
    public struct EntitySpawnInfo
    {
        public EntityType Type;
        public int ConfigId;
        public int CampId;
        public UnityEngine.Vector3 Position;
        public float Rotation;
        public float MaxLifeTime;
    }

    // ========================================================================
    // 输入与命令数据结构
    // ========================================================================

    /// <summary>
    /// 输入类型
    /// </summary>
    public enum InputType
    {
        None = 0,
        Move = 1,           // 移动
        Attack = 2,         // 攻击
        Skill = 3,          // 释放技能
        Interact = 4,       // 交互
        Select = 5,         // 选择目标
        Cancel = 6          // 取消
    }

    /// <summary>
    /// 玩家输入
    /// </summary>
    public struct PlayerInput
    {
        /// <summary>
        /// 输入类型
        /// </summary>
        public InputType Type;

        /// <summary>
        /// 玩家 ID（多玩家时使用）
        /// </summary>
        public int PlayerId;

        /// <summary>
        /// 目标实体 ID（选择/攻击时）
        /// </summary>
        public int TargetEntityId;

        /// <summary>
        /// 目标位置（移动/技能释放位置）
        /// </summary>
        public UnityEngine.Vector3 TargetPosition;

        /// <summary>
        /// 技能 ID（释放技能时）
        /// </summary>
        public int SkillId;

        /// <summary>
        /// 输入时间戳
        /// </summary>
        public float Timestamp;
    }

    /// <summary>
    /// 命令类型
    /// </summary>
    public enum CommandType
    {
        None = 0,
        SpawnEntity = 1,        // 生成实体
        DestroyEntity = 2,      // 销毁实体
        DamageEntity = 3,       // 造成伤害
        HealEntity = 4,         // 治疗
        AddBuff = 5,            // 添加 Buff
        RemoveBuff = 6,         // 移除 Buff
        MoveEntity = 7,         // 移动实体
        SetTimeScale = 8,       // 设置时间缩放
        EndBattle = 9           // 结束战斗
    }

    /// <summary>
    /// 游戏命令
    /// </summary>
    public struct GameplayCommand
    {
        public CommandType Type;
        public int EntityId;
        public int TargetId;
        public int IntParam;
        public float FloatParam;
        public UnityEngine.Vector3 VectorParam;
    }

    // ========================================================================
    // 战斗结果
    // ========================================================================

    /// <summary>
    /// 战斗结果类型
    /// </summary>
    public enum BattleResultType
    {
        None = 0,       // 未结束
        Victory = 1,    // 胜利
        Defeat = 2,     // 失败
        Draw = 3,       // 平局
        Timeout = 4     // 超时
    }

    /// <summary>
    /// 战斗结果
    /// </summary>
    public struct BattleResult
    {
        /// <summary>
        /// 结果类型
        /// </summary>
        public BattleResultType ResultType;

        /// <summary>
        /// 战斗持续时间（秒）
        /// </summary>
        public float Duration;

        /// <summary>
        /// 获得的分数
        /// </summary>
        public int Score;

        /// <summary>
        /// 击杀数
        /// </summary>
        public int KillCount;

        /// <summary>
        /// 死亡数
        /// </summary>
        public int DeathCount;

        /// <summary>
        /// 是否已结束
        /// </summary>
        public bool IsEnded => ResultType != BattleResultType.None;
    }

    /// <summary>
    /// 玩法配置
    /// 用于初始化玩法世界时传递配置参数
    /// </summary>
    public class GameplayConfig
    {
        /// <summary>
        /// 是否启用调试模式
        /// </summary>
        public bool DebugMode { get; set; } = false;

        /// <summary>
        /// 游戏速度倍率（1.0 为正常速度）
        /// </summary>
        public float TimeScale { get; set; } = 1.0f;

        /// <summary>
        /// 最大实体数量限制
        /// </summary>
        public int MaxEntityCount { get; set; } = 1000;

        /// <summary>
        /// 是否使用 DOTS 实现（仅在支持平台有效）
        /// </summary>
        public bool UseDotsImplementation { get; set; } = false;

        /// <summary>
        /// 关卡 ID
        /// </summary>
        public int LevelId { get; set; } = 0;

        /// <summary>
        /// 难度等级
        /// </summary>
        public int Difficulty { get; set; } = 1;

        /// <summary>
        /// 随机种子（0 表示使用系统时间）
        /// </summary>
        public int RandomSeed { get; set; } = 0;
    }
}
