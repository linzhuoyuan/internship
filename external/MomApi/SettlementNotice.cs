using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace Quantmom.Api;

[DataContract]
public class DictWrapper
{
    [DataMember(Order = 1)]
    public Dictionary<string, string> Dict = new();

    public DictWrapper()
    {
    }

    public DictWrapper(Dictionary<string, string> dict)
    {
        Dict = dict;
    }

    public static implicit operator DictWrapper(Dictionary<string, string> dict)
    {
        return new DictWrapper(dict);
    }

    public static implicit operator Dictionary<string, string>(DictWrapper wrapper)
    {
        return wrapper.Dict!;
    }
}

[DataContract]
public class SettlementNotice
{
    [DataMember(Order = 1)]
    private List<DictWrapper> _missingData = new();
    [DataMember(Order = 2)]
    private List<DictWrapper> _assignedData = new();
    [DataMember(Order = 3)]
    private List<DictWrapper> _dividendData = new();

    public IEnumerable<Dictionary<string, string>> Missing => _missingData.Select(n => n.Dict);
    public IEnumerable<Dictionary<string, string>> Assigned => _assignedData.Select(n => n.Dict);
    public IEnumerable<Dictionary<string, string>> Dividend => _dividendData.Select(n => n.Dict);

    public void AddMissing(IEnumerable<Dictionary<string, string>>? items)
    {
        if (items == null)
        {
            return;
        }

        foreach (var item in items)
        {
            _missingData.Add(item);
        }
    }

    public void AddAssigned(IEnumerable<Dictionary<string, string>>? items)
    {
        if (items == null)
        {
            return;
        }
        foreach (var item in items)
        {
            _assignedData.Add(item);
        }
    }

    public void AddDividend(IEnumerable<Dictionary<string, string>>? items)
    {
        if (items == null)
        {
            return;
        }
        foreach (var item in items)
        {
            _dividendData.Add(item);
        }
    }
}