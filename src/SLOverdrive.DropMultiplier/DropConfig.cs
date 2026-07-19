using BepInEx.Configuration;

namespace SLOverdrive.DropMultiplier
{
    /// <summary>
    /// Plugin settings, cached into plain fields.
    ///
    /// Reading <c>ConfigEntry.Value</c> is not free, so the patch reads these fields
    /// instead and the cache is refreshed whenever a setting changes.
    /// </summary>
    internal sealed class DropConfig
    {
        private readonly ConfigEntry<bool> _scaleQuantity;
        private readonly ConfigEntry<float> _multiplier;
        private readonly ConfigEntry<int> _maxPerStack;
        private readonly ConfigEntry<bool> _skipEquipment;

        private readonly ConfigEntry<bool> _scaleDropRate;
        private readonly ConfigEntry<float> _dropRateMultiplier;
        private readonly ConfigEntry<int> _maxDropRate;

        private readonly ConfigEntry<bool> _scaleChance;
        private readonly ConfigEntry<float> _chanceMultiplier;
        private readonly ConfigEntry<int> _maxChance;
        private readonly ConfigEntry<int> _onlyBelow;
        private readonly ConfigEntry<string> _dropTableRowClass;
        private readonly ConfigEntry<string> _chanceField;
        private readonly ConfigEntry<string> _chanceItemField;

        private readonly ConfigEntry<bool> _scaleRewardWeight;
        private readonly ConfigEntry<float> _rewardWeightMultiplier;
        private readonly ConfigEntry<int> _maxRewardWeight;
        private readonly ConfigEntry<string> _rewardTypeFilter;
        private readonly ConfigEntry<string> _rewardRowClass;
        private readonly ConfigEntry<string> _rewardWeightField;
        private readonly ConfigEntry<string> _rewardTypeField;
        private readonly ConfigEntry<string> _rewardItemField;

        private readonly ConfigEntry<bool> _scaleRewardGroup;
        private readonly ConfigEntry<int> _pickCount;
        private readonly ConfigEntry<float> _groupRateMultiplier;
        private readonly ConfigEntry<int> _maxGroupRate;
        private readonly ConfigEntry<string> _rewardGroupRowClass;
        private readonly ConfigEntry<string> _pickCountField;
        private readonly ConfigEntry<string> _groupRateField;

        private readonly ConfigEntry<bool> _dryRun;
        private readonly ConfigEntry<bool> _verbose;
        private readonly ConfigEntry<bool> _logDropPackets;
        private readonly ConfigEntry<bool> _logDropIds;
        private readonly ConfigEntry<string> _dropManagerType;

        public bool ScaleQuantity { get; private set; }
        public float Multiplier { get; private set; }
        public int MaxPerStack { get; private set; }
        public bool SkipEquipment { get; private set; }

        public bool ScaleDropRate { get; private set; }
        public float DropRateMultiplier { get; private set; }
        public int MaxDropRate { get; private set; }

        public bool ScaleChance { get; private set; }
        public float ChanceMultiplier { get; private set; }
        public int MaxChance { get; private set; }
        public int OnlyBelow { get; private set; }
        public string DropTableRowClass { get; private set; }
        public string ChanceField { get; private set; }
        public string ChanceItemField { get; private set; }

        public bool ScaleRewardWeight { get; private set; }
        public float RewardWeightMultiplier { get; private set; }
        public int MaxRewardWeight { get; private set; }
        public string RewardTypeFilter { get; private set; }
        public string RewardRowClass { get; private set; }
        public string RewardWeightField { get; private set; }
        public string RewardTypeField { get; private set; }
        public string RewardItemField { get; private set; }

        public bool ScaleRewardGroup { get; private set; }
        public int PickCount { get; private set; }
        public float GroupRateMultiplier { get; private set; }
        public int MaxGroupRate { get; private set; }
        public string RewardGroupRowClass { get; private set; }
        public string PickCountField { get; private set; }
        public string GroupRateField { get; private set; }

        public bool DryRun { get; private set; }
        public bool Verbose { get; private set; }
        public bool LogDropPackets { get; private set; }
        public bool LogDropIds { get; private set; }
        public string DropManagerType { get; private set; }

