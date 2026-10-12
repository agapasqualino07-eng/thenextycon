using System;
using System.Collections.Generic;
using UnityEngine;

namespace OfficinaTycoon.UnityLayer
{
    /// <summary>
    /// Procedural PLACEHOLDER art so the prototype has recognisable shapes without imported assets.
    /// Real illustrated assets must replace these (see Docs/ASSETS_TO_REPLACE.md).
    /// </summary>
    public static class SpriteFactory
    {
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        /// <summary>White rounded-rectangle 9-sliceable sprite, tintable via Image.color.</summary>
        public static Sprite RoundedRect()
        {
            return Cached("rounded", () =>
            {
                const int size = 64, radius = 20;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var px = new Color32[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float cx = Mathf.Clamp(x, radius, size - 1 - radius);
                        float cy = Mathf.Clamp(y, radius, size - 1 - radius);
                        float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                        float a = Mathf.Clamp01(radius - d + 0.5f);
                        px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                    }
                tex.SetPixels32(px); tex.Apply();
                tex.filterMode = FilterMode.Bilinear;
                return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
            });
        }

        /// <summary>White gear silhouette with a hole, tintable. Different tooth counts give each machine a distinct shape.</summary>
        public static Sprite Gear(int teeth)
        {
            teeth = Mathf.Clamp(teeth, 5, 16);
            return Cached("gear" + teeth, () =>
            {
                const int size = 128;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var px = new Color32[size * size];
                float c = (size - 1) / 2f;
                float rOuter = size * 0.48f, rBody = size * 0.38f, rHole = size * 0.14f;
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float dx = x - c, dy = y - c;
                        float r = Mathf.Sqrt(dx * dx + dy * dy);
                        float ang = Mathf.Atan2(dy, dx);
                        // tooth profile: trapezoid wave along the angle
                        float t = Mathf.Repeat(ang / (2 * Mathf.PI) * teeth, 1f);
                        float tooth = t < 0.5f ? Mathf.Clamp01((0.5f - Mathf.Abs(t - 0.25f)) * 6f - 0.5f) : 0f;
                        float edge = Mathf.Lerp(rBody, rOuter, tooth);
                        float a = Mathf.Clamp01(edge - r + 0.5f) * Mathf.Clamp01(r - rHole + 0.5f);
                        px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                    }
                tex.SetPixels32(px); tex.Apply();
                tex.filterMode = FilterMode.Bilinear;
                return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100);
            });
        }

        static Sprite Cached(string key, Func<Sprite> make)
        {
            if (Cache.TryGetValue(key, out var s) && s != null) return s;
            s = make();
            Cache[key] = s;
            return s;
        }
    }
}
