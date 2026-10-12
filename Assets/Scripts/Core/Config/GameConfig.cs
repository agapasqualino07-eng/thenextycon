using System;
using System.Collections.Generic;

namespace OfficinaTycoon
{
    // All tunable numbers live here. Defaults are in DefaultConfig.cs; a JSON file can override any part
    // (see JsonMapper rules). Nothing in gameplay code hardcodes costs, rewards or probabilities.

    [Serializable]
    public class GameConfig
    {
        public int SchemaVersion = 1;
        public EconomyConfig Economy = new EconomyConfig();
        public List<PhaseDef> Phases = new List<PhaseDef>();
        public List<MachineDef> Machines = new List<MachineDef>();
        public List<UpgradeDef> Upgrades = new List<UpgradeDef>();
        public List<SynergyDef> Synergies = new List<SynergyDef>();
        public List<OrderDef> Orders = new List<OrderDef>();
        public OfflineConfig Offline = new OfflineConfig();
        public ExperimentalConfig Experimental = new ExperimentalConfig();
        public SurgeConfig Surge = new SurgeConfig();
        public CrateConfig Crate = new CrateConfig();
        public AdsConfig Ads = new AdsConfig();
        public FrenzyConfig Frenzy = new FrenzyConfig();
        public DailyConfig Daily = new DailyConfig();
        public SocialConfig Social = new SocialConfig();
    }

    [Serializable]
    public class EconomyConfig
    {
        /// <summary>Hard ceiling for the balance (overflow protection).</summary>
        public double MaxBalance = 1e24;
        public double StartingBalance = 0;
        /// <summary>Currency per manual tap before multipliers.</summary>
        public double TapBase = 1;
        /// <summary>Extra tap value as a fraction of base production per second (keeps taps relevant, but small).</summary>
        public double TapOpsFraction = 0.02;
        /// <summary>Every N levels of a machine its output is multiplied by MilestoneMultiplier.</summary>
        public int MilestoneEvery = 25;
        public double MilestoneMultiplier = 2.0;
        /// <summary>Each machine overhaul (paid in spare parts) adds this fraction to the machine's output.</summary>
        public double OverhaulBonus = 0.15;
        public int OverhaulMaxLevel = 5;
        public int OverhaulPartsBase = 5;      // parts for the first overhaul
        public double OverhaulPartsGrowth = 2; // x per further overhaul
        /// <summary>Product of all temporary boosts can never exceed this.</summary>
        public double MaxTempMultiplier = 4.0;
        public double AutosaveSeconds = 20;
    }

    [Serializable]
    public class PhaseDef
    {
        public string Id = "";
        public string Name = "";
        /// <summary>Lifetime currency earned required to enter this phase.</summary>
        public double TotalEarnedRequired = 0;
        /// <summary>Crates granted (once) when the phase is entered.</summary>
        public int CratesOnEnter = 0;
    }

    [Serializable]
    public class MachineDef
    {
        public string Id = "";
        public string Name = "";
        public string Description = "";
        public string Good = "";            // what it produces (flavour + order text)
        public double BaseCost = 10;
        /// <summary>Cost of level n+1 = BaseCost * CostGrowth^n (n = current level).</summary>
        public double CostGrowth = 1.15;
        /// <summary>Output per second contributed by each level before multipliers.</summary>
        public double BaseOutput = 1;
        public int MaxLevel = 500;
        public int UnlockPhase = 0;
        public string UnlockRequiresMachineId = "";
        public int UnlockRequiresLevel = 0;
        public string SpriteKey = "";
    }

    public enum UpgradeEffect { MachineMultiplier, GlobalMultiplier, TapMultiplier, OfflineEfficiencyAdd, OfflineCapHoursAdd }

