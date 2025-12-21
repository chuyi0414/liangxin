using System;
using System.Collections.Generic;
using UnityEngine;

namespace CYFramework.Core.Procedure
{
    [Serializable]
    /// <summary>
    /// 流程注册表条目
    /// </summary>
    public class ProcedureRegistryEntry
    {
        /// <summary>
        /// 流程显示名（可选，不填则用类型名推导）
        /// </summary>
        [Tooltip("流程显示名（可选，不填则用类型名推导）")]
        /// <summary>
        /// 流程显示名
        /// </summary>
        public string Name;

        /// <summary>
        /// 流程类型全名（AssemblyQualifiedName）
        /// </summary>
        [Tooltip("流程类型全名（AssemblyQualifiedName）")]
        /// <summary>
        /// 流程类型全名（含程序集限定名）
        /// </summary>
        public string TypeName;

        /// <summary>
        /// 注册顺序（越小越先注册）
        /// </summary>
        [Tooltip("注册顺序（越小越先注册）")]
        /// <summary>
        /// 注册顺序（数值越小越靠前）
        /// </summary>
        public int Order;
    }

    [CreateAssetMenu(menuName = "CYFramework/Procedure Registry", fileName = "ProcedureRegistry")]
    /// <summary>
    /// 流程注册表资产（用于运行期快速注册流程）
    /// </summary>
    public class ProcedureRegistryAsset : ScriptableObject
    {
        /// <summary>
        /// 流程注册表
        /// </summary>
        [Tooltip("流程注册表")]
        /// <summary>
        /// 流程注册列表
        /// </summary>
        public List<ProcedureRegistryEntry> Procedures = new();
    }
}
