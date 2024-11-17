namespace LumiTracker.Watcher
{
    public class SpinLockedValue<T>(T? InitValue)
    {
        private SpinLock _spinLock = new SpinLock();
        private T? _value = InitValue;

        public T? Value
        {
            get
            {
                bool lockTaken = false;
                try
                {
                    _spinLock.Enter(ref lockTaken);
                    return _value;
                }
                finally
                {
                    if (lockTaken) _spinLock.Exit();
                }
            }
            set
            {
                bool lockTaken = false;
                try
                {
                    _spinLock.Enter(ref lockTaken);
                    _value = value;
                }
                finally
                {
                    if (lockTaken) _spinLock.Exit();
                }
            }
        }
    }
}
