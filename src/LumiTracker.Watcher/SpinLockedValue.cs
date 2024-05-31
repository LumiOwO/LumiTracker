using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;

namespace LumiTracker.Watcher
{
    public class SpinLockedValue<T>(T? Value)
    {
        private SpinLock _spinLock = new SpinLock();
        private T? _value = Value;

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
