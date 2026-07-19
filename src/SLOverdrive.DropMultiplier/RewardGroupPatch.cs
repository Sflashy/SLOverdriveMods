using System;
using BepInEx.Logging;
using SLOverdrive.Core.Il2Cpp;

namespace SLOverdrive.DropMultiplier
{
    /// <summary>
    /// Controls how many items a reward group hands out, and how often it fires.
    ///
    /// This is the layer between the drop slot and the item list:
    ///
    /// <code>
    /// ContentsDrop row  ──▶  RewardGroup row  ──▶  Reward rows (the items)
    ///                          mode  = Random | Rate | Fix
    ///                          rate  = 4000            ← how often it fires
    ///                          count = 1               ← how many items it gives
    /// </code>
    ///
    /// The count is <c>1</c> in every one of the game's 10,482 group rows, which is why a
    /// drop only ever yields a single item no matter how much the weights are tuned.
    /// Raising it is the only way to get several items out of one group.
    /// </summary>
    internal static class RewardGroupPatch
    {
        private static DropConfig _config;
        private static ManualLogSource _log;

        private static int _countChanged;
        private static int _rateChanged;
        private static bool _reported;

        public static void Initialize(DropConfig config, ManualLogSource log)
        {
            _config = config;
            _log = log;
        }

        /// <summary>Harmony postfix on the RewardGroup row loader, once per row at startup.</summary>
        public static void Postfix(object __instance)
        {
            if (_config == null || __instance == null) return;

            try
            {
                if (_config.DryRun) return;

                ApplyPickCount(__instance);
                ApplyGroupRate(__instance);
            }
            catch (Exception ex)
            {
                _log.LogError("Error while adjusting reward group: " + ex);
            }
        }

        /// <summary>
        /// Raises how many entries the group hands out.
        ///
        /// Asking for more items than the group holds is harmless - the game simply runs
        /// out of entries - but a very large number across every group produces an
        /// enormous drop packet, so the setting is capped.
        /// </summary>
        private static void ApplyPickCount(object row)
        {
            if (_config.PickCount <= 1) return;

            int current = Il2CppReflect.GetInt(row, _config.PickCountField, -1);
            if (current <= 0 || current >= _config.PickCount) return;

            if (Il2CppReflect.SetMember(row, _config.PickCountField, _config.PickCount))
                _countChanged++;
        }

        /// <summary>
        /// Raises the fire rate of groups that roll for it.
        ///
        /// Only <c>Random</c> mode groups carry a meaningful rate; <c>Rate</c> and
        /// <c>Fix</c> groups store zero and are left alone.
        /// </summary>
        private static void ApplyGroupRate(object row)
        {
            if (_config.GroupRateMultiplier <= 1f) return;

            int rate = Il2CppReflect.GetInt(row, _config.GroupRateField, -1);
            if (rate <= 0) return;

            int scaled = (int)Math.Round(rate * _config.GroupRateMultiplier, MidpointRounding.AwayFromZero);
            if (_config.MaxGroupRate > 0 && scaled > _config.MaxGroupRate)
                scaled = _config.MaxGroupRate;

            if (scaled == rate) return;

            if (Il2CppReflect.SetMember(row, _config.GroupRateField, scaled))
                _rateChanged++;
        }

        public static void ReportSummary()
        {
            if (_reported || (_countChanged == 0 && _rateChanged == 0)) return;
            _reported = true;

            _log.LogInfo($"Reward groups: {_countChanged} pick count(s), {_rateChanged} rate(s) raised.");
        }
    }
}
