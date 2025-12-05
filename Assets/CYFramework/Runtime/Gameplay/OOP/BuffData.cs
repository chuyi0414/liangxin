// ============================================================================
// CYFramework - Buff 数据定义
// 定义 Buff/状态效果的数据结构
// ============================================================================

namespace CYFramework.Runtime.Gameplay.OOP
{
    /// <summary>
    /// Buff 类型
    /// </summary>
    public enum BuffType
    {
        /// <summary>
        /// 未知
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// 增益（加血、加攻、加速等）
        /// </summary>
        Buff = 1,

        /// <summary>
        /// 减益（中毒、减速、虚弱等）
        /// </summary>
        Debuff = 2,

        /// <summary>
        /// 控制（眩晕、冰冻、沉默等）
        /// </summary>
        Control = 3
    }

    /// <summary>
    /// Buff 效果类型
    /// </summary>
    public enum BuffEffectType
    {
        /// <summary>
        /// 无效果
        /// </summary>
        None = 0,

        /// <summary>
        /// 修改攻击力（值为百分比，100 = +100%）
        /// </summary>
        ModifyAttack = 1,

        /// <summary>
        /// 修改防御力
        /// </summary>
        ModifyDefense = 2,

        /// <summary>
        /// 修改移动速度
        /// </summary>
        ModifyMoveSpeed = 3,

        /// <summary>
        /// 持续回血
        /// </summary>
        HealOverTime = 4,

        /// <summary>
        /// 持续掉血（中毒等）
        /// </summary>
        DamageOverTime = 5,

        /// <summary>
        /// 眩晕（无法移动和攻击）
        /// </summary>
        Stun = 6,

        /// <summary>
        /// 减速
        /// </summary>
        Slow = 7,

        /// <summary>
        /// 护盾（吸收伤害）
        /// </summary>
        Shield = 8
    }

    /// <summary>
    /// Buff 数据
    /// </summary>
    public struct BuffData
    {
        /// <summary>
        /// Buff 唯一 ID（运行时生成）
        /// </summary>
        public int Id;

        /// <summary>
        /// Buff 配置 ID
        /// </summary>
        public int ConfigId;

        /// <summary>
        /// 所属实体 ID
        /// </summary>
        public int OwnerId;

        /// <summary>
        /// 施加者实体 ID
        /// </summary>
        public int CasterId;

        /// <summary>
        /// Buff 类型
        /// </summary>
        public BuffType Type;

        /// <summary>
        /// 效果类型
        /// </summary>
        public BuffEffectType EffectType;

        /// <summary>
        /// 效果数值
        /// </summary>
        public int EffectValue;

        /// <summary>
        /// 已存在时间
        /// </summary>
        public float ElapsedTime;

        /// <summary>
        /// 持续时间（<=0 表示永久）
        /// </summary>
        public float Duration;

        /// <summary>
        /// 触发间隔（用于持续伤害/回血等）
        /// </summary>
        public float TickInterval;

        /// <summary>
        /// 上次触发时间
        /// </summary>
        public float LastTickTime;

        /// <summary>
        /// 当前层数
        /// </summary>
        public int StackCount;

        /// <summary>
        /// 最大层数
        /// </summary>
        public int MaxStackCount;

        /// <summary>
        /// 是否已过期
        /// </summary>
        public bool IsExpired;
    }

    /// <summary>
    /// 添加 Buff 的参数
    /// </summary>
    public struct AddBuffParams
    {
        public int ConfigId;
        public int TargetId;
        public int CasterId;
        public BuffType Type;
        public BuffEffectType EffectType;
        public int EffectValue;
        public float Duration;
        public float TickInterval;
        public int MaxStackCount;
    }
}
