using System;
using BepInEx.Logging;
using SLOverdrive.Core.Il2Cpp;

namespace SLOverdrive.DropMultiplier
{
    /// <summary>
    /// Raises the drop probability stored in the game's ContentsDrop table.
    ///
    /// This is the real rarity control. Each row of the table describes one possible
    /// drop and carries a probability in per-mille:
    ///
    /// <code>
    /// prob=100  Normal    common material
    /// prob= 20  Normal
    /// prob=  2  Specific  a named item
    /// prob=  1  Specific  "Almighty Shaman", "Black Lion", ...
    /// </code>
    ///
    /// The rows are decrypted by the game and handed to <c>LoadDataSheet</c> once at
    /// startup, which is where this patch edits them. Unlike the packet and roll
    /// patches, this changes *what* drops rather than how much.
    /// </summary>
    internal static class DropChancePatch
    {
        /// <summary>Probabilities are per-mille: 1000 would be certain, the table caps at 999.</summary>
        public const int Scale = 1000;

        private static DropConfig _config;
        private static ManualLogSource _log;

        private static int _rowsSeen;
        private static int _rowsChanged;
        private static bool _reported;

        public static void Initialize(DropConfig config, ManualLogSource log)
        {
            _config = config;
            _log = log;
        }

        /// <summary>
        /// Harmony postfix on the row loader. <c>__instance</c> is one ContentsDrop row.
        /// Runs once per row while the table loads, so there is no per-frame cost.
        /// </summary>
        public static void Postfix(object __instance)
        {
            if (_config == null || __instance == null) return;

            try
            {
                string field = _config.ChanceField;

                var current = Il2CppReflect.GetMember(__instance, field);
                if (current == null) return;

                int chance;
                try { chance = Convert.ToInt32(current); }
                catch { return; }

                if (chance <= 0) return;

                _rowsSeen++;

                // Leaving common drops alone keeps the loot mix recognisable - only the
                // rare rows are worth boosting, and they are the ones causing the grind.
                if (_config.OnlyBelow > 0 && chance > _config.OnlyBelow) return;

                int scaled = (int)Math.Round(chance * _config.ChanceMultiplier, MidpointRounding.AwayFromZero);
                if (scaled < 1) scaled = 1;

                int cap = _config.MaxChance > 0 ? _config.MaxChance : Scale - 1;
                if (scaled > cap) scaled = cap;

                if (scaled == chance) return;

                if (_config.DryRun) return;

                if (Il2CppReflect.SetMember(__instance, field, scaled))
                    _rowsChanged++;
            }
            catch (Exception ex)
            {
                _log.LogError("Error while adjusting drop chance: " + ex);
            }
        }

        /// <summary>
        /// One line for the whole table. The per-row detail is deliberately not logged:
        /// the table has thousands of rows and repeats across stages, which buried the
        /// drop list that actually matters.
        /// </summary>
        public static void ReportSummary()
        {
            if (_rowsChanged == 0 || _reported) return;
            _reported = true;

            _log.LogInfo($"Drop table: {_rowsChanged} of {_rowsSeen} chance(s) raised.");
        }
    }
}
