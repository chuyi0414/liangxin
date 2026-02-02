// ============================================================================
// CYFramework 2.2 - 单元测试
// 文档位置：9.1 测试分层 - 单元测试
// 范围：Core Services / 纯逻辑
// ============================================================================

using NUnit.Framework;
using CYFramework.Infrastructure;
using CYFramework.Core.Event;

namespace CYFramework.Tests.EditMode
{
    /// <summary>
    /// ServiceLocator 测试
    /// </summary>
    [TestFixture]
    public class ServiceLocatorTests
    {
        [SetUp]
        /// <summary>
        /// 每个测试前的初始化
        /// </summary>
        public void Setup()
        {
            // 每个测试前清理
            ServiceLocator.ClearAll();
        }
        
        [TearDown]
        /// <summary>
        /// 每个测试后的清理
        /// </summary>
        public void TearDown()
        {
            ServiceLocator.ClearAll();
        }
        
        [Test]
        /// <summary>
        /// 注册并获取服务
        /// </summary>
        public void Register_And_Get_Service()
        {
            // Arrange
            ServiceLocator.Register<ITestService, TestServiceImpl>();
            
            // Act
            // 获取服务实例
            var service = ServiceLocator.Get<ITestService>();
            
            // Assert
            Assert.IsNotNull(service);
            Assert.IsInstanceOf<TestServiceImpl>(service);
        }
        
        [Test]
        /// <summary>
        /// 单例服务应返回同一实例
        /// </summary>
        public void Get_Same_Singleton_Instance()
        {
            // Arrange
            ServiceLocator.Register<ITestService, TestServiceImpl>();
            
            // Act
            // 第一次获取实例
            var service1 = ServiceLocator.Get<ITestService>();
            // 第二次获取实例
            var service2 = ServiceLocator.Get<ITestService>();
            
            // Assert
            Assert.AreSame(service1, service2);
        }
        
        [Test]
        /// <summary>
        /// Scoped 服务在清理后应变更实例
        /// </summary>
        public void Scoped_Service_Different_After_Clear()
        {
            // Arrange
            ServiceLocator.Register<ITestService, TestServiceImpl>(ServiceScope.Scoped);
            // 第一次获取实例
            var service1 = ServiceLocator.Get<ITestService>();
            
            // Act
            ServiceLocator.ClearScoped();
            // 第二次获取实例
            var service2 = ServiceLocator.Get<ITestService>();
            
            // Assert
            Assert.AreNotSame(service1, service2);
        }
        
        [Test]
        /// <summary>
        /// Transient 服务每次获取都新建
        /// </summary>
        public void Transient_Always_New_Instance()
        {
            // Arrange
            ServiceLocator.Register<ITestService, TestServiceImpl>(ServiceScope.Transient);
            
            // Act
            // 第一次获取实例
            var service1 = ServiceLocator.Get<ITestService>();
            // 第二次获取实例
            var service2 = ServiceLocator.Get<ITestService>();
            
            // Assert
            Assert.AreNotSame(service1, service2);
        }
        
        [Test]
        /// <summary>
        /// 未注册时 TryGet 返回 false
        /// </summary>
        public void TryGet_Returns_False_When_Not_Registered()
        {
            // Act
            // 是否成功获取
            bool found = ServiceLocator.TryGet<ITestService>(out var service); // service 为获取到的实例
            
            // Assert
            Assert.IsFalse(found);
            Assert.IsNull(service);
        }
        
        [Test]
        /// <summary>
        /// 注册实例并获取
        /// </summary>
        public void Register_Instance()
        {
            // Arrange
            // 服务实例
            var instance = new TestServiceImpl();
            ServiceLocator.RegisterInstance<ITestService>(instance);
            
            // Act
            // 获取实例
            var retrieved = ServiceLocator.Get<ITestService>();
            
            // Assert
            Assert.AreSame(instance, retrieved);
        }
        