    [Serializable]
    public class UpgradeDef
    {
        public string Id = "";
        public string Name = "";
        public string Description = "";
        public double Cost = 100;
        public UpgradeEffect Effect = UpgradeEffect.GlobalMultiplier;
        public string TargetMachineId = "";
        /// <summary>Multiplier for *Multiplier effects; amount to add for Offline* effects.</summary>
        public double Value = 2.0;
        public string RequiresMachineId = "";
        public int RequiresMachineLevel = 0;
        public int UnlockPhase = 0;
    }

    /// <summary>Each level of Source adds BonusPerSourceLevel (fraction) to Target's multiplier.</summary>
    [Serializable]
    public class SynergyDef
    {
        public string Id = "";
        public string SourceMachineId = "";
        public string TargetMachineId = "";
        public double BonusPerSourceLevel = 0.01;
        public int UnlockPhase = 2;
    }

    public enum OrderType { EarnCurrency, TapCount, MachineLevel, BuyUpgrade, OutputPerSecond, CompleteEvent, ProduceWithMachine }

    [Serializable]
    public class OrderDef
    {
        public string Id = "";
        public string Title = "";
        public string Description = "";
        public OrderType Type = OrderType.EarnCurrency;
        /// <summary>Machine id (MachineLevel, ProduceWithMachine) or upgrade id (BuyUpgrade).</summary>
        public string TargetId = "";
        public double Target = 1;
        public int UnlockPhase = 0;
        public string PrerequisiteOrderId = "";
        /// <summary>0 = no deadline. On expiry progress resets and the order restarts, with no penalty.</summary>
        public double DeadlineSeconds = 0;
        public bool IsTutorial = false;
        /// <summary>Reward = max(RewardMin, RewardOpsSeconds * base production per second).</summary>
        public double RewardOpsSeconds = 60;
        public double RewardMin = 10;
        public int RewardCrates = 0;
        public int RewardParts = 0;
    }

    [Serializable]
    public class OfflineConfig
    {
        public double MaxSeconds = 8 * 3600;
        /// <summary>Fraction of base production earned while away.</summary>
        public double Efficiency = 0.5;
        public double MinSecondsToShow = 30;
        /// <summary>Optional rewarded-ad bonus, declared up-front: + this fraction of the base reward.</summary>
        public double AdBonusFraction = 0.5;
        /// <summary>Ceiling for all offline-related upgrades.</summary>
        public double MaxEfficiency = 0.9;
        public double MaxSecondsCeiling = 24 * 3600;
    }

    [Serializable]
    public class ExperimentOutcomeDef
    {
        public string Id = "";
        public string Label = "";
        public double ProbabilityPercent = 0;
        /// <summary>Returned amount as a multiple of the cost paid (0.3 = lose 70%).</summary>
        public double ReturnMultiplier = 1;
        public double BonusBoostMultiplier = 0;
        public double BonusBoostSeconds = 0;
    }

    [Serializable]
    public class ExperimentalConfig
    {
        public int UnlockPhase = 1;
        public double FirstOfferDelaySeconds = 120;
        public double CooldownSeconds = 900;
        public double DeclineCooldownSeconds = 300; // declining is free and only delays the next offer
        /// <summary>Costs are expressed in seconds of base production, with a floor.</summary>
        public double PrudentCostOpsSeconds = 60;
        public double ExperimentalCostOpsSeconds = 120;
        public double MinCost = 50;
        public double PrudentReturnMultiplier = 1.2;
        public double PrudentDurationSeconds = 20;
        public double ExperimentalDurationSeconds = 45;
        /// <summary>Hard cap for any payout, in seconds of base production (inflation guard).</summary>
        public double MaxRewardOpsSeconds = 900;
        public List<ExperimentOutcomeDef> Outcomes = new List<ExperimentOutcomeDef>();
    }

    [Serializable]
    public class SurgeOutcomeDef
    {
        public string Id = "";
        public string Label = "";
        public double ProbabilityPercent = 0;
        public double BoostMultiplier = 2;
        public double BoostSeconds = 60;
    }

