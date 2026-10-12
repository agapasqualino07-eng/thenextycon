using System.Collections.Generic;

namespace OfficinaTycoon
{
    /// <summary>
    /// Shipped defaults. Edit numbers here (or override via JSON) and re-run the simulator
    /// (Tools/Simulator) and the config validation tests.
    /// </summary>
    public static class DefaultConfig
    {
        public static GameConfig Create()
        {
            var c = new GameConfig();

            c.Phases.Add(new PhaseDef { Id = "p0", Name = "Piccola officina", TotalEarnedRequired = 0 });
            c.Phases.Add(new PhaseDef { Id = "p1", Name = "Officina in crescita", TotalEarnedRequired = 2000000, CratesOnEnter = 1 });
            c.Phases.Add(new PhaseDef { Id = "p2", Name = "Stabilimento industriale", TotalEarnedRequired = 30000000000, CratesOnEnter = 2 });

            // ---- Machines ----------------------------------------------------------------
            // cost(n) = BaseCost * CostGrowth^n ; output = BaseOutput * level * multipliers
            c.Machines.Add(M("drill", "Trapano a colonna", "Il primo pezzo: fori precisi.", "Piastre forate", 15, 1.15, 0.5, 0, "", 0));
            c.Machines.Add(M("lathe", "Tornio", "Lavora alberi e bulloni.", "Alberi", 450, 1.16, 4, 0, "drill", 5));
            c.Machines.Add(M("mill", "Fresatrice", "Sagome complesse in metallo.", "Ingranaggi", 13500, 1.17, 32, 0, "lathe", 5));
            c.Machines.Add(M("cnc", "Centro CNC", "Lavorazioni automatiche a 5 assi.", "Componenti CNC", 405000, 1.18, 256, 1, "mill", 5));
            c.Machines.Add(M("press", "Pressa idraulica", "Stampaggio ad alto tonnellaggio.", "Scocche", 12000000, 1.19, 2048, 1, "cnc", 5));
            c.Machines.Add(M("robot", "Braccio robotico", "Saldature e movimentazione.", "Gruppi saldati", 365000000, 1.2, 16384, 1, "press", 5));
            c.Machines.Add(M("line", "Linea di assemblaggio", "Montaggio continuo.", "Motori assemblati", 11000000000, 1.21, 131072, 2, "robot", 10));
            c.Machines.Add(M("smart", "Fabbrica intelligente", "Produzione coordinata dal gestionale.", "Veicoli finiti", 328000000000, 1.22, 1048576, 2, "line", 10));

            // ---- Permanent upgrades ------------------------------------------------------
            c.Upgrades.Add(U("up_bench", "Banco ergonomico", "Raddoppia il valore dei tocchi.", 60, UpgradeEffect.TapMultiplier, "", 2, "", 0, 0));
            c.Upgrades.Add(U("up_drill", "Punte al titanio", "Trapano a colonna x2.", 900, UpgradeEffect.MachineMultiplier, "drill", 2, "drill", 10, 0));
            c.Upgrades.Add(U("up_lathe", "Utensili in widia", "Tornio x2.", 7200, UpgradeEffect.MachineMultiplier, "lathe", 2, "lathe", 10, 0));
            c.Upgrades.Add(U("up_mill", "Refrigerante ad alta pressione", "Fresatrice x2.", 57600, UpgradeEffect.MachineMultiplier, "mill", 2, "mill", 10, 0));
            c.Upgrades.Add(U("up_led", "Illuminazione LED", "Tutta la produzione +25%.", 600000, UpgradeEffect.GlobalMultiplier, "", 1.25, "", 0, 1));
            c.Upgrades.Add(U("up_night", "Turno di notte", "Guadagni offline +15% di efficienza.", 2000000, UpgradeEffect.OfflineEfficiencyAdd, "", 0.15, "", 0, 1));
            c.Upgrades.Add(U("up_cnc", "Controllo adattivo", "Centro CNC x2.", 460000, UpgradeEffect.MachineMultiplier, "cnc", 2, "cnc", 10, 1));
            c.Upgrades.Add(U("up_press", "Accumulatori idraulici", "Pressa idraulica x2.", 3700000, UpgradeEffect.MachineMultiplier, "press", 2, "press", 10, 1));
            c.Upgrades.Add(U("up_robot", "Sensori di visione", "Braccio robotico x2.", 29000000, UpgradeEffect.MachineMultiplier, "robot", 2, "robot", 10, 1));
            c.Upgrades.Add(U("up_store", "Magazzino ampliato", "Limite offline +4 ore.", 400000000, UpgradeEffect.OfflineCapHoursAdd, "", 4, "", 0, 2));
            c.Upgrades.Add(U("up_erp", "Gestionale ERP", "Tutta la produzione +50%.", 20000000000, UpgradeEffect.GlobalMultiplier, "", 1.5, "", 0, 2));
            c.Upgrades.Add(U("up_line", "Nastri sincronizzati", "Linea di assemblaggio x2.", 236000000, UpgradeEffect.MachineMultiplier, "line", 2, "line", 10, 2));
            c.Upgrades.Add(U("up_smart", "Gemello digitale", "Fabbrica intelligente x2.", 1900000000, UpgradeEffect.MachineMultiplier, "smart", 2, "smart", 10, 2));
            c.Upgrades.Add(U("up_lean", "Produzione snella", "Tutta la produzione x2.", 50000000000000, UpgradeEffect.GlobalMultiplier, "", 2, "", 0, 2));

            // ---- Synergies (advanced phase) ----------------------------------------------
            c.Synergies.Add(new SynergyDef { Id = "syn_cnc_press", SourceMachineId = "cnc", TargetMachineId = "press", BonusPerSourceLevel = 0.01, UnlockPhase = 2 });
            c.Synergies.Add(new SynergyDef { Id = "syn_robot_line", SourceMachineId = "robot", TargetMachineId = "line", BonusPerSourceLevel = 0.01, UnlockPhase = 2 });
            c.Synergies.Add(new SynergyDef { Id = "syn_line_smart", SourceMachineId = "line", TargetMachineId = "smart", BonusPerSourceLevel = 0.01, UnlockPhase = 2 });

            // ---- Orders -------------------------------------------------------------------
            // Tutorial chain (first minute): tap, buy a machine, see production.
            c.Orders.Add(O("t1", "Primi pezzi", "Lavora 10 pezzi a mano toccando il banco.", OrderType.TapCount, "", 10, 0, "", 0, 0, 15, 0, true));
            c.Orders.Add(O("t2", "Un po' di aiuto", "Acquista il Trapano a colonna.", OrderType.MachineLevel, "drill", 1, 0, "t1", 0, 0, 30, 0, true));
            c.Orders.Add(O("t3", "Produzione automatica", "Raggiungi 1 euro al secondo di produzione.", OrderType.OutputPerSecond, "", 1, 0, "t2", 0, 0, 60, 0, true));
            // Phase 0
            c.Orders.Add(O("a1", "Ordine officina locale", "Guadagna 500 dalla produzione.", OrderType.EarnCurrency, "", 500, 0, "t3", 0, 0, 30, 0));
            c.Orders.Add(O("a2", "Secondo macchinario", "Porta il Tornio al livello 3.", OrderType.MachineLevel, "lathe", 3, 0, "t3", 0, 0, 40, 0));
            c.Orders.Add(O("a3", "Utensili migliori", "Acquista le Punte al titanio.", OrderType.BuyUpgrade, "up_drill", 1, 0, "t3", 0, 0, 40, 0));
            c.Orders.Add(O("a4", "Lotto di alberi", "Produci 2.000 con il Tornio.", OrderType.ProduceWithMachine, "lathe", 2000, 0, "a2", 0, 1, 60, 0));
            c.Orders.Add(O("a5", "Consegna express", "Guadagna 3.000 in 3 minuti.", OrderType.EarnCurrency, "", 3000, 0, "a1", 180, 0, 90, 0));
            c.Orders.Add(O("a6", "Terza linea", "Porta la Fresatrice al livello 5.", OrderType.MachineLevel, "mill", 5, 0, "a4", 0, 1, 90, 0));
            // Phase 1
            c.Orders.Add(O("b1", "Commessa regionale", "Raggiungi 5.000 al secondo.", OrderType.OutputPerSecond, "", 5000, 1, "a6", 0, 1, 120, 0));
            c.Orders.Add(O("b2", "Esperimenti industriali", "Completa un evento industriale.", OrderType.CompleteEvent, "any", 1, 1, "", 0, 0, 120, 0));
            c.Orders.Add(O("b3", "Controllo numerico", "Porta il Centro CNC al livello 10.", OrderType.MachineLevel, "cnc", 10, 1, "", 0, 1, 120, 0));
            c.Orders.Add(O("b4", "Grande commessa", "Guadagna 50 milioni dalla produzione.", OrderType.EarnCurrency, "", 50000000, 1, "b1", 0, 1, 180, 5));
            c.Orders.Add(O("b5", "Reparto presse", "Porta la Pressa idraulica al livello 10.", OrderType.MachineLevel, "press", 10, 1, "b3", 0, 0, 150, 0));
            c.Orders.Add(O("b6", "Automazione", "Porta il Braccio robotico al livello 5.", OrderType.MachineLevel, "robot", 5, 1, "b5", 0, 1, 180, 0));
            // Phase 2
            c.Orders.Add(O("c1", "Contratto industriale", "Raggiungi 5 milioni al secondo.", OrderType.OutputPerSecond, "", 5000000, 2, "", 0, 1, 240, 0));
            c.Orders.Add(O("c2", "Produzione in serie", "Produci 500 miliardi con la Linea di assemblaggio.", OrderType.ProduceWithMachine, "line", 500000000000, 2, "", 0, 1, 300, 10));
            c.Orders.Add(O("c3", "Fabbrica del futuro", "Porta la Fabbrica intelligente al livello 10.", OrderType.MachineLevel, "smart", 10, 2, "c1", 0, 2, 360, 10));
            c.Orders.Add(O("c4", "Maestro dell'industria", "Guadagna 100.000 miliardi dalla produzione.", OrderType.EarnCurrency, "", 100000000000000, 2, "c3", 0, 2, 420, 15));

            // ---- Offline -------------------------------------------------------------------
            // defaults in OfflineConfig

            // ---- Experimental machine ------------------------------------------------------
            // Probabilities are shown to the player. EV (per unit of cost) is computed by RiskMath.
            // Prudent EV = 1.20. Experimental EV = 0.3*0.30 + 0.38*1.25 + 0.24*2.00 + 0.08*3.80 = 1.349.
            c.Experimental.Outcomes.Add(new ExperimentOutcomeDef { Id = "fail", Label = "Prototipo difettoso", ProbabilityPercent = 30, ReturnMultiplier = 0.30 });
            c.Experimental.Outcomes.Add(new ExperimentOutcomeDef { Id = "std", Label = "Prototipo funzionante", ProbabilityPercent = 38, ReturnMultiplier = 1.25 });
            c.Experimental.Outcomes.Add(new ExperimentOutcomeDef { Id = "good", Label = "Prototipo efficiente", ProbabilityPercent = 24, ReturnMultiplier = 2.00 });
            c.Experimental.Outcomes.Add(new ExperimentOutcomeDef { Id = "great", Label = "Prototipo eccezionale", ProbabilityPercent = 8, ReturnMultiplier = 3.80, BonusBoostMultiplier = 1.5, BonusBoostSeconds = 120 });

            // ---- Production surge ------------------------------------------------------------
            // Boost value = (multiplier-1) * seconds. Guaranteed = 90. Experiment EV = 103.5.
            c.Surge.Outcomes.Add(new SurgeOutcomeDef { Id = "weak", Label = "Domanda moderata", ProbabilityPercent = 40, BoostMultiplier = 1.5, BoostSeconds = 60 });
            c.Surge.Outcomes.Add(new SurgeOutcomeDef { Id = "normal", Label = "Domanda sostenuta", ProbabilityPercent = 35, BoostMultiplier = 2.0, BoostSeconds = 90 });
            c.Surge.Outcomes.Add(new SurgeOutcomeDef { Id = "strong", Label = "Picco di richieste", ProbabilityPercent = 20, BoostMultiplier = 3.0, BoostSeconds = 90 });
            c.Surge.Outcomes.Add(new SurgeOutcomeDef { Id = "huge", Label = "Commessa eccezionale", ProbabilityPercent = 5, BoostMultiplier = 5.0, BoostSeconds = 120 });

            // ---- Free parts crate ----------------------------------------------------------------
            c.Crate.Entries.Add(new CrateEntryDef { Id = "cur_s", Label = "Fondi di magazzino (piccoli)", Kind = CrateRewardKind.Currency, ProbabilityPercent = 40, OpsSeconds = 120 });
            c.Crate.Entries.Add(new CrateEntryDef { Id = "cur_l", Label = "Fondi di magazzino (grandi)", Kind = CrateRewardKind.Currency, ProbabilityPercent = 20, OpsSeconds = 400 });
            c.Crate.Entries.Add(new CrateEntryDef { Id = "boost", Label = "Acceleratore temporaneo", Kind = CrateRewardKind.TempBoost, ProbabilityPercent = 20, BoostMultiplier = 2, BoostSeconds = 120 });
            c.Crate.Entries.Add(new CrateEntryDef { Id = "parts", Label = "Ricambi", Kind = CrateRewardKind.Parts, ProbabilityPercent = 17, Parts = 3 });
            c.Crate.Entries.Add(new CrateEntryDef { Id = "alloy", Label = "Lega rara (molti ricambi)", Kind = CrateRewardKind.Parts, ProbabilityPercent = 3, Parts = 12 });

            return c;
        }

