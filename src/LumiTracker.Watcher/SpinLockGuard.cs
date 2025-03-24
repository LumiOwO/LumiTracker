namespace LumiTracker.Watcher
{
    public ref struct SpinLockGuard
    {
        private ref SpinLock _spinLock; // Ref to original SpinLock
        private bool _lockTaken;

        public SpinLockGuard(ref SpinLock spinLock)
        {
            _spinLock = ref spinLock; // Store reference, NOT a copy
            _lockTaken = false;
            _spinLock.Enter(ref _lockTaken); // Acquire lock
        }

        public void Dispose()
        {
            if (_lockTaken)
            {
                _spinLock.Exit(); // Release the original SpinLock
                _lockTaken = false;
            }
        }

        public static void Scope(ref SpinLock spinLock, Action func)
        {
            using (new SpinLockGuard(ref spinLock))
            {
                func();
            }
        }

        public static T Scope<T>(ref SpinLock spinLock, Func<T> func)
        {
            using (new SpinLockGuard(ref spinLock))
            {
                return func();
            }
        }
    }
}
