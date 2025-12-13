using System;
using System.Collections.Generic;

namespace CYFramework.Core.UI.MVVM
{
    /// <summary>
    /// TypedViewModel：用于高频 UI 刷新（避免装箱）
    /// 说明：
    /// - 与旧版 ViewModel（Dictionary<string, object>）相比，本方案用 ObservableProperty<T> 保存数据
    /// - 变更事件参数为泛型 T，避免 value type 装箱
    /// - 适合血条/倒计时/战斗 HUD 等高频刷新场景
    /// </summary>
    public abstract class TypedViewModel : IDisposable
    {
        private readonly List<IDisposable> _subscriptions = new();
        private bool _disposed;

        /// <summary>
        /// 初始化（子类可重写）
        /// </summary>
        public virtual void Initialize() { }

        /// <summary>
        /// 记录订阅（统一在 Dispose 时释放）
        /// </summary>
        protected void TrackSubscription(IDisposable disposable)
        {
            if (disposable != null)
            {
                _subscriptions.Add(disposable);
            }
        }

        /// <summary>
        /// 销毁 ViewModel（自动取消所有订阅）
        /// </summary>
        public virtual void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            for (int i = 0; i < _subscriptions.Count; i++)
            {
                _subscriptions[i]?.Dispose();
            }
            _subscriptions.Clear();
        }

        protected sealed class Subscription : IDisposable
        {
            private Action _onDispose;

            public Subscription(Action onDispose)
            {
                _onDispose = onDispose;
            }

            public void Dispose()
            {
                _onDispose?.Invoke();
                _onDispose = null;
            }
        }

        /// <summary>
        /// 订阅一个 ObservableProperty<T> 的变化
        /// 说明：
        /// - 返回 IDisposable，可手动释放
        /// - 同时会自动 TrackSubscription，确保 ViewModel.Dispose 时不会漏解绑
        /// </summary>
        protected IDisposable Subscribe<T>(ObservableProperty<T> property, ObservableProperty<T>.ChangedHandler handler)
        {
            property.Subscribe(handler);
            var sub = new Subscription(() => property.Unsubscribe(handler));
            TrackSubscription(sub);
            return sub;
        }
    }
}
