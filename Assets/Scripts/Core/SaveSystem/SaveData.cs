using System;
using System.Collections.Generic;

namespace OfficinaTycoon
{
    // Persisted schema. Rules for evolving it:
    //  1. Adding a field with a default needs NO migration (JsonMapper keeps field initializers).
    //  2. Renaming/removing/re-interpreting a field needs a SaveMigrator step and a SaveData.CurrentVersion bump.
    // Only Lists are used (no Dictionaries) to keep the mapper trivial.

    [Serializable]
    public class SaveData
    {
        public const int CurrentVersion = 1;

        public int Version = CurrentVersion;
        public long SavedAtTicks = 0;
        public ulong RngState = 0;
        public WalletSave Wallet = new WalletSave();
        public ProductionSave Production = new ProductionSave();
        public int PhaseIndex = 0;
        public OrdersSave Orders = new OrdersSave();
        public ExperimentSave Experiment = new ExperimentSave();
        public SurgeSave Surge = new SurgeSave();
        public CrateSave Crate = new CrateSave();
        public OfflineSave Offline = new OfflineSave();
        public AdSave Ads = new AdSave();
        public DailySave Daily = new DailySave();
        public List<string> Claimed = new List<string>();
        public SettingsSave Settings = new SettingsSave();
        public StatsSave Stats = new StatsSave();
    }

    [Serializable] public class WalletSave { public double Balance = 0; public double TotalEarned = 0; public double TotalSpent = 0; }

    [Serializable] public class MachineSave { public string Id = ""; public int Level = 0; public int Overhaul = 0; public double Produced = 0; }
    [Serializable] public class BoostSave { public string Source = ""; public double Multiplier = 1; public double Remaining = 0; }

    [Serializable]
    public class ProductionSave
    {
        public List<MachineSave> Machines = new List<MachineSave>();
        public List<string> Upgrades = new List<string>();
        public List<BoostSave> Boosts = new List<BoostSave>();
        public int Parts = 0;
        public long TapCount = 0;
    }

    public enum OrderStatus { Active, Completed, Claimed }

    [Serializable]
    public class OrderStateSave
    {
        public string Id = "";
        public OrderStatus Status = OrderStatus.Active;
        public double Progress = 0;
        public double Baseline = 0;
        public double Remaining = 0;
    }

    [Serializable]
    public class OrdersSave
    {
        public List<OrderStateSave> States = new List<OrderStateSave>();
        public bool TutorialCompleted = false;
        public int EventsCompleted = 0;
    }

    public enum EventStage { Cooldown, Offered, InProgress, ReadyToCollect }
    public enum ExperimentChoice { None, Prudent, Experimental }

    [Serializable]
    public class ExperimentSave
    {
        public EventStage Stage = EventStage.Cooldown;
        public double Timer = -1;            // <0 = "not initialised": first-offer delay is applied on first run
        public int Sequence = 0;
        public ExperimentChoice Choice = ExperimentChoice.None;
        public int OutcomeIndex = -1;
        public double OfferBaseOps = 0;      // base production snapshot used to price the offer
        public double CostPrudent = 0;
        public double CostExperimental = 0;
        public double PaidCost = 0;
    }

    [Serializable]
    public class SurgeSave
    {
        public EventStage Stage = EventStage.Cooldown;   // only Cooldown/Offered are used
        public double Timer = -1;
        public int Sequence = 0;
    }

    [Serializable] public class CrateSave { public int Available = 0; public int OpenCounter = 0; public int TotalOpened = 0; }

    [Serializable]
    public class OfflineSave
    {
        public long LastValidTicks = 0;
        public int OfferSeq = 0;
        public bool HasOffer = false;
        public int OfferId = 0;
        public double Seconds = 0;
        public double Base = 0;
        public double Bonus = 0;
        public bool BonusGranted = false;
    }

    [Serializable]
    public class AdSave
    {
        public string DayKey = "";
        public int CountToday = 0;
        public long LastAdTicks = 0;
        public int BoostSeq = 0;
    }

    [Serializable]
    public class DailySave
    {
        public int LastDay = -1;         // day number (UTC) of the last claim; only ever increases
        public int Index = 0;            // position in the 7-day calendar
        public int Claims = 0;
        public bool BonusGranted = false; // ad bonus earned for the pending gift (Claims+1)
    }

    [Serializable]
    public class SettingsSave
    {
        public bool SfxOn = true;
        public bool MusicOn = true;
        public string Language = "it";
        public bool ConsentAsked = false;
        public bool PersonalizedAds = false;   // default OFF until the player decides
        public bool AdsOptOut = false;         // player chose "no ads": no ad is ever requested
        public bool PrivacyAccepted = false;
        public bool DevShowAnalytics = false;
        public bool ReducedEffects = false;    // fewer particles/animations (also honoured: OS 'reduce motion')
    }

    [Serializable]
    public class StatsSave
    {
        public int Sessions = 0;
        public long InstallTicks = 0;
        public double SecondsPlayed = 0;
        public int RiskShown = 0;
        public int RiskDeclined = 0;
    }
}
