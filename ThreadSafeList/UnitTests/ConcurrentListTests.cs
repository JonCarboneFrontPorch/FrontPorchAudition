using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;
using System.Threading;
using System.Collections;
using Xunit.Extensions;

namespace ThreadSafeList.UnitTests
{
    public class ConcurrentListTests
    {
        // Track whether all threads have completed their execution so we know when the test is over
        private static int NumActiveThreads = 0;
        // Track the number of exceptions encountered across all threads
        private static int NumExceptionsCaughtInThreads = 0;
        
        private static Random _random = new Random();

        // The parameters that will be passed to each test that determine the number of threads spawned
        // and the number of times a thread performs its intended action 
        public static IEnumerable<object[]> ThreadParameters
        {
            get
            {
                return new[]
                {
                    new object[] { 1, 1 },
                    new object[] { 10, 1 },
                    new object[] { 10, 10 },
                    new object[] { 100, 1 },
                    new object[] { 100, 10 },
                    new object[] { 100, 100 }
                };
            }
        }

        #region Actions that will be taken on the lists during tests

        public delegate void ListAction(ConcurrentList<Object> listToActOn);

        private ListAction AddAction = new ListAction(list =>
        {
            list.Add(new Object());
        });

        private ListAction InsertRandomAction = new ListAction(list =>
        {
            list.ExecuteConcurrentActions(() =>
            {
                int insertionIndex = _random.Next(0, list.Count);
                list.Insert(insertionIndex, new Object());
            }, true);
        });

        private ListAction InsertAt0Action = new ListAction(list =>
        {
            list.Insert(0, new Object());
        });

        private ListAction RemoveRandomAction = new ListAction(list =>
        {
            list.ExecuteConcurrentActions(() =>
            {
                if (list.Count > 0)
                {
                    int removalIndex = _random.Next(0, list.Count - 1);
                    list.RemoveAt(removalIndex);
                }
            }, true);
        });
        
        private ListAction EnumerateAction = new ListAction(list =>
        {
            IEnumerator listEnumerator = list.GetEnumerator();
            while (listEnumerator.MoveNext())
            {
                Object listIter = listEnumerator.Current;
            }
        });

        private ListAction ClearAction = new ListAction(list =>
        {
            list.Clear();
        });

        #endregion

        /// <summary>
        /// Test multiple add threads.
        /// </summary>
        [Theory(Timeout = 10000),
         PropertyData("ThreadParameters")]
        void TestAdd(int numParallelActionThreads, int numActionsPerThread)
        {
            var listTestObject = new ConcurrentList<Object>();
            var addThreadParams = new ConcurrencyTestParams<Object>(listTestObject, numParallelActionThreads, numActionsPerThread, AddAction);

            CreateAndRunWorkerThreads(addThreadParams);

            WaitForAllThreadsToComplete();

            // Sanity check to ensure at least one item was added
            Assert.NotEmpty(listTestObject);
        }

        /// <summary>
        /// Test multiple insertion threads.
        /// </summary>
        [Theory(Timeout = 10000),
         PropertyData("ThreadParameters")]
        void TestInsert(int numParallelActionThreads, int numActionsPerThread)
        {
            var listTestObject = new ConcurrentList<Object>();
            var insertThreadParams = new ConcurrencyTestParams<Object>(listTestObject, numParallelActionThreads, numActionsPerThread, InsertRandomAction);

            CreateAndRunWorkerThreads(insertThreadParams);

            WaitForAllThreadsToComplete();

            // Sanity check to ensure at least one item was inserted
            Assert.NotEmpty(listTestObject);
        }

        /// <summary>
        /// Test multiple insertion threads.
        /// </summary>
        [Theory(Timeout = 10000),
         PropertyData("ThreadParameters")]
        void TestInsertAt0(int numParallelActionThreads, int numActionsPerThread)
        {
            var listTestObject = new ConcurrentList<Object>();
            var insertThreadParams = new ConcurrencyTestParams<Object>(listTestObject, numParallelActionThreads, numActionsPerThread, InsertAt0Action);

            CreateAndRunWorkerThreads(insertThreadParams);

            WaitForAllThreadsToComplete();

            // Sanity check to ensure at least one item was inserted
            Assert.NotEmpty(listTestObject);
        }