    [Serializable]
    public class SurgeConfig
    {
        public int UnlockPhase = 1;
        public double FirstOfferDelaySeconds = 300;
        public double CooldownSeconds = 1500;
        public double OfferWindowSeconds = 300;
        public double GuaranteedMultiplier = 2.0;
        public double GuaranteedSeconds = 90;
        public List<SurgeOutcomeDef> Outcomes = new List<SurgeOutcomeDef>();
    }

    public enum CrateRewardKind { Currency, TempBoost, Parts }

    [Serializable]
    public class CrateEntryDef
    {
        public string Id = "";
        public string Label = "";
        public CrateRewardKind Kind = CrateRewardKind.Currency;
        public double ProbabilityPercent = 0;
        public double OpsSeconds = 0;       // Currency: seconds of base production
        public double BoostMultiplier = 0;  // TempBoost
        public double BoostSeconds = 0;
        public int Parts = 0;               // Parts
    }

    [Serializable]
    public class CrateConfig
    {
        public int MaxStored = 5;
        public double MinCurrency = 25;
        public double MaxCurrencyOpsSeconds = 1800;
        /// <summary>If a crate is earned while the storage is full it is converted to this many seconds of production.</summary>
        public double OverflowOpsSeconds = 120;
        public List<CrateEntryDef> Entries = new List<CrateEntryDef>();
    }

    /// <summary>"Frenesia": tapping fast fills a meter; when full, production (and taps) get a short boost.</summary>
    [Serializable]
    public class FrenzyConfig
    {
        public bool Enabled = true;
        public double TapsToFill = 30;
        /// <summary>Charge lost per second, so only sustained tapping fills the meter.</summary>
        public double DecayPerSecond = 1.5;
        public double Multiplier = 2;
        public double Seconds = 12;
    }

    public enum DailyRewardKind { Currency, Crate, Parts }

    [Serializable]
    public class DailyRewardDef
    {
        public DailyRewardKind Kind = DailyRewardKind.Currency;
        public double OpsSeconds = 0;     // Currency: seconds of base production
        public double MinCurrency = 50;
        public int Amount = 0;            // Crate / Parts count
    }

    /// <summary>
    /// Daily gift calendar. One gift per (UTC) day; the calendar ADVANCES each day you play and never resets
    /// if you skip days (no pressure/punishment for missing a day).
    /// </summary>
    [Serializable]
    public class DailyConfig
    {
        public bool Enabled = true;
        /// <summary>Optional rewarded ad gives the same gift again (+100%), declared before watching.</summary>
        public double AdBonusMultiplier = 1.0;
        public List<DailyRewardDef> Days = new List<DailyRewardDef>();
    }

    /// <summary>
    /// Invites without a server: every player has a share code. A friend who opens an invite link gets a one-time welcome
    /// gift on their device; the inviter gets a small reward for sharing (once per day). Rewarding the inviter when the
    /// friend actually plays needs a server to verify it and is NOT implemented.
    /// </summary>
    [Serializable]
    public class SocialConfig
    {
        public bool Enabled = true;
        public int WelcomeCrates = 1;
        public double WelcomeOpsSeconds = 300;
        public double WelcomeMin = 500;
        public int ShareCrates = 1;
    }

    [Serializable]
    public class AdsConfig
    {
        public bool Enabled = true;
        public double MinSecondsBetweenAds = 60;
        public int MaxPerDay = 12;
        /// <summary>"Watch a video for a free crate": at most this many per day (inside MaxPerDay).</summary>
        public int CrateAdsPerDay = 3;
        /// <summary>"Double the order reward with a video": currency multiplier.</summary>
        public double OrderAdMultiplier = 2.0;
        public double ShowTimeoutSeconds = 120;
        public double BoostMultiplier = 2.0;
        public double BoostSeconds = 300;
        /// <summary>Safety valve: ad boost can only be requested if its remaining time is below this.</summary>
        public double BoostRequestWhenRemainingBelow = 30;
    }
}
