// 引用 UnityEngine，使用 CreateAssetMenu
using UnityEngine; // Unity 引擎类型引用

/// <summary>
/// 基础敌人 AI：根据公司距离与攻击范围执行追击逻辑。
/// </summary>
[CreateAssetMenu(menuName = "Game/AI/Enemy/Basic", fileName = "EnemyAIBasic")] // 创建基础 AI 资产菜单
public sealed class EnemyAIBasic : EnemyAIBase // 基础敌人 AI 定义
{
    /// <summary>AI 名称（用于调试显示）。</summary>
    public override string AIName => "Basic"; // AI 名称

    /// <summary>
    /// AI 每帧逻辑：根据公司距离与攻击范围执行追击逻辑。
    /// </summary>
    /// <param name="enemy">敌人实体。</param>
    /// <param name="deltaTime">帧时间。</param>
    public override void Tick(EnemyEntity enemy, float deltaTime) // AI Tick 实现
    {
        if (enemy == null)
        {
            return; // 实体为空时直接退出
        }

        if (!enemy.TryGetCurrentPosition(out var currentPos))
        {
            return; // 无法获取当前位置时退出
        }

        if (!enemy.TryGetCompany(out var company))
        {
            enemy.StopMovement(); // 公司不存在时停止移动
            return; // 公司不存在时退出
        }

        var attackRange = enemy.BaseStats.AttackRange; // 获取攻击范围
        var companyPos = (Vector2)company.transform.position; // 获取公司坐标
        var companyChaseDistance = company.ForceChaseDistance; // 获取公司强制追击距离
        var companyChaseSqr = companyChaseDistance * companyChaseDistance; // 计算追击距离平方
        var companyDistSqr = (companyPos - currentPos).sqrMagnitude; // 计算与公司距离平方
        if (companyDistSqr <= companyChaseSqr)
        {
            if (attackRange > 0f &&
                enemy.TryGetCompanyDistanceSqr(company, currentPos, out var companyAttackDistSqr) && // 计算公司距离平方
                companyAttackDistSqr <= attackRange * attackRange)
            {
                enemy.TryAttackCompanyWithLock(company); // 进入攻击范围则尝试攻击公司
                return; // 攻击公司后退出
            }

            var destination = enemy.AdjustStandOffDestination(currentPos, companyPos); // 计算公司身位目标点
            enemy.MoveTo(destination); // 近距离直接追公司
            return; // 近距离追公司后退出
        }

        var sightRange = enemy.SightRange; // 获取可视范围
        if (sightRange <= 0f)
        {
            if (attackRange > 0f &&
                enemy.TryGetCompanyDistanceSqr(company, currentPos, out var companyAttackDistSqr) && // 计算公司距离平方
                companyAttackDistSqr <= attackRange * attackRange)
            {
                enemy.TryAttackCompanyWithLock(company); // 进入攻击范围则尝试攻击公司
                return; // 攻击公司后退出
            }

            var destination = enemy.AdjustStandOffDestination(currentPos, companyPos); // 计算公司身位目标点
            enemy.MoveTo(destination); // 无可视范围时追公司
            return; // 追公司后退出
        }

        var target = enemy.FindChaseTargetInRange(currentPos, sightRange); // 在可视范围内寻找目标
        if (target != null)
        {
            if (attackRange > 0f &&
                enemy.TryGetTargetDistanceSqr(target, currentPos, out var distSqr) &&
                distSqr <= attackRange * attackRange)
            {
                enemy.TryAttackTargetWithLock(target); // 进入攻击范围则尝试攻击并停顿
                return; // 攻击范围内保持停下
            }

            var targetPos = (Vector2)target.transform.position; // 获取目标坐标
            var destination = enemy.AdjustStandOffDestination(currentPos, targetPos); // 计算目标身位位置
            enemy.MoveTo(destination); // 追击目标并保持身位
        }
        else
        {
            if (attackRange > 0f &&
                enemy.TryGetCompanyDistanceSqr(company, currentPos, out var companyAttackDistSqr) && // 计算公司距离平方
                companyAttackDistSqr <= attackRange * attackRange)
            {
                enemy.TryAttackCompanyWithLock(company); // 进入攻击范围则尝试攻击公司
                return; // 攻击公司后退出
            }

            var destination = enemy.AdjustStandOffDestination(currentPos, companyPos); // 计算公司身位目标点
            enemy.MoveTo(destination); // 无目标时继续追公司
        }
    }
}
