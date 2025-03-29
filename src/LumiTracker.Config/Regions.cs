using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace LumiTracker.Config
{
    public struct Vec4f
    {
        public float x { get; set; } = 0;
        public float y { get; set; } = 0;
        public float z { get; set; } = 0;
        public float w { get; set; } = 0;

        public float r { get => x; set => x = value; }
        public float g { get => y; set => y = value; }
        public float b { get => z; set => z = value; }
        public float a { get => w; set => w = value; }

        public Vec4f()
        {
            x = 0;
            y = 0;
            z = 0;
            w = 0;
        }

        public Vec4f(float x, float y, float z, float w)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.w = w;
        }

        public Vec4f(float[]? values)
        {
            values ??= [];
            x = values.Length > 0 ? values[0] : 0;
            y = values.Length > 1 ? values[1] : 0;
            z = values.Length > 2 ? values[2] : 0;
            w = values.Length > 3 ? values[3] : 0;
        }

        public float this[int index]
        {
            get
            {
                return index switch
                {
                    0 => x,
                    1 => y,
                    2 => z,
                    3 => w,
                    _ => throw new IndexOutOfRangeException($"Index {index} out of range [0-3]"),
                };
            }
            set
            {
                switch (index)
                {
                    case 0: x = value; break;
                    case 1: y = value; break;
                    case 2: z = value; break;
                    case 3: w = value; break;
                    default: throw new IndexOutOfRangeException($"Index {index} out of range [0-3]");
                }
            }
        }
    }

    public static class RegionUtils
    {
        private static readonly Vec4f[,] _Regions;
        public static bool Loaded { get; set; } = false;

        static RegionUtils()
        {
            _Regions = new Vec4f[
                (int)ERatioType.NumRatioTypes,
                (int)ERegionType.NumRegionTypes
            ];

            var json = Configuration.LoadJObject(Path.Combine(Configuration.AssetsDir, "regions.json"));
            if (json == null)
            {
                Configuration.Logger.LogError("[RegionUtils] Failed to load regions.json");
                return;
            }

            try
            {
                for (ERatioType ratio = 0; ratio < ERatioType.NumRatioTypes; ratio++)
                {
                    if (!json.TryGetValue(ratio.ToString(), out JToken? innerToken)) continue;
                    JObject? innerJson = innerToken as JObject;
                    if (innerJson == null) continue;

                    for (ERegionType region = 0; region < ERegionType.NumRegionTypes; region++)
                    {
                        if (!innerJson.TryGetValue(region.ToString(), out JToken? valuesToken)) continue;

                        float[] values = valuesToken.ToObject<float[]>() ?? [];
                        _Regions[(int)ratio, (int)region] = new Vec4f(values);
                    }
                }

                Loaded = true;
            }
            catch (Exception ex)
            {
                Configuration.Logger.LogError($"[RegionUtils] Failed to parse regions.json: {ex.Message}");
            }
        }

        public static ERatioType GetRatioType(int clientWidth, int clientHeight)
        {
            const double EPSILON = 0.005;
            double ratio = (double)clientWidth / clientHeight;

            if (Math.Abs(ratio - 16.0 / 9) < EPSILON)
                return ERatioType.E16_9;
            else if (Math.Abs(ratio - 16.0 / 10) < EPSILON)
                return ERatioType.E16_10;
            else if (Math.Abs(ratio - 64.0 / 27) < EPSILON)
                return ERatioType.E64_27;
            else if (Math.Abs(ratio - 43.0 / 18) < EPSILON)
                return ERatioType.E43_18;
            else if (Math.Abs(ratio - 12.0 / 5) < EPSILON)
                return ERatioType.E12_5;

            Configuration.Logger.LogDebug($"[GetRatioType] Current client rect's ratio is unsupported: {clientWidth} / {clientHeight} = {ratio}");
            return ERatioType.E16_9; // Default
        }

        public static Vec4f Get(ERatioType ratioType, ERegionType regionType)
        {
            Debug.Assert(ratioType < ERatioType.NumRatioTypes && regionType < ERegionType.NumRegionTypes);
            return _Regions[(int)ratioType, (int)regionType];
        }
    }
}



