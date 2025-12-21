// ============================================================================
// CYFramework 2.2 - 输入缓冲系统
// 文档位置：3.2.1 输入缓冲 (Input Buffering)
// 功能：防止 Update/FixedUpdate 频率不同步导致的丢键问题
// ============================================================================

using System.Collections.Generic;
using CYFramework.Infrastructure;

namespace CYFramework.Gameplay.Abstraction
{
    /// <summary>
    /// 输入缓冲
    /// Update 收集输入 → 压入队列 → FixedTick 消费
    /// </summary>
    public class InputBuffer
    {
        /// <summary>
        /// 输入命令队列
        /// </summary>
        private readonly Queue<InputCommand> _buffer;
        /// <summary>
        /// 最大容量
        /// </summary>
        private readonly int _maxCapacity;
        
        /// <summary>
        /// 缓冲区中的命令数量
        /// </summary>
        public int Count => _buffer.Count;
        
        /// <summary>
        /// 创建输入缓冲
        /// </summary>
        /// <param name="maxCapacity">最大容量（防止积压）</param>
        public InputBuffer(int maxCapacity = 32)
        {
            _maxCapacity = maxCapacity;
            _buffer = new Queue<InputCommand>(maxCapacity);
        }
        
        /// <summary>
        /// 压入输入命令
        /// </summary>
        public void Enqueue(InputCommand command)
        {
            if (_buffer.Count >= _maxCapacity)
            {
                // 缓冲区满，丢弃最旧的命令
                _buffer.Dequeue();
                CYLog.Warning("[InputBuffer] 缓冲区满，丢弃旧命令");
            }
            
            _buffer.Enqueue(command);
        }
        
        /// <summary>
        /// 尝试取出输入命令
        /// </summary>
        public bool TryDequeue(out InputCommand command)
        {
            if (_buffer.Count > 0)
            {
                command = _buffer.Dequeue();
                return true;
            }
            
            command = default;
            return false;
        }
        
        /// <summary>
        /// 清空缓冲区
        /// </summary>
        public void Clear()
        {
            _buffer.Clear();
        }
        
        /// <summary>
        /// 查看队首命令（不移除）
        /// </summary>
        public bool TryPeek(out InputCommand command)
        {
            if (_buffer.Count > 0)
            {
                command = _buffer.Peek();
                return true;
            }
            
            command = default;
            return false;
        }
    }
}
