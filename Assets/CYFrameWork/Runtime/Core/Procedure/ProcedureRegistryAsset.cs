using System;
using System.Collections.Generic;
using UnityEngine;

namespace CYFramework.Core.Procedure
{
    [Serializable]
    public class ProcedureRegistryEntry
    {
        [Tooltip("流程显示名（可选，不填则用类型名推导）")]
        public string Name;

        [Tooltip("流程类型全名（AssemblyQualifiedName）")]
        public string TypeName;

        [Tooltip("注册顺序（越小越先注册）")]
        public int Order;
    }

    [CreateAssetMenu(menuName = "CYFramework/Procedure Registry", fileName = "ProcedureRegistry")]
    public class ProcedureRegistryAsset : ScriptableObject
    {
        [Tooltip("流程注册表")]
        public List<ProcedureRegistryEntry> Procedures = new();
    }
}
