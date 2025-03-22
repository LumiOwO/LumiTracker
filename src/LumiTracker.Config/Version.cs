using System.Diagnostics;

namespace LumiTracker.Config
{
    public readonly struct AppVersion : IComparable<AppVersion>
    {
        private readonly uint Data { get; } = 0;

        private static readonly uint MajorMask = 0b00000111;
        public readonly int Major  // 3 bits
        {
            get
            {
                return (int)((Data >> 29) & MajorMask);
            }
        }

        private static readonly uint MinorMask = 0b00011111;
        public readonly int Minor  // 5 bits
        {
            get
            {
                return (int)((Data >> 24) & MinorMask);
            }
        }

        private static readonly uint BuildMask = 0xff;
        public readonly int Build  // 8 bits
        {
            get
            {
                return (int)((Data >> 16) & BuildMask);
            }
        }

        private static readonly uint BetaMask = 0xff;
        public readonly int Beta  // 8 bits 
        {
            get
            {
                // Non-beta should be the maximum, so mapping 0 -> 0xff
                return (int)( ((Data >> 8) + 1) & BetaMask );
            }
        }
        // 0 = non-Beta
        public readonly bool IsBeta => Beta > 0;

        private static readonly uint PatchMask = 0xff;
        public readonly int Patch  // 8 bits 
        {
            get
            {
                return (int)(Data & PatchMask);
            }
        }
        public readonly bool HasPatch => Patch > 0;

        public AppVersion(int major = 0, int minor = 0, int build = 0, int beta = 0, int patch = 0)
        {
            Debug.Assert(major >= 0 && major <= MajorMask);
            Debug.Assert(minor >= 0 && minor <= MinorMask);
            Debug.Assert(build >= 0 && build <= BuildMask);
            Debug.Assert(beta  >= 0 && beta  <= BetaMask );
            Debug.Assert(patch >= 0 && patch <= PatchMask);

            Data = ( ((uint)major    ) & MajorMask ) << 29
                |  ( ((uint)minor    ) & MinorMask ) << 24
                |  ( ((uint)build    ) & BuildMask ) << 16
                |  ( ((uint)beta - 1 ) & BetaMask  ) << 8
                |  ( ((uint)patch    ) & PatchMask )
                ;
        }

        public static bool TryParse(string? versionString, out AppVersion result, int? patchOverride = null)
        {
            result = default;
            if (versionString == null)
            {
                return false;
            }
            try
            {
                result = Parse(versionString, patchOverride);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static AppVersion Parse(string versionText, int? patchOverride = null)
        {
            versionText = versionText.Trim();
            if (versionText.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                versionText = versionText[1..];
            }

            string[] segments = versionText.Split(['-'], StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0) throw new FormatException("Empty version string");

            // Parse core version (Major.Minor.Build)
            string[] coreSegments = segments[0].Split('.');
            if (coreSegments.Length < 1 || coreSegments.Length > 3)
                throw new FormatException("Invalid core version format");

            int major = ParseComponent(coreSegments[0], "Major");
            int minor = coreSegments.Length >= 2 ? ParseComponent(coreSegments[1], "Minor") : 0;
            int build = coreSegments.Length >= 3 ? ParseComponent(coreSegments[2], "Build") : 0;

            // Initialize defaults
            int beta = 0;
            int patch = 0;

            // Process suffixes
            bool hasBeta = false;
            bool hasPatch = false;
            foreach (string segment in segments.Skip(1))
            {
                if (segment.StartsWith("Beta", StringComparison.OrdinalIgnoreCase))
                {
                    if (hasBeta) throw new FormatException("Multiple Beta components");
                    beta = ParseComponent(segment[4..], "Beta");
                    hasBeta = true;
                }
                else if (segment.StartsWith("Patch", StringComparison.OrdinalIgnoreCase))
                {
                    if (hasPatch) throw new FormatException("Multiple Patch components");
                    patch = ParseComponent(segment[5..], "Patch");
                    hasPatch = true;
                }
                else
                {
                    throw new FormatException($"Invalid component: {segment}");
                }
            }

            // Apply patch override if specified
            patch = patchOverride ?? patch;

            return new AppVersion(major, minor, build, beta, patch);
        }

        private static int ParseComponent(string value, string name)
        {
            if (!int.TryParse(value, out int result) || result < 0)
                throw new FormatException($"Invalid {name} value: {value}");
            return result;
        }

        public int CompareTo(AppVersion other) => Data.CompareTo(other.Data);
        public static bool operator ==(AppVersion lhs, AppVersion rhs) => lhs.Data == rhs.Data;
        public static bool operator < (AppVersion lhs, AppVersion rhs) => lhs.Data <  rhs.Data;
        public static bool operator <=(AppVersion lhs, AppVersion rhs) => lhs.Data <= rhs.Data;
        public static bool operator !=(AppVersion lhs, AppVersion rhs) => !(lhs == rhs);
        public static bool operator > (AppVersion lhs, AppVersion rhs) => rhs < lhs;
        public static bool operator >=(AppVersion lhs, AppVersion rhs) => rhs <= lhs;

        public override bool Equals(object? obj) => obj is AppVersion other && this == other;

        public override int GetHashCode() => Data.GetHashCode();

        private string ToVersionString(bool showPatch)
        {
            string BetaStr  = IsBeta ? $"-Beta{Beta}" : "";
            string PatchStr = showPatch ? $"-Patch{Patch}" : "";
            return $"{Major}.{Minor}.{Build}{BetaStr}{PatchStr}";
        }

        public string InfoName => ToVersionString(false);

        public string FullName => ToVersionString(true);

        public override string ToString()
        {
            return FullName;
        }
    }
}
