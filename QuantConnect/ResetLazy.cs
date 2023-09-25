using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace QuantConnect
{
    public class ResetLazy<T>
    {
        private Lazy<T> _lazy;
        private readonly LazyThreadSafetyMode? _mode;
        private readonly Func<T> _valueFactory;
        private readonly bool? _isThreadSafe;

        private readonly object _locker = new object();

        public ResetLazy(Func<T> valueFactory)
        {
            _lazy = new Lazy<T>(valueFactory);
            _valueFactory = valueFactory;
        }

        public ResetLazy(Func<T> valueFactory, bool isThreadSafe)
        {
            _lazy = new Lazy<T>(valueFactory, isThreadSafe);
            _isThreadSafe = isThreadSafe;
            _valueFactory = valueFactory;
        }

        public ResetLazy(Func<T> valueFactory, LazyThreadSafetyMode mode)
        {
            _lazy = new Lazy<T>(valueFactory, mode);
            _mode = mode;
            _valueFactory = valueFactory;
        }

        public T Value => _lazy.Value;
        public bool IsValueCreated => _lazy.IsValueCreated;

        public void Reset()
        {
            lock (_locker)
            {
                if (_mode.HasValue)
                {
                    _lazy = new Lazy<T>(_valueFactory, _mode.Value);
                }
                else if (_isThreadSafe.HasValue)
                {
                    _lazy = new Lazy<T>(_valueFactory, _isThreadSafe.Value);
                }
                else
                {
                    _lazy = new Lazy<T>(_valueFactory);
                }
            }
        }

        public override string ToString()
        {
            return _lazy.ToString();
        }
    }
}
