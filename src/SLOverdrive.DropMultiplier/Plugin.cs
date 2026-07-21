using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using SLOverdrive.Core.Data;
using SLOverdrive.Core.Il2Cpp;

namespace SLOverdrive.DropMultiplier
{
    /// <summary>
    /// Multiplies item drop quantities in Solo Leveling: ARISE OVERDRIVE.
    ///
    /// Drops are rolled when a dungeon is selected on the map and delivered to the
    /// client as a GAME_BATTLE_STAGE_DROP_NFY packet. This plugin intercepts that
    /// packet and scales the item stacks before the game processes it.
    ///
    /// Intended for offline, single-player use.
    /// </summary>
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BasePlugin
    {
        public const string Guid = "sflashy.sloverdrive.dropmultiplier";
        public const string Name = "SL Overdrive - Drop Multiplier";
        public const string Version = "1.0.0";

        /// <summary>Parameter type identifying the drop packet handler.</summary>
        private const string DropPacketType = "GAME_BATTLE_STAGE_DROP_NFY";

        public override void Load()
        {
            var config = new DropConfig(Config);

            ItemDatabase.Load(Log);
            DropPacketPatch.Initialize(config, Log);
            DropRatePatch.Initialize(config, Log);
            TargetCountPatch.Initialize(config, Log);
            RewardWeightPatch.Initialize(config, Log);
            RewardCountPatch.Initialize(config, Log);
            RewardGroupPatch.Initialize(config, Log);

            // DryRun defaults to on, which is the safe default and also the most
            // common reason the mod appears to do nothing: everything is patched,
            // every line is logged, and not one value is changed. Said plainly and
            // at warning level so it is not read past.
            if (config.DryRun)
            {
                Log.LogWarning("DryRun is ON - drops are only logged, nothing is changed. " +
                               "Set DryRun = false in BepInEx/config/" +
                               "sflashy.sloverdrive.dropmultiplier.cfg to apply the mod.");
            }
            else
            {
                Log.LogInfo("DryRun is off - drops will be modified.");
            }

            var harmony = new Harmony(Guid);
            int patched = 0;

            if (config.ScaleTargetCount)
                patched += PatchTargetCount(harmony, config) ? 1 : 0;

            if (config.ScaleRewardWeight)
                patched += PatchRewardWeight(harmony, config) ? 1 : 0;

            // Quantity now scales the reward table's stack counts, on the same row
            // loader RewardWeight patches. It is a table patch like the others, so
            // the larger amount actually rolls and actually grants.
            if (config.ScaleQuantity)
                patched += PatchRewardCount(harmony, config) ? 1 : 0;

            if (config.ScaleRewardGroup)
                patched += PatchRewardGroup(harmony, config) ? 1 : 0;

            if (config.ScaleDropRate)
                patched += PatchDropRate(harmony) ? 1 : 0;

            // The packet hook is only a readout now - it prints what a dungeon is
            // about to give, and no longer tries to scale it, because editing the
            // notification never reached the grant. Installed only for that log.
            if (config.LogDropPackets)
                patched += PatchDropPacket(harmony, config) ? 1 : 0;

            if (patched == 0)
            {
                Log.LogError("Nothing was patched - this mod will do nothing.");
                Log.LogError("Either both features are disabled in the config, or the game was");
                Log.LogError("updated and the obfuscated names changed.");
            }
        }

        /// <summary>
        /// Patches the ContentsDrop row loader so stages carry more drop targets.
        /// </summary>
        private bool PatchTargetCount(Harmony harmony, DropConfig config)
        {
            var type = TypeResolver.Find(config.DropTableRowClass);
            if (type == null)
            {
                Log.LogWarning($"Drop table row class '{config.DropTableRowClass}' not found.");
                Log.LogWarning("Run the Table Dumper to find its new name after a game update.");
                return false;
            }

            var target = AccessTools.Method(type, "LoadDataSheet");
            if (target == null)
            {
                Log.LogWarning($"{config.DropTableRowClass}.LoadDataSheet not found.");
                return false;
            }

            try
            {
                var postfix = AccessTools.Method(typeof(TargetCountPatch), nameof(TargetCountPatch.Postfix));
                harmony.Patch(target, postfix: new HarmonyMethod(postfix));

                Log.LogInfo($"Patched {config.DropTableRowClass}.LoadDataSheet " +
                            $"(TargetMaxCount x{config.TargetCountMultiplier})");
                return true;
            }
            catch (System.Exception ex)
            {
                Log.LogError($"Failed to patch {config.DropTableRowClass}.LoadDataSheet - {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Patches the Reward row loader so equipment is picked more often within its group.
        /// </summary>
        private bool PatchRewardWeight(Harmony harmony, DropConfig config)
        {
            var type = TypeResolver.Find(config.RewardRowClass);
            if (type == null)
            {
                Log.LogWarning($"Reward row class '{config.RewardRowClass}' not found.");
                return false;
            }

            var target = AccessTools.Method(type, "LoadDataSheet");
            if (target == null)
            {
                Log.LogWarning($"{config.RewardRowClass}.LoadDataSheet not found.");
                return false;
            }

            try
            {
                var postfix = AccessTools.Method(typeof(RewardWeightPatch), nameof(RewardWeightPatch.Postfix));
                harmony.Patch(target, postfix: new HarmonyMethod(postfix));

                Log.LogInfo($"Patched {config.RewardRowClass}.LoadDataSheet " +
                            $"(types {config.RewardTypeFilter}, x{config.RewardWeightMultiplier})");
                return true;
            }
            catch (System.Exception ex)
            {
                Log.LogError($"Failed to patch {config.RewardRowClass}.LoadDataSheet - {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Patches the Reward row loader so each entry grants a larger stack. The
        /// same loader RewardWeight patches; Harmony runs both postfixes.
        /// </summary>
        private bool PatchRewardCount(Harmony harmony, DropConfig config)
        {
            var type = TypeResolver.Find(config.RewardRowClass);
            if (type == null)
            {
                Log.LogWarning($"Reward row class '{config.RewardRowClass}' not found.");
                return false;
            }

            var target = AccessTools.Method(type, "LoadDataSheet");
            if (target == null)
            {
                Log.LogWarning($"{config.RewardRowClass}.LoadDataSheet not found.");
                return false;
            }

            try
            {
                var postfix = AccessTools.Method(typeof(RewardCountPatch), nameof(RewardCountPatch.Postfix));
                harmony.Patch(target, postfix: new HarmonyMethod(postfix));

                Log.LogInfo($"Patched {config.RewardRowClass}.LoadDataSheet " +
                            $"(stack count x{config.Multiplier})");
                return true;
            }
            catch (System.Exception ex)
            {
                Log.LogError($"Failed to patch {config.RewardRowClass}.LoadDataSheet - {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Patches the RewardGroup row loader so a group can hand out several items.
        /// </summary>
        private bool PatchRewardGroup(Harmony harmony, DropConfig config)
        {
            var type = TypeResolver.Find(config.RewardGroupRowClass);
            if (type == null)
            {
                Log.LogWarning($"Reward group row class '{config.RewardGroupRowClass}' not found.");
                return false;
            }

            var target = AccessTools.Method(type, "LoadDataSheet");
            if (target == null)
            {
                Log.LogWarning($"{config.RewardGroupRowClass}.LoadDataSheet not found.");
                return false;
            }

            try
            {
                var postfix = AccessTools.Method(typeof(RewardGroupPatch), nameof(RewardGroupPatch.Postfix));
                harmony.Patch(target, postfix: new HarmonyMethod(postfix));

                Log.LogInfo($"Patched {config.RewardGroupRowClass}.LoadDataSheet " +
                            $"(pick count {config.PickCount}, rate x{config.GroupRateMultiplier})");
                return true;
            }
            catch (System.Exception ex)
            {
                Log.LogError($"Failed to patch {config.RewardGroupRowClass}.LoadDataSheet - {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Patches the loot roll so more drop entries are produced.
        ///
        /// Despite the name, the game's <c>dropRate</c> parameter multiplies how many
        /// drop objects each table row spawns rather than gating them, so this behaves
        /// as a quantity multiplier. The real drop rate lives on the reward group.
        ///
        /// NLib.GKDropCommon.GetDropInfoCommons(List&lt;T&gt; dropDatas, int randomSeed, int dropRate)
        ///
        /// This class is not obfuscated, so it is looked up by name directly.
        /// </summary>
        private bool PatchDropRate(Harmony harmony)
        {
            const string typeName = "NLib.GKDropCommon";

            var type = TypeResolver.Find(typeName);
            if (type == null)
            {
                Log.LogWarning($"{typeName} not found - drop rate will not be modified.");
                return false;
            }

            var target = AccessTools.Method(type, "GetDropInfoCommons");
            if (target == null)
            {
                Log.LogWarning($"{typeName}.GetDropInfoCommons not found.");
                return false;
            }

            try
            {
                var prefix = AccessTools.Method(typeof(DropRatePatch), nameof(DropRatePatch.Prefix));
                var postfix = AccessTools.Method(typeof(DropRatePatch), nameof(DropRatePatch.Postfix));
                harmony.Patch(target, prefix: new HarmonyMethod(prefix), postfix: new HarmonyMethod(postfix));

                Log.LogInfo($"Patched {typeName}.GetDropInfoCommons(dropDatas, randomSeed, dropRate)");
                return true;
            }
            catch (System.Exception ex)
            {
                Log.LogError($"Failed to patch {typeName}.GetDropInfoCommons - {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Patches the drop packet handler so item quantities are scaled after the roll.
        /// </summary>
        private bool PatchDropPacket(Harmony harmony, DropConfig config)
        {
            var declaringType = ResolveDropManager(config);
            if (declaringType == null) return false;

            var target = TypeResolver.FindMethodBySignature(declaringType, DropPacketType);
            if (target == null)
            {
                Log.LogWarning($"{declaringType.Name} has no method taking {DropPacketType}.");
                return false;
            }

            try
            {
                var prefix = AccessTools.Method(typeof(DropPacketPatch), nameof(DropPacketPatch.Prefix));
                harmony.Patch(target, prefix: new HarmonyMethod(prefix));

                Log.LogInfo($"Patched {declaringType.Name}::{target.Name}({DropPacketType})");
                return true;
            }
            catch (System.Exception ex)
            {
                Log.LogError($"Failed to patch {declaringType.Name}::{target.Name} - {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Resolves the drop manager class, falling back to a signature scan.
        ///
        /// The configured name is tried first because it is instant. If the game has been
        /// updated the name will be stale, so the plugin then searches every loaded type
        /// for one that accepts the drop packet - slower, but it survives updates.
        /// </summary>
        private System.Type ResolveDropManager(DropConfig config)
        {
            if (!string.IsNullOrWhiteSpace(config.DropManagerType))
            {
                var configured = TypeResolver.Find(config.DropManagerType);
                if (configured != null) return configured;

                Log.LogWarning($"Configured class '{config.DropManagerType}' not found. Searching by signature.");
            }

            var found = TypeResolver.FindTypesDeclaringParameter(DropPacketType).FirstOrDefault();

            if (found != null)
                Log.LogWarning($"Found drop manager '{found.Name}'. Put this in DropManagerType to skip the search.");

            return found;
        }
    }
}
