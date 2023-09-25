using System;
using System.IO;
using Python.Runtime;
using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace QuantConnect
{
    public class DataFrameValue
    {
        public DataFrameValue(object value)
        {
            Value = value;
        }

        public object Value { get; }
        public string? AsString => Value?.ToString();
        public decimal AsDecimal => Convert.ToDecimal(Value);
        public int AsInt => Convert.ToInt32(Value);
        public long AsLong => Convert.ToInt64(Value);
        public double AsDouble => Convert.ToDouble(Value);
    }

    public class DataFrameRow : Dictionary<string, DataFrameValue>
    {
    }

    public class DataFrameRecords
    {
        public List<string> Columns { get; }
        public object[][] Records { get; }

        [JsonConstructor]
        public DataFrameRecords(string[] columns, object[][] records)
        {
            Columns = new List<string>(columns);
            Records = records;
        }

        public IList<T> GetColumn<T>(string column)
        {
            var index = Columns.IndexOf(column);
            if (index == -1)
            {
                throw new KeyNotFoundException($"Column {column} not found");
            }
            return Records[index].Select(n => (T)Convert.ChangeType(n, typeof(T))).ToList();
        }

        public DataFrameRow GetRow(int index)
        {
            var row = new DataFrameRow();
            for (var i = 0; i < Columns.Count; i++)
            {
                row[Columns[i]] = new DataFrameValue(Records[i][index]);
            }
            return row;
        }

        public IEnumerable<DataFrameRow> Rows()
        {
            for (var i = 0; i < Records[0].Length; i++)
            {
                yield return GetRow(i);
            }
        }
    }

    public class PythonModule
    {
        private readonly PyObject _module;
        private readonly Dictionary<string, PyObject> _funcMap = new();

        private static string GetModuleName(string pythonFile)
        {
            return new FileInfo(pythonFile).Name.Replace(".pyc", "").Replace(".py", "");
        }

        private PyObject GetFunction(string name)
        {
            if (!_funcMap.TryGetValue(name, out var func))
            {
                using (Py.GIL())
                {
                    func = _module.GetAttr(name);
                    _funcMap[name] = func;
                }
            }
            return func;
        }

        public PythonModule(string pythonFile)
        {
            var moduleName = GetModuleName(pythonFile);
            using (Py.GIL())
            {
                _module = Py.Import(moduleName);
            }
        }

        public T? Call<T>(string funName, params object[] args)
        {

            var func = GetFunction(funName);
            if (func.IsNone() || !func.IsCallable())
            {
                throw new InvalidOperationException($"Function {funName} is not callable");
            }

            using (Py.GIL())
            {
                // ReSharper disable once CoVariantArrayConversion
                var pyArgs = new PyTuple(args.Select(x => new PyString(JsonConvert.SerializeObject(x))).ToArray());
                var result = func.Invoke(pyArgs);
                return JsonConvert.DeserializeObject<T>(result.ToSafeString());
            }
        }
    }
}