// ============================================================================
// CYFramework - 编辑器窗口
// 框架调试面板、模块状态查看、性能监控
// ============================================================================

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using CYFramework.Runtime.Core;
using CYFramework.Runtime.Gameplay.OOP;

namespace CYFramework.Editor
{
    /// <summary>
    /// CYFramework 编辑器窗口
    /// </summary>
    public class CYFrameworkEditorWindow : EditorWindow
    {
        private Vector2 _scrollPos;

        [MenuItem("CYFramework/Framework Window")]
        public static void ShowWindow()
        {
            GetWindow<CYFrameworkEditorWindow>("CYFramework");
        }

        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            GUILayout.Label("CYFramework 调试面板", EditorStyles.boldLabel);
            GUILayout.Space(10);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("框架未运行，请进入 Play 模式查看状态", MessageType.Info);
                EditorGUILayout.EndScrollView();
                return;
            }

            var entry = CYFrameworkEntry.Instance;
            if (entry == null)
            {
                EditorGUILayout.HelpBox("未找到 CYFrameworkEntry 实例", MessageType.Warning);
                EditorGUILayout.EndScrollView();
                return;
            }

            // 框架状态
            DrawFrameworkStatus(entry);
            
            GUILayout.Space(10);
            
            // 模块状态
            DrawModuleStatus(entry);
            
            GUILayout.Space(10);
            
            // 玩法世界状态
            DrawGameplayWorldStatus(entry);

            EditorGUILayout.EndScrollView();
        }

        private void DrawFrameworkStatus(CYFrameworkEntry entry)
        {
            GUILayout.Label("框架状态", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("初始化", entry.IsInitialized ? "✓ 是" : "✗ 否");
            EditorGUILayout.LabelField("运行时间", $"{Time.realtimeSinceStartup:F1} 秒");
            EditorGUILayout.LabelField("帧率", $"{1f / Time.deltaTime:F0} FPS");
            EditorGUI.indentLevel--;
        }

        private void DrawModuleStatus(CYFrameworkEntry entry)
        {
            GUILayout.Label("模块状态", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            // 定时器
            if (entry.Timer != null)
            {
                EditorGUILayout.LabelField("定时器", $"活跃: {entry.Timer.ActiveTimerCount}");
            }

            // 对象池
            if (entry.Pool != null)
            {
                EditorGUILayout.LabelField("对象池", $"缓存类型: {entry.Pool.CachedCount}");
            }

            // 资源
            if (entry.Resource != null)
            {
                EditorGUILayout.LabelField("资源缓存", $"数量: {entry.Resource.CachedCount}");
            }

            // 调度器
            if (entry.Scheduler != null)
            {
                EditorGUILayout.LabelField("调度器", $"待执行: {entry.Scheduler.PendingCount}");
            }

            // 声音
            if (entry.Sound != null)
            {
                EditorGUILayout.LabelField("音量", $"主:{entry.Sound.MasterVolume:P0} BGM:{entry.Sound.BGMVolume:P0} SFX:{entry.Sound.SFXVolume:P0}");
            }

            // 流程
            if (entry.Procedure != null && entry.Procedure.CurrentProcedure != null)
            {
                EditorGUILayout.LabelField("当前流程", entry.Procedure.CurrentProcedureType?.Name ?? "无");
            }

            EditorGUI.indentLevel--;
        }

        private void DrawGameplayWorldStatus(CYFrameworkEntry entry)
        {
            GUILayout.Label("玩法世界", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            if (entry.GameplayWorld != null)
            {
                EditorGUILayout.LabelField("状态", entry.GameplayWorld.IsRunning ? "运行中" : "已暂停");
                
                // 如果是 OOP 实现，显示更多信息
                if (entry.GameplayWorld is GameplayWorldOOP oopWorld)
                {
                    EditorGUILayout.LabelField("实体数量", oopWorld.EntityCount.ToString());
                    EditorGUILayout.LabelField("游戏时间", $"{oopWorld.GetGameTime():F1} 秒");
                }
            }
            else
            {
                EditorGUILayout.LabelField("状态", "未创建");
            }

            EditorGUI.indentLevel--;
        }

        private void OnInspectorUpdate()
        {
            if (Application.isPlaying)
            {
                Repaint();
            }
        }
    }
}
#endif
