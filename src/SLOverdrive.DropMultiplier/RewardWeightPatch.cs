using System;
using BepInEx.Logging;
using SLOverdrive.Core.Il2Cpp;

namespace SLOverdrive.DropMultiplier
{
    /// <summary>
    /// Raises the weight of chosen item types inside the game's reward groups.
    ///
    /// This is the second layer of randomness. Once a drop slot fires, the game picks
    /// one entry from a reward group by weight:
    ///
    /// <code>
    /// group 402010160
    ///   weight 10000 (33%)  Material
    ///   weight 10000 (33%)  Artifact     ← equipment lives here
    ///   weight 10000 (33%)  PlayerExp
    /// </code>
    ///
    /// Most groups are far less even than that. Across the table, 440 artifact entries
    /// sit below 1% of their group's total weight, which is why equipment feels rare
    /// even when the drop slot itself is guaranteed.
    ///
    /// <see cref="RewardGroupPatch"/> controls whether a group fires and how many
    /// entries it gives; this controls which entries win.
    /// </summary>
    internal static class RewardWeightPatch
    {
        private static DropConfig _config;
        private static ManualLogSource _log;

        private static int _rowsChanged;
        private static bool _reported;

        public static void Initialize(DropConfig config, ManualLogSource log)
        {
            _config = config;
            _log = log;
        }

        /// <summary>
        /// Harmony postfix on the Reward row loader, one call per row at startup.
        /// </summary>
        public static void Postfix(object __instance)
        {
            if (_config == null || __instance == null) return;

            try
            {
                string type = Il2CppReflect.GetMember(__instance, _config.RewardTypeField)?.ToString();
                if (string.IsNullOrEmpty(type)) return;

                if (!IsBoostedType(type)) return;

                int weight = Il2CppReflect.GetInt(__instance, _config.RewardWeightField, -1);

                // A weight of zero means the entry is deliberately switched off.
                // Multiplying it changes nothing, and forcing it on would resurrect
                // entries the developers excluded, so leave those alone.
                if (weight <= 0) return;

                int scaled = (int)Math.Round(weight * _config.RewardWeightMultiplier, MidpointRounding.AwayFromZero);
                if (scaled < 1) scaled = 1;

                if (_config.MaxRewardWeight > 0 && scaled > _config.MaxRewardWeight)
                    scaled = _config.MaxRewardWeight;

                if (scaled == weight) return;

                if (_config.DryRun) return;

                if (Il2CppReflect.SetMember(__instance, _config.RewardWeightField, scaled))
                    _rowsChanged++;
            }
            catch (Exception ex)
            {
                _log.LogError("Error while adjusting reward weight: " + ex);
            }
        }

        /// <summary>Comma separated list from the config, or "*" for every type.</summary>
        private static bool IsBoostedType(string type)
        {
            string filter = _config.RewardTypeFilter;
            if (string.IsNullOrWhiteSpace(filter) || filter == "*") return true;

            foreach (var wanted in filter.Split(','))
                if (string.Equals(wanted.Trim(), type, StringComparison.OrdinalIgnoreCase))
                    return true;

            return false;
        }

        /// <summary>
        /// One line for the whole table, for the same reason as the drop table patch:
        /// 28,000 rows of detail would drown out the drop list.
        /// </summary>
        public static void ReportSummary()
        {
            if (_rowsChanged == 0 || _reported) return;
            _reported = true;

            _log.LogInfo($"Reward table: {_rowsChanged} weight(s) raised.");
        }
    }
}