        public DropConfig(ConfigFile config)
        {
            _scaleQuantity = config.Bind("Quantity", "Enabled", true,
                "Multiply the amount of each item that drops. Applies after the loot roll, " +
                "so it changes how much you get, not what you get.");

            _multiplier = config.Bind("Quantity", "Multiplier", 2.0f,
                "Drop quantity multiplier. 2.0 = double drops.");

            _maxPerStack = config.Bind("Quantity", "MaxPerStack", 0,
                "Upper limit for a single item stack after scaling. 0 = no limit.");

            _skipEquipment = config.Bind("Quantity", "SkipEquipment", false,
                "Leave items that drop as a single unit untouched. Equipment and artifacts " +
                "normally arrive with a stack of 1 and may not behave correctly when duplicated. " +
                "Enable this to multiply only materials and currencies.");

            _scaleChance = config.Bind("DropChance", "Enabled", true,
                "Raise the drop probability stored in the game's ContentsDrop table. " +
                "This is the setting that makes rare items appear more often.");

            _chanceMultiplier = config.Bind("DropChance", "Multiplier", 5.0f,
                "Drop chance multiplier. Rare items sit at 1 per-mille (0.1%), so 5.0 " +
                "takes them to 0.5%. Common drops are usually 100 (10%).");

            _maxChance = config.Bind("DropChance", "MaxChance", 999,
                "Upper limit after scaling, in per-mille where 1000 would be certain. " +
                "The game's own table never exceeds 999.");

            _onlyBelow = config.Bind("DropChance", "OnlyBelow", 20,
                "Only raise rows at or below this probability, leaving common drops alone. " +
                "0 = scale everything. The rare rows are the ones causing the grind.");

            _dropTableRowClass = config.Bind("Advanced", "DropTableRowClass", "AJIHKOOLMFK",
                "Obfuscated class name of a ContentsDrop table row. Changes when the game updates. " +
                "Use the Table Dumper to find the new name.");

            _chanceField = config.Bind("Advanced", "ChanceField", "BIEPKPINBEN",
                "Field on the row holding the probability. In a fresh dump this is the field " +
                "with very few distinct values, all between 1 and 999.");

            _chanceItemField = config.Bind("Advanced", "ChanceItemField", "NDHGAHKPBPB",
                "Field holding the specific item ID, used only to make the log readable.");

            _scaleDropRate = config.Bind("DropRate", "Enabled", true,
                "Raise the chance used when the game rolls a dungeon's loot table. " +
                "Applies before the roll, so it changes what drops rather than how much.");

            _dropRateMultiplier = config.Bind("DropRate", "Multiplier", 2.0f,
                "Drop chance multiplier. The game's default rate is 10000 (100%), so 2.0 " +
                "gives 20000. Values above roughly 5.0 will make most rolls succeed.");

            _maxDropRate = config.Bind("DropRate", "MaxRate", 0,
                "Upper limit for the drop rate after scaling, in basis points where " +
                "10000 = 100%. 0 = no limit.");

            _scaleRewardWeight = config.Bind("RewardWeight", "Enabled", true,
                "Raise how often equipment is picked once a drop slot fires. This is the " +
                "second layer: DropChance decides whether a slot drops, this decides what " +
                "comes out of it.");

            _rewardWeightMultiplier = config.Bind("RewardWeight", "Multiplier", 10.0f,
                "Weight multiplier for the types listed below. Most artifact entries sit " +
                "between 1% and 20% of their group, so 10.0 makes equipment the likely pick.");

            _rewardTypeFilter = config.Bind("RewardWeight", "Types", "Artifact",
                "Comma separated reward types to boost. Wearable equipment is 'Artifact'. " +
                "Other values include Relic, Costume, Hunter, Shadow, Pet, Gem, Material. " +
                "Use '*' for everything, though that cancels itself out since all weights scale together.");

            _maxRewardWeight = config.Bind("RewardWeight", "MaxWeight", 0,
                "Upper limit for a weight after scaling. 0 = no limit.");

            _rewardRowClass = config.Bind("Advanced", "RewardRowClass", "COABIFJBJBJ",
                "Obfuscated class name of a Reward table row. Use the Table Dumper after a game update.");

            _rewardWeightField = config.Bind("Advanced", "RewardWeightField", "FFCECMLHKOL",
                "Field holding the pick weight within a reward group.");

            _rewardTypeField = config.Bind("Advanced", "RewardTypeField", "GFKJAPJBBEF",
                "Field holding the reward type, e.g. Artifact, Material, Gold.");

            _rewardItemField = config.Bind("Advanced", "RewardItemField", "LHMNABONBNE",
                "Field holding the item ID, used only to make the log readable.");

            _scaleRewardGroup = config.Bind("RewardGroup", "Enabled", true,
                "Change how many items each reward group hands out. The game ships every " +
                "group with a pick count of 1, which is why a drop only ever yields one item.");

            _pickCount = config.Bind("RewardGroup", "PickCount", 3,
                "How many entries each group should hand out. 1 = untouched. Groups smaller " +
                "than this simply give everything they have. Large values across every group " +
                "produce very large drop packets, so raise this gradually.");

            _groupRateMultiplier = config.Bind("RewardGroup", "RateMultiplier", 1.0f,
                "Multiplier for a group's own fire rate. Only groups in Random mode carry " +
                "one; Rate and Fix groups are unaffected. 1.0 = untouched.");

            _maxGroupRate = config.Bind("RewardGroup", "MaxRate", 10000,
                "Upper limit for a group rate after scaling. The game's own values top out " +
                "around 10000.");

            _rewardGroupRowClass = config.Bind("Advanced", "RewardGroupRowClass", "PEGMFCJCGFA",
                "Obfuscated class name of a RewardGroup table row.");

            _pickCountField = config.Bind("Advanced", "PickCountField", "CJHLCDHAFBM",
                "Field holding how many entries the group hands out. In a fresh dump this is " +
                "the column that is 1 on every single row.");

            _groupRateField = config.Bind("Advanced", "GroupRateField", "IOFPFNBNGPA",
                "Field holding the group's own fire rate.");

            _dryRun = config.Bind("Debug", "DryRun", true,
                "When true nothing is modified, drops are only logged. Use this to confirm the " +
                "logged values match your actual rewards before enabling the mod.");

            _verbose = config.Bind("Debug", "Verbose", true,
                "Log every drop the plugin sees.");

            _logDropIds = config.Bind("Debug", "LogDropIds", true,
                "Also log each drop's dropID and dropUID. Diagnostic only - these are the " +
                "keys that would let the mod look up which reward group a drop came from.");

            _logDropPackets = config.Bind("Debug", "LogDropPackets", true,
                "Log the contents of each dungeon's drop packet, even when quantity scaling " +
                "is switched off. This is the list of what you are about to receive.");

            _dropManagerType = config.Bind("Advanced", "DropManagerType", "ELAHJNHCOHO",
                "Name of the obfuscated drop manager class. This changes when the game updates. " +
                "Leave empty to let the plugin search for it automatically.");

            Refresh();
            config.SettingChanged += (_, __) => Refresh();
        }

