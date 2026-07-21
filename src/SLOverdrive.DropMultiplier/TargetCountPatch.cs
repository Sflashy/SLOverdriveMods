using System;
using BepInEx.Logging;
using SLOverdrive.Core.Il2Cpp;

namespace SLOverdrive.DropMultiplier
{
    /// <summary>
    /// Raises how many drop-carrying targets a stage has.
    ///
    /// Each ContentsDrop row describes one drop source in a stage:
    ///
    /// <code>
    /// ID  DropType  ContentsType   ContentsID  TargetType  TargetID  TargetMaxCount  RewardGroup
    /// ..  Normal    WorldContents  100110208   Monster     ..        50              30811313
    /// ..  Specific  WorldContents  100110208   Monster     11402      1              30811311
    /// </code>
    ///
    /// <c>TargetMaxCount</c> is a count, not a probability: 1 for a boss, 50 for a
    /// group of trash mobs. Raising it gives a stage more chances to produce loot,
    /// which is why it works, but it is not the game's drop rate - that lives on the
    /// reward group. See <see cref="RewardGroupPatch"/>.
    /// </summary>
    internal static class TargetCountPatch
    {
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

        /// <summary>Harmony postfix on the ContentsDrop row loader.</summary>
        public static void Postfix(object __instance)
        {
            if (_config == null || __instance == null) return;

            try
            {
                int count = Il2CppReflect.GetInt(__instance, _config.TargetCountField, -1);
                if (count <= 0) return;

                _rowsSeen++;

                // Boss rows carry a count of 1 and are best left alone: duplicating a
                // boss is not something the stage is built for.
                if (_config.OnlyAbove > 0 && count <= _config.OnlyAbove) return;

                int scaled = (int)Math.Round(count * _config.TargetCountMultiplier,
                                             MidpointRounding.AwayFromZero);
                if (scaled < 1) scaled = 1;

                if (_config.MaxTargetCount > 0 && scaled > _config.MaxTargetCount)
                    scaled = _config.MaxTargetCount;

                if (scaled == count || _config.DryRun) return;

                if (Il2CppReflect.SetMember(__instance, _config.TargetCountField, scaled))
                    _rowsChanged++;
            }
            catch (Exception ex)
            {
                _log.LogError("Error while adjusting target count: " + ex);
            }
        }

        public static void ReportSummary()
        {
            if (_reported || _rowsChanged == 0) return;
            _reported = true;

            _log.LogInfo($"Drop targets: {_rowsChanged} of {_rowsSeen} count(s) raised.");
        }
    }
}
