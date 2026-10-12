using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace OfficinaTycoon.Tests
{
    /// <summary>Tests for untrusted-input handling (save import, JSON limits) and the key-value save storage used by the web build.</summary>
    public class SecurityAndStorageTests
    {
        // ------------------------------------------------------------ key-value storage
        [Test]
        public void KeyValueStorage_SaveLoadRoundTrip_WithBackup()
        {
            var kv = new InMemoryKeyValueStore();
            var st = new KeyValueSaveStorage(kv);
            var clock = new ManualClock();
            var s = new GameSession(DefaultConfig.Create(), clock, st, new MockRewardedAdService());
            s.Start();
            s.Wallet.Add(500, "t");
            s.Production.TryBuy("drill", 3);
            s.Save();
            s.Save();
            Assert.IsTrue(kv.Data.ContainsKey("officina." + SaveManager.MainName));
            Assert.IsTrue(kv.Data.ContainsKey("officina." + SaveManager.MainName + ".bak"));
            var s2 = new GameSession(DefaultConfig.Create(), clock, st, new MockRewardedAdService());
            s2.Start();
            Assert.AreEqual(3, s2.Production.Level("drill"));
        }

        [Test]
        public void KeyValueStorage_CorruptMain_RecoversBackup_AndKeepsCorruptCopy()
        {
            var kv = new InMemoryKeyValueStore();
            var st = new KeyValueSaveStorage(kv);
            var clock = new ManualClock();
            var s = new GameSession(DefaultConfig.Create(), clock, st, new MockRewardedAdService());
            s.Start();
            s.Wallet.Add(500, "t");
            s.Production.TryBuy("drill", 2);
            s.Save(); s.Save();
            kv.Data["officina." + SaveManager.MainName] = "{garbage";
            var res = new SaveManager(st).Load();
            Assert.AreEqual(LoadStatus.RecoveredFromBackup, res.Status);
            kv.Data["officina." + SaveManager.MainName + ".bak"] = "{garbage";
            var res2 = new SaveManager(st).Load();
            Assert.AreEqual(LoadStatus.CorruptStartedNew, res2.Status);
            Assert.IsTrue(kv.Data.ContainsKey("officina." + SaveManager.MainName + ".corrupt"));
        }

        // ------------------------------------------------------------ export / import
        static string ExportOf(Action<GameSession> setup)
        {
            var r = Rig.New();
            setup(r.S);
            return r.S.Saves.Encode(r.S.Capture());
        }

        [Test]
        public void Import_ValidExport_Decodes()
        {
            string text = ExportOf(s => { s.Wallet.Add(1000, "t"); s.Production.TryBuy("drill", 4); });
            var m = new SaveManager(new InMemorySaveStorage());
            Assert.IsTrue(m.TryDecode(text, out var d, out var err), err);
            Assert.AreEqual(4, d.Production.Machines.First(x => x.Id == "drill").Level);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("hello")]
        [TestCase("{\"v\":1,\"sum\":\"00\",\"payload\":\"{}\"}")]
        [TestCase("[1,2,3]")]
        [TestCase("{\"v\":1}")]
        public void Import_InvalidText_Rejected(string text)
        {
            var m = new SaveManager(new InMemorySaveStorage());
            Assert.IsFalse(m.TryDecode(text, out var d, out var err));
            Assert.IsNull(d);
            Assert.AreEqual("invalid", err);
        }

        [Test]
        public void Import_TamperedValue_RejectedByChecksum()
        {
            string text = ExportOf(s => { s.Wallet.Add(1000, "t"); });
            string forged = text.Replace("Balance\\\":1000", "Balance\\\":999999999");
            Assert.AreNotEqual(text, forged);
            Assert.IsFalse(new SaveManager(new InMemorySaveStorage()).TryDecode(forged, out _, out _));
        }

        [Test]
        public void Import_OversizedText_RejectedQuickly()
        {
            string huge = "{\"v\":1,\"sum\":\"x\",\"payload\":\"" + new string('a', SaveManager.MaxSaveChars + 10) + "\"}";
            var sw = System.Diagnostics.Stopwatch.StartNew();
            Assert.IsFalse(new SaveManager(new InMemorySaveStorage()).TryDecode(huge, out _, out _));
            Assert.Less(sw.ElapsedMilliseconds, 500);
        }

        [Test]
        public void Import_ExtremeNumericValues_AreSanitisedOnApply()
        {
            // A save produced by hand with absurd values (valid checksum) must not crash or break invariants.
            var d = new SaveData();
            d.Wallet.Balance = 1e300; d.Wallet.TotalEarned = -5;
            d.Production.Machines.Add(new MachineSave { Id = "drill", Level = int.MaxValue, Produced = -1 });
            d.Production.Parts = -10;
            d.Crate.Available = 1000;
            d.Offline.HasOffer = true; d.Offline.Base = 1e300; d.Offline.Seconds = -1;
            var r = Rig.New(start: false);
            r.S.Apply(d);
            Assert.LessOrEqual(r.S.Wallet.Balance, r.Config.Economy.MaxBalance);
            Assert.GreaterOrEqual(r.S.Wallet.TotalEarned, 0);
            Assert.AreEqual(r.Config.Machines[0].MaxLevel, r.S.Production.Level("drill"));
            Assert.AreEqual(0, r.S.Production.Parts);
            Assert.AreEqual(r.Config.Crate.MaxStored, r.S.Crates.Available);
            Assert.GreaterOrEqual(r.S.Offline.Pending.Seconds, 0);
            double got = r.S.Offline.TryClaim();
            Assert.LessOrEqual(r.S.Wallet.Balance, r.Config.Economy.MaxBalance);
            Assert.IsTrue(Num.IsFinite(got));
        }

        [Test]
        public void Import_SaveFromNewerVersion_ReportedAsTooNew()
        {
            var r = Rig.New();
            var tree = (Dictionary<string, object>)MiniJson.Parse(JsonMapper.ToJson(r.S.Capture()));
            tree["Version"] = 50L;
            string payload = MiniJson.Serialize(tree);
            string text = MiniJson.Serialize(new Dictionary<string, object> { ["v"] = 50L, ["sum"] = SaveManager.Checksum(payload), ["payload"] = payload });
            Assert.IsFalse(new SaveManager(new InMemorySaveStorage()).TryDecode(text, out _, out var err));
            Assert.AreEqual("too_new", err);
        }

        // ------------------------------------------------------------ parser limits / mapper safety
        [Test]
        public void MiniJson_RejectsInputOverSizeCap()
        {
            Assert.Throws<JsonException>(() => MiniJson.Parse(new string(' ', MiniJson.MaxInputChars + 1)));
        }

        [Test]
        public void MiniJson_RandomGarbageNeverThrowsUnexpectedExceptionTypes()
        {
            var rnd = new Random(99);
            const string alphabet = "{}[]\":,0123456789.-eEtrufalsn\\u ";
            for (int i = 0; i < 3000; i++)
            {
                var chars = new char[rnd.Next(0, 60)];
                for (int k = 0; k < chars.Length; k++) chars[k] = alphabet[rnd.Next(alphabet.Length)];
                try { MiniJson.Parse(new string(chars)); }
                catch (JsonException) { }
            }
            Assert.Pass("only JsonException is ever thrown for malformed input");
        }

        [Test]
        public void JsonMapper_HugeNumbersSaturateInsteadOfWrapping()
        {
            var d = JsonMapper.FromJson<SaveData>("{\"SavedAtTicks\":1e300,\"PhaseIndex\":1e300,\"Production\":{\"TapCount\":-1e300}}");
            Assert.AreEqual(long.MaxValue, d.SavedAtTicks);
            Assert.AreEqual(int.MaxValue, d.PhaseIndex);
            Assert.AreEqual(long.MinValue, d.Production.TapCount);
            var r = Rig.New(start: false);
            r.S.Apply(d);
            Assert.AreEqual(r.Config.Phases.Count - 1, r.S.Progression.PhaseIndex);
            Assert.AreEqual(0, r.S.Production.TapCount);
        }

        [Test]
        public void JsonMapper_OnlyInstantiatesDeclaredFieldTypes()
        {
            // No type names are read from the data: a "$type"-style payload is just an unknown field.
            var d = JsonMapper.FromJson<SaveData>("{\"$type\":\"System.IO.FileInfo, System.IO\",\"Wallet\":{\"$type\":\"x\",\"Balance\":3}}");
            Assert.IsInstanceOf<SaveData>(d);
            Assert.IsInstanceOf<WalletSave>(d.Wallet);
            Assert.AreEqual(3, d.Wallet.Balance);
        }
    }
}
