using System.Collections.Concurrent;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.Interfaces;
using QuantConnect.Lean.Engine.RealTime;
using QuantConnect.Lean.Engine.Results;
using QuantConnect.Packets;
using QuantConnect.Scheduling;

namespace AblTest;

public class TestRealTimeHandler: IRealTimeHandler
{
    private readonly ConcurrentDictionary<ScheduledEvent, ScheduledEvent> _scheduledEvents = new ConcurrentDictionary<ScheduledEvent, ScheduledEvent>();


    public void Add(ScheduledEvent scheduledEvent)
    {
        _scheduledEvents.AddOrUpdate(scheduledEvent, scheduledEvent);
    }

    public void Remove(ScheduledEvent scheduledEvent)
    {
        _scheduledEvents.TryRemove(scheduledEvent, out _);
    }

    public bool IsActive { get; }
    public void Setup(IAlgorithm algorithm, AlgorithmNodePacket job, IResultHandler resultHandler, IApi api)
    {
    }

    public void Run()
    {
        foreach (var scheduledEvent in _scheduledEvents)
        {
            scheduledEvent.Value.OnEventFired(DateTime.UtcNow);
        }
    }

    public void SetTime(DateTime time)
    {
    }

    public void ScanPastEvents(DateTime time)
    {
    }

    public void Exit()
    {
    }

    public void OnSecuritiesChanged(SecurityChanges changes)
    {
    }
}