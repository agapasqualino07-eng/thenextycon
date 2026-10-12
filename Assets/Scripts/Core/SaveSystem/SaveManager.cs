using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace OfficinaTycoon
{
    public interface ISaveStorage
    {
        /// <summary>Returns the stored text for a name, or null if absent.</summary>
        string Read(string name);
        /// <summary>Atomically replaces the content of <paramref name="name"/> (best effort), keeping a backup copy.</summary>
        void Write(string name, string text);
        /// <summary>Moves unusable data aside (never deletes it).</summary>
        void Quarantine(string name);
    }

    public sealed class InMemorySaveStorage : ISaveStorage
    {
        public readonly Dictionary<string, string> Files = new Dictionary<string, string>();
        public int QuarantineCount;
        public string Read(string name) => Files.TryGetValue(name, out var v) ? v : null;
        public void Write(string name, string text)
        {
            if (Files.TryGetValue(name, out var old)) Files[name + ".bak"] = old;
            Files[name] = text;
        }
        public void Quarantine(string name)
        {
            if (Files.TryGetValue(name, out var v)) { Files[name + ".corrupt" + (++QuarantineCount)] = v; Files.Remove(name); }
        }
    }

    /// <summary>
    /// File storage with a write-temp-then-swap strategy:
    ///   1) write name.tmp  2) copy name -> name.bak  3) move name.tmp -> name.
    /// If the process dies mid-way, Read falls back through .tmp (only if complete: checksum verified by
    /// SaveManager) and .bak. Nothing is ever deleted without a validated replacement.
    /// </summary>
    public sealed class FileSaveStorage : ISaveStorage
    {
        readonly string _dir;
        public FileSaveStorage(string directory) { _dir = directory; Directory.CreateDirectory(_dir); }
        string P(string n) => Path.Combine(_dir, n);

        public string Read(string name) => File.Exists(P(name)) ? File.ReadAllText(P(name), Encoding.UTF8) : null;

        public void Write(string name, string text)
        {
            string main = P(name), tmp = P(name + ".tmp"), bak = P(name + ".bak");
            File.WriteAllText(tmp, text, new UTF8Encoding(false));
            if (File.Exists(main)) File.Copy(main, bak, true);
            if (File.Exists(main)) File.Delete(main);
            File.Move(tmp, main);
        }

        public void Quarantine(string name)
        {
            if (!File.Exists(P(name))) return;
            string target = P(name + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"));
            int i = 0;
            while (File.Exists(target)) target = P(name + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "-" + (++i));
            File.Move(P(name), target);
        }
    }

    /// <summary>Minimal synchronous key-value store (e.g. browser localStorage via in-process JS interop).</summary>
    public interface IKeyValueStore
    {
        string Get(string key);
        void Set(string key, string value);
        void Remove(string key);
    }

    public sealed class InMemoryKeyValueStore : IKeyValueStore
    {
        public readonly Dictionary<string, string> Data = new Dictionary<string, string>();
        public string Get(string key) => Data.TryGetValue(key, out var v) ? v : null;
        public void Set(string key, string value) { Data[key] = value; }
        public void Remove(string key) { Data.Remove(key); }
    }

    /// <summary>
    /// ISaveStorage over a key-value store. Each Set of a key-value store like localStorage is atomic, so the
    /// strategy is: copy current main -> .bak, then overwrite main. Corrupt data is moved aside, never deleted.
    /// </summary>
    public sealed class KeyValueSaveStorage : ISaveStorage
    {
        readonly IKeyValueStore _kv;
        readonly string _prefix;
        public KeyValueSaveStorage(IKeyValueStore kv, string prefix = "officina.") { _kv = kv; _prefix = prefix; }
        string K(string name) => _prefix + name;

        public string Read(string name) => _kv.Get(K(name));

        public void Write(string name, string text)
        {
            string old = _kv.Get(K(name));
            if (old != null) _kv.Set(K(name + ".bak"), old);
            _kv.Set(K(name), text);
        }

        public void Quarantine(string name)
        {
            string v = _kv.Get(K(name));
            if (v == null) return;
            _kv.Set(K(name + ".corrupt"), v);   // keeps only the latest corrupt copy (storage quota is small in browsers)
            _kv.Remove(K(name));
        }
    }

    public enum LoadStatus { NewGame, Loaded, RecoveredFromBackup, CorruptStartedNew, TooNewStartedNew }

    public sealed class LoadResult
    {
        public LoadStatus Status;
        public SaveData Data;
        public string Detail = "";
    }

    /// <summary>
    /// Versioned, checksummed save. Envelope: {"v":1,"sum":"fnv1a64-hex","payload":"<SaveData json>"}.
    /// The checksum catches truncation and accidental corruption; it is NOT tamper protection (a local
    /// save can always be edited by the device owner).
    /// </summary>
    public sealed class SaveManager
    {
        public const string MainName = "save.json";

        readonly ISaveStorage _storage;
        readonly SaveMigrator _migrator;

        public SaveManager(ISaveStorage storage, SaveMigrator migrator = null)
        {
            _storage = storage;
            _migrator = migrator ?? SaveMigrator.CreateDefault();
        }

        /// <summary>Largest save text accepted when loading or importing (real saves are a few KB).</summary>
        public const int MaxSaveChars = 1_000_000;

        /// <summary>Serialises a save into the checksummed envelope (also used for "export save").</summary>
        public string Encode(SaveData data)
        {
            data.Version = _migrator.TargetVersion;
            string payload = JsonMapper.ToJson(data);
            var env = new Dictionary<string, object>
            {
                ["v"] = (long)data.Version,
                ["sum"] = Checksum(payload),
                ["payload"] = payload
            };
            return MiniJson.Serialize(env);
        }

        /// <summary>
        /// Validates and decodes an envelope (size, JSON, checksum, version, migration) WITHOUT writing anything.
        /// Used both by Load and by "import save", so untrusted text goes through exactly the same checks.
        /// </summary>
        public bool TryDecode(string text, out SaveData data, out string error)
        {
            data = TryParse(text, out bool tooNew);
            error = data != null ? "" : (tooNew ? "too_new" : "invalid");
            return data != null;
        }

        public bool Save(SaveData data)
        {
            try
            {
                _storage.Write(MainName, Encode(data));
                return true;
            }
            catch (Exception)
            {
                return false; // never crash the game because a save failed; the next autosave retries
            }
        }

        public LoadResult Load()
        {
            string[] candidates = { MainName, MainName + ".tmp", MainName + ".bak" };
            bool anyPresent = false, tooNew = false;
            for (int i = 0; i < candidates.Length; i++)
            {
                string text;
                try { text = _storage.Read(candidates[i]); } catch (Exception) { text = null; }
                if (text == null) continue;
                anyPresent = true;
                var r = TryParse(text, out bool newer);
                if (newer) tooNew = true;
                if (r != null)
                {
                    return new LoadResult
                    {
                        Status = i == 0 ? LoadStatus.Loaded : LoadStatus.RecoveredFromBackup,
                        Data = r,
                        Detail = candidates[i]
                    };
                }
            }
            if (!anyPresent) return new LoadResult { Status = LoadStatus.NewGame, Data = new SaveData() };
            try { _storage.Quarantine(MainName); } catch (Exception) { }
            return new LoadResult
            {
                Status = tooNew ? LoadStatus.TooNewStartedNew : LoadStatus.CorruptStartedNew,
                Data = new SaveData(),
                Detail = "no valid save found"
            };
        }

        SaveData TryParse(string text, out bool tooNew)
        {
            tooNew = false;
            try
            {
                if (string.IsNullOrEmpty(text) || text.Length > MaxSaveChars) return null;
                if (!(MiniJson.Parse(text) is Dictionary<string, object> env)) return null;
                if (!env.TryGetValue("payload", out var po) || !(po is string payload)) return null;
                if (!env.TryGetValue("sum", out var so) || !(so is string sum) || sum != Checksum(payload)) return null;
                if (!(MiniJson.Parse(payload) is Dictionary<string, object> tree)) return null;

                int version = tree.TryGetValue("Version", out var vo) && vo is long lv ? (int)lv : 0;
                if (version > _migrator.TargetVersion) { tooNew = true; return null; }
                if (!_migrator.Migrate(tree, version)) return null;
                return (SaveData)JsonMapper.FromTree(typeof(SaveData), tree);
            }
            catch (Exception) { return null; }
        }

        /// <summary>FNV-1a 64-bit over UTF-8 bytes, hex.</summary>
        public static string Checksum(string s)
        {
            ulong h = 14695981039346656037UL;
            foreach (byte b in Encoding.UTF8.GetBytes(s)) { h ^= b; h *= 1099511628211UL; }
            return h.ToString("x16");
        }
    }

    /// <summary>
    /// Explicit schema migrations on the raw JSON tree (before mapping). A step upgrades version N to N+1.
    /// The shipped migrator has no steps because schema version 1 is the first release; tests register
    /// synthetic steps to prove the machinery.
    /// </summary>
    public sealed class SaveMigrator
    {
        readonly Dictionary<int, Action<Dictionary<string, object>>> _steps = new Dictionary<int, Action<Dictionary<string, object>>>();
        public int TargetVersion { get; private set; }

        public SaveMigrator(int targetVersion) { TargetVersion = targetVersion; }
        public static SaveMigrator CreateDefault() => new SaveMigrator(SaveData.CurrentVersion);

        public SaveMigrator Register(int fromVersion, Action<Dictionary<string, object>> step)
        {
            _steps[fromVersion] = step;
            return this;
        }

        /// <summary>Migrates the tree in place up to TargetVersion. Returns false if a step is missing or fails.</summary>
        public bool Migrate(Dictionary<string, object> tree, int fromVersion)
        {
            int v = fromVersion;
            while (v < TargetVersion)
            {
                if (!_steps.TryGetValue(v, out var step)) return false;
                try { step(tree); } catch (Exception) { return false; }
                v++;
                tree["Version"] = (long)v;
            }
            return true;
        }
    }
}
