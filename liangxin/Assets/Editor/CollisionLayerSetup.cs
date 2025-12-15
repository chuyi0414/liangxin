using UnityEngine;
using UnityEditor;

/// <summary>
/// 自动配置项目层级与物理碰撞矩阵
/// 包含：BaseCamp, Boss, Employee, Monster
/// </summary>
public class CollisionLayerSetup
{
    [MenuItem("Tools/自动配置/应用碰撞层级设置 (Apply Layer Rules)")]
    public static void SetupLayersAndMatrix()
    {
        // 1. 修改 TagManager 中的 Layers
        SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");
        
        // 定义我们需要的层级 ID
        int layerBase = 6;
        int layerBoss = 7;
        int layerEmp = 8;
        int layerMob = 9;

        // 设置 Layer 名称
        SetLayerName(layers, layerBase, "BaseCamp");
        SetLayerName(layers, layerBoss, "Boss");
        SetLayerName(layers, layerEmp, "Employee");
        SetLayerName(layers, layerMob, "Monster");
        
        tagManager.ApplyModifiedProperties();
        
        // 2. 配置 Physics 2D 碰撞矩阵
        // 参数: (LayerA, LayerB, ignore?) -> true表示忽略/不碰撞，false表示碰撞
        
        // --- 重置/确保碰撞 (Collide) ---
        // 大本营(BaseCamp) vs 所有人 -> 碰撞
        Physics2D.IgnoreLayerCollision(layerBase, layerBoss, false);
        Physics2D.IgnoreLayerCollision(layerBase, layerEmp, false);
        Physics2D.IgnoreLayerCollision(layerBase, layerMob, false);
        
        // --- 设置重叠/忽略 (Overlap/Ignore) ---
        
        // 员工 vs 老板 -> 重叠 (忽略碰撞)
        Physics2D.IgnoreLayerCollision(layerEmp, layerBoss, true);
        
        // 员工 vs 怪物 -> 重叠
        Physics2D.IgnoreLayerCollision(layerEmp, layerMob, true);
        
        // 老板 vs 怪物 -> 重叠
        Physics2D.IgnoreLayerCollision(layerBoss, layerMob, true);
        
        // --- 自体碰撞 (根据需求通常设为忽略，避免拥挤) ---
        // 员工内部、怪物内部、老板内部 -> 忽略
        Physics2D.IgnoreLayerCollision(layerEmp, layerEmp, true);
        Physics2D.IgnoreLayerCollision(layerMob, layerMob, true);
        Physics2D.IgnoreLayerCollision(layerBoss, layerBoss, true);
        
        Debug.Log($"<color=#00FF00><b>[CollisionSetup]</b></color> 碰撞规则已成功应用！\n" +
                  $"Layer {layerBase}: BaseCamp\n" +
                  $"Layer {layerBoss}: Boss\n" +
                  $"Layer {layerEmp}: Employee\n" +
                  $"Layer {layerMob}: Monster");
    }

    private static void SetLayerName(SerializedProperty layers, int index, string name)
    {
        SerializedProperty element = layers.GetArrayElementAtIndex(index);
        if (element.stringValue != name)
        {
            element.stringValue = name;
            // Tips: 实际修改可能需要重启编辑器或重新聚焦才能看到 Inspector 刷新，但逻辑已生效
        }
    }
}
