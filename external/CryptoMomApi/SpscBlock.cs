using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Skyline;

namespace MomCrypto.Api
{
    internal static class WinNative
    {
        [DllImport("kernel32.dll")]
        public static extern UIntPtr SetThreadAffinityMask(IntPtr hThread, UIntPtr dwThreadAffinityMask);

        [DllImport("kernel32.dll")]
        public static extern IntPtr GetCurrentThread();
    }

    public class ISpscLinkOptions
    {
        public int CpuCore { get; set; }
    }

    internal sealed class SpscBroadcastLinkNode<T> : IDisposable
    {
        private readonly SpscBroadcastLink<T> _parent;
        private readonly ISpscTargetBlock<T> _target;

        public SpscBroadcastLinkNode(SpscBroadcastLink<T> parent, ISpscTargetBlock<T> target)
        {
            _parent = parent;
            _target = target;
        }

        public void Dispose()
        {
            _parent.RemoveTarget(_target);
        }
    }

    internal sealed class SpscBroadcastLink<T> : IDisposable
    {
        private readonly ISpscSourceBlock<T> _source;
        private readonly Func<T, T> _cloneFunc;
        private readonly LongRunningTask _task;
        private List<ISpscTargetBlock<T>> _targetView;
        private readonly HashSet<ISpscTargetBlock<T>> _targets;

        public SpscBroadcastLink(ISpscSourceBlock<T> source, Func<T, T> cloneFunc)
        {
            _source = source;
            _cloneFunc = cloneFunc;
            _targets = new HashSet<ISpscTargetBlock<T>>();
            _task = LongRunningTask.StartNew(Run);
            _task.Start();
        }

        private void Run(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                while (_source.ConsumeMessage(out var input))
                {
                    foreach (var target in _targetView)
                    {
                        OfferMessage(target, input);
                    }
                }
                //Thread.SpinWait(0);
                Thread.Sleep(0);
            }

            void OfferMessage(ISpscTargetBlock<T> target, T input)
            {
                Task.Run(() => target.OfferMessage(_cloneFunc(input), _source), ct);
            }
        }

        public IDisposable AddTarget(ISpscTargetBlock<T> target)
        {
            lock (_targets)
            {
                if (!_targets.Contains(target))
                {
                    _targets.Add(target);
                    _targetView = _targets.ToList();
                }
            }
            return new SpscBroadcastLinkNode<T>(this, target);
        }

        public void RemoveTarget(ISpscTargetBlock<T> target)
        {
            lock (_targets)
            {
                if (!_targets.Contains(target))
                    return;
                _targets.Remove(target);
                _targetView = _targets.ToList();
            }
        }

        public void Dispose()
        {
            _task.Stop();
        }
    }

    internal sealed class SpscSingleLink<T> : IDisposable
    {
        private readonly ISpscSourceBlock<T> _source;
        private readonly ISpscTargetBlock<T> _target;
        private readonly ISpscLinkOptions _options;
        private readonly LongRunningTask _task;

        public SpscSingleLink(ISpscSourceBlock<T> source, ISpscTargetBlock<T> target, ISpscLinkOptions options)
        {
            _source = source;
            _target = target;
            _options = options;
            _task = LongRunningTask.StartNew(Run);
        }

        private void Run(CancellationToken ct)
        {
            if (_options != null 
                && _options.CpuCore >= 0
                && _options.CpuCore < Environment.ProcessorCount)
            {
                var affinity = (UIntPtr)(1UL << _options.CpuCore);
                WinNative.SetThreadAffinityMask(WinNative.GetCurrentThread(), affinity);
            }

            while (!ct.IsCancellationRequested)
            {
                while (_source.ConsumeMessage(out var input))
                {
                    _target.OfferMessage(input, _source);
                }
                Thread.Sleep(0);
            }
        }

        public void Dispose()
        {
            _task.Stop();
        }
    }

    public interface ISpscSourceBlock<TOutput>
    {
        bool ConsumeMessage(out TOutput output);
        IDisposable LinkTo(ISpscTargetBlock<TOutput> target, ISpscLinkOptions options = null);
    }

    public interface ISpscTargetBlock<TInput>
    {
        void OfferMessage(TInput messageValue, ISpscSourceBlock<TInput> source);
    }

    public sealed class SpscBufferBlock<TOutput> : ISpscSourceBlock<TOutput>
    {
        private readonly SpscQueue<TOutput> _queue;

        public SpscBufferBlock()
        {
            _queue = new SpscQueue<TOutput>();
        }

        public long Count => _queue.Count;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ConsumeMessage(out TOutput output)
        {
            return _queue.TryDequeue(out output);
        }

        public IDisposable LinkTo(ISpscTargetBlock<TOutput> target, ISpscLinkOptions options = null)
        {
            return new SpscSingleLink<TOutput>(this, target, options);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Post(TOutput output)
        {
            _queue.Enqueue(output);
        }
    }

    public sealed class SpscBroadcastBlock<TOutput> : ISpscSourceBlock<TOutput>, IDisposable
    {
        private readonly SpscQueue<TOutput> _queue;
        private readonly SpscBroadcastLink<TOutput> _link;
        public SpscBroadcastBlock(Func<TOutput, TOutput> cloneFunc)
        {
            _queue = new SpscQueue<TOutput>();
            _link = new SpscBroadcastLink<TOutput>(this, cloneFunc);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ConsumeMessage(out TOutput output)
        {
            return _queue.TryDequeue(out output);
        }

        public IDisposable LinkTo(ISpscTargetBlock<TOutput> target, ISpscLinkOptions options = null)
        {
            return _link.AddTarget(target);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Post(TOutput output)
        {
            _queue.Enqueue(output);
        }

        public void Dispose()
        {
            _link.Dispose();
        }
    }

    public sealed class SpscActionBlock<TInput> : ISpscTargetBlock<TInput>
    {
        private readonly Action<TInput> _action;

        public SpscActionBlock(Action<TInput> action)
        {
            _action = action;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OfferMessage(TInput messageValue, ISpscSourceBlock<TInput> source)
        {
            _action(messageValue);
        }
    }

    public sealed class SpscBufferAction<TInput> : IDisposable
    {
        private readonly Action<TInput> _action;
        private readonly SpscQueue<TInput> _queue;
        private readonly LongRunningTask _task;

        public SpscBufferAction(Action<TInput> action)
        {
            _action = action;
            _queue = new SpscQueue<TInput>();
            _task = LongRunningTask.StartNew(Run);
            _task.Start();
        }

        private void Run(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                while (_queue.TryDequeue(out var input))
                {
                    _action(input);
                }
                Thread.Sleep(0);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Post(TInput input)
        {
            _queue.Enqueue(input);
        }

        public void Dispose()
        {
            _task.Stop();
        }
    }
}
