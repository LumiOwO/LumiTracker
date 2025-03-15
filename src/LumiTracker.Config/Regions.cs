using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Logging;


namespace LumiTracker.Config
{
    public static class RegionUtils
    {
        private static readonly Dictionary<ERatioType, Dictionary<ERegionType, JToken>> _Regions;
        public static bool Loaded { get; set; } = false;

        static RegionUtils()
        {
            _Regions = new Dictionary<ERatioType, Dictionary<ERegionType, JToken>>();
            var json = Configuration.LoadJObject(Path.Combine(Configuration.AssetsDir, "regions.json"));
            if (json == null)
            {
                Configuration.Logger.LogError("[RegionUtils] Failed to load regions.json");
                return;
            }

            try
            {
                foreach (var ratioEntry in json)
                {
                    var ratioType = (ERatioType)Enum.Parse(typeof(ERatioType), ratioEntry.Key);
                    var innerDict = new Dictionary<ERegionType, JToken>();

                    foreach (var regionEntry in (JObject)ratioEntry.Value!)
                    {
                        var regionType = (ERegionType)Enum.Parse(typeof(ERegionType), regionEntry.Key);
                        innerDict[regionType] = regionEntry.Value!;
                    }

                    _Regions[ratioType] = innerDict;
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

        public static List<double> Get(ERatioType ratioType, ERegionType regionType)
        {
            JToken? token = null;
            if (!_Regions.TryGetValue(ratioType, out var regions))
            {
                Configuration.Logger.LogDebug(
                    $"Ratio = {ratioType.ToString()}, Region = {regionType.ToString()} is not found in regions data, falling back to 16:9.");
                token = _Regions[ERatioType.E16_9][regionType];
            }
            else if (regions.TryGetValue(regionType, out var value))
            {
                token = value;
            }
            else
            {
                Configuration.Logger.LogDebug(
                    $"Region = {regionType.ToString()} is not defined when Ratio = {ratioType.ToString()}, falling back to 16:9 values");
                token = _Regions[ERatioType.E16_9][regionType];
            }

            return token.ToObject<List<double>>()!;
        }
    }
}



