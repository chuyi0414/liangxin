using UnityEngine;
using CYFramework.Infrastructure;
using CYFramework.Core.Event;

/// <summary>
/// 框架测试脚本
/// 用于验证 CYFramework 是否正常工作
/// </summary>
public class FrameworkTest : MonoBehaviour
{
    void Start()
    {
        // 测试日志系统
        CYLog.Info("========================================");
        CYLog.Info("《良心防线》框架测试开始");
        CYLog.Info("========================================");

        // 测试服务定位器
        TestServiceLocator();

        // 测试事件系统
        TestEventBus();

        CYLog.Info("========================================");
        CYLog.Info("所有测试通过！框架工作正常！");
        CYLog.Info("========================================");
    }

    /// <summary>
    /// 测试服务定位器
    /// </summary>
    void TestServiceLocator()
    {
        CYLog.Info("[测试] 服务定位器...");

        // 获取事件总线
        var eventBus = ServiceLocator.Get<EventBus>();
        if (eventBus != null)
        {
            CYLog.Info("  ✓ EventBus 获取成功");
        }
        else
        {
            CYLog.Error("  ✗ EventBus 获取失败！");
        }
    }

    /// <summary>
    /// 测试事件系统
    /// </summary>
    void TestEventBus()
    {
        CYLog.Info("[测试] 事件系统...");

        var eventBus = ServiceLocator.Get<EventBus>();

        // 订阅测试事件
        eventBus.Subscribe<TestEvent>(OnTestEvent, this);

        // 发布测试事件
        var evt = new TestEvent { Message = "Hello 良心防线!" };
        eventBus.Post(ref evt);

        // 取消订阅
        eventBus.UnsubscribeAll(this);

        CYLog.Info("  ✓ 事件系统工作正常");
    }

    /// <summary>
    /// 测试事件处理方法
    /// </summary>
    void OnTestEvent(ref TestEvent evt)
    {
        CYLog.Info($"  → 收到事件: {evt.Message}");
    }
}

/// <summary>
/// 测试用事件（必须是 struct）
/// </summary>
public struct TestEvent
{
    public string Message;
}