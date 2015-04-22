using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace ThreadSafeList
{
    /// <summary>
    /// An implementation of IList that can be used by multiple threads at the same time. Utilizes an internal read/write lock to accomplish the concurrency.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ConcurrentList<T> : IList<T>
    {
        /// <summary>
        /// The lock used to ensure concurrency of the underlying collection. It is a read/write lock, so any action that requires only
        /// a read should use _lock.EnterReadLock/ExitReadLock, while any action that modifies the collection should use
        /// _lock.EnterWriteLock/ExitWriteLock.
        /// The ReaderWriterLockSlim can have one writer and many readers. A writer requires exclusive access, and waits for all readers
        /// to exit. There is no limit to the number of concurrent readers. See https://msdn.microsoft.com/en-us/library/system.threading.readerwriterlockslim.aspx
        /// for details.
        /// </summary>
        private ReaderWriterLockSlim _lock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);

        private List<T> _list = new List<T>();
        private IList<T> _listSnapshot = new List<T>();
        private bool _listSnapshotIsDirty = false;

        /// <summary>
        /// Obtains the appropriate lock and executes the provided delegate. 
        /// Utilize this method when more than one list operation needs to be executed sequentially, 
        /// and where a list operation after the first depends on the state of the list not changing.
        /// </summary>
        /// <param name="actionsToExecute">The delegate to execute containing list operations</param>
        /// <param name="containsAWriteAction">True if any of the operations in the delegate modify the list, false otherwise</param>
        public void ExecuteConcurrentActions(Action actionsToExecute, bool containsAWriteAction)
        {
            if (containsAWriteAction)
            {
                _lock.EnterWriteLock();
            }
            else
            {
                _lock.EnterReadLock();
            }

            try
            {
                actionsToExecute.Invoke();
            }
            catch { throw; }
            finally
            {
                if (containsAWriteAction)
                {
                    _lock.ExitWriteLock();
                }
                else
                {
                    _lock.ExitReadLock();
                }
            }
        }

        public int IndexOf(T item)
        {
            _lock.EnterReadLock();
            int index = _list.IndexOf(item); 
            _lock.ExitReadLock();
            return index;
        }

        public void Insert(int index, T item)
        {
            _lock.EnterWriteLock();
            _list.Insert(index, item);
            _listSnapshotIsDirty = true;
            _lock.ExitWriteLock();
        }

        public void RemoveAt(int index)
        {
            _lock.EnterWriteLock();
            _list.RemoveAt(index);
            _listSnapshotIsDirty = true;
            _lock.ExitWriteLock();
        }

        public T this[int index]
        {
            get
            {
                _lock.EnterReadLock();
                T item = _list[index];
                _lock.ExitReadLock();
                return item;
            }
            set
            {
                _lock.EnterWriteLock();
                _list[index] = value;
                _lock.ExitWriteLock();
            }
        }

        public void Add(T item)
        {
            _lock.EnterWriteLock();
            _list.Add(item);
            _listSnapshotIsDirty = true;
            _lock.ExitWriteLock();
        }

        public void Clear()
        {
            _lock.EnterWriteLock();
            _list.Clear();
            _listSnapshotIsDirty = true;
            _lock.ExitWriteLock();
        }

        public bool Contains(T item)
        {
            _lock.EnterReadLock();
            bool containsItem = _list.Contains(item);
            _lock.ExitReadLock();
            return containsItem;
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            _lock.EnterReadLock();
            _list.CopyTo(array, arrayIndex);
            _lock.ExitReadLock();
        }

        public int Count
        {
            get 
            {
                _lock.EnterReadLock();
                int count = _list.Count;
                _lock.ExitReadLock();
                return count;
            }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public bool Remove(T item)
        {
            _lock.EnterWriteLock();
            bool removed = _list.Remove(item);
            _listSnapshotIsDirty = true;
            _lock.ExitWriteLock();
            return removed;
        }

        public IEnumerator<T> GetEnumerator()
        {
            var snapshotToUse = _listSnapshot;
            if (_listSnapshotIsDirty)
            {
                _lock.EnterWriteLock();
                if (_listSnapshotIsDirty)
                {
                    _listSnapshot = new List<T>(_list);
                    snapshotToUse = _listSnapshot;
                    _listSnapshotIsDirty = false;
                }
                _lock.ExitWriteLock();
            }
            return snapshotToUse.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}
