#if UNITY_EDITOR
using System;
using System.Text;

namespace Voidborne.Editor.Data
{
    /// <summary>
    /// Editor-only string-parsing helpers shared by the Volume 2 SO generators
    /// (MachineSoGenerator, FaunaSoGenerator, EnemySoGenerator, NpcSoGenerator,
    /// RecipeSoGenerator). All methods are null-safe and case-insensitive
    /// unless otherwise noted.
    /// </summary>
    /// <remarks>
    /// Centralising these helpers avoids the V2.3-era duplication where each
    /// generator carried its own private <c>ContainsMarker</c>. Future
    /// generators should call into this class.
    ///
    /// Coop note: pure functions, no Unity references, no runtime state.
    /// </remarks>
    public static class ParsingHelpers
    {
        // ------------------------------------------------------------------
        // Tier parsing
        // ------------------------------------------------------------------

        /// <summary>
        /// Extracts the leading tier marker (T1..T7) from a role-like string
        /// (e.g. <c>"T3 — proper smelting"</c> -> 3). Returns 0 when no tier
        /// marker is present.
        /// </summary>
        /// <remarks>
        /// The role string in items.json conventionally leads with a
        /// <c>T#</c> prefix followed by an em-dash. The scan is tolerant of
        /// any whitespace / casing variant — it walks the string looking for
        /// the FIRST <c>T</c> or <c>t</c> followed by a single digit 1-7
        /// that is NOT preceded by an alphanumeric character (so "T3" matches
        /// but "BT3" does not).
        /// </remarks>
        public static int ParseTier(string role)
        {
            if (string.IsNullOrEmpty(role)) return 0;

            for (int i = 0; i < role.Length - 1; i++)
            {
                char c = role[i];
                if (c != 'T' && c != 't') continue;

                // Not preceded by an alphanumeric (so we don't latch onto
                // tokens like "BT3", "ART2"). The boundary at index 0 is
                // implicitly clean.
                if (i > 0 && char.IsLetterOrDigit(role[i - 1])) continue;

                char d = role[i + 1];
                if (d >= '1' && d <= '7')
                {
                    // Following character must not be a digit (avoid latching
                    // onto "T15" or "T10") — Tier marker is single-digit.
                    if (i + 2 < role.Length && char.IsDigit(role[i + 2])) continue;
                    return d - '0';
                }
            }

            return 0;
        }

        // ------------------------------------------------------------------
        // Marker search
        // ------------------------------------------------------------------

        /// <summary>
        /// Case-insensitive substring check. Null/empty-safe — returns
        /// <c>false</c> if either argument is null or empty.
        /// </summary>
        public static bool ContainsMarker(string text, string marker)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(marker)) return false;
            return text.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // ------------------------------------------------------------------
        // Slugify
        // ------------------------------------------------------------------

        /// <summary>
        /// Converts a free-form display name into a snake_case slug suitable
        /// for use as an asset ID. <c>"Mist Hunter"</c> -> <c>"mist_hunter"</c>.
        /// Strips diacritics and characters outside [a-z0-9_]; collapses
        /// runs of separators; trims leading/trailing underscores.
        /// </summary>
        public static string SlugifyName(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;

            var sb = new StringBuilder(name.Length);
            bool lastWasSeparator = true; // suppress leading underscore

            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];

                if (c >= 'A' && c <= 'Z')
                {
                    sb.Append((char)(c + 32));
                    lastWasSeparator = false;
                }
                else if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
                {
                    sb.Append(c);
                    lastWasSeparator = false;
                }
                else
                {
                    // Any non-alphanumeric character becomes a single
                    // underscore (collapsing runs).
                    if (!lastWasSeparator)
                    {
                        sb.Append('_');
                        lastWasSeparator = true;
                    }
                }
            }

            // Trim trailing underscore.
            int len = sb.Length;
            if (len > 0 && sb[len - 1] == '_') sb.Length = len - 1;

            return sb.ToString();
        }

        // ------------------------------------------------------------------
        // Behavior classification
        // ------------------------------------------------------------------

        private static readonly string[] AggressiveMarkers =
        {
            "predator", "aggressive", "hunter", "stalker", "ambush"
        };

        private static readonly string[] PassiveMarkers =
        {
            "docile", "passive", "graze", "flee"
        };

        private static readonly string[] TameableMarkers =
        {
            "tame", "domesticated", "companion"
        };

        /// <summary>
        /// True if any entry in <paramref name="behaviors"/> contains an
        /// aggression marker (predator / aggressive / hunter / stalker / ambush).
        /// Null-safe.
        /// </summary>
        public static bool IsAggressive(string[] behaviors)
        {
            return AnyContainsAny(behaviors, AggressiveMarkers);
        }

        /// <summary>
        /// True if any entry in <paramref name="behaviors"/> contains a
        /// passive-behavior marker (docile / passive / graze / flee).
        /// Null-safe.
        /// </summary>
        public static bool IsPassive(string[] behaviors)
        {
            return AnyContainsAny(behaviors, PassiveMarkers);
        }

        /// <summary>
        /// True if any of the supplied texts (behaviors, description, or the
        /// raw name in a <c>(tame)</c> suffix) signals the creature can be
        /// tamed. Null-safe.
        /// </summary>
        public static bool IsTameable(string[] behaviors, string desc)
        {
            return IsTameable(behaviors, desc, null);
        }

        /// <summary>
        /// Overload that also inspects the creature's display name for a
        /// <c>(tame)</c> annotation. Null-safe.
        /// </summary>
        public static bool IsTameable(string[] behaviors, string desc, string name)
        {
            if (AnyContainsAny(behaviors, TameableMarkers)) return true;
            for (int i = 0; i < TameableMarkers.Length; i++)
            {
                if (ContainsMarker(desc, TameableMarkers[i])) return true;
            }
            if (!string.IsNullOrEmpty(name) &&
                name.IndexOf("(tame)", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------
        // Internals
        // ------------------------------------------------------------------

        private static bool AnyContainsAny(string[] haystack, string[] needles)
        {
            if (haystack == null || haystack.Length == 0) return false;
            for (int i = 0; i < haystack.Length; i++)
            {
                string entry = haystack[i];
                if (string.IsNullOrEmpty(entry)) continue;
                for (int j = 0; j < needles.Length; j++)
                {
                    if (entry.IndexOf(needles[j], StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }
            }
            return false;
        }
    }
}
#endif
