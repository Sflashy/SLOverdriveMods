using System;
using BepInEx.Logging;
using SLOverdrive.Core.Il2Cpp;

namespace SLOverdrive.DropMultiplier
{
    /// <summary>
    /// Multiplies the stack size a reward entry grants.
    ///
    /// This is the real quantity lever. Each Reward row carries the amount it
    /// hands over, as a min/max range the game rolls between:
    ///
    /// <code>
    /// Reward
    ///   RewardCountMin  ← low end of the stack
    ///   RewardCountMax  ← high end
    /// </code>
    ///
    /// The mod's original quantity multiplier edited the drop notification packet
    /// instead - the reward screen - which is built after the game has already
    /// decided what to grant. Measured offline, that changed the number on screen
    /// and added nothing to the bag. This edits the table the roll reads from, the
    /// same place <see cref="RewardGroupPatch"/> works, so the larger amount is
    /// what actually rolls and what actually lands in inventory.
    ///
    /// The Reward table is shared: stage-clear rewards and other reward sources
    /// read from it too, so this raises those as well, not only monster drops.
    ///
    /// Only the reward types in Quantity/Types are touched - materials and
    /// currencies by default. Equipment and costumes are left out, because
    /// multiplying a single unique item makes a hundred copies rather than a
    /// bigger stack; more of those is the RewardGroup PickCount lever's job.
    /// </summary>
    internal static class RewardCountPatch
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
        /// Shares the loader with <see cref="RewardWeightPatch"/>; Harmony runs both.
        /// </summary>
        public static void Postfix(object __instance)
        {
            if (_config == null || __instance == null) return;

            try
            {
                // Filter by reward type, not by stack size. An earlier version
                // scaled only entries whose max was above one, on the theory that
                // those were the stacks - but that conflated two unrelated things.
                // A meteorite mining node grants its material through a max-one
                // entry, exactly like an artifact does, so a size test skipped the
                // node material and would have scaled equipment that happened to
                // drop in bulk. The type is what actually separates "a material to
                // pile up" from "a single unique item", so that is what is checked.
                string type = Il2CppReflect.GetMember(__instance, _config.RewardTypeField)?.ToString();
                if (string.IsNullOrEmpty(type) || !IsScaledType(type)) return;

                bool changed = false;
                changed |= Scale(__instance, _config.RewardCountMinField);
                changed |= Scale(__instance, _config.RewardCountMaxField);
                if (changed) _rowsChanged++;
            }
            catch (Exception ex)
            {
                _log.LogError("Error while adjusting reward count: " + ex);
            }
        }

        /// <summary>The Quantity/Types list, or "*" for every type.</summary>
        private static bool IsScaledType(string type)
        {
            string filter = _config.QuantityTypes;
            if (string.IsNullOrWhiteSpace(filter) || filter == "*") return true;

            foreach (var wanted in filter.Split(','))
                if (string.Equals(wanted.Trim(), type, StringComparison.OrdinalIgnoreCase))
                    return true;

            return false;
        }

        /// <summary>Scales one count field on a row. Returns true when it changed.</summary>
        private static bool Scale(object row, string field)
        {
            int count = Il2CppReflect.GetInt(row, field, -1);

            // A min of zero is a chance-of-nothing entry; leave the zero so the
            // chance survives, only the max it rolls toward grows.
            if (count < 1) return false;

            int scaled = (int)Math.Round(count * _config.Multiplier, MidpointRounding.AwayFromZero);
            if (scaled < 1) scaled = 1;

            if (_config.MaxPerStack > 0 && scaled > _config.MaxPerStack)
                scaled = _config.MaxPerStack;

            if (scaled == count) return false;

            if (_config.DryRun) return false;

            return Il2CppReflect.SetMember(row, field, scaled);
        }

        /// <summary>
        /// One line for the whole table, for the same reason as the other table
        /// patches: 28,000 rows of detail would drown out the drop list.
        /// </summary>
        public static void ReportSummary()
        {
            if (_rowsChanged == 0 || _reported) return;
            _reported = true;

            _log.LogInfo($"Reward table: {_rowsChanged} stack size(s) raised.");
        }
    }
}