        // 测试用接口和实现
        /// <summary>
        /// 测试用接口
        /// </summary>
        private interface ITestService { }
        /// <summary>
        /// 测试用实现
        /// </summary>
        private class TestServiceImpl : ITestService { }
    }
    
    /// <summary>
    /// EventBus 测试
    /// </summary>
    [TestFixture]
    public class EventBusTests
    {
        /// <summary>
        /// 事件总线实例
        /// </summary>
        private EventBus _eventBus;
        /// <summary>
        /// 是否收到事件
        /// </summary>
        private bool _received;
        /// <summary>
        /// 接收到的数值
        /// </summary>
        private int _receivedValue;
        /// <summary>
        /// 回调次数
        /// </summary>
        private int _callCount;
        /// <summary>
        /// 回调顺序记录
        /// </summary>
        private System.Collections.Generic.List<int> _order;
        
        [SetUp]
        /// <summary>
        /// 每个测试前的初始化
        /// </summary>
        public void Setup()
        {
            _eventBus = new EventBus();
            _received = false;
            _receivedValue = 0;
            _callCount = 0;
            _order = new System.Collections.Generic.List<int>();
        }
        
        [TearDown]
        /// <summary>
        /// 每个测试后的清理
        /// </summary>
        public void TearDown()
        {
            _eventBus.Dispose();
        }
        
        [Test]
        /// <summary>
        /// 订阅并派发事件
        /// </summary>
        public void Subscribe_And_Post_Event()
        {
            // Arrange
            _eventBus.Subscribe<TestEvent>((ref TestEvent e) => _received = true, this);
            
            // Act
            // 测试事件
            var evt = new TestEvent { Value = 42 };
            _eventBus.Post(ref evt);
            
            // Assert
            Assert.IsTrue(_received);
        }
        
        [Test]
        /// <summary>
        /// 事件数据正确传递
        /// </summary>
        public void Event_Contains_Correct_Data()
        {
            // Arrange
            _eventBus.Subscribe<TestEvent>((ref TestEvent e) => _receivedValue = e.Value, this);
            
            // Act
            // 测试事件
            var evt = new TestEvent { Value = 123 };
            _eventBus.Post(ref evt);
            
            // Assert
            Assert.AreEqual(123, _receivedValue);
        }
        
        [Test]
        /// <summary>
        /// 取消订阅后停止接收
        /// </summary>
        public void Unsubscribe_Stops_Receiving()
        {
            // Arrange
            // 处理函数
            void Handler(ref TestEvent e) => _callCount++;
            
            _eventBus.Subscribe<TestEvent>(Handler, this);
            
            // Act
            // 第一次事件
            var evt1 = new TestEvent();
            _eventBus.Post(ref evt1);
            
            _eventBus.Unsubscribe<TestEvent>(Handler);
            
            // 第二次事件
            var evt2 = new TestEvent();
            _eventBus.Post(ref evt2);
            
            // Assert
            Assert.AreEqual(1, _callCount);
        }
        
        [Test]
        /// <summary>
        /// 事件优先级顺序
        /// </summary>
        public void Priority_Ordering()
        {
            // Arrange
            _eventBus.Subscribe<TestEvent>((ref TestEvent e) => _order.Add(1), this, priority: 1);
            _eventBus.Subscribe<TestEvent>((ref TestEvent e) => _order.Add(3), this, priority: 3);
            _eventBus.Subscribe<TestEvent>((ref TestEvent e) => _order.Add(2), this, priority: 2);
            
            // Act
            // 测试事件
            var evt = new TestEvent();
            _eventBus.Post(ref evt);
            
            // Assert - 优先级小的先执行（按文档）
            Assert.AreEqual(1, _order[0]);
            Assert.AreEqual(2, _order[1]);
            Assert.AreEqual(3, _order[2]);
        }
        
        /// <summary>
        /// 测试事件
        /// </summary>
        private struct TestEvent
        {
            /// <summary>
            /// 事件数值
            /// </summary>
            public int Value;
        }
    }
}
