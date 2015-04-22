This Visual Studio 2010 solution contains the ConcurrentList<T> class, which implements the IList<T> interface. The unit tests can be ran by installing the TestDriven.Net Visual Studio extension (http://testdriven.net/download.aspx), after which a "Run Tests" context-menu option will be available when right-clicking the test file.

Notes regarding the new ConcurrentList<T> class:
  - Implements System.Collections.Generic.IList<T>, as per the requirements.
  - Contains an instance of a System.Collections.Generic.List<T>.
    * This approach was chosen to avoid creating a collection from scratch. The .Net List<T> class implements IList<T>, so calling the IList<T> methods on the contained List<T> (while providing a layer of thread-safety) seemed like a clean approach.
  - Utilizes a read/write lock to provide thread-safe access to the contained List<T>.
    * This allows threads to concurrently perform reads on the list without blocking each other, while ensuring that writer threads have exclusive access and do not invalidate the list for other readers/writers.
  - Contains an ExecuteConcurrentActions method for ensuring that multiple list operations can occur without releasing the lock between operations.
    * Upon attempting to use the class in the unit tests, the need for this method quickly became apparent.
	* This approached seemed cleaner and more safe than exposing direct control of the lock object.
  - Maintains a cached version of the list for use with GetEnumerator.
    * List operations that modify the list mark the cached version as dirty, which results in a new copy of the list being cached on the next call to GetEnumerator.
    * This allows the returned IEnumerator to be used without requiring the lock during iteration.
  
Several unit tests were created to attempt to expose thread-safety issues:
  - The tests implemented do not yet cover the entirety of the class.
  - While testing concurrency is difficult and not foolproof, it was valuable for determining how the class will be utilized by consumers and exposed some basic mistakes during development.
  - Tests can be failed by removing/commenting out relevant locking/unlocking in ConcurrentList<T>.