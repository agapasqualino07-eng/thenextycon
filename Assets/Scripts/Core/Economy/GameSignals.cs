using System;

namespace OfficinaTycoon
{
    /// <summary>
    /// Lightweight event hub: systems publish facts, others (orders, analytics, UI) subscribe.
    /// Avoids systems referencing each other directly. Plain C# events, no engine types.
    /// </summary>
    public sealed class GameSignals
    {
        /// <summary>Currency earned by production or taps (NOT rewards). Args: amount, machineId ("" for taps).</summary>
        public event Action<double, string> Earned;
        public event Action Tapped;
        public event Action<string, int> MachineLevelChanged;     // id, new level
        public event Action<string> UpgradePurchased;
        public event Action<string> RiskEventCompleted;           // "experimental" | "surge"
        public event Action<string> CrateOpened;
        public event Action<string> OrderClaimed;
        public event Action<int> PhaseChanged;
        public event Action<string> Toast;                         // short player-facing messages (loc keys)

        public void RaiseEarned(double amount, string machineId) => Earned?.Invoke(amount, machineId);
        public void RaiseTapped() => Tapped?.Invoke();
        public void RaiseMachineLevel(string id, int level) => MachineLevelChanged?.Invoke(id, level);
        public void RaiseUpgrade(string id) => UpgradePurchased?.Invoke(id);
        public void RaiseRiskEventCompleted(string kind) => RiskEventCompleted?.Invoke(kind);
        public void RaiseCrateOpened(string entryId) => CrateOpened?.Invoke(entryId);
        public void RaiseOrderClaimed(string id) => OrderClaimed?.Invoke(id);
        public void RaisePhase(int phase) => PhaseChanged?.Invoke(phase);
        public void RaiseToast(string key) => Toast?.Invoke(key);
    }
}
