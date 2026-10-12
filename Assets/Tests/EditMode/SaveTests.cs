using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace OfficinaTycoon.Tests
{
    public class SaveTests
    {
        // ------------------------------------------------------------- JSON + mapper
        [Test]
        public void MiniJson_RoundTripsAllTypes()
        {
            var tree = new Dictionary<string, object>
            {
                ["s"] = "ciao \"mondo\"\n\\ è",
                ["i"] = 5L,
                ["big"] = 638000000000000000L,
                ["u"] = 18446744073709551615UL,
                ["d"] = 1.5e300,
                ["neg"] = -0.25,
                ["t"] = true,
                ["n"] = null,
                ["arr"] = new List<object> { 1L, "x", new Dictionary<string, object> { ["k"] = false } }
            };
            string json = MiniJson.Serialize(tree);
            var back = (Dictionary<string, object>)MiniJson.Parse(json);
            Assert.AreEqual(tree["s"], back["s"]);
            Assert.AreEqual(638000000000000000L, back["big"]);
            Assert.AreEqual(18446744073709551615UL, back["u"]);
            Assert.AreEqual(1.5e300, (double)back["d"]);
            Assert.AreEqual(-0.25, (double)back["neg"]);
            Assert.AreEqual(true, back["t"]);
            Assert.IsNull(back["n"]);
            Assert.AreEqual(3, ((List<object>)back["arr"]).Count);
        }

        [TestCase("")]
        [TestCase("{")]
        [TestCase("{\"a\":}")]
        [TestCase("[1,2")]
        [TestCase("{\"a\":1} extra")]
        [TestCase("\"unterminated")]
        [TestCase("{'a':1}")]
        [TestCase("nul")]
        public void MiniJson_RejectsMalformed(string bad)
        {
            Assert.Throws<JsonException>(() => MiniJson.Parse(bad));
        }

        [Test]
        public void MiniJson_RejectsExcessiveDepth()
        {
            string deep = new string('[', 200) + new string(']', 200);
            Assert.Throws<JsonException>(() => MiniJson.Parse(deep));
        }

        [Test]
        public void MiniJson_RefusesNonFiniteNumbers()
        {
            Assert.Throws<JsonException>(() => MiniJson.Serialize(double.NaN));
            Assert.Throws<JsonException>(() => MiniJson.Serialize(double.PositiveInfinity));
        }

        [Test]
        public void JsonMapper_SaveData_RoundTrips()
        {
            var d = new SaveData();
            d.RngState = 18446744073709551000UL;
            d.SavedAtTicks = 639000000000000123L;
            d.Wallet.Balance = 123456789.125;
            d.Production.Machines.Add(new MachineSave { Id = "drill", Level = 7, Overhaul = 1, Produced = 99.5 });
            d.Experiment.Stage = EventStage.InProgress;
            d.Experiment.Choice = ExperimentChoice.Experimental;
            d.Claimed.Add("order:t1");
            var back = JsonMapper.FromJson<SaveData>(JsonMapper.ToJson(d));
            Assert.AreEqual(d.RngState, back.RngState);
            Assert.AreEqual(d.SavedAtTicks, back.SavedAtTicks);
            Assert.AreEqual(123456789.125, back.Wallet.Balance);
            Assert.AreEqual("drill", back.Production.Machines[0].Id);
            Assert.AreEqual(EventStage.InProgress, back.Experiment.Stage);
            Assert.AreEqual(ExperimentChoice.Experimental, back.Experiment.Choice);
            Assert.AreEqual("order:t1", back.Claimed[0]);
        }

        [Test]
        public void JsonMapper_MissingFieldsKeepDefaults_UnknownIgnored_BadEnumFallsBack()
        {
            var d = JsonMapper.FromJson<SaveData>("{\"Wallet\":{\"Balance\":5},\"FutureField\":123,\"Experiment\":{\"Stage\":\"Nonsense\"}}");
            Assert.AreEqual(5, d.Wallet.Balance);
            Assert.IsTrue(d.Settings.SfxOn, "missing Settings keeps defaults");
            Assert.AreEqual(EventStage.Cooldown, d.Experiment.Stage);
        }

        // ------------------------------------------------------------- game save / load
        [Test]
        public void SaveLoad_RestoresFullGameState()
        {
            var a = Rig.New();
            a.GiveProduction(12);
            a.S.Production.TryBuyUpgrade("up_bench");
            a.S.Production.AddParts(7);
            a.S.Production.AddBoost("crate", 2, 100);
            for (int i = 0; i < 10; i++) a.S.Production.Tap();
            a.Run(5);
            a.S.Orders.Update(0);
            double bal = a.S.Wallet.Balance, earned = a.S.Wallet.TotalEarned;
            var b = a.Restart();
            Assert.AreEqual(LoadStatus.Loaded, b.S.LastLoad.Status);
            Assert.AreEqual(bal, b.S.Wallet.Balance, 1e-6);
            Assert.AreEqual(earned, b.S.Wallet.TotalEarned, 1e-6);
            Assert.AreEqual(12, b.S.Production.Level("drill"));
            Assert.IsTrue(b.S.Production.HasUpgrade("up_bench"));
            Assert.AreEqual(7, b.S.Production.Parts);
            Assert.Greater(b.S.Production.BoostRemaining("crate"), 90);
            Assert.AreEqual(a.S.Production.TapCount, b.S.Production.TapCount);
            Assert.AreEqual(a.S.Orders.Views().Count(), b.S.Orders.Views().Count());
        }

        [Test]
        public void Load_NoSave_StartsNewGame()
        {
            var r = Rig.New();
            Assert.AreEqual(LoadStatus.NewGame, r.S.LastLoad.Status);
            Assert.AreEqual(1, r.S.Stats.Sessions);
        }

        [Test]
        public void Load_GarbageData_StartsNewAndKeepsCorruptFile()
        {
            var st = new InMemorySaveStorage();
            st.Files[SaveManager.MainName] = "this is not json";
            var m = new SaveManager(st);
            var res = m.Load();
            Assert.AreEqual(LoadStatus.CorruptStartedNew, res.Status);
            Assert.IsNotNull(res.Data);
            Assert.AreEqual(1, st.QuarantineCount, "corrupt data must be preserved, not deleted");
            Assert.IsTrue(st.Files.Keys.Any(k => k.Contains("corrupt")));
        }

        [Test]
        public void Load_TruncatedFile_RejectedByChecksum()
        {
            var r = Rig.New();
            r.GiveProduction(5);
            r.S.Save();
            string good = r.Storage.Files[SaveManager.MainName];
            var st = new InMemorySaveStorage();
            st.Files[SaveManager.MainName] = good.Substring(0, good.Length / 2);
            Assert.AreEqual(LoadStatus.CorruptStartedNew, new SaveManager(st).Load().Status);
        }

        [Test]
        public void Load_TamperedPayload_RejectedByChecksum()
        {
            var r = Rig.New();
            r.GiveProduction(5);
            r.S.Save();
            string good = r.Storage.Files[SaveManager.MainName];
            var st = new InMemorySaveStorage();
            st.Files[SaveManager.MainName] = good.Replace("Level\\\":5", "Level\\\":9");
            Assert.AreNotEqual(good, st.Files[SaveManager.MainName], "test must actually modify the file");
            Assert.AreEqual(LoadStatus.CorruptStartedNew, new SaveManager(st).Load().Status);
        }

        [Test]
        public void Load_CorruptMain_RecoversFromBackup()
        {
            var r = Rig.New();
            r.GiveProduction(5);
            r.S.Save();          // first good save
            r.S.Save();          // second save creates the .bak of the first
            Assert.IsTrue(r.Storage.Files.ContainsKey(SaveManager.MainName + ".bak"));
            r.Storage.Files[SaveManager.MainName] = "{broken";
            var res = new SaveManager(r.Storage).Load();
            Assert.AreEqual(LoadStatus.RecoveredFromBackup, res.Status);
            Assert.AreEqual(5, res.Data.Production.Machines.First(m => m.Id == "drill").Level);
        }

        [Test]
        public void Load_PartialTempFileIgnored_CompleteTempUsed()
        {
            var r = Rig.New();
            r.GiveProduction(5);
            r.S.Save();
            string good = r.Storage.Files[SaveManager.MainName];
            // Simulate crash after writing a complete .tmp but before swap: main missing.
            var st = new InMemorySaveStorage();
            st.Files[SaveManager.MainName + ".tmp"] = good;
            Assert.AreEqual(LoadStatus.RecoveredFromBackup, new SaveManager(st).Load().Status);
            st.Files[SaveManager.MainName + ".tmp"] = good.Substring(0, 20);
            Assert.AreEqual(LoadStatus.CorruptStartedNew, new SaveManager(st).Load().Status);
        }

        [Test]
        public void Load_SaveFromNewerApp_IsNotLoadedOrDeleted()
        {
            var r = Rig.New();
            r.S.Save();
            var d = r.S.Capture();
            var tree = (Dictionary<string, object>)MiniJson.Parse(JsonMapper.ToJson(d));
            tree["Version"] = 99L;
            string payload = MiniJson.Serialize(tree);
            var env = new Dictionary<string, object> { ["v"] = 99L, ["sum"] = SaveManager.Checksum(payload), ["payload"] = payload };
            var st = new InMemorySaveStorage();
            st.Files[SaveManager.MainName] = MiniJson.Serialize(env);
            var res = new SaveManager(st).Load();
            Assert.AreEqual(LoadStatus.TooNewStartedNew, res.Status);
            Assert.AreEqual(1, st.QuarantineCount);
        }

        [Test]
        public void Migration_UpgradesOldVersionStepByStep()
        {
            // Synthetic schema: pretend v1 stored the balance as top-level "Money" and v2 moved it into Wallet.
            var migrator = new SaveMigrator(2).Register(1, tree =>
            {
                var wallet = (Dictionary<string, object>)tree["Wallet"];
                wallet["Balance"] = tree["Money"];
                tree.Remove("Money");
            });
            var old = new Dictionary<string, object>
            {
                ["Version"] = 1L,
                ["Money"] = 777.0,
                ["Wallet"] = new Dictionary<string, object> { ["TotalEarned"] = 900.0 }
            };
            string payload = MiniJson.Serialize(old);
            var env = new Dictionary<string, object> { ["v"] = 1L, ["sum"] = SaveManager.Checksum(payload), ["payload"] = payload };
            var st = new InMemorySaveStorage();
            st.Files[SaveManager.MainName] = MiniJson.Serialize(env);
            var res = new SaveManager(st, migrator).Load();
            Assert.AreEqual(LoadStatus.Loaded, res.Status);
            Assert.AreEqual(777.0, res.Data.Wallet.Balance);
            Assert.AreEqual(900.0, res.Data.Wallet.TotalEarned);
        }

        [Test]
        public void Migration_MissingStep_TreatsAsCorrupt_NotSilentlyLoaded()
        {
            var migrator = new SaveMigrator(3).Register(2, t => { });  // no step for 1 -> 2
            var tree = new Dictionary<string, object> { ["Version"] = 1L };
            Assert.IsFalse(migrator.Migrate(tree, 1));
        }

        [Test]
        public void Migration_ThrowingStep_IsHandled()
        {
            var migrator = new SaveMigrator(2).Register(1, t => throw new InvalidOperationException("bad"));
            Assert.IsFalse(migrator.Migrate(new Dictionary<string, object>(), 1));
        }

        [Test]
        public void Save_WritesCurrentVersion()
        {
            var r = Rig.New();
            r.S.Save();
            var env = (Dictionary<string, object>)MiniJson.Parse(r.Storage.Files[SaveManager.MainName]);
            Assert.AreEqual((long)SaveData.CurrentVersion, env["v"]);
        }

        [Test]
        public void Restore_UnknownIdsAndInvalidValues_AreSanitised()
        {
            var d = new SaveData();
            d.Production.Machines.Add(new MachineSave { Id = "ghost", Level = 5 });
            d.Production.Machines.Add(new MachineSave { Id = "drill", Level = 999999, Overhaul = 99 });
            d.Production.Upgrades.Add("ghost_upgrade");
            d.Production.Boosts.Add(new BoostSave { Source = "x", Multiplier = 1000, Remaining = 1e9 });
            d.Wallet.Balance = -50;
            d.Orders.States.Add(new OrderStateSave { Id = "ghost", Progress = 5 });
            var r = Rig.New(start: false);
            r.S.Apply(d);
            Assert.AreEqual(0, r.S.Wallet.Balance);
            Assert.AreEqual(500, r.S.Production.Level("drill"));
            Assert.AreEqual(5, r.S.Production.OverhaulLevel("drill"));
            Assert.AreEqual(0, r.S.Orders.Views().Count());
            Assert.LessOrEqual(r.S.Production.TempMultiplier, r.Config.Economy.MaxTempMultiplier);
        }

        // ------------------------------------------------------------- offline earnings
        static Rig WithProduction()
        {
            var r = Rig.New();
            r.GiveProduction(20);
            r.S.Wallet.Restore(0, r.S.Wallet.TotalEarned, 0);
            return r;
        }

        [Test]
        public void Offline_RewardMatchesElapsedTimesOutputTimesEfficiency()
        {
            var a = WithProduction();
            double ops = a.S.Production.BaseOutputPerSecond;
            a.S.Pause();
            a.Clock.Advance(3600);
            var b = a.Restart(saveFirst: false);
            Assert.IsNotNull(b.S.Offline.Pending);
            Assert.AreEqual(Math.Floor(3600 * ops * 0.5), b.S.Offline.Pending.Base, 1);
            Assert.AreEqual(3600, b.S.Offline.Pending.Seconds, 1e-6);
        }

        [Test]
        public void Offline_IsCappedAtConfiguredMaximum()
        {
            var a = WithProduction();
            double ops = a.S.Production.BaseOutputPerSecond;
            a.S.Pause();
            a.Clock.Advance(30 * 24 * 3600);
            var b = a.Restart(saveFirst: false);
            Assert.AreEqual(a.Config.Offline.MaxSeconds, b.S.Offline.Pending.Seconds, 1e-6);
            Assert.AreEqual(Math.Floor(a.Config.Offline.MaxSeconds * ops * 0.5), b.S.Offline.Pending.Base, 1);
            Assert.IsTrue(b.S.Offline.Pending.HitCap);
        }

        [Test]
        public void Offline_UpgradesRaiseEfficiencyAndCap_ButStayBounded()
        {
            var a = WithProduction();
            a.ReachPhase(2);
            a.Money(1e15);
            Assert.IsTrue(a.S.Production.TryBuyUpgrade("up_night"));
            Assert.IsTrue(a.S.Production.TryBuyUpgrade("up_store"));
            Assert.AreEqual(0.65, a.S.Production.OfflineEfficiency, 1e-9);
            Assert.AreEqual(12 * 3600, a.S.Production.OfflineMaxSeconds, 1e-6);
            Assert.LessOrEqual(a.S.Production.OfflineEfficiency, a.Config.Offline.MaxEfficiency);
        }

        [Test]
        public void Offline_ClockMovedBackwards_GivesNothing_AndDoesNotAllowFarming()
        {
            var a = WithProduction();
            a.S.Pause();
            long saved = a.S.Offline.LastValidTicks;
            a.Clock.Advance(-3600);                          // user sets the clock back 1h
            var b = a.Restart();
            Assert.IsNull(b.S.Offline.Pending);
            Assert.IsTrue(b.S.Offline.ClockAnomaly);
            Assert.AreEqual(saved, b.S.Offline.LastValidTicks, "LastValid must never move backwards");
            b.Clock.Advance(3600 + 60);                      // clock returns to reality + 1 minute
            var c = b.Restart(saveFirst: false);
            Assert.IsNotNull(c.S.Offline.Pending);
            Assert.Less(c.S.Offline.Pending.Seconds, 120, "only the real elapsed time since the last valid stamp counts");
        }

        [Test]
        public void Offline_BelowMinimumDoesNotShowOffer()
        {
            var a = WithProduction();
            a.S.Pause();
            a.Clock.Advance(5);
            Assert.IsNull(a.Restart().S.Offline.Pending);
        }

        [Test]
        public void Offline_NoProduction_NoOffer()
        {
            var a = Rig.New();
            a.S.Pause();
            a.Clock.Advance(7200);
            Assert.IsNull(a.Restart().S.Offline.Pending);
        }

        [Test]
        public void Offline_ClaimPaysOnce()
        {
            var a = WithProduction();
            a.S.Pause();
            a.Clock.Advance(1800);
            var b = a.Restart(saveFirst: false);
            double bal = b.S.Wallet.Balance;
            double expected = b.S.Offline.Pending.Base;
            Assert.AreEqual(expected, b.S.ClaimOffline(), 1e-9);
            Assert.AreEqual(bal + expected, b.S.Wallet.Balance, 1e-6);
            Assert.AreEqual(0, b.S.ClaimOffline());
            Assert.AreEqual(bal + expected, b.S.Wallet.Balance, 1e-6);
        }

        [Test]
        public void Offline_PendingOfferSurvivesRestartUntilClaimed_NotDuplicated()
        {
            var a = WithProduction();
            a.S.Pause();
            a.Clock.Advance(1800);
            var b = a.Restart(saveFirst: false);
            double firstBase = b.S.Offline.Pending.Base;
            int id = b.S.Offline.Pending.Id;
            var c = b.Restart();                       // quit without claiming, immediately reopen
            Assert.IsNotNull(c.S.Offline.Pending);
            Assert.AreEqual(id, c.S.Offline.Pending.Id);
            Assert.AreEqual(firstBase, c.S.Offline.Pending.Base, firstBase * 0.01 + 1, "no double counting of the same absence");
            double bal = c.S.Wallet.Balance;
            c.S.ClaimOffline();
            Assert.IsNull(c.S.Offline.Pending);   // pending cleared
            var d = c.Restart();
            Assert.IsNull(d.S.Offline.Pending, "claimed offer must not reappear after restart");
        }

        [Test]
        public void Offline_ResumeFromBackground_ComputesOfflineToo()
        {
            var a = WithProduction();
            a.S.Pause();
            a.Clock.Advance(600);
            a.S.Resume();
            Assert.IsNotNull(a.S.Offline.Pending);
            Assert.AreEqual(600, a.S.Offline.Pending.Seconds, 1e-6);
        }

        [Test]
        public void Offline_AdvancesEventTimersAndBoosts()
        {
            var a = WithProduction();
            a.S.Production.AddBoost("crate", 2, 100);
            a.ReachPhase(1);
            a.S.Pause();
            a.Clock.Advance(7200);
            var b = a.Restart(saveFirst: false);
            Assert.AreEqual(0, b.S.Production.BoostRemaining("crate"));
            Assert.AreEqual(EventStage.Offered, b.S.Experimental.Stage, "first offer delay elapsed while away");
        }

        [Test]
        public void Frame_LongDeltaIsClampedAndNotTreatedAsOffline()
        {
            var r = WithProduction();
            double ops = r.S.Production.BaseOutputPerSecond;
            double before = r.S.Wallet.Balance;
            r.S.Update(100000);
            Assert.AreEqual(before + ops * GameSession.MaxFrameSeconds, r.S.Wallet.Balance, ops * 0.01 + 1e-6);
        }

        // ------------------------------------------------------------- duplicate protection
        [Test]
        public void Rewards_OrderClaimOnlyOnce_EvenAfterRestore()
        {
            var r = Rig.New();
            for (int i = 0; i < 10; i++) r.S.Production.Tap();
            r.S.Orders.Update(0);
            Assert.AreEqual(OrderStatus.Completed, r.S.Orders.Get("t1").Status);
            r.S.Save();                                       // checkpoint BEFORE claiming
            double balBefore = r.S.Wallet.Balance;
            Assert.IsTrue(r.S.Orders.TryClaim("t1"));
            Assert.IsFalse(r.S.Orders.TryClaim("t1"));
            double afterOnce = r.S.Wallet.Balance;
            Assert.AreEqual(balBefore + 15, afterOnce, 1e-9);
            r.S.Save();
            var r2 = r.Restart();
            Assert.IsFalse(r2.S.Orders.TryClaim("t1"));
            Assert.AreEqual(afterOnce, r2.S.Wallet.Balance, 1e-6);
        }

        [Test]
        public void Ledger_RestoreKeepsKeys_AndBoundsSize()
        {
            var l = new ClaimLedger();
            for (int i = 0; i < ClaimLedger.Capacity + 50; i++) Assert.IsTrue(l.TryClaim("k" + i));
            Assert.AreEqual(ClaimLedger.Capacity, l.Snapshot().Count);
            Assert.IsFalse(l.TryClaim("k" + (ClaimLedger.Capacity + 49)));
            var l2 = new ClaimLedger();
            l2.Restore(l.Snapshot());
            Assert.IsFalse(l2.TryClaim("k" + (ClaimLedger.Capacity + 10)));
            Assert.IsFalse(l2.TryClaim(""));
            Assert.IsFalse(l2.TryClaim(null));
        }

        [Test]
        public void Rng_StateIsPersisted_SoReloadReplaysSameRolls()
        {
            var cfg = DefaultConfig.Create();
            var clock = new ManualClock();
            var st = new InMemorySaveStorage();
            var s1 = new GameSession(cfg, clock, st, new MockRewardedAdService());
            s1.Start();
            s1.Rng.NextDouble(); s1.Rng.NextDouble();
            s1.Save();
            double next1 = s1.Rng.NextDouble();
            var s2 = new GameSession(DefaultConfig.Create(), clock, st, new MockRewardedAdService());
            s2.Start();
            Assert.AreEqual(next1, s2.Rng.NextDouble(), 0, "restoring a save replays the same random sequence (no re-rolling by reloading)");
        }
    }
}
