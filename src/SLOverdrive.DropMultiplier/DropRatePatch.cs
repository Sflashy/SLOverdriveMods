using System;
using BepInEx.Logging;
using SLOverdrive.Core.Il2Cpp;

namespace SLOverdrive.DropMultiplier
{
    /// <summary>
    /// Raises the drop chance used when the game rolls a dungeon's loot table.
    ///
    /// <code>
    /// NLib.GKDropCommon.GetDropInfoCommons(
    ///     List&lt;AJIHKOOLMFK&gt; dropDatas,   // loot table rows
    ///     int randomSeed,                 // same seed later carried by the drop packet
    ///     int dropRate = 10000)           // chance, in basis points: 10000 = 100%
    /// </code>
    ///
    /// This runs *before* the drop packet is built, so unlike
    /// <see cref="DropPacketPatch"/> it changes what drops rather than how much.
    /// The two are independent and can be used together.
    /// </summary>
    internal static class DropRatePatch
    {
        /// <summary>The game's default drop rate, treated as 100%.</summary>
        public const int BaseRate = 10000;

        private static DropConfig _config;
        private static ManualLogSource _log;

        public static void Initialize(DropConfig config, ManualLogSource log)
        {
            _config = config;
            _log = log;
        }

        /// <summary>
        /// Harmony prefix. The method is static, so the parameters map directly:
        /// <c>__0</c> = dropDatas, <c>__1</c> = randomSeed, <c>__2</c> = dropRate.
        /// </summary>
        public static void Prefix(object __0, int __1, ref int __2)
        {
            if (_config == null) return;

            try
            {
                int original = __2;
                if (original <= 0) return;

                int scaled = (int)Math.Round(original * _config.DropRateMultiplier, MidpointRounding.AwayFromZero);

                if (_config.MaxDropRate > 0 && scaled > _config.MaxDropRate)
                    scaled = _config.MaxDropRate;

                if (_config.Verbose)
                {
                    int rows = Il2CppReflect.ListCount(__0);
                    string line =
                        $"  [{(_config.DryRun ? "DRY" : "MOD")}] drop roll: {rows} table row(s), " +
                        $"seed={__1}, rate {Percent(original)} -> {Percent(scaled)}";
                    _log.LogInfo(line);
                }

                _lastRowCount = Il2CppReflect.ListCount(__0);

                if (!_config.DryRun) __2 = scaled;
            }
            catch (Exception ex)
            {
                _log.LogError("Error while adjusting drop rate: " + ex);
            }
        }

        private static int _lastRowCount;

        /// <summary>
        /// Logs how many entries survived the roll.
        ///
        /// This tells us what <c>dropRate</c> actually does. If the returned count is
        /// lower than the input row count, the rate gates individual entries and raising
        /// it should let more through. If the counts always match, the rate is applied
        /// somewhere else and this patch has no effect.
        /// </summary>
        public static void Postfix(object __result)
        {
            if (_config == null || !_config.Verbose) return;

            try
            {
                int produced = Il2CppReflect.ListCount(__result);
                string line = $"  [---] drop roll result: {_lastRowCount} row(s) in, {produced} entry(ies) out";
                _log.LogInfo(line);
            }
            catch (Exception ex)
            {
                _log.LogError("Error while logging drop roll result: " + ex);
            }
        }

        /// <summary>Formats a basis-point rate as a percentage, e.g. 10000 -> "100%".</summary>
        private static string Percent(int rate)
            => $"{rate * 100f / BaseRate:0.#}%";
    }
}
