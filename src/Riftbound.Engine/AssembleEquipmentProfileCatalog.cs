namespace Riftbound.Engine;

internal sealed record AssembleEquipmentProfile(
    string CardNo,
    string DisplayName,
    string OptionalCost,
    string OptionalCostLabel,
    string PowerTrait,
    int PowerCost,
    string PaymentResourceReason,
    int ExperienceCost = 0,
    int RequiredGraveyardRecycleCardCount = 0,
    bool RequiresDestroyFriendlyUnitCost = false,
    int ManaCost = 0,
    bool ReduceManaCostByTargetPower = false);

internal static class AssembleEquipmentProfileCatalog
{
    private const string LongSwordCardNo = "SFD·022/221";
    private const int LongSwordAssemblePowerCost = 1;
    private const string LongSwordAssembleOptionalCost = "ASSEMBLE_RED";
    private const string SoulSwordCardNo = "UNL-039/219";
    private const int SoulSwordAssemblePowerCost = 1;
    private const string SoulSwordAssembleOptionalCost = "ASSEMBLE_GREEN";
    private const string JaggedDirkCardNo = "SFD·009/221";
    private const int JaggedDirkAssemblePowerCost = 1;
    private const string JaggedDirkAssembleOptionalCost = "ASSEMBLE_RED";
    private const string RecurveBowCardNo = "SFD·016/221";
    private const int RecurveBowAssemblePowerCost = 1;
    private const string RecurveBowAssembleOptionalCost = "ASSEMBLE_RED";
    private const string ArionsFallCardNo = "SFD·030/221";
    private const int ArionsFallAssemblePowerCost = 1;
    private const string ArionsFallAssembleOptionalCost = "ASSEMBLE_RED";
    private const string WitheredBattleaxeCardNo = "UNL-019/219";
    private const int WitheredBattleaxeAssemblePowerCost = 1;
    private const string WitheredBattleaxeAssembleOptionalCost = "ASSEMBLE_RED";
    private const string BrutalizerCardNo = "SFD·042/221";
    private const int BrutalizerAssemblePowerCost = 1;
    private const string BrutalizerAssembleOptionalCost = "ASSEMBLE_GREEN";
    private const string GuardianAngelCardNo = "SFD·051/221";
    private const int GuardianAngelAssemblePowerCost = 1;
    private const string GuardianAngelAssembleOptionalCost = "ASSEMBLE_GREEN";
    private const string ClothArmorCardNo = "SFD·064/221";
    private const int ClothArmorAssemblePowerCost = 1;
    private const string ClothArmorAssembleOptionalCost = "ASSEMBLE_BLUE";
    private const string HextechInfusedBulwarkCardNo = "SFD·073/221";
    private const int HextechInfusedBulwarkAssemblePowerCost = 1;
    private const string HextechInfusedBulwarkAssembleOptionalCost = "ASSEMBLE_BLUE";
    private const string WanderersGuidebookCardNo = "SFD·086/221";
    private const int WanderersGuidebookAssemblePowerCost = 1;
    private const string WanderersGuidebookAssembleOptionalCost = "ASSEMBLE_BLUE";
    private const string ZDriveCardNo = "SFD·090/221";
    private const int ZDriveAssemblePowerCost = 1;
    private const string ZDriveAssembleOptionalCost = "ASSEMBLE_BLUE";
    private const string SteraksGageCardNo = "SFD·056/221";
    private const int SteraksGageAssemblePowerCost = 1;
    private const string SteraksGageAssembleOptionalCost = "ASSEMBLE_GREEN";
    private const string SvarshangSongCardNo = "SFD·059/221";
    private const int SvarshangSongAssemblePowerCost = 1;
    private const string SvarshangSongAssembleOptionalCost = "ASSEMBLE_GREEN";
    private const string DoransShieldCardNo = "SFD·033/221";
    private const int DoransShieldAssemblePowerCost = 1;
    private const string DoransShieldAssembleOptionalCost = "ASSEMBLE_GREEN";
    private const string DoransRingCardNo = "SFD·124/221";
    private const int DoransRingAssemblePowerCost = 1;
    private const string DoransRingAssembleOptionalCost = "ASSEMBLE_PURPLE";
    private const string DoransBladeCardNo = "SFD·095/221";
    private const int DoransBladeAssemblePowerCost = 1;
    private const string DoransBladeAssembleOptionalCost = "ASSEMBLE_ORANGE";
    private const string HexdrinkerCardNo = "SFD·102/221";
    private const int HexdrinkerAssemblePowerCost = 1;
    private const string HexdrinkerAssembleOptionalCost = "ASSEMBLE_ORANGE";
    private const string WarmogsArmorCardNo = "SFD·108/221";
    private const int WarmogsArmorAssemblePowerCost = 1;
    private const string WarmogsArmorAssembleOptionalCost = "ASSEMBLE_ORANGE";
    private const string TrinityForceCardNo = "SFD·115/221";
    private const int TrinityForceAssemblePowerCost = 1;
    private const string TrinityForceAssembleOptionalCost = "ASSEMBLE_ORANGE";
    private const string HuntersMacheteCardNo = "UNL-096/219";
    private const int HuntersMacheteAssemblePowerCost = 1;
    private const string HuntersMacheteAssembleOptionalCost = "ASSEMBLE_ORANGE";
    private const string BoneClubCardNo = "SFD·118/221";
    private const int BoneClubAssemblePowerCost = 1;
    private const string BoneClubAssembleOptionalCost = "ASSEMBLE_ORANGE";
    private const string BoneClubPromoCardNo = "SFD·118a/221·P";
    private const int BoneClubPromoAssemblePowerCost = 1;
    private const string BoneClubPromoAssembleOptionalCost = "ASSEMBLE_ORANGE";
    private const string BootsOfSwiftnessCardNo = "SFD·133/221";
    private const int BootsOfSwiftnessAssemblePowerCost = 1;
    private const string BootsOfSwiftnessAssembleOptionalCost = "ASSEMBLE_PURPLE";
    private const string CullCardNo = "SFD·134/221";
    private const int CullAssemblePowerCost = 1;
    private const string CullAssembleOptionalCost = "ASSEMBLE_PURPLE";
    private const string EdgeOfNightCardNo = "SFD·139/221";
    private const int EdgeOfNightAssemblePowerCost = 1;
    private const string EdgeOfNightAssembleOptionalCost = "ASSEMBLE_PURPLE";
    private const string LastRitesCardNo = "SFD·150/221";
    private const int LastRitesAssemblePowerCost = 1;
    private const string LastRitesAssembleOptionalCost = "ASSEMBLE_PURPLE";
    private const int LastRitesRequiredGraveyardRecycleCardCount = 2;
    private const string VanguardsEyeCardNo = "SFD·153/221";
    private const int VanguardsEyeAssemblePowerCost = 1;
    private const string VanguardsEyeAssembleOptionalCost = "ASSEMBLE_YELLOW";
    private const string BfSwordCardNo = "SFD·161/221";
    private const int BfSwordAssemblePowerCost = 1;
    private const string BfSwordAssembleOptionalCost = "ASSEMBLE_YELLOW";
    private const string SacredShearsCardNo = "SFD·172/221";
    private const int SacredShearsAssemblePowerCost = 1;
    private const string SacredShearsAssembleOptionalCost = "ASSEMBLE_YELLOW";
    private const string BladeOfTheRuinedKingCardNo = "SFD·178/221";
    private const int BladeOfTheRuinedKingAssemblePowerCost = 1;
    private const string BladeOfTheRuinedKingAssembleOptionalCost = "ASSEMBLE_YELLOW";
    private const string SpinningAxeCardNo = "SFD·186/221";
    private const int SpinningAxeAssemblePowerCost = 1;
    private const string SpinningAxeAssembleOptionalCost = "ASSEMBLE_ANY_POWER";
    private const string HearthfireCloakCardNo = "SFD·190/221";
    private const int HearthfireCloakAssemblePowerCost = 1;
    private const string HearthfireCloakAssembleOptionalCost = "ASSEMBLE_ANY_POWER";
    private const string RabadonsDeathcapCardNo = "SFD·191/221";
    private const int RabadonsDeathcapAssemblePowerCost = 1;
    private const string RabadonsDeathcapAssembleOptionalCost = "ASSEMBLE_ANY_POWER";
    private const string ShurelyasRequiemCardNo = "SFD·192/221";
    private const int ShurelyasRequiemAssemblePowerCost = 1;
    private const string ShurelyasRequiemAssembleOptionalCost = "ASSEMBLE_ANY_POWER";
    private const string HextechGauntletCardNo = "UNL-188/219";
    private const int HextechGauntletAssembleManaCost = 3;
    private const int HextechGauntletAssemblePowerCost = 1;
    private const string HextechGauntletAssembleOptionalCost = "ASSEMBLE_3_ANY_POWER";
    private const string ShepherdsHeirloomCardNo = "UNL-158/219";
    private const int ShepherdsHeirloomAssembleExperienceCost = 1;
    private const string ShepherdsHeirloomAssembleOptionalCost = "SPEND_EXPERIENCE:1";

