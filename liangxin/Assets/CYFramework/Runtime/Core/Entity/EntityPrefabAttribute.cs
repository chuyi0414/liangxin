using System;

namespace CYFramework.Core.Entity
{
    /// <summary>
    /// 实体预制体路径标记：路径需相对 Resources，且不包含 .prefab。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class EntityPrefabAttribute : Attribute
    {
        /// <summary>
        /// 预制体资源路径（相对 Resources）。
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// 实体类型标识（可选，为空则默认使用组件类型名）。
        /// </summary>
        public string EntityType { get; }

        /// <summary>
        /// 实体分组名（可选，为空则使用默认根节点）。
        /// </summary>
        public string GroupName { get; }

        public EntityPrefabAttribute(string path) : this(path, string.Empty, string.Empty)
        {
        }

        public EntityPrefabAttribute(string path, string entityType, string groupName = null)
        {
            Path = path;
            EntityType = entityType ?? string.Empty;
            GroupName = groupName ?? string.Empty;
        }
    }
}
