using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace OfficinaTycoon.UnityLayer
{
    /// <summary>
    /// Entry point. Creates the GameSession (pure C# logic), the audio service and the UI, and forwards the Unity
    /// lifecycle (frame update, pause/resume, quit). It contains no gameplay rules.
    /// It lives in the Main scene, and also creates itself if the scene has none (RuntimeInitializeOnLoadMethod),
    /// so pressing Play in any scene yields a working game.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        GameSession _session;
        MainUI _ui;
        AudioService _audio;
        DevClock _clock;
        UnityMockAdService _mockAds;
        DebugAnalytics _analytics;
        FileSaveStorage _storage;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureExists()
        {
            if (Compat.FindFirst<GameBootstrap>() == null) new GameObject("GameBootstrap").AddComponent<GameBootstrap>();
        }

        void Start()
        {
            Application.targetFrameRate = 60;
            Screen.orientation = ScreenOrientation.Portrait;
            EnsureCamera();
            EnsureEventSystem();
            BeginSession();
        }

        static void EnsureCamera()
        {
            if (Camera.main != null) return;
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = UiKit.Bg;
            cam.orthographic = true;
            go.AddComponent<AudioListener>();
        }

        void EnsureEventSystem()
        {
            if (Compat.FindFirst<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            es.transform.SetParent(transform, false);   // lives as long as the bootstrap, independent of UI rebuilds
        }

        void BeginSession()
        {
            _clock = new DevClock();
            _audio = gameObject.GetComponent<AudioService>() ?? gameObject.AddComponent<AudioService>();
            _mockAds = new UnityMockAdService();
            _analytics = new DebugAnalytics();
            if (Debug.isDebugBuild || Application.isEditor) _analytics.Log = msg => Debug.Log(msg);
            _storage = new FileSaveStorage(Application.persistentDataPath);

            var cfg = ConfigLoader.Load();
            IRewardedAdService network = _mockAds;
            bool realAds = false;
#if OFFICINA_ADMOB
            network = new AdMobRewardedAdService();
            realAds = true;
#endif
            // A release build without a real ad SDK must NOT show fake ads: rewarded ads are simply disabled there.
            if (!realAds && !(Debug.isDebugBuild || Application.isEditor)) cfg.Ads.Enabled = false;

            var gated = new ConsentGatedAdService(network, () => _session != null && _session.Settings.ConsentAsked && !_session.Settings.AdsOptOut);
            _session = new GameSession(cfg, _clock, _storage, gated, _analytics);
            _session.Start();

            var uiGo = new GameObject("UI");
            uiGo.transform.SetParent(transform, false);
            _ui = uiGo.AddComponent<MainUI>();
            _ui.Setup(_session, _audio, _mockAds, _clock, ResetProgress);
        }

        void Update()
        {
            if (_session != null) _session.Update(Time.unscaledDeltaTime);
        }

        void OnApplicationPause(bool paused)
        {
            if (_session == null || !_session.Started) return;
            if (paused) _session.Pause();
            else
            {
                _session.Resume();
                if (_ui != null) _ui.OnAppResumed();
            }
        }

        void OnApplicationQuit()
        {
            if (_session != null && _session.Started) _session.End();
        }

        /// <summary>Player-confirmed reset. The old save files are moved aside (not deleted) so nothing is destroyed irrecoverably.</summary>
        void ResetProgress()
        {
            if (_session != null && _session.Started) _session.End();
            _storage.Quarantine(SaveManager.MainName);
            _storage.Quarantine(SaveManager.MainName + ".bak");
            _storage.Quarantine(SaveManager.MainName + ".tmp");
            if (_ui != null) Destroy(_ui.gameObject);
            _session = null;
            BeginSession();
        }
    }
}