        static MachineDef M(string id, string name, string desc, string good, double cost, double growth, double output,
                            int phase, string reqId, int reqLevel)
            => new MachineDef { Id = id, Name = name, Description = desc, Good = good, BaseCost = cost, CostGrowth = growth,
                                BaseOutput = output, UnlockPhase = phase, UnlockRequiresMachineId = reqId,
                                UnlockRequiresLevel = reqLevel, SpriteKey = "machine_" + id };

        static UpgradeDef U(string id, string name, string desc, double cost, UpgradeEffect eff, string target, double value,
                            string reqMachine, int reqLevel, int phase)
            => new UpgradeDef { Id = id, Name = name, Description = desc, Cost = cost, Effect = eff, TargetMachineId = target,
                                Value = value, RequiresMachineId = reqMachine, RequiresMachineLevel = reqLevel, UnlockPhase = phase };

        static OrderDef O(string id, string title, string desc, OrderType type, string targetId, double target, int phase,
                          string prereq, double deadline, int crates, double rewardOpsSeconds, int parts, bool tutorial = false)
            => new OrderDef { Id = id, Title = title, Description = desc, Type = type, TargetId = targetId, Target = target,
                              UnlockPhase = phase, PrerequisiteOrderId = prereq, DeadlineSeconds = deadline,
                              RewardCrates = crates, RewardOpsSeconds = rewardOpsSeconds, RewardParts = parts, IsTutorial = tutorial,
                              RewardMin = tutorial ? rewardOpsSeconds : 25 };
    }
}
