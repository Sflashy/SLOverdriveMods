using System;
using BepInEx.Logging;
using SLOverdrive.Core.Data;
using SLOverdrive.Core.Il2Cpp;

namespace SLOverdrive.DropMultiplier
{
    /// <summary>
    /// Scales the item stacks carried by an incoming battle drop packet.
    ///
    /// <code>
    /// GAME_BATTLE_STAGE_DROP_NFY
    ///  ├─ contentsType, contentsID, randomSeed
    ///  └─ dropInfoList : List&lt;BattleDropInfo_Network&gt;
    ///       └─ itemInfoList : List&lt;DropItemInfo_Network&gt;
    ///            ├─ itemID
    ///            └─ stack      ← scaled here
    /// </code>
    ///
    /// Harmony patches must be static, so the config and logger are injected once
    /// at plugin startup rather than passed per call.
    /// </summary>
    internal static class DropPacketPatch
    {
        private static DropConfig _config;
        private static ManualLogSource _log;

        public static void Initialize(DropConfig config, ManualLogSource log)
        {
            _config = config;
            _log = log;
        }

        /// <summary>Harmony prefix. <c>__0</c> is the drop notification packet.</summary>
        public static void Prefix(object __0)
        {
            if (_config == null || __0 == null) return;

            try
            {
                var drops = Il2CppReflect.GetMember(__0, "dropInfoList");
                int dropCount = Il2CppReflect.ListCount(drops);

                if (dropCount == 0)
                {
                    if (_config.Verbose) _log.LogInfo("Drop packet received with an empty drop list.");
                    return;
                }

                int modified = 0;

                for (int i = 0; i < dropCount; i++)
                {
                    var drop = Il2CppReflect.ListItem(drops, i);

                    // Diagnostic: dropID should point back at a ContentsDrop row, which is
                    // how we would find the reward group behind this drop.
                    if (_config.LogDropIds)
                    {
                        long uid = Convert.ToInt64(Il2CppReflect.GetMember(drop, "dropUID") ?? 0L);
                        int id = Il2CppReflect.GetInt(drop, "dropID");
                        string ids = $"  drop[{i}] dropID={id} dropUID={uid}";
                        _log.LogInfo(ids);
                    }

                    var items = Il2CppReflect.GetMember(drop, "itemInfoList");
                    int itemCount = Il2CppReflect.ListCount(items);

                    for (int j = 0; j < itemCount; j++)
                    {
                        if (ScaleItem(Il2CppReflect.ListItem(items, j)))
                            modified++;
                    }
                }

                // Table patch totals are reported here rather than at load time,
                // because the tables are still loading when the plugin starts.
                TargetCountPatch.ReportSummary();
                RewardWeightPatch.ReportSummary();
                RewardGroupPatch.ReportSummary();

                _log.LogInfo($"Drop packet: {dropCount} drop(s), {modified} stack(s) modified.");
            }
            catch (Exception ex)
            {
                _log.LogError("Error while processing drop packet: " + ex);
            }
        }

        /// <summary>Scales one item entry. Returns true when the stack was changed.</summary>
        private static bool ScaleItem(object item)
        {
            if (item == null) return false;

            var stackValue = Il2CppReflect.GetMember(item, "stack");
            if (stackValue == null) return false;

            int stack;
            try { stack = Convert.ToInt32(stackValue); }
            catch { return false; }

            if (stack <= 0) return false;

            string name = ItemDatabase.NameOf(Il2CppReflect.GetMember(item, "itemID"));

            // With quantity scaling off this patch is still useful as a readout of
            // what the dungeon is about to hand over.
            if (!_config.ScaleQuantity)
            {
                string listed = $"  {name,-38} {stack,5}";
                _log.LogInfo(listed);
                return false;
            }

            // Equipment and artifacts arrive as a single unit. The game does not
            // necessarily expect duplicates, so the user can opt out of touching them.
            if (_config.SkipEquipment && stack == 1)
            {
                // Assigned to a local first: BepInEx's logging interpolated string
                // handler has no alignment overload, the default string one does.
                string skipped = $"  {name,-38} {stack,5}    (skipped)";
                _log.LogInfo(skipped);
                return false;
            }

            int scaled = (int)Math.Round(stack * _config.Multiplier, MidpointRounding.AwayFromZero);
            if (scaled < 1) scaled = 1;
            if (_config.MaxPerStack > 0 && scaled > _config.MaxPerStack) scaled = _config.MaxPerStack;

            string line = $"  [{(_config.DryRun ? "DRY" : "MOD")}] {name,-38} {stack,5} -> {scaled}";
            _log.LogInfo(line);

            if (_config.DryRun || scaled == stack) return false;

            return Il2CppReflect.SetMember(item, "stack", scaled);
        }
    }
}