        private void Refresh()
        {
            ScaleQuantity = _scaleQuantity.Value;
            Multiplier = _multiplier.Value;
            MaxPerStack = _maxPerStack.Value;
            SkipEquipment = _skipEquipment.Value;

            ScaleChance = _scaleChance.Value;
            ChanceMultiplier = _chanceMultiplier.Value;
            MaxChance = _maxChance.Value;
            OnlyBelow = _onlyBelow.Value;
            DropTableRowClass = _dropTableRowClass.Value;
            ChanceField = _chanceField.Value;
            ChanceItemField = _chanceItemField.Value;

            ScaleDropRate = _scaleDropRate.Value;
            DropRateMultiplier = _dropRateMultiplier.Value;
            MaxDropRate = _maxDropRate.Value;

            ScaleRewardWeight = _scaleRewardWeight.Value;
            RewardWeightMultiplier = _rewardWeightMultiplier.Value;
            MaxRewardWeight = _maxRewardWeight.Value;
            RewardTypeFilter = _rewardTypeFilter.Value;
            RewardRowClass = _rewardRowClass.Value;
            RewardWeightField = _rewardWeightField.Value;
            RewardTypeField = _rewardTypeField.Value;
            RewardItemField = _rewardItemField.Value;

            ScaleRewardGroup = _scaleRewardGroup.Value;
            PickCount = _pickCount.Value;
            GroupRateMultiplier = _groupRateMultiplier.Value;
            MaxGroupRate = _maxGroupRate.Value;
            RewardGroupRowClass = _rewardGroupRowClass.Value;
            PickCountField = _pickCountField.Value;
            GroupRateField = _groupRateField.Value;

            DryRun = _dryRun.Value;
            Verbose = _verbose.Value;
            LogDropPackets = _logDropPackets.Value;
            LogDropIds = _logDropIds.Value;
            DropManagerType = _dropManagerType.Value;
        }
    }
}