    private static readonly IReadOnlyDictionary<string, AssembleEquipmentProfile> Profiles =
        new Dictionary<string, AssembleEquipmentProfile>(StringComparer.Ordinal)
        {
            [LongSwordCardNo] = new(
                LongSwordCardNo,
                "长剑",
                LongSwordAssembleOptionalCost,
                "装配红色符能",
                RuneTrait.Red,
                LongSwordAssemblePowerCost,
                "payment resource action: recycle red rune for assemble cost"),
            [SoulSwordCardNo] = new(
                SoulSwordCardNo,
                "灵魂之剑",
                SoulSwordAssembleOptionalCost,
                "装配绿色符能",
                RuneTrait.Green,
                SoulSwordAssemblePowerCost,
                "payment resource action: recycle green rune for assemble cost"),
            [JaggedDirkCardNo] = new(
                JaggedDirkCardNo,
                "锯齿短匕",
                JaggedDirkAssembleOptionalCost,
                "装配红色符能",
                RuneTrait.Red,
                JaggedDirkAssemblePowerCost,
                "payment resource action: recycle red rune for assemble cost"),
            [RecurveBowCardNo] = new(
                RecurveBowCardNo,
                "反曲之弓",
                RecurveBowAssembleOptionalCost,
                "装配红色符能",
                RuneTrait.Red,
                RecurveBowAssemblePowerCost,
                "payment resource action: recycle red rune for assemble cost"),
            [ArionsFallCardNo] = new(
                ArionsFallCardNo,
                "阿瑞昂的陨落",
                ArionsFallAssembleOptionalCost,
                "装配红色符能",
                RuneTrait.Red,
                ArionsFallAssemblePowerCost,
                "payment resource action: recycle red rune for assemble cost"),
            [WitheredBattleaxeCardNo] = new(
                WitheredBattleaxeCardNo,
                "枯萎战斧",
                WitheredBattleaxeAssembleOptionalCost,
                "装配红色符能",
                RuneTrait.Red,
                WitheredBattleaxeAssemblePowerCost,
                "payment resource action: recycle red rune for assemble cost"),
            [BrutalizerCardNo] = new(
                BrutalizerCardNo,
                "残暴之力",
                BrutalizerAssembleOptionalCost,
                "装配绿色符能",
                RuneTrait.Green,
                BrutalizerAssemblePowerCost,
                "payment resource action: recycle green rune for assemble cost"),
            [GuardianAngelCardNo] = new(
                GuardianAngelCardNo,
                "守护天使",
                GuardianAngelAssembleOptionalCost,
                "装配绿色符能",
                RuneTrait.Green,
                GuardianAngelAssemblePowerCost,
                "payment resource action: recycle green rune for assemble cost"),
            [ClothArmorCardNo] = new(
                ClothArmorCardNo,
                "布甲",
                ClothArmorAssembleOptionalCost,
                "装配蓝色符能",
                RuneTrait.Blue,
                ClothArmorAssemblePowerCost,
                "payment resource action: recycle blue rune for assemble cost"),
            [HextechInfusedBulwarkCardNo] = new(
                HextechInfusedBulwarkCardNo,
                "海克斯注力刚壁",
                HextechInfusedBulwarkAssembleOptionalCost,
                "装配蓝色符能",
                RuneTrait.Blue,
                HextechInfusedBulwarkAssemblePowerCost,
                "payment resource action: recycle blue rune for assemble cost"),
            [WanderersGuidebookCardNo] = new(
                WanderersGuidebookCardNo,
                "云游图鉴",
                WanderersGuidebookAssembleOptionalCost,
                "装配蓝色符能",
                RuneTrait.Blue,
                WanderersGuidebookAssemblePowerCost,
                "payment resource action: recycle blue rune for assemble cost"),
            [ZDriveCardNo] = new(
                ZDriveCardNo,
                "Z型驱动",
                ZDriveAssembleOptionalCost,
                "装配蓝色符能",
                RuneTrait.Blue,
                ZDriveAssemblePowerCost,
                "payment resource action: recycle blue rune for assemble cost"),
            [SteraksGageCardNo] = new(
                SteraksGageCardNo,
                "斯特拉克的挑战护手",
                SteraksGageAssembleOptionalCost,
                "装配绿色符能",
                RuneTrait.Green,
                SteraksGageAssemblePowerCost,
                "payment resource action: recycle green rune for assemble cost"),
            [SvarshangSongCardNo] = new(
                SvarshangSongCardNo,
                "斯弗尔尚歌",
                SvarshangSongAssembleOptionalCost,
                "装配绿色符能",
                RuneTrait.Green,
                SvarshangSongAssemblePowerCost,
                "payment resource action: recycle green rune for assemble cost"),
            [DoransShieldCardNo] = new(
                DoransShieldCardNo,
                "多兰之盾",
                DoransShieldAssembleOptionalCost,
                "装配绿色符能",
                RuneTrait.Green,
                DoransShieldAssemblePowerCost,
                "payment resource action: recycle green rune for assemble cost"),
            [DoransRingCardNo] = new(
                DoransRingCardNo,
                "多兰之戒",
                DoransRingAssembleOptionalCost,
                "装配紫色符能",
                RuneTrait.Purple,
                DoransRingAssemblePowerCost,
                "payment resource action: recycle purple rune for assemble cost"),
            [DoransBladeCardNo] = new(
                DoransBladeCardNo,
                "多兰之刃",
                DoransBladeAssembleOptionalCost,
                "装配橙色符能",
                RuneTrait.Orange,
                DoransBladeAssemblePowerCost,
                "payment resource action: recycle orange rune for assemble cost"),
            [HexdrinkerCardNo] = new(
                HexdrinkerCardNo,
                "海克斯饮魔刀",
                HexdrinkerAssembleOptionalCost,
                "装配橙色符能",
                RuneTrait.Orange,
                HexdrinkerAssemblePowerCost,
                "payment resource action: recycle orange rune for assemble cost"),
            [WarmogsArmorCardNo] = new(
                WarmogsArmorCardNo,
                "狂徒铠甲",
                WarmogsArmorAssembleOptionalCost,
                "装配橙色符能",
                RuneTrait.Orange,
                WarmogsArmorAssemblePowerCost,
                "payment resource action: recycle orange rune for assemble cost"),
            [TrinityForceCardNo] = new(
                TrinityForceCardNo,
                "三相之力",
                TrinityForceAssembleOptionalCost,
                "装配橙色符能",
                RuneTrait.Orange,
                TrinityForceAssemblePowerCost,
                "payment resource action: recycle orange rune for assemble cost"),
            [HuntersMacheteCardNo] = new(
                HuntersMacheteCardNo,
                "猎人的宽刃刀",
                HuntersMacheteAssembleOptionalCost,
                "装配橙色符能",
                RuneTrait.Orange,
                HuntersMacheteAssemblePowerCost,
                "payment resource action: recycle orange rune for assemble cost"),
            [BoneClubCardNo] = new(
                BoneClubCardNo,
                "碎骨棒",
                BoneClubAssembleOptionalCost,
                "装配橙色符能",
                RuneTrait.Orange,
                BoneClubAssemblePowerCost,
                "payment resource action: recycle orange rune for assemble cost"),
            [BoneClubPromoCardNo] = new(
                BoneClubPromoCardNo,
                "碎骨棒",
                BoneClubPromoAssembleOptionalCost,
                "装配橙色符能",
                RuneTrait.Orange,
                BoneClubPromoAssemblePowerCost,
                "payment resource action: recycle orange rune for assemble cost"),
            [BootsOfSwiftnessCardNo] = new(
                BootsOfSwiftnessCardNo,
                "轻灵之靴",
                BootsOfSwiftnessAssembleOptionalCost,
                "装配紫色符能",
                RuneTrait.Purple,
                BootsOfSwiftnessAssemblePowerCost,
                "payment resource action: recycle purple rune for assemble cost"),
            [CullCardNo] = new(
                CullCardNo,
                "萃取",
                CullAssembleOptionalCost,
                "装配紫色符能",
                RuneTrait.Purple,
                CullAssemblePowerCost,
                "payment resource action: recycle purple rune for assemble cost"),
            [EdgeOfNightCardNo] = new(
                EdgeOfNightCardNo,
                "夜之锋刃",
                EdgeOfNightAssembleOptionalCost,
                "装配紫色符能",
                RuneTrait.Purple,
                EdgeOfNightAssemblePowerCost,
                "payment resource action: recycle purple rune for assemble cost"),
            [LastRitesCardNo] = new(
                LastRitesCardNo,
                "临终仪式",
                LastRitesAssembleOptionalCost,
                "装配紫色符能",
                RuneTrait.Purple,
                LastRitesAssemblePowerCost,
                "payment resource action: recycle purple rune for assemble cost",
                RequiredGraveyardRecycleCardCount: LastRitesRequiredGraveyardRecycleCardCount),
            [VanguardsEyeCardNo] = new(
                VanguardsEyeCardNo,
                "先锋之眼",
                VanguardsEyeAssembleOptionalCost,
                "装配黄色符能",
                RuneTrait.Yellow,
                VanguardsEyeAssemblePowerCost,
                "payment resource action: recycle yellow rune for assemble cost"),
            [BfSwordCardNo] = new(
                BfSwordCardNo,
                "暴风大剑",
                BfSwordAssembleOptionalCost,
                "装配黄色符能",
                RuneTrait.Yellow,
                BfSwordAssemblePowerCost,
                "payment resource action: recycle yellow rune for assemble cost"),
            [SacredShearsCardNo] = new(
                SacredShearsCardNo,
                "神圣剪刀",
                SacredShearsAssembleOptionalCost,
                "装配黄色符能",
                RuneTrait.Yellow,
                SacredShearsAssemblePowerCost,
                "payment resource action: recycle yellow rune for assemble cost"),
            [BladeOfTheRuinedKingCardNo] = new(
                BladeOfTheRuinedKingCardNo,
                "破败王者之刃",
                BladeOfTheRuinedKingAssembleOptionalCost,
                "装配黄色符能",
                RuneTrait.Yellow,
                BladeOfTheRuinedKingAssemblePowerCost,
                "payment resource action: recycle yellow rune for assemble cost",
                RequiresDestroyFriendlyUnitCost: true),
            [SpinningAxeCardNo] = new(
                SpinningAxeCardNo,
                "旋转飞斧",
                SpinningAxeAssembleOptionalCost,
                "装配任意符能",
                string.Empty,
                SpinningAxeAssemblePowerCost,
                "payment resource action: recycle any rune for assemble cost"),
            [HearthfireCloakCardNo] = new(
                HearthfireCloakCardNo,
                "炉火斗篷",
                HearthfireCloakAssembleOptionalCost,
                "装配任意符能",
                string.Empty,
                HearthfireCloakAssemblePowerCost,
                "payment resource action: recycle any rune for assemble cost"),
            [RabadonsDeathcapCardNo] = new(
                RabadonsDeathcapCardNo,
                "灭世者的死亡之冠",
                RabadonsDeathcapAssembleOptionalCost,
                "装配任意符能",
                string.Empty,
                RabadonsDeathcapAssemblePowerCost,
                "payment resource action: recycle any rune for assemble cost"),
            [ShurelyasRequiemCardNo] = new(
                ShurelyasRequiemCardNo,
                "舒瑞娅的安魂曲",
                ShurelyasRequiemAssembleOptionalCost,
                "装配任意符能",
                string.Empty,
                ShurelyasRequiemAssemblePowerCost,
                "payment resource action: recycle any rune for assemble cost"),
            [HextechGauntletCardNo] = new(
                HextechGauntletCardNo,
                "海克斯科技护手",
                HextechGauntletAssembleOptionalCost,
                "装配 3 法力 + 任意符能（按目标战力减费）",
                string.Empty,
                HextechGauntletAssemblePowerCost,
                "payment resource action: recycle any rune for assemble cost",
                ManaCost: HextechGauntletAssembleManaCost,
                ReduceManaCostByTargetPower: true),
            [ShepherdsHeirloomCardNo] = new(
                ShepherdsHeirloomCardNo,
                "牧人的传家宝",
                ShepherdsHeirloomAssembleOptionalCost,
                "消耗 1 经验",
                string.Empty,
                0,
                "experience assemble cost",
                ShepherdsHeirloomAssembleExperienceCost)
        };

    public static AssembleEquipmentProfile FallbackRepresentative => Profiles[LongSwordCardNo];

    public static bool HasImplementedRepresentative(string? cardNo)
    {
        return TryGet(cardNo, out _);
    }

    public static bool TryGet(string? cardNo, out AssembleEquipmentProfile profile)
    {
        profile = default!;
        return !string.IsNullOrWhiteSpace(cardNo)
            && Profiles.TryGetValue(cardNo.Trim(), out profile!);
    }
}
