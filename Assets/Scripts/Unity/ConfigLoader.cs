using System;
using UnityEngine;

namespace OfficinaTycoon.UnityLayer
{
    /// <summary>
    /// Loads the game configuration. The code defaults (DefaultConfig) are used unless a TextAsset named
    /// "game_config" exists in a Resources folder. Overrides are validated; an invalid file is ignored with an error log
    /// so a bad edit can never ship a broken economy silently.
    /// Export the current defaults with the menu: Officina > Export Default Config JSON.
    /// </summary>
    public static class ConfigLoader
    {
        public static GameConfig Load()
        {
            var asset = Resources.Load<TextAsset>("game_config");
            if (asset != null)
            {
                try
                {
                    var cfg = JsonMapper.FromJson<GameConfig>(asset.text);
                    var errors = ConfigValidator.Validate(cfg);
                    if (errors.Count == 0) return cfg;
                    Debug.LogError("game_config.json is invalid, using built-in defaults:\n - " + string.Join("\n - ", errors));
                }
                catch (Exception e)
                {
                    Debug.LogError("game_config.json could not be parsed, using built-in defaults: " + e.Message);
                }
            }
            return DefaultConfig.Create();
        }
    }

    /// <summary>Clock that can be shifted in development builds to test offline rewards without waiting. Offset is 0 in release.</summary>
    public sealed class DevClock : IClock
    {
        public double OffsetSeconds;
        public DateTime UtcNow => DateTime.UtcNow.AddSeconds(OffsetSeconds);
    }
}
