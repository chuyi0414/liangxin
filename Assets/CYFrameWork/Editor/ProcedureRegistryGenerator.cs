using System;
using System.Collections.Generic;
using System.Linq;
using CYFramework.Core.Procedure;
using UnityEditor;
using UnityEngine;

namespace CYFramework.Editor
{
    public static class ProcedureRegistryGenerator
    {
        private const string OutputFolder = "Assets/CYFramework/Resources/CYFramework";
        private const string OutputAssetPath = "Assets/CYFramework/Resources/CYFramework/ProcedureRegistry.asset";

        [MenuItem("CYFramework/Generate Procedure Registry")]
        private static void Generate()
        {
            var registry = AssetDatabase.LoadAssetAtPath<ProcedureRegistryAsset>(OutputAssetPath);
            if (registry == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/CYFramework/Resources"))
                {
                    AssetDatabase.CreateFolder("Assets/CYFramework", "Resources");
                }

                if (!AssetDatabase.IsValidFolder(OutputFolder))
                {
                    if (!AssetDatabase.IsValidFolder("Assets/CYFramework/Resources/CYFramework"))
                    {
                        AssetDatabase.CreateFolder("Assets/CYFramework/Resources", "CYFramework");
                    }
                }

                registry = ScriptableObject.CreateInstance<ProcedureRegistryAsset>();
                AssetDatabase.CreateAsset(registry, OutputAssetPath);
            }

            var entries = new List<ProcedureRegistryEntry>();

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                var asm = assemblies[i];

                // 过滤掉系统/Unity 程序集，减少扫描开销
                var fullName = asm.FullName;
                if (fullName.StartsWith("Unity") || fullName.StartsWith("System") || fullName.StartsWith("mscorlib") || fullName.StartsWith("netstandard"))
                {
                    continue;
                }

                Type[] types;
                try
                {
                    types = asm.GetTypes();
                }
                catch
                {
                    continue;
                }

                for (int t = 0; t < types.Length; t++)
                {
                    var type = types[t];
                    if (type == null || !type.IsClass || type.IsAbstract) continue;
                    if (!typeof(ProcedureBase).IsAssignableFrom(type)) continue;

                    var attr = type.GetCustomAttributes(typeof(AutoRegisterProcedureAttribute), inherit: false)
                        .FirstOrDefault() as AutoRegisterProcedureAttribute;
                    if (attr == null) continue;

                    entries.Add(new ProcedureRegistryEntry
                    {
                        Name = attr.Name,
                        TypeName = type.AssemblyQualifiedName,
                        Order = attr.Order
                    });
                }
            }

            registry.Procedures = entries
                .OrderBy(e => e.Order)
                .ThenBy(e => e.TypeName)
                .ToList();

            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            UnityEngine.Debug.Log($"[CYFramework] ProcedureRegistry 生成完成: {registry.Procedures.Count} 个 -> {OutputAssetPath}");
        }
    }
}
