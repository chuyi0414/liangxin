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
        public void Setup()
        {
            // 每个测试前清理
            ServiceLocator.ClearAll();
        }
        
        [TearDown]
        public void TearDown()
        {
            ServiceLocator.ClearAll();
        }
        
        [Test]
        public void Register_And_Get_Service()
        {
            // Arrange
            ServiceLocator.Register<ITestService, TestServiceImpl>();
            
            // Act
            var service = ServiceLocator.Get<ITestService>();
            
            // Assert
            Assert.IsNotNull(service);
            Assert.IsInstanceOf<TestServiceImpl>(service);
        }
        
        [Test]
        public void Get_Same_Singleton_Instance()
        {
            // Arrange
            ServiceLocator.Register<ITestService, TestServiceImpl>();
            
            // Act
            var service1 = ServiceLocator.Get<ITestService>();
            var service2 = ServiceLocator.Get<ITestService>();
            
            // Assert
            Assert.AreSame(service1, service2);
        }
        
        [Test]
        public void Scoped_Service_Different_After_Clear()
        {
            // Arrange
            ServiceLocator.Register<ITestService, TestServiceImpl>(ServiceScope.Scoped);
            var service1 = ServiceLocator.Get<ITestService>();
            
            // Act
            ServiceLocator.ClearScoped();
            var service2 = ServiceLocator.Get<ITestService>();
            
            // Assert
            Assert.AreNotSame(service1, service2);
        }
        
        [Test]
        public void Transient_Always_New_Instance()
        {
            // Arrange
            ServiceLocator.Register<ITestService, TestServiceImpl>(ServiceScope.Transient);
            
            // Act
            var service1 = ServiceLocator.Get<ITestService>();
            var service2 = ServiceLocator.Get<ITestService>();
            
            // Assert
            Assert.AreNotSame(service1, service2);
        }
        
        [Test]
        public void TryGet_Returns_False_When_Not_Registered()
        {
            // Act
            bool found = ServiceLocator.TryGet<ITestService>(out var service);
            
            // Assert
            Assert.IsFalse(found);
            Assert.IsNull(service);
        }
        
        [Test]
        public void Register_Instance()
        {
            // Arrange
            var instance = new TestServiceImpl();
            ServiceLocator.RegisterInstance<ITestService>(instance);
            
            // Act
            var retrieved = ServiceLocator.Get<ITestService>();
            
            // Assert
            Assert.AreSame(instance, retrieved);
        }
        
        // 测试用接口和实现
        private interface ITestService { }
        private class TestServiceImpl : ITestService { }
    }
    
    /// <summary>
    /// EventBus 测试
    /// </summary>
    [TestFixture]
    public class EventBusTests
    {
        private EventBus _eventBus;
        private bool _received;
        private int _receivedValue;
        private int _callCount;
        private System.Collections.Generic.List<int> _order;
        
        [SetUp]
        public void Setup()
        {
            _eventBus = new EventBus();
            _received = false;
            _receivedValue = 0;
            _callCount = 0;
            _order = new System.Collections.Generic.List<int>();
        }
        
        [TearDown]
        public void TearDown()
        {
            _eventBus.Dispose();
        }
        
        [Test]
        public void Subscribe_And_Post_Event()
        {
            // Arrange
            _eventBus.Subscribe<TestEvent>((ref TestEvent e) => _received = true, this);
            
            // Act
            var evt = new TestEvent { Value = 42 };
            _eventBus.Post(ref evt);
            
            // Assert
            Assert.IsTrue(_received);
        }
        
        [Test]
        public void Event_Contains_Correct_Data()
        {
            // Arrange
            _eventBus.Subscribe<TestEvent>((ref TestEvent e) => _receivedValue = e.Value, this);
            
            // Act
            var evt = new TestEvent { Value = 123 };
            _eventBus.Post(ref evt);
            
            // Assert
            Assert.AreEqual(123, _receivedValue);
        }
        
        [Test]
        public void Unsubscribe_Stops_Receiving()
        {
            // Arrange
            void Handler(ref TestEvent e) => _callCount++;
            
            _eventBus.Subscribe<TestEvent>(Handler, this);
            
            // Act
            var evt1 = new TestEvent();
            _eventBus.Post(ref evt1);
            
            _eventBus.Unsubscribe<TestEvent>(Handler);
            
            var evt2 = new TestEvent();
            _eventBus.Post(ref evt2);
            
            // Assert
            Assert.AreEqual(1, _callCount);
        }
        
        [Test]
        public void Priority_Ordering()
        {
            // Arrange
            _eventBus.Subscribe<TestEvent>((ref TestEvent e) => _order.Add(1), this, priority: 1);
            _eventBus.Subscribe<TestEvent>((ref TestEvent e) => _order.Add(3), this, priority: 3);
            _eventBus.Subscribe<TestEvent>((ref TestEvent e) => _order.Add(2), this, priority: 2);
            
            // Act
            var evt = new TestEvent();
            _eventBus.Post(ref evt);
            
            // Assert - 优先级小的先执行（按文档）
            Assert.AreEqual(1, _order[0]);
            Assert.AreEqual(2, _order[1]);
            Assert.AreEqual(3, _order[2]);
        }
        
        private struct TestEvent
        {
            public int Value;
        }
    }
}
