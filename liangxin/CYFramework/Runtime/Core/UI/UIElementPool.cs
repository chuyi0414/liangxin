using System; // 系统命名空间引用
using CYFramework.Core.Pool; // 对象池引用
using UnityEngine; // Unity 引擎引用

namespace CYFramework.Core.UI // UI 命名空间
{
    /// <summary>
    /// UI 元素对象池包装。
    /// </summary>
    public sealed class UIElementPool // UI 元素对象池包装
    {
        /// <summary>底层对象池。</summary>
        private readonly GameObjectPool _pool; // 底层对象池

        /// <summary>
        /// 构造 UI 元素对象池。
        /// </summary>
        /// <param name="pool">底层对象池。</param>
        public UIElementPool(GameObjectPool pool) // 构造函数
        {
            _pool = pool ?? throw new ArgumentNullException(nameof(pool)); // 绑定底层对象池
        }

        /// <summary>
        /// 预热对象池。
        /// </summary>
        public void Warmup() // 预热接口
        {
            _pool.Warmup(); // 调用底层预热
        }

        /// <summary>
        /// 从池中获取 UI 元素（默认位置与旋转）。
        /// </summary>
        /// <param name="parent">目标父节点。</param>
        public GameObject Get(RectTransform parent) // 获取对象
        {
            var go = _pool.Get(Vector3.zero, Quaternion.identity, parent); // 从池中取对象
            if (go == null) // 判空检查
            {
                return null; // 返回空
            }

            FixUiZ(go); // 修正 UI Z
            return go; // 返回对象
        }

        /// <summary>
        /// 从池中获取 UI 元素（指定位置与旋转）。
        /// </summary>
        /// <param name="position">目标位置。</param>
        /// <param name="rotation">目标旋转。</param>
        /// <param name="parent">目标父节点。</param>
        public GameObject Get(Vector3 position, Quaternion rotation, RectTransform parent) // 获取对象
        {
            var go = _pool.Get(position, rotation, parent); // 从池中取对象
            if (go == null) // 判空检查
            {
                return null; // 返回空
            }

            FixUiZ(go); // 修正 UI Z
            return go; // 返回对象
        }

        /// <summary>
        /// 归还 UI 元素。
        /// </summary>
        /// <param name="go">目标对象。</param>
        public void Return(GameObject go) // 归还对象
        {
            if (go == null) // 判空检查
            {
                return; // 直接返回
            }

            _pool.Return(go); // 归还对象到池
        }

        /// <summary>
        /// 修正 UI 元素 Z 坐标。
        /// </summary>
        /// <param name="go">目标对象。</param>
        private static void FixUiZ(GameObject go) // 修正方法
        {
            if (go == null) // 判空检查
            {
                return; // 直接返回
            }

            var rectTransform = go.transform as RectTransform; // 获取 RectTransform
            if (rectTransform == null) // 判空检查
            {
                return; // 直接返回
            }

            var pos = rectTransform.anchoredPosition3D; // 获取三维位置
            pos.z = 0f; // 修正 Z
            rectTransform.anchoredPosition3D = pos; // 写回位置
        }
    }
}
