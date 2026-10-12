using Microsoft.JSInterop;

namespace OfficinaTycoon.Web.Services
{
    /// <summary>
    /// localStorage through synchronous in-process JS interop (available only in WebAssembly).
    /// Each setItem is atomic; quota errors are reported to the caller (SaveManager treats a failed write as "save failed").
    /// </summary>
    public sealed class BrowserKeyValueStore : IKeyValueStore
    {
        readonly IJSInProcessRuntime _js;
        public BrowserKeyValueStore(IJSInProcessRuntime js) { _js = js; }
        public string Get(string key) => _js.Invoke<string>("officina.storage.get", key);
        public void Set(string key, string value) => _js.InvokeVoid("officina.storage.set", key, value);
        public void Remove(string key) => _js.InvokeVoid("officina.storage.remove", key);
    }

    /// <summary>Real clock with an offset that only the dev build can change (to test offline earnings without waiting).</summary>
    public sealed class OffsetClock : IClock
    {
        public double OffsetSeconds;
        public DateTime UtcNow => DateTime.UtcNow.AddSeconds(OffsetSeconds);
    }

    /// <summary>
    /// Simulated rewarded ad for the DEV build only: the UI shows a countdown and reports a reward only when it completes.
    /// The public build has ads disabled (there is no ad network on the web version).
    /// </summary>
    public sealed class WebMockAdService : IRewardedAdService
    {
        public MockAdBehavior Behavior = MockAdBehavior.Complete;
        public Action<IRewardedAdCallbacks, MockAdBehavior> Presenter;
        public bool IsReady => Behavior != MockAdBehavior.Unavailable && Behavior != MockAdBehavior.NoNetwork;
        public void Load() { }
        public void Show(string placement, IRewardedAdCallbacks callbacks)
        {
            switch (Behavior)
            {
                case MockAdBehavior.Fail: callbacks.OnFailed(AdFailReason.Error, "mock error"); return;
                case MockAdBehavior.Unavailable: callbacks.OnFailed(AdFailReason.Unavailable, "mock unavailable"); return;
                case MockAdBehavior.NoNetwork: callbacks.OnFailed(AdFailReason.NoNetwork, "mock offline"); return;
                case MockAdBehavior.Hang: return;
            }
            if (Presenter == null) { callbacks.OnFailed(AdFailReason.Error, "no presenter"); return; }
            Presenter(callbacks, Behavior);
        }
    }
}
