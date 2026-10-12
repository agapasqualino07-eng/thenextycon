using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace OfficinaTycoon
{
    /// <summary>
    /// Reflection mapper between plain data classes (public fields) and MiniJson trees.
    /// Rules that make config and save evolution safe:
    ///  - Missing JSON fields keep the value set by the class's field initializers (new fields get defaults).
    ///  - Unknown JSON fields are ignored.
    ///  - Lists are replaced wholesale when present in the JSON.
    ///  - Unknown enum names fall back to the default value.
    /// IL2CPP note: the assembly is preserved by Assets/link.xml so fields are not stripped.
    /// </summary>
    public static class JsonMapper
    {
        public static string ToJson(object obj) => MiniJson.Serialize(ToTree(obj));

        public static T FromJson<T>(string json) where T : new() => (T)FromTree(typeof(T), MiniJson.Parse(json));

        public static object ToTree(object obj)
        {
            if (obj == null) return null;
            Type t = obj.GetType();
            if (obj is string || obj is bool || obj is long || obj is ulong || obj is double) return obj;
            if (obj is int i) return (long)i;
            if (obj is float f) return (double)f;
            if (t.IsEnum) return obj.ToString();
            if (obj is IList list)
            {
                var l = new List<object>(list.Count);
                foreach (var item in list) l.Add(ToTree(item));
                return l;
            }
            var d = new Dictionary<string, object>();
            foreach (FieldInfo fi in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (fi.IsDefined(typeof(NonSerializedAttribute), false)) continue;
                d[fi.Name] = ToTree(fi.GetValue(obj));
            }
            return d;
        }

        public static object FromTree(Type t, object node, object existing = null)
        {
            if (t == typeof(string)) return node as string ?? (node == null ? null : Convert.ToString(node, System.Globalization.CultureInfo.InvariantCulture));
            if (t == typeof(bool)) return node is bool b ? b : false;
            if (t == typeof(int)) return (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, ToDouble(node)));
            if (t == typeof(long)) return node is long l ? l : SaturatingLong(ToDouble(node));
            if (t == typeof(ulong)) return node is ulong ul ? ul : (node is long ll && ll >= 0 ? (ulong)ll : 0UL);
            if (t == typeof(double)) return Num.Sanitize(ToDouble(node));
            if (t == typeof(float)) return (float)Num.Sanitize(ToDouble(node));
            if (t.IsEnum)
            {
                if (node is string name && Enum.IsDefined(t, name)) return Enum.Parse(t, name);
                return existing ?? Activator.CreateInstance(t);
            }
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>))
            {
                Type et = t.GetGenericArguments()[0];
                var res = (IList)Activator.CreateInstance(t);
                if (node is List<object> src)
                    foreach (var item in src) res.Add(FromTree(et, item));
                return res;
            }
            // class with public fields
            object target = existing ?? Activator.CreateInstance(t);
            if (node is Dictionary<string, object> dict)
            {
                foreach (FieldInfo fi in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (fi.IsDefined(typeof(NonSerializedAttribute), false)) continue;
                    if (!dict.TryGetValue(fi.Name, out object child)) continue;
                    if (child == null && !fi.FieldType.IsValueType) { continue; }
                    fi.SetValue(target, FromTree(fi.FieldType, child, fi.GetValue(target)));
                }
            }
            return target;
        }

        static long SaturatingLong(double d)
        {
            if (!Num.IsFinite(d)) return 0;
            if (d >= long.MaxValue) return long.MaxValue;
            if (d <= long.MinValue) return long.MinValue;
            return (long)d;
        }

        static double ToDouble(object n)
        {
            switch (n)
            {
                case double d: return d;
                case long l: return l;
                case ulong u: return u;
                case int i: return i;
                default: return 0.0;
            }
        }
    }
}
