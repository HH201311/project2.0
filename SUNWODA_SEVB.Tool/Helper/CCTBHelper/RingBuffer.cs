using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Tool.Helper.CCTBHelper
{
    /// <summary>
    /// 固定长度的循环缓存
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public sealed class RingBuffer<T> : IDisposable
    {
        private readonly T[] _buf;
        private int _head = 0;
        private int _tail = 0;
        private int _count = 0;

        private bool _disposed = false;

        public RingBuffer(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be > 0.");
            _buf = new T[capacity];
        }

        public void Add(T item)
        {
            ThrowIfDisposed();

            _buf[_tail] = item;
            _tail = (_tail + 1) % _buf.Length;
            if (_count == _buf.Length)
            {
                // 覆盖最旧
                _head = (_head + 1) % _buf.Length;
            }
            else
            {
                _count++;
            }
        }

        public void Clear()
        {
            ThrowIfDisposed();

            // 如果存储的是Disposable对象，需要主动释放
            if (typeof(IDisposable).IsAssignableFrom(typeof(T)))
            {
                Array.ForEach(_buf, item =>
                {
                    if (item is IDisposable disposable) disposable.Dispose();
                });
            }
            else
            {
                Array.Clear(_buf, 0, _buf.Length);
            }

            _head = 0;
            _tail = 0;
            _count = 0;
        }

        public IEnumerable<T> TakeAll()
        {
            ThrowIfDisposed();

            for (int i = 0; i < _count; i++)
            {
                var idx = (_head + i) % _buf.Length;
                yield return _buf[idx];
            }
        }

        // IDisposable 实现
        public void Dispose()
        {
            if (_disposed) return;
            // 清理托管引用，帮助 GC 提早回收
            Clear();
            _disposed = true;
            // 如果未来需要释放非托管资源，可以在这里释放
            GC.SuppressFinalize(this);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(RingBuffer<T>), "RingBuffer has been disposed.");
        }
    }
}
