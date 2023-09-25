using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace MomCrypto.Api
{
    public static class ConstantHelper
    {
        private class CacheItem
        {
            public readonly IDictionary<byte, string> NameMap = new Dictionary<byte, string>(10);
            public readonly IDictionary<string, byte> ValueMap = new Dictionary<string, byte>(10);
        }

        private static readonly Dictionary<Type, CacheItem> Cache = new(10);

        static ConstantHelper()
        {
            RegisterType(typeof(MomPosiDirectionType));
            RegisterType(typeof(MomProductClassType));
            RegisterType(typeof(MomOrderStatusType));
            RegisterType(typeof(MomOrderSubmitStatusType));
            RegisterType(typeof(MomDirectionType));
            RegisterType(typeof(MomOffsetFlagType));
            RegisterType(typeof(MomTimeConditionType));
            RegisterType(typeof(MomVolumeConditionType));
            RegisterType(typeof(MomOrderPriceTypeType));
            RegisterType(typeof(MomContingentConditionType));
            RegisterType(typeof(MomTradeTypeType));
            RegisterType(typeof(MomTradeSourceType));
            RegisterType(typeof(MomCashJournalTypeType));
        }

        public static void RegisterType(Type type)
        {
            var item = new CacheItem();
            var fields = type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy);
            foreach (var field in fields)
            {
                var description = field.GetCustomAttribute<DescriptionAttribute>();
                if (description == null)
                {
                    continue;
                }

                var value = (byte)field.GetRawConstantValue();
                item.NameMap.Add(value, description.Description);
                item.ValueMap.Add(description.Description, value);
            }
            Cache[type] = item;
        }

        public static List<byte> GetValues<T>()
        {
            var type = typeof(T);
            if (Cache.TryGetValue(type, out var item))
            {
                return item.ValueMap.Values.ToList();
            }
            var list = new List<byte>();
            var fields = type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy);
            foreach (var field in fields)
            {
                list.Add((byte)field.GetRawConstantValue());
            }
            return list;
        }

        public static byte GetValue<T>(string name)
        {
            var type = typeof(T);
            if (Cache.TryGetValue(type, out var item))
            {
                if (item.ValueMap.TryGetValue(name, out var value))
                {
                    return value;
                }
                throw new IndexOutOfRangeException($"not found {name} in {typeof(T).Name}");
            }

            var fields = type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy);
            foreach (var field in fields)
            {
                var description = field.GetCustomAttribute<DescriptionAttribute>();
                if (description != null && name == description.Description)
                {
                    return (byte)field.GetRawConstantValue();
                }
            }
            throw new IndexOutOfRangeException($"not found {name} in {typeof(T).Name}");
        }

        public static List<byte> GetValues<T>(string names)
        {
            var items = names.Split(',');
            var type = typeof(T);
            if (Cache.TryGetValue(type, out var cache))
            {
                return items
                    .Select(name =>
                    {
                        if (cache.ValueMap.TryGetValue(name, out var value))
                        {
                            return value;
                        }
                        throw new IndexOutOfRangeException($"not found {name} in {typeof(T).Name}");
                    }).ToList();
            }

            var list = new List<byte>();
            foreach (var name in items)
            {
                list.Add(GetValue<T>(name));
            }
            return list;
        }

        public static IDictionary<byte, string> GetNames<T>()
        {
            var type = typeof(T);
            if (Cache.TryGetValue(type, out var item))
            {
                return item.NameMap;
            }

            var names = new Dictionary<byte, string>();
            var fields = type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy);
            foreach (var field in fields)
            {
                var description = field.GetCustomAttribute<DescriptionAttribute>();
                if (description != null)
                {
                    names.Add((byte)field.GetRawConstantValue(), description.Description);
                }
            }

            return names;
        }

        public static List<string> GetConstantValues<T>()
        {
            var list = new List<string>();
            var fields = typeof(T).GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy);
            foreach (var field in fields)
            {
                var description = field.GetCustomAttribute<DescriptionAttribute>();
                if (description == null)
                {
                    continue;
                }

                list.Add((string)field.GetRawConstantValue());
            }

            return list;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string GetUndefinedName(byte value)
        {
            return $"_Undefined({value})_";
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string GetName<T>(byte value)
        {
            var names = GetNames<T>();
            return !names.TryGetValue(value, out var name) ? GetUndefinedName(value) : name;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string GetNames<T>(IEnumerable<byte> values)
        {
            var names = GetNames<T>();
            return string.Join(",", values.Select(value => !names.TryGetValue(value, out var name) ? GetUndefinedName(value) : name));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string GetNames<T>(params byte[] values)
        {
            return GetNames<T>((IEnumerable<byte>)values);
        }
    }
}