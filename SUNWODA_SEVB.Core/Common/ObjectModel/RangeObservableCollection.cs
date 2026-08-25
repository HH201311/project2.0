using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Common.ObjectModel
{
    public class RangeObservableCollection<T> : ObservableCollection<T>
    {
        private bool _suppressNotification = false;
        private readonly object _syncRoot = new object();

        /// <summary>
        /// 批量添加元素（线程安全）
        /// </summary>
        public void AddRange(IEnumerable<T> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));

            lock (_syncRoot)
            {
                _suppressNotification = true;
                foreach (var item in items)
                {
                    Add(item);
                }
                _suppressNotification = false;
            }

            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        /// <summary>
        /// 批量移除元素
        /// </summary>
        public void RemoveRange(IEnumerable<T> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));

            lock (_syncRoot)
            {
                _suppressNotification = true;
                foreach (var item in items)
                {
                    Remove(item);
                }
                _suppressNotification = false;
            }

            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        /// <summary>
        /// 移除指定范围的元素（按索引）
        /// </summary>
        public void RemoveRange(int index, int count)
        {
            if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(index));
            if (count < 0 || index + count > Count) throw new ArgumentOutOfRangeException(nameof(count));

            lock (_syncRoot)
            {
                for (int i = 0; i < count; i++)
                {
                    RemoveAt(index);
                }
            }

            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        /// <summary>
        /// 批量插入元素
        /// </summary>
        public void InsertRange(int index, IEnumerable<T> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (index < 0 || index > Count) throw new ArgumentOutOfRangeException(nameof(index));

            lock (_syncRoot)
            {
                _suppressNotification = true;
                foreach (var item in items)
                {
                    Insert(index++, item);
                }
                _suppressNotification = false;
            }

            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        /// <summary>
        /// 替换所有元素
        /// </summary>
        public void ReplaceRange(IEnumerable<T> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));

            lock (_syncRoot)
            {
                Clear();
                AddRange(items);
            }
        }

        /// <summary>
        /// 移除前N个元素，保留最后maxCount个元素
        /// </summary>
        public void TrimExcess(int maxCount)
        {
            if (maxCount < 0) throw new ArgumentOutOfRangeException(nameof(maxCount));

            lock (_syncRoot)
            {
                if (Count > maxCount)
                {
                    var removeCount = Count - maxCount;
                    for (int i = 0; i < removeCount; i++)
                    {
                        RemoveAt(0);
                    }

                    OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
                }
            }
        }

        /// <summary>
        /// 暂停通知，执行批量操作后手动恢复
        /// </summary>
        public IDisposable DelayNotifications()
        {
            return new NotificationSuppressor(this);
        }

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (!_suppressNotification)
                base.OnCollectionChanged(e);
        }

        /// <summary>
        /// 临时抑制通知的帮助类
        /// </summary>
        private class NotificationSuppressor : IDisposable
        {
            private readonly RangeObservableCollection<T> _collection;

            public NotificationSuppressor(RangeObservableCollection<T> collection)
            {
                _collection = collection;
                Monitor.Enter(_collection._syncRoot);
                _collection._suppressNotification = true;
            }

            public void Dispose()
            {
                _collection._suppressNotification = false;
                Monitor.Exit(_collection._syncRoot);
                _collection.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }
        }
    }
}
