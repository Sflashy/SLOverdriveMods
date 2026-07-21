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
        private readonly ConfigEntry<string> _quantityTypes;
        private readonly ConfigEntry<float> _multiplier;
        private readonly ConfigEntry<int> _maxPerStack;

        private readonly ConfigEntry<bool> _scaleDropRate;
        private readonly ConfigEntry<float> _dropRateMultiplier;
        private readonly ConfigEntry<int> _maxDropRate;

        private readonly ConfigEntry<bool> _scaleTargetCount;
        private readonly ConfigEntry<float> _targetCountMultiplier;
        private readonly ConfigEntry<int> _maxTargetCount;
        private readonly ConfigEntry<int> _onlyAbove;
        private readonly ConfigEntry<string> _dropTableRowClass;
        private readonly ConfigEntry<string> _targetCountField;

        private readonly ConfigEntry<bool> _scaleRewardWeight;
        private readonly ConfigEntry<float> _rewardWeightMultiplier;
        private readonly ConfigEntry<int> _maxRewardWeight;
        private readonly ConfigEntry<string> _rewardTypeFilter;
        private readonly ConfigEntry<string> _rewardRowClass;
        private readonly ConfigEntry<string> _rewardWeightField;
        private readonly ConfigEntry<string> _rewardTypeField;
        private readonly ConfigEntry<string> _rewardItemField;
        private readonly ConfigEntry<string> _rewardCountMinField;
        private readonly ConfigEntry<string> _rewardCountMaxField;

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
        public string QuantityTypes { get; private set; }
        public float Multiplier { get; private set; }
        public int MaxPerStack { get; private set; }

        public bool ScaleDropRate { get; private set; }
        public float DropRateMultiplier { get; private set; }
        public int MaxDropRate { get; private set; }

        public bool ScaleTargetCount { get; private set; }
        public float TargetCountMultiplier { get; private set; }
        public int MaxTargetCount { get; private set; }
        public int OnlyAbove { get; private set; }
        public string DropTableRowClass { get; private set; }
        public string TargetCountField { get; private set; }

        public bool ScaleRewardWeight { get; private set; }
        public float RewardWeightMultiplier { get; private set; }
        public int MaxRewardWeight { get; private set; }
        public string RewardTypeFilter { get; private set; }
        public string RewardRowClass { get; private set; }
        public string RewardWeightField { get; private set; }
        public string RewardTypeField { get; private set; }
        public string RewardItemField { get; private set; }
        public string RewardCountMinField { get; private set; }
        public string RewardCountMaxField { get; private set; }

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
                "Multiply the stack size each reward entry grants. This edits Reward.RewardCount " +
                "in the table, before the roll, so it changes the amount that actually reaches " +
                "your inventory. Which reward types it touches is set by Types below.");

            _quantityTypes = config.Bind("Quantity", "Types", "Material,Gold,UseItem",
                "Comma separated reward types to multiply, or '*' for all.\n" +
                "  Material, Gold - plain stackables; multiplying grows the stack.\n" +
                "  UseItem        - almost entirely reward BOXES (641 of 665 are containers " +
                "like 'Artifact Equipment'). Multiplying these gives more boxes, which open into " +
                "more equipment, so this is also the 'more gear' lever. At the default x2 that is " +
                "a box or two extra; the flooding you may have read about only happens if you " +
                "crank the multiplier high. Keep the multiplier modest and it stays a normal drop.\n" +
                "Artifact is left out: those are single items, so multiplying makes literal " +
                "duplicate copies rather than a box of variety, and the boxes above cover gear.");

            _multiplier = config.Bind("Quantity", "Multiplier", 2.0f,
                "Stack multiplier. 2.0 = double the amount of each drop. This is also the flood " +
                "control: 2-3 stays a normal-feeling drop, while a large value turns the box " +
                "types above into far more equipment than you can use. A box that opened once " +
                "opens three times at 3.0; five meteorites become fifteen.");

            _maxPerStack = config.Bind("Quantity", "MaxPerStack", 0,
                "Upper limit for a stack after scaling. 0 = no limit. Some entries grant "
                + "hundreds already (currencies), so a large multiplier without a cap is a lot.");

            _scaleTargetCount = config.Bind("TargetCount", "Enabled", false,
                "Raise how many drop-carrying targets a stage has. ContentsDrop.TargetMaxCount " +
                "is a count, not a probability: 1 for a boss, 50 for a group of mobs. More " +
                "targets means more chances to roll loot. For the actual drop rate see " +
                "[RewardGroup] RateMultiplier.");

            _targetCountMultiplier = config.Bind("TargetCount", "Multiplier", 2.0f,
                "Target count multiplier.");

            _maxTargetCount = config.Bind("TargetCount", "MaxCount", 999,
                "Upper limit after scaling. The game's own values stop at 999.");

            _onlyAbove = config.Bind("TargetCount", "OnlyAbove", 1,
                "Leave rows at or below this count alone. The default of 1 protects boss " +
                "rows, which are meant to be unique.");

            _dropTableRowClass = config.Bind("Advanced", "DropTableRowClass", "AJIHKOOLMFK",
                "Obfuscated class name of a ContentsDrop table row. Changes when the game " +
                "updates. Use the Table Dumper to find the new name.");

            _targetCountField = config.Bind("Advanced", "TargetCountField", "BIEPKPINBEN",
                "ContentsDrop.TargetMaxCount. Run the Table Dumper in sheet mode to see " +
                "the real column names and find this one after a game update.");

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

            _rewardCountMinField = config.Bind("Advanced", "RewardCountMinField", "JFHBPIMFCKF",
                "Reward.RewardCountMin - the low end of the stack a reward entry grants. " +
                "This is where [Quantity] multiplies, before the roll, so it reaches the grant.");

            _rewardCountMaxField = config.Bind("Advanced", "RewardCountMaxField", "OOCBMBCAMDK",
                "Reward.RewardCountMax - the high end of the stack a reward entry grants.");

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
            QuantityTypes = _quantityTypes.Value;
            Multiplier = _multiplier.Value;
            MaxPerStack = _maxPerStack.Value;

            ScaleTargetCount = _scaleTargetCount.Value;
            TargetCountMultiplier = _targetCountMultiplier.Value;
            MaxTargetCount = _maxTargetCount.Value;
            OnlyAbove = _onlyAbove.Value;
            DropTableRowClass = _dropTableRowClass.Value;
            TargetCountField = _targetCountField.Value;

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
            RewardCountMinField = _rewardCountMinField.Value;
            RewardCountMaxField = _rewardCountMaxField.Value;

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
