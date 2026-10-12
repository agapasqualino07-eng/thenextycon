using System.Diagnostics;
using Microsoft.JSInterop;

namespace OfficinaTycoon.Web.Services
{
    /// <summary>
    /// Owns the GameSession in the browser: the single update loop, page lifecycle (hidden/visible/pagehide),
    /// persistence in localStorage, export/import and reset. Contains no game rules (those live in Core).
    /// </summary>
    public sealed class GameHost : IDisposable
    {
        // The demo build (dev tools, simulated videos) keeps its own save so it can never touch real progress
        // even when served from the same site.
        public static string SavePrefix => DevToolsCompiled ? "officina-demo." : "officina.";

        readonly IJSInProcessRuntime _js;
        DotNetObjectReference<GameHost> _selfRef;
        KeyValueSaveStorage _storage;
        PeriodicTimer _timer;
        readonly Stopwatch _watch = Stopwatch.StartNew();
        long _lastTicks;
        double _uiAccumulator;
        bool _hidden;

        public GameSession Session { get; private set; }
        public OffsetClock Clock { get; } = new OffsetClock();
        public WebMockAdService MockAds { get; } = new WebMockAdService();
        public DebugAnalytics DebugAnalytics { get; private set; }
        public bool DevMode { get; private set; }
        public bool AdsAvailable => Session != null && Session.Config.Ads.Enabled;
        public string Version => typeof(GameHost).Assembly.GetName().Version?.ToString(3) ?? "?";

        /// <summary>Raised ~10 times per second and after lifecycle changes, so the UI can re-render.</summary>
        public event Action Changed;
        /// <summary>Raised when the app becomes visible again (the UI shows the offline popup if any).</summary>
        public event Action Resumed;

        public GameHost(IJSRuntime js) { _js = (IJSInProcessRuntime)js; }

        public static bool DevToolsCompiled
        {
            get
            {
#if OFFICINA_DEV
                return true;
#else
                return false;
#endif
            }
        }

        public void Start(bool devRequested)
        {
            if (Session != null) return;
            // The demo build always runs in demo mode (simulated videos + dev tools); the public build never does.
            DevMode = DevToolsCompiled;
            _storage = new KeyValueSaveStorage(new BrowserKeyValueStore(_js), SavePrefix);
            CreateSession();
            _selfRef = DotNetObjectReference.Create(this);
            _js.InvokeVoid("officina.lifecycle.register", _selfRef);
            _ = RunLoop();
        }

        void CreateSession()
        {
            var cfg = DefaultConfig.Create();
            // The web version has no ad network: rewarded ads exist only as a clearly labelled simulation in the dev build.
            cfg.Ads.Enabled = DevMode;
            DebugAnalytics = DevMode ? new DebugAnalytics() : null;
            IAnalyticsService analytics = DebugAnalytics != null ? DebugAnalytics : new NullAnalytics();
            Session = new GameSession(cfg, Clock, _storage, MockAds, analytics);
            Session.Start();
            _lastTicks = _watch.ElapsedTicks;
        }

        async Task RunLoop()
        {
            _timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));
            while (await _timer.WaitForNextTickAsync())
            {
                long now = _watch.ElapsedTicks;
                double dt = (now - _lastTicks) / (double)Stopwatch.Frequency;
                _lastTicks = now;
                // While hidden the browser may keep throttled timers running: we do NOT produce then,
                // the absence is paid by the offline system on return (no double counting).
                if (!_hidden && Session != null) Session.Update(dt);
                _uiAccumulator += dt;
                if (_uiAccumulator >= 0.1)
                {
                    _uiAccumulator = 0;
                    Changed?.Invoke();
                }
            }
        }

        [JSInvokable]
        public void OnVisibilityChanged(bool hidden)
        {
            if (Session == null || !Session.Started || hidden == _hidden) return;
            _hidden = hidden;
            if (hidden) Session.Pause();
            else
            {
                _lastTicks = _watch.ElapsedTicks;
                Session.Resume();
                Resumed?.Invoke();
            }
            Changed?.Invoke();
        }

        [JSInvokable]
        public void OnPageHide()
        {
            if (Session != null && Session.Started) Session.Pause();
        }

        // ------------------------------------------------------------ backup
        public string ExportSave() => Session.Saves.Encode(Session.Capture());

        /// <summary>Validates untrusted text with the same checks as loading; only a valid save replaces the current one.</summary>
        public bool ImportSave(string text, out string error)
        {
            text = (text ?? "").Trim();
            if (!Session.Saves.TryDecode(text, out _, out error)) return false;
            Session.End();                                   // current progress is saved and becomes the .bak copy
            _storage.Write(SaveManager.MainName, text);
            Session = null;
            CreateSession();
            Changed?.Invoke();
            return true;
        }

        /// <summary>Player-confirmed reset: previous data is moved aside (not deleted) and a new game starts.</summary>
        public void ResetProgress()
        {
            Session.End();
            _storage.Quarantine(SaveManager.MainName);
            _storage.Quarantine(SaveManager.MainName + ".bak");
            Session = null;
            CreateSession();
            Changed?.Invoke();
        }

        public void Dispose()
        {
            _timer?.Dispose();
            _selfRef?.Dispose();
        }
    }
}