        /// <summary>
        /// Test multiple add threads.
        /// </summary>
        [Theory(Timeout = 10000),
         PropertyData("ThreadParameters")]
        void TestRemove(int numParallelActionThreads, int numActionsPerThread)
        {
            var listTestObject = new ConcurrentList<Object>();
            const int numItemsToAdd = 10000;
            // Populate the list before removing items
            for (int addCount = 0; addCount < numItemsToAdd; addCount++)
            {
                listTestObject.Add(new Object());
            }
            var removeThreadParams = new ConcurrencyTestParams<Object>(listTestObject, numParallelActionThreads, numActionsPerThread, RemoveRandomAction);

            CreateAndRunWorkerThreads(removeThreadParams);

            WaitForAllThreadsToComplete();

            // Sanity check to ensure at least one item was removed
            Assert.InRange(listTestObject.Count, 0, numItemsToAdd);
        }
        
        /// <summary>
        /// Test performing many operations concurrently (i.e. add, remove, insert, enumerate).
        /// </summary>
        [Theory(Timeout = 15000),
         PropertyData("ThreadParameters")]
        void TestManyConcurrentOperations(int numParallelActionThreads, int numActionsPerThread)
        {
            var listTestObject = new ConcurrentList<Object>();
            var addThreadParams = new ConcurrencyTestParams<Object>(listTestObject, numParallelActionThreads, numActionsPerThread, AddAction);
            var insertThreadParams = new ConcurrencyTestParams<Object>(listTestObject, numParallelActionThreads, numActionsPerThread, InsertRandomAction);
            var insertAt0ThreadParams = new ConcurrencyTestParams<Object>(listTestObject, numParallelActionThreads, numActionsPerThread, InsertAt0Action);
            var removeThreadParams = new ConcurrencyTestParams<Object>(listTestObject, numParallelActionThreads, numActionsPerThread, RemoveRandomAction);
            var enumerateThreadParams = new ConcurrencyTestParams<Object>(listTestObject, numParallelActionThreads, numActionsPerThread, EnumerateAction);
            var clearThreadParams = new ConcurrencyTestParams<Object>(listTestObject, numParallelActionThreads, numActionsPerThread, ClearAction);

            CreateAndRunWorkerThreads(addThreadParams);
            CreateAndRunWorkerThreads(insertThreadParams);
            CreateAndRunWorkerThreads(insertAt0ThreadParams);
            CreateAndRunWorkerThreads(removeThreadParams);
            CreateAndRunWorkerThreads(enumerateThreadParams);
            CreateAndRunWorkerThreads(clearThreadParams);

            WaitForAllThreadsToComplete();
        }

        private static void CreateAndRunWorkerThreads(ConcurrencyTestParams<Object> threadParams)
        {
            for (int threadCounter = 0; threadCounter < threadParams.ThreadsToSpawn; threadCounter++)
            {
                if (ThreadPool.QueueUserWorkItem(new WaitCallback(PerformListActionIterations), threadParams))
                {
                    Interlocked.Increment(ref NumActiveThreads);
                }
            }
        }

        public static void PerformListActionIterations(Object listActionObj)
        {
            var testParams = listActionObj as ConcurrencyTestParams<Object>;
            for (int currentIteration = 0; currentIteration < testParams.IterationsToPerform; currentIteration++)
            {
                try
                {
                    testParams.ListAction(testParams.List);
                    Thread.Sleep(0);
                }
                catch
                {
                    Interlocked.Increment(ref NumExceptionsCaughtInThreads);
                }
            }

            Interlocked.Decrement(ref NumActiveThreads);
        }
        

        private static void WaitForAllThreadsToComplete()
        {
            do
            {
                Thread.Sleep(10);
            }
            while (NumActiveThreads > 0);

            Assert.Equal(0, NumExceptionsCaughtInThreads);
        }
    }

    public class ConcurrencyTestParams<T>
    {
        public ConcurrentList<T> List { get; private set; }
        public ConcurrentListTests.ListAction ListAction { get; private set; } 
        public int ThreadsToSpawn { get; private set; }
        public int IterationsToPerform { get; private set; }

        public ConcurrencyTestParams(ConcurrentList<T> listToTest, int threadsToSpawn, int iterationsToPerform, ConcurrentListTests.ListAction actionToPerform)
        {
            this.List = listToTest;
            this.ListAction = actionToPerform;
            this.ThreadsToSpawn = threadsToSpawn;
            this.IterationsToPerform = iterationsToPerform;
        }
    }
}
