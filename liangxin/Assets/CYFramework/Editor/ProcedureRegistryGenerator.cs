using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CYFramework.Core.Procedure;
using UnityEditor;
using UnityEngine;

namespace CYFramework.Editor
{
    public static class ProcedureRegistryGenerator
    {
        private const string OutputFolder = "Assets/CYFramework/Resources/CYFramework";
        private const string OutputAssetPath = "Assets/CYFramework/Resources/CYFramework/ProcedureRegistry.asset";

        // IL2CPP/裁剪保护：避免 WebGL/微信/移动端在 Managed Stripping 下把流程类型裁剪掉，导致 Type.GetType 失败。
        // 说明：生成的 link.xml 会跟随工程打包生效，无需运行时反射扫描程序集。
        private const string LinkXmlPath = "Assets/CYFramework/link.xml";

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
            var preserveTypesByAssembly = new Dictionary<string, List<string>>(StringComparer.Ordinal);

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

                    var asmName = type.Assembly.GetName().Name;
                    if (!preserveTypesByAssembly.TryGetValue(asmName, out var list))
                    {
                        list = new List<string>(16);
                        preserveTypesByAssembly[asmName] = list;
                    }
                    list.Add(type.FullName);
                }
            }

            registry.Procedures = entries
                .OrderBy(e => e.Order)
                .ThenBy(e => e.TypeName)
                .ToList();

            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssets();

            // 同步生成 link.xml（抗裁剪）
            GenerateLinkXml(preserveTypesByAssembly);
            AssetDatabase.ImportAsset(LinkXmlPath);

            AssetDatabase.Refresh();

            UnityEngine.Debug.Log($"[CYFramework] ProcedureRegistry 生成完成: {registry.Procedures.Count} 个 -> {OutputAssetPath}");
        }

        /// <summary>
        /// 生成 link.xml：保留所有流程类型，避免 IL2CPP/Managed Stripping 裁剪。
        /// </summary>
        private static void GenerateLinkXml(Dictionary<string, List<string>> preserveTypesByAssembly)
        {
            try
            {
                var sb = new StringBuilder(1024);
                sb.AppendLine("<linker>");

                foreach (var kv in preserveTypesByAssembly.OrderBy(k => k.Key))
                {
                    var asmName = kv.Key;
                    var types = kv.Value;
                    if (types == null || types.Count == 0) continue;

                    sb.Append("  <assembly fullname=\"").Append(asmName).AppendLine("\">");
                    for (int i = 0; i < types.Count; i++)
                    {
                        var fullName = types[i];
                        if (string.IsNullOrEmpty(fullName)) continue;
                        sb.Append("    <type fullname=\"").Append(fullName).AppendLine("\" preserve=\"all\"/>");
                    }
                    sb.AppendLine("  </assembly>");
                }

                sb.AppendLine("</linker>");

                // 写入（UTF8 无 BOM），避免不同工具链产生重复 BOM 差异。
                File.WriteAllText(LinkXmlPath, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[CYFramework] link.xml 生成失败: {ex.Message}");
            }
        }
    }
}
