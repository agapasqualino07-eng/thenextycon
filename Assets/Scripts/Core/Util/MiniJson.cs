using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace OfficinaTycoon
{
    public sealed class JsonException : Exception
    {
        public JsonException(string message) : base(message) { }
    }

    /// <summary>
    /// Minimal JSON reader/writer with no engine dependency, so saves and config behave
    /// identically in Unity and in the plain-.NET test runner.
    /// Tree types: Dictionary&lt;string,object&gt;, List&lt;object&gt;, string, long, ulong, double, bool, null.
    /// Integer literals are kept as long/ulong (DateTime ticks exceed double's 2^53 precision).
    /// </summary>
    public static class MiniJson
    {
        const int MaxDepth = 64;
        /// <summary>Hard cap on input size: the parser is only used for saves/config, never for large documents.</summary>
        public const int MaxInputChars = 4_000_000;

        // ---------------- Writer ----------------
        public static string Serialize(object tree)
        {
            var sb = new StringBuilder(256);
            Write(sb, tree, 0);
            return sb.ToString();
        }

        static void Write(StringBuilder sb, object v, int depth)
        {
            if (depth > MaxDepth) throw new JsonException("max depth exceeded");
            switch (v)
            {
                case null: sb.Append("null"); break;
                case bool b: sb.Append(b ? "true" : "false"); break;
                case string s: WriteString(sb, s); break;
                case int i: sb.Append(i.ToString(CultureInfo.InvariantCulture)); break;
                case long l: sb.Append(l.ToString(CultureInfo.InvariantCulture)); break;
                case ulong ul: sb.Append(ul.ToString(CultureInfo.InvariantCulture)); break;
                case double d:
                    if (!Num.IsFinite(d)) throw new JsonException("non-finite number cannot be serialized");
                    sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
                    break;
                case Dictionary<string, object> dict:
                    sb.Append('{');
                    bool first = true;
                    foreach (var kv in dict)
                    {
                        if (!first) sb.Append(',');
                        first = false;
                        WriteString(sb, kv.Key);
                        sb.Append(':');
                        Write(sb, kv.Value, depth + 1);
                    }
                    sb.Append('}');
                    break;
                case List<object> list:
                    sb.Append('[');
                    for (int k = 0; k < list.Count; k++)
                    {
                        if (k > 0) sb.Append(',');
                        Write(sb, list[k], depth + 1);
                    }
                    sb.Append(']');
                    break;
                default:
                    throw new JsonException("unsupported type " + v.GetType().Name);
            }
        }

        static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        // ---------------- Parser ----------------
        public static object Parse(string json)
        {
            if (json == null) throw new JsonException("null input");
            if (json.Length > MaxInputChars) throw new JsonException("input too large");
            int pos = 0;
            SkipWs(json, ref pos);
            object v = ReadValue(json, ref pos, 0);
            SkipWs(json, ref pos);
            if (pos != json.Length) throw new JsonException("trailing characters at " + pos);
            return v;
        }

        static void SkipWs(string s, ref int p)
        {
            while (p < s.Length && (s[p] == ' ' || s[p] == '\n' || s[p] == '\r' || s[p] == '\t')) p++;
        }

        static object ReadValue(string s, ref int p, int depth)
        {
            if (depth > MaxDepth) throw new JsonException("max depth exceeded");
            if (p >= s.Length) throw new JsonException("unexpected end");
            char c = s[p];
            if (c == '{') return ReadObject(s, ref p, depth);
            if (c == '[') return ReadArray(s, ref p, depth);
            if (c == '"') return ReadString(s, ref p);
            if (Match(s, ref p, "true")) return true;
            if (Match(s, ref p, "false")) return false;
            if (Match(s, ref p, "null")) return null;
            return ReadNumber(s, ref p);
        }

        static bool Match(string s, ref int p, string lit)
        {
            if (string.CompareOrdinal(s, p, lit, 0, lit.Length) == 0) { p += lit.Length; return true; }
            return false;
        }

        static object ReadObject(string s, ref int p, int depth)
        {
            var d = new Dictionary<string, object>();
            p++; // {
            SkipWs(s, ref p);
            if (p < s.Length && s[p] == '}') { p++; return d; }
            while (true)
            {
                SkipWs(s, ref p);
                if (p >= s.Length || s[p] != '"') throw new JsonException("expected key at " + p);
                string key = ReadString(s, ref p);
                SkipWs(s, ref p);
                if (p >= s.Length || s[p] != ':') throw new JsonException("expected ':' at " + p);
                p++;
                SkipWs(s, ref p);
                d[key] = ReadValue(s, ref p, depth + 1);
                SkipWs(s, ref p);
                if (p >= s.Length) throw new JsonException("unterminated object");
                if (s[p] == ',') { p++; continue; }
                if (s[p] == '}') { p++; return d; }
                throw new JsonException("expected ',' or '}' at " + p);
            }
        }

        static object ReadArray(string s, ref int p, int depth)
        {
            var l = new List<object>();
            p++; // [
            SkipWs(s, ref p);
            if (p < s.Length && s[p] == ']') { p++; return l; }
            while (true)
            {
                SkipWs(s, ref p);
                l.Add(ReadValue(s, ref p, depth + 1));
                SkipWs(s, ref p);
                if (p >= s.Length) throw new JsonException("unterminated array");
                if (s[p] == ',') { p++; continue; }
                if (s[p] == ']') { p++; return l; }
                throw new JsonException("expected ',' or ']' at " + p);
            }
        }

        static string ReadString(string s, ref int p)
        {
            var sb = new StringBuilder();
            p++; // opening quote
            while (true)
            {
                if (p >= s.Length) throw new JsonException("unterminated string");
                char c = s[p++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                if (p >= s.Length) throw new JsonException("bad escape");
                char e = s[p++];
                switch (e)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u':
                        if (p + 4 > s.Length) throw new JsonException("bad unicode escape");
                        if (!int.TryParse(s.Substring(p, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int code))
                            throw new JsonException("bad unicode escape");
                        sb.Append((char)code);
                        p += 4;
                        break;
                    default: throw new JsonException("bad escape \\" + e);
                }
            }
        }

        static object ReadNumber(string s, ref int p)
        {
            int start = p;
            bool isInt = true;
            if (p < s.Length && s[p] == '-') p++;
            while (p < s.Length)
            {
                char c = s[p];
                if (c >= '0' && c <= '9') { p++; continue; }
                if (c == '.' || c == 'e' || c == 'E' || c == '+' || c == '-') { isInt = false; p++; continue; }
                break;
            }
            if (p == start) throw new JsonException("unexpected character '" + s[p] + "' at " + p);
            string tok = s.Substring(start, p - start);
            if (isInt)
            {
                if (long.TryParse(tok, NumberStyles.Integer, CultureInfo.InvariantCulture, out long l)) return l;
                if (ulong.TryParse(tok, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong ul)) return ul;
            }
            if (double.TryParse(tok, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) && Num.IsFinite(d)) return d;
            throw new JsonException("bad number '" + tok + "'");
        }
    }
}
