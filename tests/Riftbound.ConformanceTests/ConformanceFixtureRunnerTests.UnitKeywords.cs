using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Engine;
using Riftbound.Contracts;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class ConformanceFixtureRunnerTests
{

    [Theory]
    [InlineData("p2-preflight-play-dockside-lurker-vanilla-unit.fixture.json", "P1-UNIT-DOCKSIDE-LURKER", 3)]
    [InlineData("p2-preflight-play-vanguard-sergeant-vanilla-unit.fixture.json", "P1-UNIT-VANGUARD-SERGEANT", 4)]
    [InlineData("p2-preflight-play-playful-imp-vanilla-unit.fixture.json", "P1-UNIT-PLAYFUL-IMP", 5)]
    [InlineData("p2-preflight-play-super-mech-vanilla-unit.fixture.json", "P1-UNIT-SUPER-MECH", 8)]
    [InlineData("p2-preflight-play-mountain-drake-vanilla-unit.fixture.json", "P1-UNIT-MOUNTAIN-DRAKE", 10)]
    [InlineData("p2-preflight-play-sfd-aphelios-vanilla-unit.fixture.json", "P1-UNIT-SFD-APHELIOS", 4)]
    [InlineData("p2-preflight-play-sfd-aphelios-promo-vanilla-unit.fixture.json", "P1-UNIT-SFD-APHELIOS-PROMO", 4)]
    [InlineData("p2-preflight-play-sfd-ahri-vanilla-unit.fixture.json", "P1-UNIT-SFD-AHRI", 3)]
    [InlineData("p2-preflight-play-sfd-ahri-promo-vanilla-unit.fixture.json", "P1-UNIT-SFD-AHRI-PROMO", 3)]
    [InlineData("p2-preflight-play-watchful-sentinel-vanilla-unit.fixture.json", "P1-UNIT-WATCHFUL-SENTINEL", 1)]
    [InlineData("p2-preflight-play-mechanical-trickster-vanilla-unit.fixture.json", "P1-UNIT-MECHANICAL-TRICKSTER", 4)]
    [InlineData("p2-preflight-play-kenken-vanilla-unit.fixture.json", "P1-UNIT-KENKEN", 6)]
    [InlineData("p2-preflight-play-shadow-guard-vanilla-unit.fixture.json", "P1-UNIT-SHADOW-GUARD", 5)]
    [InlineData("p2-preflight-play-bromain-lord-ambush-static.fixture.json", "P1-UNIT-BROMAIN-LORD", 5)]
    [InlineData("p2-preflight-play-katarina-face-down-static.fixture.json", "P1-UNIT-KATARINA-FACE-DOWN", 5)]
    [InlineData("p2-preflight-play-ivern-look-unit-static.fixture.json", "P1-UNIT-IVERN-LOOK", 4)]
    [InlineData("p2-preflight-play-ivern-alt-a-look-unit-static.fixture.json", "P1-UNIT-IVERN-A-LOOK", 4)]
    [InlineData("p2-preflight-play-ringblade-dancer-ambush-static.fixture.json", "P1-UNIT-RINGBLADE-DANCER", 3)]
    [InlineData("p2-preflight-play-khazix-lone-enemy-static.fixture.json", "P1-UNIT-UNL-143-KHAZIX", 4)]
    [InlineData("p2-preflight-play-khazix-alt-a-lone-enemy-static.fixture.json", "P1-UNIT-UNL-143A-KHAZIX", 4)]
    [InlineData("p2-preflight-play-diana-ambush-spell-power-static.fixture.json", "P1-UNIT-UNL-149-DIANA", 3)]
    [InlineData("p2-preflight-play-diana-alt-a-ambush-spell-power-static.fixture.json", "P1-UNIT-UNL-149A-DIANA", 3)]
    [InlineData("p2-preflight-play-vi-ambush-attack-stun-static.fixture.json", "P1-UNIT-UNL-176-VI", 5)]
    [InlineData("p2-preflight-play-vi-alt-a-ambush-attack-stun-static.fixture.json", "P1-UNIT-UNL-176A-VI", 5)]
    [InlineData("p2-preflight-play-nami-no-green-payment-static.fixture.json", "P1-UNIT-NAMI-NO-GREEN", 3)]
    [InlineData("p2-preflight-play-yi-no-level-reduction-static.fixture.json", "P1-UNIT-YI-NO-LEVEL", 12)]
    [InlineData("p2-preflight-play-yi-alt-a-no-level-reduction-static.fixture.json", "P1-UNIT-YI-A-NO-LEVEL", 12)]
    [InlineData("p2-preflight-play-hwei-move-discard-static.fixture.json", "P1-UNIT-HWEI-MOVE", 5)]
    [InlineData("p2-preflight-play-intimidating-challenger-move-static.fixture.json", "P1-UNIT-INTIMIDATING-CHALLENGER", 5)]
    [InlineData("p2-preflight-play-great-tooth-sea-beast-roam-static.fixture.json", "P1-UNIT-GREAT-TOOTH-SEA-BEAST", 6)]
    [InlineData("p2-preflight-play-lantern-sea-fiend-bounce-static.fixture.json", "P1-UNIT-LANTERN-SEA-FIEND", 5)]
    [InlineData("p2-preflight-play-meticulous-investigator-hand-static.fixture.json", "P1-UNIT-METICULOUS-INVESTIGATOR", 3)]
    [InlineData("p2-preflight-play-gatekeeper-maduli-move-static.fixture.json", "P1-UNIT-GATEKEEPER-MADULI", 6)]
    [InlineData("p2-preflight-play-syndra-spell-duel-echo-static.fixture.json", "P1-UNIT-SYNDRA-SPELL-DUEL", 6)]
    [InlineData("p2-preflight-play-zilean-token-copy-static.fixture.json", "P1-UNIT-ZILEAN-TOKEN-COPY", 5)]
    [InlineData("p2-preflight-play-mageseeker-patrol-move-tax-static.fixture.json", "P1-UNIT-MAGESEEKER-PATROL", 4)]
    [InlineData("p2-preflight-play-ashe-hand-banish-static.fixture.json", "P1-UNIT-ASHE-HAND-BANISH", 4)]
    [InlineData("p2-preflight-play-rift-herald-move-last-breath-static.fixture.json", "P1-UNIT-RIFT-HERALD", 7)]
    [InlineData("p2-preflight-play-rift-herald-alt-a-move-last-breath-static.fixture.json", "P1-UNIT-RIFT-HERALD-A", 7)]
    [InlineData("p2-preflight-play-baron-nashor-pit-static.fixture.json", "P1-UNIT-BARON-NASHOR", 12)]
    [InlineData("p2-preflight-play-baron-nashor-alt-a-pit-static.fixture.json", "P1-UNIT-BARON-NASHOR-A", 12)]
    [InlineData("p2-preflight-play-baron-nashor-promo-pit-static.fixture.json", "P1-UNIT-BARON-NASHOR-PROMO", 12)]
    [InlineData("p2-preflight-play-void-grasshopper-vanilla-unit.fixture.json", "P1-UNIT-VOID-GRASSHOPPER", 3)]
    [InlineData("p2-preflight-play-minotaur-reckoner-vanilla-unit.fixture.json", "P1-UNIT-MINOTAUR-RECKONER", 5)]
    [InlineData("p2-preflight-play-voidling-seedling-vanilla-unit.fixture.json", "P1-UNIT-VOIDLING-SEEDLING", 2)]
    [InlineData("p2-preflight-play-ascended-believer-no-spell-vanilla-unit.fixture.json", "P1-UNIT-ASCENDED-BELIEVER", 1)]
    [InlineData("p2-preflight-play-ascended-believer-four-plus-spell-unit.fixture.json", "P1-UNIT-ASCENDED-BELIEVER", 5)]
    [InlineData("p2-preflight-play-sly-salamander-no-experience-vanilla-unit.fixture.json", "P1-UNIT-SLY-SALAMANDER", 4)]
    [InlineData("p2-preflight-play-ogn-fiora-not-powerful-vanilla-unit.fixture.json", "P1-UNIT-OGN-FIORA", 4)]
    [InlineData("p2-preflight-play-balanced-disciple-no-other-power-vanilla-unit.fixture.json", "P1-UNIT-BALANCED-DISCIPLE", 3)]
    [InlineData("p2-preflight-play-crescent-guard-no-spell-vanilla-unit.fixture.json", "P1-UNIT-CRESCENT-GUARD", 4)]
    [InlineData("p2-preflight-play-silk-dancer-vanilla-unit.fixture.json", "P1-UNIT-SILK-DANCER", 3)]
    [InlineData("p2-preflight-play-skyhorn-shepherd-vanilla-unit.fixture.json", "P1-UNIT-SKYHORN-SHEPHERD", 3)]
    [InlineData("p2-preflight-play-yeti-brawler-vanilla-unit.fixture.json", "P1-UNIT-YETI-BRAWLER", 6)]
    [InlineData("p2-preflight-play-yashila-vanilla-unit.fixture.json", "P1-UNIT-YASHILA", 6)]
    [InlineData("p2-preflight-play-targon-seer-no-level-unit.fixture.json", "P1-UNIT-TARGON-SEER", 6)]
    [InlineData("p2-preflight-play-soul-shepherd-vanilla-unit.fixture.json", "P1-UNIT-SOUL-SHEPHERD", 3)]
    [InlineData("p2-preflight-play-savage-jawfish-vanilla-unit.fixture.json", "P1-UNIT-SAVAGE-JAWFISH", 5)]
    [InlineData("p2-preflight-play-sfd-020-draven-vanilla-unit.fixture.json", "P1-UNIT-SFD-020-DRAVEN", 4)]
    [InlineData("p2-preflight-play-sfd-renata-glasc-vanilla-unit.fixture.json", "P1-UNIT-SFD-RENATA", 4)]
    [InlineData("p2-preflight-play-sfd-renata-glasc-alt-a-vanilla-unit.fixture.json", "P1-UNIT-SFD-RENATA-A", 4)]
    [InlineData("p2-preflight-play-desert-plunderer-vanilla-unit.fixture.json", "P1-UNIT-DESERT-PLUNDERER", 5)]
    [InlineData("p2-preflight-play-xerath-activated-skill-unit.fixture.json", "P1-UNIT-XERATH", 5)]
    [InlineData("p2-preflight-play-dragon-soul-sage-activated-skill-unit.fixture.json", "P1-UNIT-DRAGON-SOUL-SAGE", 1)]
    [InlineData("p2-preflight-play-sfd-088-renata-glasc-activated-skill-unit.fixture.json", "P1-UNIT-SFD-088-RENATA", 4)]
    [InlineData("p2-preflight-play-sfd-088-renata-glasc-alt-a-activated-skill-unit.fixture.json", "P1-UNIT-SFD-088A-RENATA", 4)]
    [InlineData("p2-preflight-play-ogn-draven-score-static-zero.fixture.json", "P1-UNIT-OGN-028-DRAVEN", 3)]
    [InlineData("p2-preflight-play-sfd-fiora-one-on-one-vanilla-unit.fixture.json", "P1-UNIT-SFD-110-FIORA", 3)]
    [InlineData("p2-preflight-play-sfd-fiora-alt-a-one-on-one-vanilla-unit.fixture.json", "P1-UNIT-SFD-110A-FIORA", 3)]
    [InlineData("p2-preflight-play-sfd-141-irelia-spell-cost-static.fixture.json", "P1-UNIT-SFD-141-IRELIA", 4)]
    [InlineData("p2-preflight-play-sfd-141a-irelia-spell-cost-static.fixture.json", "P1-UNIT-SFD-141A-IRELIA", 4)]
    [InlineData("p2-preflight-play-dragon-caller-cost-static.fixture.json", "P1-UNIT-DRAGON-CALLER", 3)]
    [InlineData("p2-preflight-play-waterbender-vanilla-unit.fixture.json", "P1-UNIT-WATERBENDER", 2)]
    [InlineData("p2-preflight-play-wise-elder-no-boon-static.fixture.json", "P1-UNIT-WISE-ELDER", 4)]
    [InlineData("p2-preflight-play-eager-apprentice-spell-cost-static.fixture.json", "P1-UNIT-EAGER-APPRENTICE", 3)]
    [InlineData("p2-preflight-play-arena-service-crew-equipment-trigger-static.fixture.json", "P1-UNIT-ARENA-SERVICE-CREW", 3)]
    [InlineData("p2-preflight-play-poro-herder-no-poro-static.fixture.json", "P1-UNIT-PORO-HERDER", 3)]
    [InlineData("p2-preflight-play-ravenbloom-student-spell-trigger-static.fixture.json", "P1-UNIT-RAVENBLOOM-STUDENT", 2)]
    [InlineData("p2-preflight-play-bilgewater-bully-no-boon-roam-static.fixture.json", "P1-UNIT-BILGEWATER-BULLY", 6)]
    [InlineData("p2-preflight-play-ember-monk-standby-trigger-static.fixture.json", "P1-UNIT-EMBER-MONK", 4)]
    [InlineData("p2-preflight-play-hidden-tracker-follow-move-static.fixture.json", "P1-UNIT-HIDDEN-TRACKER", 4)]
    [InlineData("p2-preflight-play-undercover-agent-last-breath-static.fixture.json", "P1-UNIT-UNDERCOVER-AGENT", 5)]
    [InlineData("p2-preflight-play-traveling-merchant-move-trigger-static.fixture.json", "P1-UNIT-TRAVELING-MERCHANT", 2)]
    [InlineData("p2-preflight-play-ogn-kogmaw-last-breath-static.fixture.json", "P1-UNIT-OGN-KOGMAW", 1)]
    [InlineData("p2-preflight-play-ogn-jinx-discard-trigger-static.fixture.json", "P1-UNIT-OGN-JINX", 5)]
    [InlineData("p2-preflight-play-ogn-flame-chompers-discard-static.fixture.json", "P1-UNIT-OGN-FLAME-CHOMPERS", 3)]
    [InlineData("p2-preflight-play-ogn-molten-drake-active-entry-static.fixture.json", "P1-UNIT-OGN-MOLTEN-DRAKE", 8)]
    [InlineData("p2-preflight-play-brinhil-no-opponent-play-static.fixture.json", "P1-UNIT-BRINHIL", 5)]
    [InlineData("p2-preflight-play-ogn-tryndamere-overkill-score-static.fixture.json", "P1-UNIT-OGN-TRYNDAMERE", 8)]
    [InlineData("p2-preflight-play-ogn-ahri-hold-score-static.fixture.json", "P1-UNIT-OGN-AHRI-HOLD", 4)]
    [InlineData("p2-preflight-play-ogn-ahri-alt-a-hold-score-static.fixture.json", "P1-UNIT-OGN-AHRI-HOLD-A", 4)]
    [InlineData("p2-preflight-play-ogn-mageseeker-warden-static.fixture.json", "P1-UNIT-OGN-MAGESEEKER-WARDEN", 5)]
    [InlineData("p2-preflight-play-ogn-sona-ready-runes-static.fixture.json", "P1-UNIT-OGN-SONA-RUNES", 4)]
    [InlineData("p2-preflight-play-ogn-yasuo-attack-damage-static.fixture.json", "P1-UNIT-OGN-YASUO-ATTACK", 6)]
    [InlineData("p2-preflight-play-ogn-yasuo-alt-a-attack-damage-static.fixture.json", "P1-UNIT-OGN-YASUO-ATTACK-A", 6)]
    [InlineData("p2-preflight-play-ogn-leona-score-static.fixture.json", "P1-UNIT-OGN-LEONA-SCORE", 6)]
    [InlineData("p2-preflight-play-ogn-leona-alt-a-score-static.fixture.json", "P1-UNIT-OGN-LEONA-SCORE-A", 6)]
    [InlineData("p2-preflight-play-ogn-ahri-combat-power-static.fixture.json", "P1-UNIT-OGN-AHRI-COMBAT", 3)]
    [InlineData("p2-preflight-play-ogn-viktor-opponent-turn-token-static.fixture.json", "P1-UNIT-OGN-VIKTOR-OPPONENT-TURN", 3)]
    [InlineData("p2-preflight-play-ogn-viktor-alt-a-opponent-turn-token-static.fixture.json", "P1-UNIT-OGN-VIKTOR-OPPONENT-TURN-A", 3)]
    [InlineData("p2-preflight-play-ogn-ahri-alt-a-combat-power-static.fixture.json", "P1-UNIT-OGN-AHRI-COMBAT-A", 3)]
    [InlineData("p2-preflight-play-ogn-malzahar-tap-rune-static.fixture.json", "P1-UNIT-OGN-MALZAHAR-TAP", 3)]
    [InlineData("p2-preflight-play-ogn-udyr-boon-stance-static.fixture.json", "P1-UNIT-OGN-UDYR-BOON", 6)]
    [InlineData("p2-preflight-play-ogn-twisted-fate-rune-reveal-static.fixture.json", "P1-UNIT-OGN-TWISTED-FATE", 4)]
    [InlineData("p2-preflight-play-ogn-peak-guardian-boon-static.fixture.json", "P1-UNIT-OGN-PEAK-GUARDIAN", 5)]
    [InlineData("p2-preflight-play-ogn-viktor-destroyed-unit-token-static.fixture.json", "P1-UNIT-OGN-VIKTOR-DESTROYED-UNIT", 4)]
    [InlineData("p2-preflight-play-ogn-viktor-alt-a-destroyed-unit-token-static.fixture.json", "P1-UNIT-OGN-VIKTOR-DESTROYED-UNIT-A", 4)]
    [InlineData("p2-preflight-play-ogs-annie-damage-plus-static.fixture.json", "P1-UNIT-OGS-ANNIE-DAMAGE-PLUS", 4)]
    [InlineData("p2-preflight-play-ogs-yi-eight-runes-static.fixture.json", "P1-UNIT-OGS-YI-EIGHT-RUNES", 4)]
    [InlineData("p2-preflight-play-ogs-lux-high-cost-spell-static.fixture.json", "P1-UNIT-OGS-LUX-HIGH-COST-SPELL", 5)]
    [InlineData("p2-preflight-play-sfd-020a-draven-vanilla-unit.fixture.json", "P1-UNIT-SFD-020A-DRAVEN", 4)]
    [InlineData("p2-preflight-play-skateboard-pro-no-equipment-static.fixture.json", "P1-UNIT-SKATEBOARD-PRO", 4)]
    [InlineData("p2-preflight-play-alley-thief-no-optional-equipment-static.fixture.json", "P1-UNIT-ALLEY-THIEF", 3)]
    [InlineData("p2-preflight-play-fervid-fan-defense-trigger-static.fixture.json", "P1-UNIT-FERVID-FAN", 2)]
    [InlineData("p2-preflight-play-treasure-hunter-move-gold-static.fixture.json", "P1-UNIT-TREASURE-HUNTER", 1)]
    [InlineData("p2-preflight-play-honest-broker-last-breath-gold-static.fixture.json", "P1-UNIT-HONEST-BROKER", 2)]
    [InlineData("p2-preflight-play-icevale-archer-attack-payment-static.fixture.json", "P1-UNIT-ICEVALE-ARCHER", 2)]
    [InlineData("p2-preflight-play-black-market-broker-face-down-static.fixture.json", "P1-UNIT-BLACK-MARKET-BROKER", 3)]
    [InlineData("p2-preflight-play-corrupt-enforcer-move-discard-static.fixture.json", "P1-UNIT-CORRUPT-ENFORCER", 4)]
    [InlineData("p2-preflight-play-jar-meddarda-spell-target-static.fixture.json", "P1-UNIT-JAR-MEDDARDA", 5)]
    [InlineData("p2-preflight-play-prominent-patron-hold-gold-static.fixture.json", "P1-UNIT-PROMINENT-PATRON", 5)]
    [InlineData("p2-preflight-play-royal-attendant-legend-ready-static.fixture.json", "P1-UNIT-ROYAL-ATTENDANT", 4)]
    [InlineData("p2-preflight-play-sfd-049-aphelios-weapon-trigger-static.fixture.json", "P1-UNIT-SFD-049-APHELIOS", 4)]
    [InlineData("p2-preflight-play-sfd-bard-no-optional-legend-static.fixture.json", "P1-UNIT-SFD-BARD", 4)]
    [InlineData("p2-preflight-play-big-swindler-gold-choice-static.fixture.json", "P1-UNIT-BIG-SWINDLER", 3)]
    [InlineData("p2-preflight-play-sfd-jayce-no-optional-equipment-static.fixture.json", "P1-UNIT-SFD-JAYCE", 4)]
    [InlineData("p2-preflight-play-zaunite-thug-no-optional-equipment-static.fixture.json", "P1-UNIT-ZAUNITE-THUG", 3)]
    [InlineData("p2-preflight-play-glasc-bartender-last-breath-static.fixture.json", "P1-UNIT-GLASC-BARTENDER", 5)]
    [InlineData("p2-preflight-play-sfd-170-reksai-attack-reveal-static.fixture.json", "P1-UNIT-SFD-170-REKSAI", 5)]
    [InlineData("p2-preflight-play-sfd-170a-reksai-attack-reveal-static.fixture.json", "P1-UNIT-SFD-170A-REKSAI", 5)]
    [InlineData("p2-preflight-play-sfd-soraka-damage-redirection-static.fixture.json", "P1-UNIT-SFD-SORAKA", 4)]
    [InlineData("p2-preflight-play-gai-beast-entry-power-static.fixture.json", "P1-UNIT-GAI-BEAST", 5)]
    [InlineData("p2-preflight-play-sfd-180-fiora-powerful-ready-static.fixture.json", "P1-UNIT-SFD-180-FIORA", 3)]
    [InlineData("p2-preflight-play-sfd-180a-fiora-powerful-ready-static.fixture.json", "P1-UNIT-SFD-180A-FIORA", 3)]
    [InlineData("p2-preflight-play-sfd-239-soraka-damage-redirection-static.fixture.json", "P1-UNIT-SFD-239-SORAKA", 4)]
    [InlineData("p2-preflight-play-sfd-239-promo-soraka-damage-redirection-static.fixture.json", "P1-UNIT-SFD-239-PROMO-SORAKA", 4)]
    [InlineData("p2-preflight-play-sfd-228-bard-no-optional-legend-static.fixture.json", "P1-UNIT-SFD-228-BARD", 4)]
    [InlineData("p2-preflight-play-sfd-228-promo-bard-no-optional-legend-static.fixture.json", "P1-UNIT-SFD-228-PROMO-BARD", 4)]
    [InlineData("p2-preflight-play-sfd-082-ezreal-combat-damage-static.fixture.json", "P1-UNIT-SFD-082-EZREAL", 3)]
    [InlineData("p2-preflight-play-sfd-082a-ezreal-combat-damage-static.fixture.json", "P1-UNIT-SFD-082A-EZREAL", 3)]
    [InlineData("p2-preflight-play-sfd-082b-ezreal-combat-damage-static.fixture.json", "P1-UNIT-SFD-082B-EZREAL", 3)]
    [InlineData("p2-preflight-play-ogs-014-lux-tap-resource-static.fixture.json", "P1-UNIT-OGS-014-LUX", 2)]
    [InlineData("p2-preflight-play-demacia-envoy-experience-static.fixture.json", "P1-UNIT-DEMACIA-ENVOY", 2)]
    [InlineData("p2-preflight-play-fate-weaver-look-spell-static.fixture.json", "P1-UNIT-FATE-WEAVER", 4)]
    [InlineData("p2-preflight-play-theatrical-visionary-last-breath-static.fixture.json", "P1-UNIT-THEATRICAL-VISIONARY", 4)]
    [InlineData("p2-preflight-play-ruined-shark-cannon-last-breath-static.fixture.json", "P1-UNIT-RUINED-SHARK-CANNON", 6)]
    [InlineData("p2-preflight-play-wildclaw-shaman-no-boon-consume-static.fixture.json", "P1-UNIT-WILDCLAW-SHAMAN", 3)]
    [InlineData("p2-preflight-play-ogn-jinx-alt-a-discard-trigger-static.fixture.json", "P1-UNIT-OGN-JINX-A", 5)]
    [InlineData("p2-preflight-play-albus-ferros-no-boon-call-rune-static.fixture.json", "P1-UNIT-ALBUS-FERROS", 3)]
    [InlineData("p2-preflight-play-dunehorn-beast-low-hand-active-static.fixture.json", "P1-UNIT-DUNEHORN-BEAST", 7)]
    [InlineData("p2-preflight-play-apprentice-blacksmith-move-reveal-static.fixture.json", "P1-UNIT-APPRENTICE-BLACKSMITH", 2)]
    [InlineData("p2-preflight-play-mountain-ape-elder-boon-ready-static.fixture.json", "P1-UNIT-MOUNTAIN-APE-ELDER", 5)]
    public async Task CoreRuleEnginePlaysVanillaSourceUnit(string fixtureFileName, string sourceObjectId, int expectedPower)
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", fixtureFileName),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal([sourceObjectId], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(expectedPower, result.FinalState.CardObjects[sourceObjectId].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects[sourceObjectId].Tags);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysBalancedDiscipleOtherPowerDraw()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-balanced-disciple-other-power-draw.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-DRAWN-001"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(3, result.FinalState.CardObjects["P1-UNIT-BALANCED-DISCIPLE"].Power);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysCrescentGuardReadyAfterSpellPayment()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-crescent-guard-spell-ready-payment.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-UNIT-CRESCENT-GUARD"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Hand);
        Assert.False(result.FinalState.CardObjects["P1-UNIT-CRESCENT-GUARD"].IsExhausted);
        Assert.Equal(["PLAYED_SPELL_THIS_TURN:P1"], result.FinalState.UntilEndOfTurnEffects);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsCrescentGuardReadyPaymentWithoutSpellMemory()
    {
        var state = new MatchState(
            "crescent-guard-no-spell-ready-reject-room",
            0,
            260330537,
            "P1",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(4, 0, new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [RuneTrait.Purple] = 1
                }),
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with { Hand = ["P1-UNIT-CRESCENT-GUARD"] },
                ["P2"] = PlayerZones.Empty
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-CRESCENT-GUARD"] = new(
                    "P1-UNIT-CRESCENT-GUARD",
                    cardNo: "UNL-122/219",
                    ownerId: "P1",
                    controllerId: "P1")
            });

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-crescent-guard-no-spell-ready-reject", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-UNIT-CRESCENT-GUARD",
                "UNL-122/219",
                [],
                OptionalCosts: ["SPEND_POWER:purple:1"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCardBehavior, result.ErrorCode);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(["P1-UNIT-CRESCENT-GUARD"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(new RunePool(4, 0, new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [RuneTrait.Purple] = 1
        }), result.State.RunePools["P1"]);
    }

    [Fact]
    public async Task CoreRuleEngineGrantsOgnFioraKeywordsWhenBoonMakesPowerful()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-ogn-fiora-powerful-boon-keywords.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(5, result.FinalState.CardObjects["P1-UNIT-OGN-FIORA"].Power);
        Assert.Equal(
            [CardObjectTags.UnitCard, CardCombatKeywordNames.Steadfast, CardObjectTags.Boon, CardObjectTags.Spellshield, CardCombatKeywordNames.Roam],
            result.FinalState.CardObjects["P1-UNIT-OGN-FIORA"].Tags);
    }

    [Theory]
    [InlineData("p2-preflight-play-demacia-envoy-experience-static.fixture.json", 1)]
    [InlineData("p2-preflight-play-spring-messenger-experience-static.fixture.json", 2)]
    [InlineData("p2-preflight-play-shepherds-heirloom-weapon-equipment.fixture.json", 1)]
    public async Task P4FixedExperienceGainOnPlayUpdatesControllerExperience(
        string fixtureFileName,
        int expectedExperience)
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", fixtureFileName),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(expectedExperience, result.FinalState.PlayerExperience["P1"]);
        Assert.Equal(0, result.FinalState.PlayerExperience["P2"]);
    }

    [Fact]
    public async Task P4DynamicExperienceGainOnPlayCountsFriendlyFieldUnits()
    {
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("UNL-157/219", out var sternSergeantDefinition));
        Assert.Equal(1, sternSergeantDefinition.GainExperienceOnPlayPerFriendlyFieldUnit);

        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-stern-sergeant-dynamic-experience.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(3, result.FinalState.PlayerExperience["P1"]);
        Assert.Equal(0, result.FinalState.PlayerExperience["P2"]);
        Assert.Contains("EXPERIENCE_GAINED", result.EventKinds);
    }

    [Fact]
    public async Task P4ExperienceOptionalCostReducesManaAndSpendsExperience()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-poppy-spend-experience-reduce-cost.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.PlayerExperience["P1"]);
        Assert.Equal(new RunePool(0, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-UNL-178-POPPY"], result.FinalState.PlayerZones["P1"].Base);
        var costPaidEvent = Assert.Single(result.Events, gameEvent => gameEvent.Kind == "COST_PAID");
        Assert.StartsWith("PLAY_CARD:", Assert.IsType<string>(costPaidEvent.Payload["paymentId"]), StringComparison.Ordinal);
        Assert.Equal("PLAY_CARD", costPaidEvent.Payload["paymentWindow"]);
        Assert.Equal("P1-UNIT-UNL-178-POPPY", costPaidEvent.Payload["sourceObjectId"]);
        Assert.Equal("POPPY_AMBUSH_BARRIER_NO_EXPERIENCE_STATIC", costPaidEvent.Payload["reason"]);
        Assert.Equal(6, costPaidEvent.Payload["baseManaCost"]);
        Assert.Equal(3, costPaidEvent.Payload["totalManaCost"]);
        Assert.Equal(3, costPaidEvent.Payload["experienceCost"]);
        Assert.Equal(3, costPaidEvent.Payload["optionalCostManaReduction"]);
        Assert.Equal(0, costPaidEvent.Payload["remainingMana"]);
        Assert.Equal(0, costPaidEvent.Payload["remainingExperience"]);
        Assert.Equal(["SPEND_EXPERIENCE:3"], Assert.IsType<string[]>(costPaidEvent.Payload["optionalCosts"]));
    }

    [Fact]
    public async Task CoreRuleEngineRejectsPoppyExperienceOptionalCostWhenExperienceIsInsufficient()
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerExperience = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 2,
                ["P2"] = 0
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-UNL-178-POPPY"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p4-poppy-insufficient-experience", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-UNIT-UNL-178-POPPY",
                "UNL-178/219",
                [],
                OptionalCosts: ["SPEND_EXPERIENCE:3"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InsufficientCost, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(2, result.State.PlayerExperience["P1"]);
        Assert.Equal(new RunePool(3, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-UNL-178-POPPY"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    [Theory]
    [InlineData(3, "P1-UNIT-DOCKSIDE-LURKER", "OGN·175/298", "P1-BASE-DOCKSIDE-LURKER-TARGET-001")]
    [InlineData(4, "P1-UNIT-VANGUARD-SERGEANT", "OGN·219/298", "P1-BASE-VANGUARD-SERGEANT-TARGET-001")]
    [InlineData(5, "P1-UNIT-PLAYFUL-IMP", "OGN·049/298", "P1-BASE-PLAYFUL-IMP-TARGET-001")]
    [InlineData(7, "P1-UNIT-SUPER-MECH", "OGN·088/298", "P1-BASE-SUPER-MECH-TARGET-001")]
    [InlineData(9, "P1-UNIT-MOUNTAIN-DRAKE", "OGN·142/298", "P1-BASE-MOUNTAIN-DRAKE-TARGET-001")]
    [InlineData(4, "P1-UNIT-SFD-APHELIOS", "SFD·224/221", "P1-BASE-SFD-APHELIOS-TARGET-001")]
    [InlineData(4, "P1-UNIT-SFD-APHELIOS-PROMO", "SFD·224*/221", "P1-BASE-SFD-APHELIOS-PROMO-TARGET-001")]
    [InlineData(3, "P1-UNIT-SFD-AHRI", "SFD·227/221", "P1-BASE-SFD-AHRI-TARGET-001")]
    [InlineData(3, "P1-UNIT-SFD-AHRI-PROMO", "SFD·227*/221", "P1-BASE-SFD-AHRI-PROMO-TARGET-001")]
    [InlineData(2, "P1-UNIT-WATCHFUL-SENTINEL", "OGN·096/298", "P1-BASE-WATCHFUL-SENTINEL-TARGET-001")]
    [InlineData(5, "P1-UNIT-MECHANICAL-TRICKSTER", "OGN·239/298", "P1-BASE-MECHANICAL-TRICKSTER-TARGET-001")]
    [InlineData(6, "P1-UNIT-KENKEN", "UNL-035/219", "P1-BASE-KENKEN-TARGET-001")]
    [InlineData(4, "P1-UNIT-SHADOW-GUARD", "UNL-037/219", "P1-BASE-SHADOW-GUARD-TARGET-001")]
    [InlineData(5, "P1-UNIT-BROMAIN-LORD", "UNL-012/219", "P1-BASE-BROMAIN-LORD-TARGET-001")]
    [InlineData(5, "P1-UNIT-KATARINA-FACE-DOWN", "UNL-023/219", "P1-BASE-KATARINA-FACE-DOWN-TARGET-001")]
    [InlineData(5, "P1-UNIT-IVERN-LOOK", "UNL-051/219", "P1-BASE-IVERN-LOOK-TARGET-001")]
    [InlineData(5, "P1-UNIT-IVERN-A-LOOK", "UNL-051a/219", "P1-BASE-IVERN-A-LOOK-TARGET-001")]
    [InlineData(3, "P1-UNIT-RINGBLADE-DANCER", "UNL-071/219", "P1-BASE-RINGBLADE-DANCER-TARGET-001")]
    [InlineData(4, "P1-UNIT-UNL-143-KHAZIX", "UNL-143/219", "P1-BASE-UNL-143-KHAZIX-TARGET-001")]
    [InlineData(4, "P1-UNIT-UNL-143A-KHAZIX", "UNL-143a/219", "P1-BASE-UNL-143A-KHAZIX-TARGET-001")]
    [InlineData(4, "P1-UNIT-UNL-149-DIANA", "UNL-149/219", "P1-BASE-UNL-149-DIANA-TARGET-001")]
    [InlineData(4, "P1-UNIT-UNL-149A-DIANA", "UNL-149a/219", "P1-BASE-UNL-149A-DIANA-TARGET-001")]
    [InlineData(5, "P1-UNIT-UNL-176-VI", "UNL-176/219", "P1-BASE-UNL-176-VI-TARGET-001")]
    [InlineData(5, "P1-UNIT-UNL-176A-VI", "UNL-176a/219", "P1-BASE-UNL-176A-VI-TARGET-001")]
    [InlineData(3, "P1-UNIT-NAMI-NO-GREEN", "UNL-052/219", "P1-BASE-NAMI-NO-GREEN-TARGET-001")]
    [InlineData(12, "P1-UNIT-YI-NO-LEVEL", "UNL-059/219", "P1-BASE-YI-NO-LEVEL-TARGET-001")]
    [InlineData(12, "P1-UNIT-YI-A-NO-LEVEL", "UNL-059a/219", "P1-BASE-YI-A-NO-LEVEL-TARGET-001")]
    [InlineData(5, "P1-UNIT-HWEI-MOVE", "UNL-080/219", "P1-BASE-HWEI-MOVE-TARGET-001")]
    [InlineData(5, "P1-UNIT-INTIMIDATING-CHALLENGER", "UNL-105/219", "P1-BASE-INTIMIDATING-CHALLENGER-TARGET-001")]
    [InlineData(6, "P1-UNIT-GREAT-TOOTH-SEA-BEAST", "UNL-126/219", "P1-BASE-GREAT-TOOTH-SEA-BEAST-TARGET-001")]
    [InlineData(5, "P1-UNIT-LANTERN-SEA-FIEND", "UNL-132/219", "P1-BASE-LANTERN-SEA-FIEND-TARGET-001")]
    [InlineData(3, "P1-UNIT-METICULOUS-INVESTIGATOR", "UNL-135/219", "P1-BASE-METICULOUS-INVESTIGATOR-TARGET-001")]
    [InlineData(7, "P1-UNIT-GATEKEEPER-MADULI", "UNL-144/219", "P1-BASE-GATEKEEPER-MADULI-TARGET-001")]
    [InlineData(6, "P1-UNIT-SYNDRA-SPELL-DUEL", "UNL-146/219", "P1-BASE-SYNDRA-SPELL-DUEL-TARGET-001")]
    [InlineData(5, "P1-UNIT-ZILEAN-TOKEN-COPY", "UNL-086/219", "P1-BASE-ZILEAN-TARGET-001")]
    [InlineData(4, "P1-UNIT-MAGESEEKER-PATROL", "UNL-163/219", "P1-BASE-MAGESEEKER-PATROL-TARGET-001")]
    [InlineData(5, "P1-UNIT-ASHE-HAND-BANISH", "UNL-169/219", "P1-BASE-ASHE-HAND-BANISH-TARGET-001")]
    [InlineData(8, "P1-UNIT-RIFT-HERALD", "UNL-179/219", "P1-BASE-RIFT-HERALD-TARGET-001")]
    [InlineData(8, "P1-UNIT-RIFT-HERALD-A", "UNL-179a/219", "P1-BASE-RIFT-HERALD-A-TARGET-001")]
    [InlineData(10, "P1-UNIT-BARON-NASHOR", "UNL-147/219", "P1-BASE-BARON-NASHOR-TARGET-001")]
    [InlineData(10, "P1-UNIT-BARON-NASHOR-A", "UNL-147a/219", "P1-BASE-BARON-NASHOR-A-TARGET-001")]
    [InlineData(10, "P1-UNIT-BARON-NASHOR-PROMO", "UNL-238/219", "P1-BASE-BARON-NASHOR-PROMO-TARGET-001")]
    [InlineData(3, "P1-UNIT-VOID-GRASSHOPPER", "SFD·010/221", "P1-BASE-VOID-GRASSHOPPER-TARGET-001")]
    [InlineData(5, "P1-UNIT-MINOTAUR-RECKONER", "SFD·014/221", "P1-BASE-MINOTAUR-RECKONER-TARGET-001")]
    [InlineData(2, "P1-UNIT-VOIDLING-SEEDLING", "SFD·018/221", "P1-BASE-VOIDLING-SEEDLING-TARGET-001")]
    [InlineData(3, "P1-UNIT-ASCENDED-BELIEVER", "UNL-004/219", "P1-BASE-ASCENDED-BELIEVER-TARGET-001")]
    [InlineData(4, "P1-UNIT-SLY-SALAMANDER", "UNL-108/219", "P1-BASE-SLY-SALAMANDER-TARGET-001")]
    [InlineData(4, "P1-UNIT-OGN-FIORA", "OGN·232/298", "P1-BASE-OGN-FIORA-TARGET-001")]
    [InlineData(3, "P1-UNIT-BALANCED-DISCIPLE", "UNL-097/219", "P1-BASE-BALANCED-DISCIPLE-TARGET-001")]
    [InlineData(4, "P1-UNIT-CRESCENT-GUARD", "UNL-122/219", "P1-BASE-CRESCENT-GUARD-TARGET-001")]
    [InlineData(3, "P1-UNIT-SILK-DANCER", "SFD·038/221", "P1-BASE-SILK-DANCER-TARGET-001")]
    [InlineData(4, "P1-UNIT-SKYHORN-SHEPHERD", "SFD·048/221", "P1-BASE-SKYHORN-SHEPHERD-TARGET-001")]
    [InlineData(6, "P1-UNIT-YETI-BRAWLER", "UNL-018/219", "P1-BASE-YETI-BRAWLER-TARGET-001")]
    [InlineData(7, "P1-UNIT-YASHILA", "UNL-050/219", "P1-BASE-YASHILA-TARGET-001")]
    [InlineData(6, "P1-UNIT-TARGON-SEER", "UNL-098/219", "P1-BASE-TARGON-SEER-TARGET-001")]
    [InlineData(5, "P1-UNIT-SOUL-SHEPHERD", "UNL-077/219", "P1-BASE-SOUL-SHEPHERD-TARGET-001")]
    [InlineData(5, "P1-UNIT-SAVAGE-JAWFISH", "UNL-129/219", "P1-BASE-SAVAGE-JAWFISH-TARGET-001")]
    [InlineData(4, "P1-UNIT-SFD-020-DRAVEN", "SFD·020/221", "P1-BASE-SFD-020-DRAVEN-TARGET-001")]
    [InlineData(4, "P1-UNIT-SFD-020A-DRAVEN", "SFD·020a/221", "P1-BASE-SFD-020A-DRAVEN-TARGET-001")]
    [InlineData(4, "P1-UNIT-SKATEBOARD-PRO", "SFD·072/221", "P1-BASE-SKATEBOARD-PRO-TARGET-001")]
    [InlineData(3, "P1-UNIT-ALLEY-THIEF", "SFD·074/221", "P1-BASE-ALLEY-THIEF-TARGET-001")]
    [InlineData(2, "P1-UNIT-FERVID-FAN", "SFD·128/221", "P1-BASE-FERVID-FAN-TARGET-001")]
    [InlineData(2, "P1-UNIT-TREASURE-HUNTER", "SFD·130/221", "P1-BASE-TREASURE-HUNTER-TARGET-001")]
    [InlineData(2, "P1-UNIT-HONEST-BROKER", "SFD·155/221", "P1-BASE-HONEST-BROKER-TARGET-001")]
    [InlineData(2, "P1-UNIT-ICEVALE-ARCHER", "UNL-065/219", "P1-BASE-ICEVALE-ARCHER-TARGET-001")]
    [InlineData(3, "P1-UNIT-BLACK-MARKET-BROKER", "SFD·121/221", "P1-BASE-BLACK-MARKET-BROKER-TARGET-001")]
    [InlineData(3, "P1-UNIT-CORRUPT-ENFORCER", "SFD·123/221", "P1-BASE-CORRUPT-ENFORCER-TARGET-001")]
    [InlineData(5, "P1-UNIT-JAR-MEDDARDA", "SFD·142/221", "P1-BASE-JAR-MEDDARDA-TARGET-001")]
    [InlineData(6, "P1-UNIT-PROMINENT-PATRON", "SFD·152/221", "P1-BASE-PROMINENT-PATRON-TARGET-001")]
    [InlineData(3, "P1-UNIT-ROYAL-ATTENDANT", "SFD·039/221", "P1-BASE-ROYAL-ATTENDANT-TARGET-001")]
    [InlineData(4, "P1-UNIT-SFD-049-APHELIOS", "SFD·049/221", "P1-BASE-SFD-049-APHELIOS-TARGET-001")]
    [InlineData(5, "P1-UNIT-SFD-058-ORNN", "SFD·058/221", "P1-BASE-SFD-058-ORNN-TARGET-001")]
    [InlineData(5, "P1-UNIT-SFD-058A-ORNN", "SFD·058a/221", "P1-BASE-SFD-058A-ORNN-TARGET-001")]
    [InlineData(4, "P1-UNIT-SFD-BARD", "SFD·079/221", "P1-BASE-SFD-BARD-TARGET-001")]
    [InlineData(3, "P1-UNIT-BIG-SWINDLER", "SFD·081/221", "P1-BASE-BIG-SWINDLER-TARGET-001")]
    [InlineData(4, "P1-UNIT-SFD-JAYCE", "SFD·084/221", "P1-BASE-SFD-JAYCE-TARGET-001")]
    [InlineData(3, "P1-UNIT-ZAUNITE-THUG", "SFD·160/221", "P1-BASE-ZAUNITE-THUG-TARGET-001")]
    [InlineData(5, "P1-UNIT-GLASC-BARTENDER", "SFD·165/221", "P1-BASE-GLASC-BARTENDER-TARGET-001")]
    [InlineData(5, "P1-UNIT-SFD-170-REKSAI", "SFD·170/221", "P1-BASE-SFD-170-REKSAI-TARGET-001")]
    [InlineData(5, "P1-UNIT-SFD-170A-REKSAI", "SFD·170a/221", "P1-BASE-SFD-170A-REKSAI-TARGET-001")]
    [InlineData(4, "P1-UNIT-SFD-SORAKA", "SFD·173/221", "P1-BASE-SFD-SORAKA-TARGET-001")]
    [InlineData(6, "P1-UNIT-GAI-BEAST", "SFD·175/221", "P1-BASE-GAI-BEAST-TARGET-001")]
    [InlineData(3, "P1-UNIT-SFD-180-FIORA", "SFD·180/221", "P1-BASE-SFD-180-FIORA-TARGET-001")]
    [InlineData(3, "P1-UNIT-SFD-180A-FIORA", "SFD·180a/221", "P1-BASE-SFD-180A-FIORA-TARGET-001")]
    [InlineData(4, "P1-UNIT-SFD-239-SORAKA", "SFD·239/221", "P1-BASE-SFD-239-SORAKA-TARGET-001")]
    [InlineData(4, "P1-UNIT-SFD-239-PROMO-SORAKA", "SFD·239*/221", "P1-BASE-SFD-239-PROMO-SORAKA-TARGET-001")]
    [InlineData(4, "P1-UNIT-SFD-228-BARD", "SFD·228/221", "P1-BASE-SFD-228-BARD-TARGET-001")]
    [InlineData(4, "P1-UNIT-SFD-228-PROMO-BARD", "SFD·228*/221", "P1-BASE-SFD-228-PROMO-BARD-TARGET-001")]
    [InlineData(4, "P1-UNIT-SFD-082-EZREAL", "SFD·082/221", "P1-BASE-SFD-082-EZREAL-TARGET-001")]
    [InlineData(4, "P1-UNIT-SFD-082A-EZREAL", "SFD·082a/221", "P1-BASE-SFD-082A-EZREAL-TARGET-001")]
    [InlineData(4, "P1-UNIT-SFD-082B-EZREAL", "SFD·082b/221·P", "P1-BASE-SFD-082B-EZREAL-TARGET-001")]
    [InlineData(4, "P1-UNIT-OGS-014-LUX", "OGS·014/024", "P1-BASE-OGS-014-LUX-TARGET-001")]
    [InlineData(2, "P1-UNIT-DEMACIA-ENVOY", "UNL-092/219", "P1-BASE-DEMACIA-ENVOY-TARGET-001")]
    [InlineData(5, "P1-UNIT-FATE-WEAVER", "UNL-064/219", "P1-BASE-FATE-WEAVER-TARGET-001")]
    [InlineData(4, "P1-UNIT-THEATRICAL-VISIONARY", "UNL-062/219", "P1-BASE-THEATRICAL-VISIONARY-TARGET-001")]
    [InlineData(6, "P1-UNIT-RUINED-SHARK-CANNON", "UNL-067/219", "P1-BASE-RUINED-SHARK-CANNON-TARGET-001")]
    [InlineData(4, "P1-UNIT-SFD-RENATA", "SFD·171/221", "P1-BASE-SFD-RENATA-TARGET-001")]
    [InlineData(4, "P1-UNIT-SFD-RENATA-A", "SFD·171a/221", "P1-BASE-SFD-RENATA-A-TARGET-001")]
    [InlineData(6, "P1-UNIT-DESERT-PLUNDERER", "SFD·105/221", "P1-BASE-DESERT-PLUNDERER-TARGET-001")]
    [InlineData(5, "P1-UNIT-XERATH", "UNL-026/219", "P1-BASE-XERATH-TARGET-001")]
    [InlineData(2, "P1-UNIT-DRAGON-SOUL-SAGE", "UNL-093/219", "P1-BASE-DRAGON-SOUL-SAGE-TARGET-001")]
    [InlineData(5, "P1-UNIT-SFD-088-RENATA", "SFD·088/221", "P1-BASE-SFD-088-RENATA-TARGET-001")]
    [InlineData(5, "P1-UNIT-SFD-088A-RENATA", "SFD·088a/221", "P1-BASE-SFD-088A-RENATA-TARGET-001")]
    [InlineData(5, "P1-UNIT-OGN-028-DRAVEN", "OGN·028/298", "P1-BASE-OGN-028-DRAVEN-TARGET-001")]
    [InlineData(3, "P1-UNIT-SFD-110-FIORA", "SFD·110/221", "P1-BASE-SFD-110-FIORA-TARGET-001")]
    [InlineData(3, "P1-UNIT-SFD-110A-FIORA", "SFD·110a/221", "P1-BASE-SFD-110A-FIORA-TARGET-001")]
    [InlineData(4, "P1-UNIT-SFD-141-IRELIA", "SFD·141/221", "P1-BASE-SFD-141-IRELIA-TARGET-001")]
    [InlineData(4, "P1-UNIT-SFD-141A-IRELIA", "SFD·141a/221", "P1-BASE-SFD-141A-IRELIA-TARGET-001")]
    [InlineData(4, "P1-UNIT-DRAGON-CALLER", "OGN·140/298", "P1-BASE-DRAGON-CALLER-TARGET-001")]
    [InlineData(3, "P1-UNIT-WATERBENDER", "OGN·055/298", "P1-BASE-WATERBENDER-TARGET-001")]
    [InlineData(4, "P1-UNIT-WISE-ELDER", "OGN·065/298", "P1-BASE-WISE-ELDER-TARGET-001")]
    [InlineData(3, "P1-UNIT-EAGER-APPRENTICE", "OGN·084/298", "P1-BASE-EAGER-APPRENTICE-TARGET-001")]
    [InlineData(3, "P1-UNIT-ARENA-SERVICE-CREW", "OGN·091/298", "P1-BASE-ARENA-SERVICE-CREW-TARGET-001")]
    [InlineData(3, "P1-UNIT-PORO-HERDER", "OGN·061/298", "P1-BASE-PORO-HERDER-TARGET-001")]
    [InlineData(2, "P1-UNIT-RAVENBLOOM-STUDENT", "OGN·103/298", "P1-BASE-RAVENBLOOM-STUDENT-TARGET-001")]
    [InlineData(6, "P1-UNIT-BILGEWATER-BULLY", "OGN·125/298", "P1-BASE-BILGEWATER-BULLY-TARGET-001")]
    [InlineData(4, "P1-UNIT-EMBER-MONK", "OGN·167/298", "P1-BASE-EMBER-MONK-TARGET-001")]
    [InlineData(4, "P1-UNIT-HIDDEN-TRACKER", "OGN·177/298", "P1-BASE-HIDDEN-TRACKER-TARGET-001")]
    [InlineData(5, "P1-UNIT-UNDERCOVER-AGENT", "OGN·178/298", "P1-BASE-UNDERCOVER-AGENT-TARGET-001")]
    [InlineData(2, "P1-UNIT-TRAVELING-MERCHANT", "OGN·185/298", "P1-BASE-TRAVELING-MERCHANT-TARGET-001")]
    [InlineData(3, "P1-UNIT-OGN-KOGMAW", "OGN·190/298", "P1-BASE-OGN-KOGMAW-TARGET-001")]
    [InlineData(5, "P1-UNIT-OGN-JINX", "OGN·202/298", "P1-BASE-OGN-JINX-TARGET-001")]
    [InlineData(3, "P1-UNIT-OGN-FLAME-CHOMPERS", "OGN·006/298", "P1-BASE-OGN-FLAME-CHOMPERS-TARGET-001")]
    [InlineData(8, "P1-UNIT-OGN-MOLTEN-DRAKE", "OGN·011/298", "P1-BASE-OGN-MOLTEN-DRAKE-TARGET-001")]
    [InlineData(6, "P1-UNIT-BRINHIL", "OGN·026/298", "P1-BASE-BRINHIL-TARGET-001")]
    [InlineData(7, "P1-UNIT-OGN-TRYNDAMERE", "OGN·034/298", "P1-BASE-OGN-TRYNDAMERE-TARGET-001")]
    [InlineData(5, "P1-UNIT-OGN-AHRI-HOLD", "OGN·066/298", "P1-BASE-OGN-AHRI-HOLD-TARGET-001")]
    [InlineData(5, "P1-UNIT-OGN-AHRI-HOLD-A", "OGN·066a/298", "P1-BASE-OGN-AHRI-HOLD-A-TARGET-001")]
    [InlineData(6, "P1-UNIT-OGN-MAGESEEKER-WARDEN", "OGN·070/298", "P1-BASE-OGN-MAGESEEKER-WARDEN-TARGET-001")]
    [InlineData(4, "P1-UNIT-OGN-SONA-RUNES", "OGN·073/298", "P1-BASE-OGN-SONA-RUNES-TARGET-001")]
    [InlineData(6, "P1-UNIT-OGN-YASUO-ATTACK", "OGN·076/298", "P1-BASE-OGN-YASUO-ATTACK-TARGET-001")]
    [InlineData(6, "P1-UNIT-OGN-YASUO-ATTACK-A", "OGN·076a/298", "P1-BASE-OGN-YASUO-ATTACK-A-TARGET-001")]
    [InlineData(6, "P1-UNIT-OGN-LEONA-SCORE", "OGN·079/298", "P1-BASE-OGN-LEONA-SCORE-TARGET-001")]
    [InlineData(6, "P1-UNIT-OGN-LEONA-SCORE-A", "OGN·079a/298", "P1-BASE-OGN-LEONA-SCORE-A-TARGET-001")]
    [InlineData(3, "P1-UNIT-OGN-AHRI-COMBAT", "OGN·119/298", "P1-BASE-OGN-AHRI-COMBAT-TARGET-001")]
    [InlineData(4, "P1-UNIT-OGN-VIKTOR-OPPONENT-TURN", "OGN·117/298", "P1-BASE-OGN-VIKTOR-OPPONENT-TURN-TARGET-001")]
    [InlineData(4, "P1-UNIT-OGN-VIKTOR-OPPONENT-TURN-A", "OGN·117a/298", "P1-BASE-OGN-VIKTOR-OPPONENT-TURN-A-TARGET-001")]
    [InlineData(3, "P1-UNIT-OGN-AHRI-COMBAT-A", "OGN·119a/298", "P1-BASE-OGN-AHRI-COMBAT-A-TARGET-001")]
    [InlineData(4, "P1-UNIT-OGN-MALZAHAR-TAP", "OGN·113/298", "P1-BASE-OGN-MALZAHAR-TAP-TARGET-001")]
    [InlineData(6, "P1-UNIT-OGN-UDYR-BOON", "OGN·157/298", "P1-BASE-OGN-UDYR-BOON-TARGET-001")]
    [InlineData(4, "P1-UNIT-OGN-TWISTED-FATE", "OGN·200/298", "P1-BASE-OGN-TWISTED-FATE-TARGET-001")]
    [InlineData(6, "P1-UNIT-OGN-PEAK-GUARDIAN", "OGN·223/298", "P1-BASE-OGN-PEAK-GUARDIAN-TARGET-001")]
    [InlineData(4, "P1-UNIT-OGN-VIKTOR-DESTROYED-UNIT", "OGN·246/298", "P1-BASE-OGN-VIKTOR-DESTROYED-UNIT-TARGET-001")]
    [InlineData(4, "P1-UNIT-OGN-VIKTOR-DESTROYED-UNIT-A", "OGN·246a/298", "P1-BASE-OGN-VIKTOR-DESTROYED-UNIT-A-TARGET-001")]
    [InlineData(5, "P1-UNIT-OGS-ANNIE-DAMAGE-PLUS", "OGS·001/024", "P1-BASE-OGS-ANNIE-DAMAGE-PLUS-TARGET-001")]
    [InlineData(5, "P1-UNIT-OGS-YI-EIGHT-RUNES", "OGS·004/024", "P1-BASE-OGS-YI-EIGHT-RUNES-TARGET-001")]
    [InlineData(6, "P1-UNIT-OGS-LUX-HIGH-COST-SPELL", "OGS·006/024", "P1-BASE-OGS-LUX-HIGH-COST-SPELL-TARGET-001")]
    [InlineData(4, "P1-UNIT-WILDCLAW-SHAMAN", "OGN·147/298", "P1-BASE-WILDCLAW-SHAMAN-TARGET-001")]
    [InlineData(5, "P1-UNIT-OGN-JINX-A", "OGN·202a/298", "P1-BASE-OGN-JINX-A-TARGET-001")]
    [InlineData(4, "P1-UNIT-ALBUS-FERROS", "OGN·230/298", "P1-BASE-ALBUS-FERROS-TARGET-001")]
    [InlineData(7, "P1-UNIT-DUNEHORN-BEAST", "SFD·027/221", "P1-BASE-DUNEHORN-BEAST-TARGET-001")]
    [InlineData(2, "P1-UNIT-APPRENTICE-BLACKSMITH", "SFD·041/221", "P1-BASE-APPRENTICE-BLACKSMITH-TARGET-001")]
    [InlineData(5, "P1-UNIT-MOUNTAIN-APE-ELDER", "SFD·047/221", "P1-BASE-MOUNTAIN-APE-ELDER-TARGET-001")]
    public Task CoreRuleEngineRejectsVanillaSourceUnitWhenTargetsAreProvided(
        int mana,
        string sourceObjectId,
        string cardNo,
        string targetObjectId) =>
        AssertSourceUnitWithTargetRejectedAsync(
            mana,
            sourceObjectId,
            cardNo,
            targetObjectId);

    [Fact]
    public async Task P4XerathTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-xerath-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(5, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-XERATH-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-UNIT-XERATH"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-XERATH-TARGET-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-XERATH-TARGET-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-XERATH-TARGET-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-BASE-XERATH-TARGET-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4DragonSoulSageTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-dragon-soul-sage-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(2, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-DRAGON-SOUL-SAGE-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-UNIT-DRAGON-SOUL-SAGE"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-DRAGON-SOUL-SAGE-TARGET-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-DRAGON-SOUL-SAGE-TARGET-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-DRAGON-SOUL-SAGE-TARGET-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-BASE-DRAGON-SOUL-SAGE-TARGET-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4FluftPoroTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-fluft-poro-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(5, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-FLUFT-PORO-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-UNIT-FLUFT-PORO"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-FLUFT-PORO-TARGET-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-FLUFT-PORO-TARGET-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-FLUFT-PORO-TARGET-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-BASE-FLUFT-PORO-TARGET-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4Sfd088RenataGlascTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-sfd-088-renata-glasc-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(5, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-SFD-088-RENATA-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-UNIT-SFD-088-RENATA"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-SFD-088-RENATA-TARGET-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-SFD-088-RENATA-TARGET-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-SFD-088-RENATA-TARGET-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-BASE-SFD-088-RENATA-TARGET-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4Sfd088aRenataGlascTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-sfd-088a-renata-glasc-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(5, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-SFD-088A-RENATA-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-UNIT-SFD-088A-RENATA"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-SFD-088A-RENATA-TARGET-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-SFD-088A-RENATA-TARGET-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-SFD-088A-RENATA-TARGET-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-BASE-SFD-088A-RENATA-TARGET-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4Ogn028DravenTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-ogn-028-draven-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(5, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-OGN-028-DRAVEN-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-UNIT-OGN-028-DRAVEN"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-OGN-028-DRAVEN-TARGET-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-OGN-028-DRAVEN-TARGET-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-OGN-028-DRAVEN-TARGET-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-BASE-OGN-028-DRAVEN-TARGET-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4Sfd110FioraTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-sfd-110-fiora-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(3, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-SFD-110-FIORA-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-UNIT-SFD-110-FIORA"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-SFD-110-FIORA-TARGET-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-SFD-110-FIORA-TARGET-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-SFD-110-FIORA-TARGET-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-BASE-SFD-110-FIORA-TARGET-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4Sfd110aFioraTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-sfd-110a-fiora-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(3, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-SFD-110A-FIORA-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-UNIT-SFD-110A-FIORA"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-SFD-110A-FIORA-TARGET-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-SFD-110A-FIORA-TARGET-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-SFD-110A-FIORA-TARGET-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-BASE-SFD-110A-FIORA-TARGET-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4Sfd141IreliaTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-sfd-141-irelia-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(4, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-SFD-141-IRELIA-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-UNIT-SFD-141-IRELIA"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-SFD-141-IRELIA-TARGET-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-SFD-141-IRELIA-TARGET-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-SFD-141-IRELIA-TARGET-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-BASE-SFD-141-IRELIA-TARGET-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4Sfd141aIreliaTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-sfd-141a-irelia-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(4, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-SFD-141A-IRELIA-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-UNIT-SFD-141A-IRELIA"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-SFD-141A-IRELIA-TARGET-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-SFD-141A-IRELIA-TARGET-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-SFD-141A-IRELIA-TARGET-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-BASE-SFD-141A-IRELIA-TARGET-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4DragonCallerTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-dragon-caller-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(4, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-DRAGON-CALLER-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-UNIT-DRAGON-CALLER"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-DRAGON-CALLER-TARGET-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-DRAGON-CALLER-TARGET-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-DRAGON-CALLER-TARGET-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-BASE-DRAGON-CALLER-TARGET-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4WaterbenderTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-waterbender-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(3, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-WATERBENDER-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-UNIT-WATERBENDER"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-WATERBENDER-TARGET-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-WATERBENDER-TARGET-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-WATERBENDER-TARGET-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-BASE-WATERBENDER-TARGET-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4WiseElderTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-wise-elder-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(4, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-WISE-ELDER-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-UNIT-WISE-ELDER"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-WISE-ELDER-TARGET-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-WISE-ELDER-TARGET-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-WISE-ELDER-TARGET-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-BASE-WISE-ELDER-TARGET-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4EagerApprenticeTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-eager-apprentice-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(3, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-EAGER-APPRENTICE-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-UNIT-EAGER-APPRENTICE"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-EAGER-APPRENTICE-TARGET-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-EAGER-APPRENTICE-TARGET-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-EAGER-APPRENTICE-TARGET-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-BASE-EAGER-APPRENTICE-TARGET-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4ArenaServiceCrewTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-arena-service-crew-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(3, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-ARENA-SERVICE-CREW-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-UNIT-ARENA-SERVICE-CREW"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-ARENA-SERVICE-CREW-TARGET-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-ARENA-SERVICE-CREW-TARGET-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-ARENA-SERVICE-CREW-TARGET-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-BASE-ARENA-SERVICE-CREW-TARGET-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4PoroHerderTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-poro-herder-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(3, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-PORO-HERDER-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-UNIT-PORO-HERDER"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-PORO-HERDER-TARGET-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-PORO-HERDER-TARGET-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-PORO-HERDER-TARGET-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-BASE-PORO-HERDER-TARGET-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4RavenbloomStudentTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-ravenbloom-student-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(2, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-RAVENBLOOM-STUDENT-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-UNIT-RAVENBLOOM-STUDENT"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-RAVENBLOOM-STUDENT-TARGET-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-RAVENBLOOM-STUDENT-TARGET-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-RAVENBLOOM-STUDENT-TARGET-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-BASE-RAVENBLOOM-STUDENT-TARGET-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4ResonantSoulTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-resonant-soul-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(6, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-RESONANT-SOUL-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-UNIT-RESONANT-SOUL"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-RESONANT-SOUL-TARGET-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-RESONANT-SOUL-TARGET-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-RESONANT-SOUL-TARGET-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-BASE-RESONANT-SOUL-TARGET-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4BilgewaterBullyTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-bilgewater-bully-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(6, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-BILGEWATER-BULLY-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-UNIT-BILGEWATER-BULLY"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-BILGEWATER-BULLY-TARGET-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-BILGEWATER-BULLY-TARGET-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-BILGEWATER-BULLY-TARGET-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-BASE-BILGEWATER-BULLY-TARGET-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4SharpshooterPirateTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-sharpshooter-pirate-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(3, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-SHARPSHOOTER-PIRATE-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-UNIT-SHARPSHOOTER-PIRATE"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-SHARPSHOOTER-PIRATE-TARGET-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-SHARPSHOOTER-PIRATE-TARGET-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-SHARPSHOOTER-PIRATE-TARGET-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-BASE-SHARPSHOOTER-PIRATE-TARGET-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4DuneDrakeTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-dune-drake-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(5, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-DUNE-DRAKE-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-UNIT-DUNE-DRAKE"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-DUNE-DRAKE-TARGET-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-DUNE-DRAKE-TARGET-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-DUNE-DRAKE-TARGET-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-BASE-DUNE-DRAKE-TARGET-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4EmberMonkTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-ember-monk-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(4, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-EMBER-MONK-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-UNIT-EMBER-MONK"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-EMBER-MONK-TARGET-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-EMBER-MONK-TARGET-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-EMBER-MONK-TARGET-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-BASE-EMBER-MONK-TARGET-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4HiddenTrackerTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-hidden-tracker-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(4, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-HIDDEN-TRACKER-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-UNIT-HIDDEN-TRACKER"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-HIDDEN-TRACKER-TARGET-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-HIDDEN-TRACKER-TARGET-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-HIDDEN-TRACKER-TARGET-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-BASE-HIDDEN-TRACKER-TARGET-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4UndercoverAgentTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-undercover-agent-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(5, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-UNDERCOVER-AGENT-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-UNIT-UNDERCOVER-AGENT"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-UNDERCOVER-AGENT-TARGET-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-UNDERCOVER-AGENT-TARGET-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-UNDERCOVER-AGENT-TARGET-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-BASE-UNDERCOVER-AGENT-TARGET-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4TravelingMerchantTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-traveling-merchant-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(2, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-TRAVELING-MERCHANT-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-UNIT-TRAVELING-MERCHANT"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-TRAVELING-MERCHANT-TARGET-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-TRAVELING-MERCHANT-TARGET-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-TRAVELING-MERCHANT-TARGET-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-BASE-TRAVELING-MERCHANT-TARGET-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4OgnKogmawTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-ogn-kogmaw-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(3, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-OGN-KOGMAW-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-UNIT-OGN-KOGMAW"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-OGN-KOGMAW-TARGET-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-OGN-KOGMAW-TARGET-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-OGN-KOGMAW-TARGET-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-BASE-OGN-KOGMAW-TARGET-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4NoxianDrummerTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-noxian-drummer-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(3, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-NOXIAN-DRUMMER-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-UNIT-NOXIAN-DRUMMER"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-NOXIAN-DRUMMER-TARGET-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-NOXIAN-DRUMMER-TARGET-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-NOXIAN-DRUMMER-TARGET-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-BASE-NOXIAN-DRUMMER-TARGET-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4TideCallerTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-tide-caller-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(2, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-TIDE-CALLER-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-UNIT-TIDE-CALLER"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(["P2-BASE-TIDE-CALLER-TARGET-001"], result.FinalState.PlayerZones["P2"].Base);
        Assert.Equal(0, result.FinalState.CardObjects["P2-BASE-TIDE-CALLER-TARGET-001"].Damage);
        Assert.Equal(2, result.FinalState.CardObjects["P2-BASE-TIDE-CALLER-TARGET-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P2-BASE-TIDE-CALLER-TARGET-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4Ogn202JinxTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-ogn-202-jinx-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(5, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-OGN-JINX-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-UNIT-OGN-JINX"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-OGN-JINX-TARGET-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-OGN-JINX-TARGET-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-OGN-JINX-TARGET-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-BASE-OGN-JINX-TARGET-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4GhostMatronTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-ghost-matron-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(4, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-GHOST-MATRON-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-UNIT-GHOST-MATRON"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-GHOST-MATRON-TARGET-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-GHOST-MATRON-TARGET-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-GHOST-MATRON-TARGET-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-BASE-GHOST-MATRON-TARGET-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4AlbusFerrosTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-albus-ferros-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(4, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-ALBUS-FERROS-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-UNIT-ALBUS-FERROS"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-ALBUS-FERROS-TARGET-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-ALBUS-FERROS-TARGET-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-ALBUS-FERROS-TARGET-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-BASE-ALBUS-FERROS-TARGET-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4OgnKarthusTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-ogn-karthus-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(3, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-OGN-KARTHUS-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-UNIT-OGN-KARTHUS"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-OGN-KARTHUS-TARGET-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-OGN-KARTHUS-TARGET-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-OGN-KARTHUS-TARGET-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-BASE-OGN-KARTHUS-TARGET-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4DunehornBeastTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-dunehorn-beast-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(7, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-DUNEHORN-BEAST-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-UNIT-DUNEHORN-BEAST"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-DUNEHORN-BEAST-TARGET-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-DUNEHORN-BEAST-TARGET-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-DUNEHORN-BEAST-TARGET-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-BASE-DUNEHORN-BEAST-TARGET-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4GloompathGuardTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-gloompath-guard-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(6, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-GLOOMPATH-GUARD-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-UNIT-GLOOMPATH-GUARD"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-GLOOMPATH-GUARD-TARGET-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-GLOOMPATH-GUARD-TARGET-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-GLOOMPATH-GUARD-TARGET-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-BASE-GLOOMPATH-GUARD-TARGET-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4ApprenticeBlacksmithTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-apprentice-blacksmith-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(2, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-APPRENTICE-BLACKSMITH-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-UNIT-APPRENTICE-BLACKSMITH"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-APPRENTICE-BLACKSMITH-TARGET-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-APPRENTICE-BLACKSMITH-TARGET-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-APPRENTICE-BLACKSMITH-TARGET-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-BASE-APPRENTICE-BLACKSMITH-TARGET-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4MountainApeElderTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-mountain-ape-elder-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(5, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-MOUNTAIN-APE-ELDER-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-UNIT-MOUNTAIN-APE-ELDER"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-MOUNTAIN-APE-ELDER-TARGET-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-MOUNTAIN-APE-ELDER-TARGET-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-MOUNTAIN-APE-ELDER-TARGET-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-BASE-MOUNTAIN-APE-ELDER-TARGET-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4Sfd089RumbleTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-sfd-089-rumble-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(5, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-SFD-089-RUMBLE-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-UNIT-SFD-089-RUMBLE"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-SFD-089-RUMBLE-TARGET-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-SFD-089-RUMBLE-TARGET-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-SFD-089-RUMBLE-TARGET-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-BASE-SFD-089-RUMBLE-TARGET-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4Sfd089aRumbleTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-sfd-089a-rumble-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(5, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-SFD-089A-RUMBLE-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-UNIT-SFD-089A-RUMBLE"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-SFD-089A-RUMBLE-TARGET-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-SFD-089A-RUMBLE-TARGET-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-SFD-089A-RUMBLE-TARGET-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-BASE-SFD-089A-RUMBLE-TARGET-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4YordleExplorerTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-yordle-explorer-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(4, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-YORDLE-EXPLORER-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-UNIT-YORDLE-EXPLORER"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-YORDLE-EXPLORER-TARGET-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-YORDLE-EXPLORER-TARGET-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-YORDLE-EXPLORER-TARGET-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-BASE-YORDLE-EXPLORER-TARGET-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4BlackMarketBrokerTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-black-market-broker-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(3, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-BLACK-MARKET-BROKER-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-UNIT-BLACK-MARKET-BROKER"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-BLACK-MARKET-BROKER-TARGET-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-BLACK-MARKET-BROKER-TARGET-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-BLACK-MARKET-BROKER-TARGET-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-BASE-BLACK-MARKET-BROKER-TARGET-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4HuntingSeaCrewTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-hunting-sea-crew-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(4, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-HUNTING-SEA-CREW-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-UNIT-HUNTING-SEA-CREW"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-HUNTING-SEA-CREW-TARGET-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-HUNTING-SEA-CREW-TARGET-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-HUNTING-SEA-CREW-TARGET-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-BASE-HUNTING-SEA-CREW-TARGET-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4GhostlyCentaurTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-ghostly-centaur-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(6, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-GHOSTLY-CENTAUR-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-UNIT-GHOSTLY-CENTAUR"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-GHOSTLY-CENTAUR-TARGET-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-GHOSTLY-CENTAUR-TARGET-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-GHOSTLY-CENTAUR-TARGET-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-BASE-GHOSTLY-CENTAUR-TARGET-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4CorruptEnforcerTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-corrupt-enforcer-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(3, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-CORRUPT-ENFORCER-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-UNIT-CORRUPT-ENFORCER"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-CORRUPT-ENFORCER-TARGET-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-CORRUPT-ENFORCER-TARGET-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-CORRUPT-ENFORCER-TARGET-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-BASE-CORRUPT-ENFORCER-TARGET-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4MightyFaerieTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-mighty-faerie-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(4, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-MIGHTY-FAERIE-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-UNIT-MIGHTY-FAERIE"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-MIGHTY-FAERIE-TARGET-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-MIGHTY-FAERIE-TARGET-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-MIGHTY-FAERIE-TARGET-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-BASE-MIGHTY-FAERIE-TARGET-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4JarMeddardaTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-jar-meddarda-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(5, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-JAR-MEDDARDA-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-UNIT-JAR-MEDDARDA"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-JAR-MEDDARDA-TARGET-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-JAR-MEDDARDA-TARGET-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-JAR-MEDDARDA-TARGET-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-BASE-JAR-MEDDARDA-TARGET-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4SfdVexTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-sfd-vex-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(5, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-SFD-VEX-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-UNIT-SFD-VEX-COMBAT"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-SFD-VEX-COMBAT-TARGET-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-SFD-VEX-COMBAT-TARGET-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-SFD-VEX-COMBAT-TARGET-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-BASE-SFD-VEX-COMBAT-TARGET-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4ProminentPatronTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-prominent-patron-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(6, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-PROMINENT-PATRON-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-UNIT-PROMINENT-PATRON"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-PROMINENT-PATRON-TARGET-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-PROMINENT-PATRON-TARGET-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-PROMINENT-PATRON-TARGET-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-BASE-PROMINENT-PATRON-TARGET-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4RoyalAttendantTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-royal-attendant-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(3, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-ROYAL-ATTENDANT-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-UNIT-ROYAL-ATTENDANT"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-ROYAL-ATTENDANT-TARGET-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-ROYAL-ATTENDANT-TARGET-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-ROYAL-ATTENDANT-TARGET-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-BASE-ROYAL-ATTENDANT-TARGET-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4Sfd049ApheliosTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-sfd-049-aphelios-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(4, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-SFD-049-APHELIOS-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-UNIT-SFD-049-APHELIOS"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-SFD-049-APHELIOS-TARGET-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-SFD-049-APHELIOS-TARGET-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-SFD-049-APHELIOS-TARGET-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-BASE-SFD-049-APHELIOS-TARGET-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4Sfd058OrnnTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-sfd-058-ornn-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(5, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-SFD-058-ORNN-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-UNIT-SFD-058-ORNN"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-SFD-058-ORNN-TARGET-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-SFD-058-ORNN-TARGET-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-SFD-058-ORNN-TARGET-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-BASE-SFD-058-ORNN-TARGET-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4Sfd058aOrnnTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-sfd-058a-ornn-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(5, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-SFD-058A-ORNN-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-UNIT-SFD-058A-ORNN"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-SFD-058A-ORNN-TARGET-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-SFD-058A-ORNN-TARGET-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-SFD-058A-ORNN-TARGET-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-BASE-SFD-058A-ORNN-TARGET-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4Sfd079BardTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-sfd-079-bard-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(4, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-SFD-079-BARD-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-UNIT-SFD-BARD"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-SFD-BARD-TARGET-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-SFD-BARD-TARGET-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-SFD-BARD-TARGET-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-BASE-SFD-BARD-TARGET-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4BigSwindlerTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-big-swindler-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(3, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-BIG-SWINDLER-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-UNIT-BIG-SWINDLER"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-BIG-SWINDLER-TARGET-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-BIG-SWINDLER-TARGET-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-BIG-SWINDLER-TARGET-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-BASE-BIG-SWINDLER-TARGET-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4SfdJayceTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-sfd-jayce-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(4, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-SFD-JAYCE-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-UNIT-SFD-JAYCE"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-SFD-JAYCE-TARGET-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-SFD-JAYCE-TARGET-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-SFD-JAYCE-TARGET-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-BASE-SFD-JAYCE-TARGET-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4ZauniteThugTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-zaunite-thug-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(3, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-ZAUNITE-THUG-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-UNIT-ZAUNITE-THUG"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-ZAUNITE-THUG-TARGET-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-ZAUNITE-THUG-TARGET-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-ZAUNITE-THUG-TARGET-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-BASE-ZAUNITE-THUG-TARGET-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4GlascBartenderTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-glasc-bartender-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(5, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-GLASC-BARTENDER-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-UNIT-GLASC-BARTENDER"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-GLASC-BARTENDER-TARGET-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-GLASC-BARTENDER-TARGET-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-GLASC-BARTENDER-TARGET-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-BASE-GLASC-BARTENDER-TARGET-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4Sfd170ReksaiTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-sfd-170-reksai-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(5, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-SFD-170-REKSAI-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-UNIT-SFD-170-REKSAI"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-SFD-170-REKSAI-TARGET-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-SFD-170-REKSAI-TARGET-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-SFD-170-REKSAI-TARGET-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-BASE-SFD-170-REKSAI-TARGET-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4Sfd170aReksaiTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-sfd-170a-reksai-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(5, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-SFD-170A-REKSAI-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-UNIT-SFD-170A-REKSAI"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-SFD-170A-REKSAI-TARGET-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-SFD-170A-REKSAI-TARGET-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-SFD-170A-REKSAI-TARGET-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-BASE-SFD-170A-REKSAI-TARGET-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task P4SfdSorakaTargetRejectedFixture()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p4-play-sfd-soraka-target-rejected.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.Tick);
        Assert.Equal(new RunePool(4, 0), result.FinalState.RunePools["P1"]);
        Assert.Equal(["P1-SFD-SORAKA-MAIN-001"], result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(["P1-UNIT-SFD-SORAKA"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-SFD-SORAKA-TARGET-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-SFD-SORAKA-TARGET-001"].Damage);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-SFD-SORAKA-TARGET-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-BASE-SFD-SORAKA-TARGET-001"].Tags);
        Assert.Empty(result.FinalState.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysSnowstalkingWolfDestroyTraitUnitAdditionalCost()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-snowstalking-wolf-trait-destroy-ambush-static.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-UNIT-SNOWSTALKING-WOLF"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P1-BASE-SNOWSTALKING-WOLF-COST-001"], result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.False(result.FinalState.CardObjects.ContainsKey("P1-BASE-SNOWSTALKING-WOLF-COST-001"));
        Assert.Equal([CardObjectTags.UnitCard, "犬形"], result.FinalState.CardObjects["P1-UNIT-SNOWSTALKING-WOLF"].Tags);
        Assert.Equal(["P1"], result.FinalState.DestroyedUnitOwnerIdsThisTurn);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsSnowstalkingWolfWithoutTraitAdditionalCost()
    {
        var state = PunishmentState(mana: 4) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-SNOWSTALKING-WOLF"],
                    Base = ["P1-BASE-SNOWSTALKING-WOLF-COST-001"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BASE-SNOWSTALKING-WOLF-COST-001"] = new(
                    "P1-BASE-SNOWSTALKING-WOLF-COST-001",
                    tags: [CardObjectTags.UnitCard, "犬形"])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-snowstalking-wolf-missing-trait-additional-cost", "P1", "PLAY_CARD"),
            new PlayCardCommand("P1-UNIT-SNOWSTALKING-WOLF", "UNL-166/219", []),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(4, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-SNOWSTALKING-WOLF"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-SNOWSTALKING-WOLF-COST-001"], result.State.PlayerZones["P1"].Base);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsSnowstalkingWolfWhenAdditionalCostUnitLacksTrait()
    {
        var state = PunishmentState(mana: 4) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-SNOWSTALKING-WOLF"],
                    Base = ["P1-BASE-SNOWSTALKING-WOLF-COST-001"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BASE-SNOWSTALKING-WOLF-COST-001"] = new(
                    "P1-BASE-SNOWSTALKING-WOLF-COST-001",
                    tags: [CardObjectTags.UnitCard, "机械"])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-snowstalking-wolf-wrong-trait-additional-cost", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-UNIT-SNOWSTALKING-WOLF",
                "UNL-166/219",
                [],
                OptionalCosts: ["DESTROY_FRIENDLY_TRAIT_UNIT:P1-BASE-SNOWSTALKING-WOLF-COST-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(4, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-SNOWSTALKING-WOLF"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-BASE-SNOWSTALKING-WOLF-COST-001"], result.State.PlayerZones["P1"].Base);
        Assert.True(result.State.CardObjects.ContainsKey("P1-BASE-SNOWSTALKING-WOLF-COST-001"));
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsSnowstalkingWolfWhenTargetsAreProvided()
    {
        var state = PunishmentState(mana: 4) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-SNOWSTALKING-WOLF"],
                    Base =
                    [
                        "P1-BASE-SNOWSTALKING-WOLF-COST-001",
                        "P1-BASE-SNOWSTALKING-WOLF-TARGET-001"
                    ]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-BASE-SNOWSTALKING-WOLF-COST-001"] = new(
                    "P1-BASE-SNOWSTALKING-WOLF-COST-001",
                    tags: [CardObjectTags.UnitCard, "犬形"]),
                ["P1-BASE-SNOWSTALKING-WOLF-TARGET-001"] = new(
                    "P1-BASE-SNOWSTALKING-WOLF-TARGET-001",
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-snowstalking-wolf-with-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-UNIT-SNOWSTALKING-WOLF",
                "UNL-166/219",
                ["P1-BASE-SNOWSTALKING-WOLF-TARGET-001"],
                OptionalCosts: ["DESTROY_FRIENDLY_TRAIT_UNIT:P1-BASE-SNOWSTALKING-WOLF-COST-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(4, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-SNOWSTALKING-WOLF"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(
            ["P1-BASE-SNOWSTALKING-WOLF-COST-001", "P1-BASE-SNOWSTALKING-WOLF-TARGET-001"],
            result.State.PlayerZones["P1"].Base);
        Assert.Empty(result.State.StackItems);
    }

    [Theory]
    [InlineData("P1-UNIT-IVERN-TRAIT", "UNL-177/219", "CHOOSE_DOG", "P1-BASE-IVERN-TRAIT-TARGET-001")]
    [InlineData("P1-UNIT-IVERN-A-TRAIT", "UNL-177a/219", "CHOOSE_PORO", "P1-BASE-IVERN-A-TRAIT-TARGET-001")]
    public Task CoreRuleEngineRejectsIvernTraitChoiceSourceUnitWhenTargetsAreProvided(
        string sourceObjectId,
        string cardNo,
        string mode,
        string targetObjectId) =>
        AssertSourceUnitWithTargetRejectedAsync(
            6,
            sourceObjectId,
            cardNo,
            targetObjectId,
            mode);

    [Theory]
    [InlineData("p2-preflight-play-aggressive-dragonhound-active-unit.fixture.json", "P1-UNIT-AGGRESSIVE-DRAGONHOUND", 3, "CARD_TYPE:UNIT|犬形|龙", false)]
    [InlineData("p2-preflight-play-yi-active-unit.fixture.json", "P1-UNIT-YI", 6, "CARD_TYPE:UNIT|游走", false)]
    [InlineData("p2-preflight-play-vanguard-squire-active-unit.fixture.json", "P1-UNIT-VANGUARD-SQUIRE", 5, "CARD_TYPE:UNIT|精锐", false)]
    [InlineData("p2-preflight-play-warwick-active-unit.fixture.json", "P1-UNIT-WARWICK", 5, "CARD_TYPE:UNIT|犬形", false)]
    [InlineData("p2-preflight-play-arc-vi-roam-static.fixture.json", "P1-UNIT-ARC-VI", 3, "CARD_TYPE:UNIT|游走", true)]
    [InlineData("p2-preflight-play-arc-caitlyn-damage-order-static.fixture.json", "P1-UNIT-ARC-CAITLYN", 3, "CARD_TYPE:UNIT", true)]
    [InlineData("p2-preflight-play-arc-heimerdinger-yordle-static.fixture.json", "P1-UNIT-ARC-HEIMERDINGER", 3, "CARD_TYPE:UNIT|约德尔人", true)]
    [InlineData("p2-preflight-play-arc-warwick-active-unit.fixture.json", "P1-UNIT-ARC-WARWICK", 5, "CARD_TYPE:UNIT|犬形", false)]
    [InlineData("p2-preflight-play-arc-jinx-discard-trigger-static.fixture.json", "P1-UNIT-ARC-JINX", 5, "CARD_TYPE:UNIT", true)]
    [InlineData("p2-preflight-play-arc-viktor-destroyed-unit-trigger-static.fixture.json", "P1-UNIT-ARC-VIKTOR", 4, "CARD_TYPE:UNIT", true)]
    [InlineData("p2-preflight-play-arena-councilor-active-unit.fixture.json", "P1-UNIT-ARENA-COUNCILOR", 3, "CARD_TYPE:UNIT|约德尔人", false)]
    [InlineData("p2-preflight-play-shadow-base-unit-static.fixture.json", "P1-UNIT-SHADOW", 3, "CARD_TYPE:UNIT", true)]
    public async Task CoreRuleEngineUsesCardTextForUnitEntryState(
        string fixtureFileName,
        string sourceObjectId,
        int expectedPower,
        string expectedTags,
        bool expectedExhausted)
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", fixtureFileName),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal([sourceObjectId], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(expectedPower, result.FinalState.CardObjects[sourceObjectId].Power);
        Assert.Equal(expectedTags.Split('|'), result.FinalState.CardObjects[sourceObjectId].Tags);
        Assert.Equal(expectedExhausted, result.FinalState.CardObjects[sourceObjectId].IsExhausted);
    }

    [Theory]
    [InlineData(3, "P1-UNIT-AGGRESSIVE-DRAGONHOUND", "SFD·006/221", "P1-BASE-AGGRESSIVE-DRAGONHOUND-TARGET-001")]
    [InlineData(7, "P1-UNIT-YI", "OGS·009/024", "P1-BASE-YI-TARGET-001")]
    [InlineData(6, "P1-UNIT-VANGUARD-SQUIRE", "OGS·016/024", "P1-BASE-VANGUARD-SQUIRE-TARGET-001")]
    [InlineData(6, "P1-UNIT-WARWICK", "OGN·159/298", "P1-BASE-WARWICK-TARGET-001")]
    [InlineData(2, "P1-UNIT-ARC-VI", "ARC-001/006", "P1-BASE-ARC-VI-TARGET-001")]
    [InlineData(3, "P1-UNIT-ARC-CAITLYN", "ARC-002/006", "P1-BASE-ARC-CAITLYN-TARGET-001")]
    [InlineData(3, "P1-UNIT-ARC-HEIMERDINGER", "ARC-003/006", "P1-BASE-ARC-HEIMERDINGER-TARGET-001")]
    [InlineData(6, "P1-UNIT-ARC-WARWICK", "ARC-004/006", "P1-BASE-ARC-WARWICK-TARGET-001")]
    [InlineData(5, "P1-UNIT-ARC-JINX", "ARC-005/006", "P1-BASE-ARC-JINX-TARGET-001")]
    [InlineData(4, "P1-UNIT-ARC-VIKTOR", "ARC-006/006", "P1-BASE-ARC-VIKTOR-TARGET-001")]
    [InlineData(5, "P1-UNIT-ARENA-COUNCILOR", "UNL-001/219", "P1-BASE-ARENA-COUNCILOR-TARGET-001")]
    [InlineData(3, "P1-UNIT-SHADOW", "UNL-194/219", "P1-BASE-SHADOW-TARGET-001")]
    public Task CoreRuleEngineRejectsActiveEntrySourceUnitWhenTargetsAreProvided(
        int mana,
        string sourceObjectId,
        string cardNo,
        string targetObjectId) =>
        AssertSourceUnitWithTargetRejectedAsync(
            mana,
            sourceObjectId,
            cardNo,
            targetObjectId);

    [Theory]
    [InlineData("p2-preflight-play-blast-crew-apprentice-no-optional-damage.fixture.json", "P1-UNIT-BLAST-CREW-APPRENTICE", 2, "CARD_TYPE:UNIT")]
    [InlineData("p2-preflight-play-frostcoat-cub-no-optional-power-minus-two.fixture.json", "P1-UNIT-FROSTCOAT-CUB", 3, "CARD_TYPE:UNIT|犬形")]
    [InlineData("p2-preflight-play-ship-monkey-no-optional-boon.fixture.json", "P1-UNIT-SHIP-MONKEY", 2, "CARD_TYPE:UNIT|海盗")]
    [InlineData("p2-preflight-play-pyke-no-optional-ready-power.fixture.json", "P1-UNIT-PYKE", 2, "CARD_TYPE:UNIT|待命|游走")]
    [InlineData("p2-preflight-play-pyke-alt-a-no-optional-ready-power.fixture.json", "P1-UNIT-PYKE-A", 2, "CARD_TYPE:UNIT|待命|游走")]
    [InlineData("p2-preflight-play-tiny-guardian-no-optional-draw.fixture.json", "P1-UNIT-TINY-GUARDIAN", 2, "CARD_TYPE:UNIT")]
    [InlineData("p2-preflight-play-blazing-drake-no-optional-haste.fixture.json", "P1-UNIT-BLAZING-DRAKE", 5, "CARD_TYPE:UNIT|急速|龙")]
    [InlineData("p2-preflight-play-legion-rearguard-no-optional-haste.fixture.json", "P1-UNIT-LEGION-REARGUARD", 2, "CARD_TYPE:UNIT|崔法利|急速")]
    [InlineData("p2-preflight-play-baby-shark-no-optional-haste.fixture.json", "P1-UNIT-BABY-SHARK", 1, "CARD_TYPE:UNIT|强攻4|急速")]
    [InlineData("p2-preflight-play-reksai-no-optional-haste.fixture.json", "P1-UNIT-REKSAI", 3, "CARD_TYPE:UNIT|强攻|急速")]
    [InlineData("p2-preflight-play-reksai-alt-a-no-optional-haste.fixture.json", "P1-UNIT-REKSAI-A", 3, "CARD_TYPE:UNIT|强攻|急速")]
    [InlineData("p2-preflight-play-kaisa-no-optional-haste.fixture.json", "P1-UNIT-KAISA", 4, "CARD_TYPE:UNIT|急速")]
    [InlineData("p2-preflight-play-kaisa-alt-a-no-optional-haste.fixture.json", "P1-UNIT-KAISA-A", 4, "CARD_TYPE:UNIT|急速")]
    [InlineData("p2-preflight-play-rengar-no-optional-haste.fixture.json", "P1-UNIT-RENGAR", 4, "CARD_TYPE:UNIT|强攻2|急速|法盾|游走|猫科")]
    [InlineData("p2-preflight-play-rengar-alt-a-no-optional-haste.fixture.json", "P1-UNIT-RENGAR-A", 4, "CARD_TYPE:UNIT|强攻2|急速|法盾|游走|猫科")]
    [InlineData("p2-preflight-play-nilah-no-optional-haste.fixture.json", "P1-UNIT-NILAH", 4, "CARD_TYPE:UNIT|急速|恶魔|游走")]
    [InlineData("p2-preflight-play-miss-fortune-no-optional-haste.fixture.json", "P1-UNIT-MISS-FORTUNE", 5, "CARD_TYPE:UNIT|急速|海盗|游走")]
    [InlineData("p2-preflight-play-miss-fortune-alt-a-no-optional-haste.fixture.json", "P1-UNIT-MISS-FORTUNE-A", 5, "CARD_TYPE:UNIT|急速|海盗|游走")]
    [InlineData("p2-preflight-play-sivir-no-optional-haste.fixture.json", "P1-UNIT-SIVIR", 4, "CARD_TYPE:UNIT|急速")]
    [InlineData("p2-preflight-play-sivir-alt-a-no-optional-haste.fixture.json", "P1-UNIT-SIVIR-A", 4, "CARD_TYPE:UNIT|急速")]
    [InlineData("p2-preflight-play-lillia-no-optional-haste.fixture.json", "P1-UNIT-LILLIA", 3, "CARD_TYPE:UNIT|仙灵|急速")]
    [InlineData("p2-preflight-play-lillia-alt-a-no-optional-haste.fixture.json", "P1-UNIT-LILLIA-A", 3, "CARD_TYPE:UNIT|仙灵|急速")]
    [InlineData("p2-preflight-play-azir-no-optional-haste.fixture.json", "P1-UNIT-AZIR", 4, "CARD_TYPE:UNIT|急速|鸟类")]
    [InlineData("p2-preflight-play-azir-alt-a-no-optional-haste.fixture.json", "P1-UNIT-AZIR-A", 4, "CARD_TYPE:UNIT|急速|鸟类")]
    [InlineData("p2-preflight-play-mr-root-no-optional-haste.fixture.json", "P1-UNIT-MR-ROOT", 1, "CARD_TYPE:UNIT|仙灵|急速")]
    [InlineData("p2-preflight-play-mech-maniac-no-optional-haste.fixture.json", "P1-UNIT-MECH-MANIAC", 3, "CARD_TYPE:UNIT|急速")]
    [InlineData("p2-preflight-play-xersai-fish-no-optional-haste.fixture.json", "P1-UNIT-XERSAI-FISH", 6, "CARD_TYPE:UNIT|急速")]
    [InlineData("p2-preflight-play-karina-veraze-no-optional-haste.fixture.json", "P1-UNIT-KARINA-VERAZE", 6, "CARD_TYPE:UNIT|急速")]
    [InlineData("p2-preflight-play-crimson-signet-treant-no-optional-haste.fixture.json", "P1-UNIT-CRIMSON-SIGNET-TREANT", 4, "CARD_TYPE:UNIT|急速")]
    [InlineData("p2-preflight-play-crimson-signet-treant-alt-a-no-optional-haste.fixture.json", "P1-UNIT-CRIMSON-SIGNET-TREANT-A", 4, "CARD_TYPE:UNIT|急速")]
    [InlineData("p2-preflight-play-tasty-faerie-no-optional-haste.fixture.json", "P1-UNIT-TASTY-FAERIE", 6, "CARD_TYPE:UNIT|仙灵|急速")]
    [InlineData("p2-preflight-play-ekko-no-optional-haste.fixture.json", "P1-UNIT-EKKO", 5, "CARD_TYPE:UNIT|急速")]
    [InlineData("p2-preflight-play-armed-assaulter-no-optional-haste.fixture.json", "P1-UNIT-ARMED-ASSAULTER", 6, "CARD_TYPE:UNIT|急速|百炼")]
    [InlineData("p2-preflight-play-ancient-berserker-no-optional-haste.fixture.json", "P1-UNIT-ANCIENT-BERSERKER", 4, "CARD_TYPE:UNIT|急速|灵体")]
    [InlineData("p2-preflight-play-kraken-hunter-no-optional-haste.fixture.json", "P1-UNIT-KRAKEN-HUNTER", 5, "CARD_TYPE:UNIT|强攻|急速|海盗")]
    [InlineData("p2-preflight-play-lee-sin-no-optional-haste.fixture.json", "P1-UNIT-LEE-SIN", 6, "CARD_TYPE:UNIT|急速")]
    [InlineData("p2-preflight-play-lee-sin-alt-a-no-optional-haste.fixture.json", "P1-UNIT-LEE-SIN-A", 6, "CARD_TYPE:UNIT|急速")]
    public async Task CoreRuleEnginePlaysSourceUnitWithoutOptionalAdditionalCost(
        string fixtureFileName,
        string sourceObjectId,
        int expectedPower,
        string expectedTags)
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", fixtureFileName),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal([sourceObjectId], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(expectedPower, result.FinalState.CardObjects[sourceObjectId].Power);
        Assert.Equal(expectedTags.Split('|'), result.FinalState.CardObjects[sourceObjectId].Tags);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysShipMonkeyOptionalBoon()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-ship-monkey-optional-boon.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-UNIT-SHIP-MONKEY"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(3, result.FinalState.CardObjects["P1-UNIT-SHIP-MONKEY"].Power);
        Assert.Contains(CardObjectTags.Boon, result.FinalState.CardObjects["P1-UNIT-SHIP-MONKEY"].Tags);
        Assert.Contains("海盗", result.FinalState.CardObjects["P1-UNIT-SHIP-MONKEY"].Tags);
    }

    [Fact]
    public void CoreRuleEngineShipMonkeyPromptExposesOptionalBoonCost()
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-SHIP-MONKEY"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-SHIP-MONKEY"] = new(
                    "P1-UNIT-SHIP-MONKEY",
                    cardNo: "SFD·098/221",
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "PLAY_CARD", StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(playCandidate.Metadata);
        var sourceRequirement = Assert.Single(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]));
        var optionalCostChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            sourceRequirement["optionalCostChoices"]);

        var optionalBoonCost = Assert.Single(
            optionalCostChoices,
            choice => string.Equals(choice.Id, "SPEND_MANA:1", StringComparison.Ordinal));
        Assert.Equal("额外支付 1 法力：给予我增益", optionalBoonCost.Label);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysTinyGuardianOptionalDraw()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-tiny-guardian-optional-draw.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-UNIT-TINY-GUARDIAN"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P1-TINY-GUARDIAN-DRAW-001"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Empty(result.FinalState.PlayerZones["P1"].MainDeck);
        Assert.Equal(2, result.FinalState.CardObjects["P1-UNIT-TINY-GUARDIAN"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P1-UNIT-TINY-GUARDIAN"].Tags);
    }

    [Fact]
    public void CoreRuleEngineTinyGuardianPromptExposesOptionalGreenDrawCost()
    {
        var state = PunishmentState(mana: 2) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(2, 0, new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [RuneTrait.Green] = 1
                }),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-TINY-GUARDIAN"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-TINY-GUARDIAN"] = new(
                    "P1-UNIT-TINY-GUARDIAN",
                    cardNo: "OGN·044/298",
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "PLAY_CARD", StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(playCandidate.Metadata);
        var sourceRequirement = Assert.Single(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]));
        var optionalCostChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            sourceRequirement["optionalCostChoices"]);

        var optionalDrawCost = Assert.Single(
            optionalCostChoices,
            choice => string.Equals(choice.Id, "SPEND_POWER:green:1", StringComparison.Ordinal));
        Assert.Equal("额外支付 1 绿色符能：抽 1 张牌", optionalDrawCost.Label);
    }

    [Theory]
    [InlineData("p2-preflight-play-pyke-optional-ready-power.fixture.json", "P1-UNIT-PYKE")]
    [InlineData("p2-preflight-play-pyke-alt-a-optional-ready-power.fixture.json", "P1-UNIT-PYKE-A")]
    public async Task CoreRuleEnginePlaysPykeOptionalReadyPower(
        string fixtureFileName,
        string sourceObjectId)
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", fixtureFileName),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal([sourceObjectId], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(4, result.FinalState.CardObjects[sourceObjectId].Power);
        Assert.Equal(2, result.FinalState.CardObjects[sourceObjectId].UntilEndOfTurnPowerModifier);
        Assert.False(result.FinalState.CardObjects[sourceObjectId].IsExhausted);
        Assert.Equal([CardObjectTags.UnitCard, CardObjectTags.Standby, "游走"], result.FinalState.CardObjects[sourceObjectId].Tags);
    }

    [Fact]
    public void CoreRuleEnginePykePromptExposesOptionalRedReadyPowerCost()
    {
        var state = PunishmentState(mana: 3) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(3, 0, new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [RuneTrait.Red] = 1
                }),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-PYKE"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-PYKE"] = new(
                    "P1-UNIT-PYKE",
                    cardNo: "UNL-028/219",
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "PLAY_CARD", StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(playCandidate.Metadata);
        var sourceRequirement = Assert.Single(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]));
        var optionalCostChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            sourceRequirement["optionalCostChoices"]);

        var optionalReadyPowerCost = Assert.Single(
            optionalCostChoices,
            choice => string.Equals(choice.Id, "SPEND_POWER:red:1", StringComparison.Ordinal));
        Assert.Equal("额外支付 1 红色符能：活跃并本回合战力 +2", optionalReadyPowerCost.Label);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysBlastCrewApprenticeOptionalDamage()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-blast-crew-apprentice-optional-damage.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-UNIT-BLAST-CREW-APPRENTICE"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(2, result.FinalState.CardObjects["P1-UNIT-BLAST-CREW-APPRENTICE"].Power);
        Assert.Equal(2, result.FinalState.CardObjects["P2-BLAST-CREW-TARGET-001"].Damage);
    }

    [Fact]
    public void CoreRuleEngineBlastCrewApprenticePromptExposesOptionalDamageCost()
    {
        var state = PunishmentState(mana: 3) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(3, 0, new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [RuneTrait.Red] = 1
                }),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-BLAST-CREW-APPRENTICE"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-BLAST-CREW-TARGET-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-BLAST-CREW-APPRENTICE"] = new(
                    "P1-UNIT-BLAST-CREW-APPRENTICE",
                    cardNo: "SFD·013/221",
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-BLAST-CREW-TARGET-001"] = new(
                    "P2-BLAST-CREW-TARGET-001",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    cardNo: "UNL-097/219",
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "PLAY_CARD", StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(playCandidate.Metadata);
        var sourceRequirement = Assert.Single(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]));
        var optionalCostChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
                sourceRequirement["optionalCostChoices"])
            .ToArray();

        Assert.Equal(0, sourceRequirement["minTargetCount"]);
        // The paid entry trigger chooses a target after the permanent enters (Core Rule 359.2).
        Assert.Equal(0, sourceRequirement["maxTargetCount"]);
        Assert.Contains(optionalCostChoices, choice =>
            string.Equals(choice.Id, "SPEND_MANA:1", StringComparison.Ordinal)
            && string.Equals(choice.Label, "额外支付 1 法力，需同时支付 1 红色符能", StringComparison.Ordinal));
        Assert.Contains(optionalCostChoices, choice =>
            string.Equals(choice.Id, "SPEND_POWER:red:1", StringComparison.Ordinal)
            && string.Equals(choice.Label, "额外支付 1 红色符能，需同时支付 1 法力：对一名单位造成 2 点伤害", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysFrostcoatCubOptionalPowerMinusTwo()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-frostcoat-cub-optional-power-minus-two.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-UNIT-FROSTCOAT-CUB"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(3, result.FinalState.CardObjects["P1-UNIT-FROSTCOAT-CUB"].Power);
        Assert.Equal(1, result.FinalState.CardObjects["P2-FROSTCOAT-CUB-TARGET-001"].Power);
        Assert.Equal(-2, result.FinalState.CardObjects["P2-FROSTCOAT-CUB-TARGET-001"].UntilEndOfTurnPowerModifier);
    }

    [Fact]
    public void CoreRuleEngineFrostcoatCubPromptExposesOptionalBluePowerModifierCost()
    {
        var state = PunishmentState(mana: 3) with
        {
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(3, 0, new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [RuneTrait.Blue] = 1
                }),
                ["P2"] = RunePool.Empty
            },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-FROSTCOAT-CUB"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-FROSTCOAT-CUB-TARGET-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-FROSTCOAT-CUB"] = new(
                    "P1-UNIT-FROSTCOAT-CUB",
                    cardNo: "SFD·067/221",
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-FROSTCOAT-CUB-TARGET-001"] = new(
                    "P2-FROSTCOAT-CUB-TARGET-001",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    cardNo: "UNL-097/219",
                    ownerId: "P2",
                    controllerId: "P2")
            }
        };

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, "PLAY_CARD", StringComparison.Ordinal));
        var metadata = Assert.IsType<Dictionary<string, object?>>(playCandidate.Metadata);
        var sourceRequirement = Assert.Single(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            metadata["sourceRequirements"]));
        var optionalCostChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            sourceRequirement["optionalCostChoices"]);

        Assert.Equal(0, sourceRequirement["minTargetCount"]);
        // The paid entry trigger chooses a target after the permanent enters (Core Rule 359.2).
        Assert.Equal(0, sourceRequirement["maxTargetCount"]);
        var optionalPowerModifierCost = Assert.Single(
            optionalCostChoices,
            choice => string.Equals(choice.Id, "SPEND_POWER:blue:1", StringComparison.Ordinal));
        Assert.Equal("额外支付 1 蓝色符能：一名单位本回合战力 -2", optionalPowerModifierCost.Label);
    }

    [Theory]
    [InlineData(2, "P1-UNIT-BLAST-CREW-APPRENTICE", "SFD·013/221", "P1-BASE-BLAST-CREW-APPRENTICE-TARGET-001")]
    [InlineData(3, "P1-UNIT-FROSTCOAT-CUB", "SFD·067/221", "P1-BASE-FROSTCOAT-CUB-TARGET-001")]
    [InlineData(2, "P1-UNIT-SHIP-MONKEY", "SFD·098/221", "P1-BASE-SHIP-MONKEY-TARGET-001")]
    [InlineData(3, "P1-UNIT-PYKE", "UNL-028/219", "P1-BASE-PYKE-TARGET-001")]
    [InlineData(3, "P1-UNIT-PYKE-A", "UNL-028a/219", "P1-BASE-PYKE-A-TARGET-001")]
    [InlineData(2, "P1-UNIT-TINY-GUARDIAN", "OGN·044/298", "P1-BASE-TINY-GUARDIAN-TARGET-001")]
    [InlineData(5, "P1-UNIT-BLAZING-DRAKE", "OGN·001/298", "P1-BASE-BLAZING-DRAKE-TARGET-001")]
    [InlineData(2, "P1-UNIT-LEGION-REARGUARD", "OGN·010/298", "P1-BASE-LEGION-REARGUARD-TARGET-001")]
    [InlineData(3, "P1-UNIT-BABY-SHARK", "UNL-006/219", "P1-BASE-BABY-SHARK-TARGET-001")]
    [InlineData(3, "P1-UNIT-REKSAI", "SFD·029/221", "P1-BASE-REKSAI-TARGET-001")]
    [InlineData(3, "P1-UNIT-REKSAI-A", "SFD·029a/221", "P1-BASE-REKSAI-A-TARGET-001")]
    [InlineData(4, "P1-UNIT-KAISA", "OGN·039/298", "P1-BASE-KAISA-TARGET-001")]
    [InlineData(4, "P1-UNIT-KAISA-A", "OGN·039a/298", "P1-BASE-KAISA-A-TARGET-001")]
    [InlineData(4, "P1-UNIT-RENGAR", "UNL-024/219", "P1-BASE-RENGAR-TARGET-001")]
    [InlineData(4, "P1-UNIT-RENGAR-A", "UNL-024a/219", "P1-BASE-RENGAR-A-TARGET-001")]
    [InlineData(3, "P1-UNIT-NILAH", "UNL-115/219", "P1-BASE-NILAH-TARGET-001")]
    [InlineData(5, "P1-UNIT-MISS-FORTUNE", "OGN·162/298", "P1-BASE-MISS-FORTUNE-TARGET-001")]
    [InlineData(5, "P1-UNIT-MISS-FORTUNE-A", "OGN·162a/298", "P1-BASE-MISS-FORTUNE-A-TARGET-001")]
    [InlineData(4, "P1-UNIT-SIVIR", "SFD·143/221", "P1-BASE-SIVIR-TARGET-001")]
    [InlineData(4, "P1-UNIT-SIVIR-A", "SFD·143a/221", "P1-BASE-SIVIR-A-TARGET-001")]
    [InlineData(3, "P1-UNIT-LILLIA", "UNL-082/219", "P1-BASE-LILLIA-TARGET-001")]
    [InlineData(3, "P1-UNIT-LILLIA-A", "UNL-082a/219", "P1-BASE-LILLIA-A-TARGET-001")]
    [InlineData(4, "P1-UNIT-AZIR", "SFD·177/221", "P1-BASE-AZIR-TARGET-001")]
    [InlineData(4, "P1-UNIT-AZIR-A", "SFD·177a/221", "P1-BASE-AZIR-A-TARGET-001")]
    [InlineData(2, "P1-UNIT-MR-ROOT", "UNL-127/219", "P1-BASE-MR-ROOT-TARGET-001")]
    [InlineData(5, "P1-UNIT-MECH-MANIAC", "SFD·068/221", "P1-BASE-MECH-MANIAC-TARGET-001")]
    [InlineData(7, "P1-UNIT-XERSAI-FISH", "SFD·103/221", "P1-BASE-XERSAI-FISH-TARGET-001")]
    [InlineData(7, "P1-UNIT-KARINA-VERAZE", "SFD·179/221", "P1-BASE-KARINA-VERAZE-TARGET-001")]
    [InlineData(4, "P1-UNIT-CRIMSON-SIGNET-TREANT", "UNL-029/219", "P1-BASE-CRIMSON-SIGNET-TREANT-TARGET-001")]
    [InlineData(4, "P1-UNIT-CRIMSON-SIGNET-TREANT-A", "UNL-029a/219", "P1-BASE-CRIMSON-SIGNET-TREANT-A-TARGET-001")]
    [InlineData(7, "P1-UNIT-TASTY-FAERIE", "OGN·075/298", "P1-BASE-TASTY-FAERIE-TARGET-001")]
    [InlineData(5, "P1-UNIT-EKKO", "OGN·110/298", "P1-BASE-EKKO-TARGET-001")]
    [InlineData(6, "P1-UNIT-ARMED-ASSAULTER", "SFD·002/221", "P1-BASE-ARMED-ASSAULTER-TARGET-001")]
    [InlineData(5, "P1-UNIT-ANCIENT-BERSERKER", "SFD·131/221", "P1-BASE-ANCIENT-BERSERKER-TARGET-001")]
    [InlineData(3, "P1-UNIT-KRAKEN-HUNTER", "OGN·150/298", "P1-BASE-KRAKEN-HUNTER-TARGET-001")]
    [InlineData(6, "P1-UNIT-LEE-SIN", "OGN·151/298", "P1-BASE-LEE-SIN-TARGET-001")]
    [InlineData(6, "P1-UNIT-LEE-SIN-A", "OGN·151a/298", "P1-BASE-LEE-SIN-A-TARGET-001")]
    [InlineData(7, "P1-UNIT-THOUSAND-TAILED-WATCHER", "OGN·116/298", "P1-BASE-THOUSAND-TAILED-WATCHER-TARGET-001")]
    public Task CoreRuleEngineRejectsSourceUnitWithoutOptionalAdditionalCostWhenTargetsAreProvided(
        int mana,
        string sourceObjectId,
        string cardNo,
        string targetObjectId) =>
        AssertSourceUnitWithTargetRejectedAsync(
            mana,
            sourceObjectId,
            cardNo,
            targetObjectId);

    [Fact]
    public async Task CoreRuleEnginePlaysThousandTailedWatcherAllEnemyUnitsMinus3()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-thousand-tailed-watcher-all-enemy-units-minus-3.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(
            ["P1-BASE-THOUSAND-TAILED-WATCHER-FRIENDLY-001", "P1-UNIT-THOUSAND-TAILED-WATCHER"],
            result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(7, result.FinalState.CardObjects["P1-UNIT-THOUSAND-TAILED-WATCHER"].Power);
        Assert.Equal(
            [CardObjectTags.UnitCard, "急速"],
            result.FinalState.CardObjects["P1-UNIT-THOUSAND-TAILED-WATCHER"].Tags);
        Assert.Equal(4, result.FinalState.CardObjects["P1-BASE-THOUSAND-TAILED-WATCHER-FRIENDLY-001"].Power);
        Assert.Equal(0, result.FinalState.CardObjects["P1-BASE-THOUSAND-TAILED-WATCHER-FRIENDLY-001"].UntilEndOfTurnPowerModifier);
        Assert.Equal(2, result.FinalState.CardObjects["P2-BASE-THOUSAND-TAILED-WATCHER-TARGET-001"].Power);
        Assert.Equal(-3, result.FinalState.CardObjects["P2-BASE-THOUSAND-TAILED-WATCHER-TARGET-001"].UntilEndOfTurnPowerModifier);
        Assert.Equal(1, result.FinalState.CardObjects["P2-BATTLEFIELD-THOUSAND-TAILED-WATCHER-TARGET-001"].Power);
        Assert.Equal(-1, result.FinalState.CardObjects["P2-BATTLEFIELD-THOUSAND-TAILED-WATCHER-TARGET-001"].UntilEndOfTurnPowerModifier);
        Assert.Equal(1, result.FinalState.CardObjects["P2-BATTLEFIELD-THOUSAND-TAILED-WATCHER-TARGET-002"].Power);
        Assert.Equal(0, result.FinalState.CardObjects["P2-BATTLEFIELD-THOUSAND-TAILED-WATCHER-TARGET-002"].UntilEndOfTurnPowerModifier);
        Assert.Equal(3, result.EventKinds.Count(kind => string.Equals(kind, "POWER_MODIFIED_UNTIL_END_OF_TURN", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysHeartsplitDragonDiscardOpponentHand()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-heartsplit-dragon-discard-opponent-hand.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-UNIT-HEARTSPLIT-DRAGON"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P2-HEARTSPLIT-HAND-KEEP-001"], result.FinalState.PlayerZones["P2"].Hand);
        Assert.Equal(["P2-HEARTSPLIT-DISCARD-001"], result.FinalState.PlayerZones["P2"].Graveyard);
        Assert.Equal(7, result.FinalState.CardObjects["P1-UNIT-HEARTSPLIT-DRAGON"].Power);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsHeartsplitDragonWhenTargetIsFriendlyHand()
    {
        var state = PunishmentState(mana: 7) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-HEARTSPLIT-DRAGON", "P1-HEARTSPLIT-FRIENDLY-HAND-001"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Hand = ["P2-HEARTSPLIT-HAND-001"]
                }
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-heartsplit-dragon-friendly-hand-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-UNIT-HEARTSPLIT-DRAGON",
                "OGN·192/298",
                ["P1-HEARTSPLIT-FRIENDLY-HAND-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(7, 0), result.State.RunePools["P1"]);
        Assert.Equal(
            ["P1-UNIT-HEARTSPLIT-DRAGON", "P1-HEARTSPLIT-FRIENDLY-HAND-001"],
            result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-HEARTSPLIT-HAND-001"], result.State.PlayerZones["P2"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysCharmingSpiritDiscardOpponentHand()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-charming-spirit-discard-chosen-player-hand.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-UNIT-CHARMING-SPIRIT"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(["P2-CHARMING-SPIRIT-HAND-KEEP-001"], result.FinalState.PlayerZones["P2"].Hand);
        Assert.Equal(["P2-CHARMING-SPIRIT-DISCARD-001"], result.FinalState.PlayerZones["P2"].Graveyard);
        Assert.Equal(2, result.FinalState.CardObjects["P1-UNIT-CHARMING-SPIRIT"].Power);
        Assert.Equal([CardObjectTags.UnitCard, "灵体"], result.FinalState.CardObjects["P1-UNIT-CHARMING-SPIRIT"].Tags);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysCharmingSpiritDiscardFriendlyHand()
    {
        var engine = new CoreRuleEngine();
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-CHARMING-SPIRIT", "P1-CHARMING-SPIRIT-FRIENDLY-DISCARD-001"]
                },
                ["P2"] = PlayerZones.Empty
            }
        };

        var play = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-charming-spirit-friendly-hand-play", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-UNIT-CHARMING-SPIRIT",
                "UNL-121/219",
                ["P1-CHARMING-SPIRIT-FRIENDLY-DISCARD-001"]),
            CancellationToken.None);
        var p1Pass = await engine.ResolveAsync(
            play.State,
            new PlayerIntent("intent-charming-spirit-friendly-hand-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-charming-spirit-friendly-hand-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(play.Accepted);
        Assert.True(p1Pass.Accepted);
        Assert.True(p2Pass.Accepted);
        Assert.Equal(["P1-UNIT-CHARMING-SPIRIT"], p2Pass.State.PlayerZones["P1"].Base);
        Assert.Empty(p2Pass.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-CHARMING-SPIRIT-FRIENDLY-DISCARD-001"], p2Pass.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(2, p2Pass.State.CardObjects["P1-UNIT-CHARMING-SPIRIT"].Power);
        Assert.Equal([CardObjectTags.UnitCard, "灵体"], p2Pass.State.CardObjects["P1-UNIT-CHARMING-SPIRIT"].Tags);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsCharmingSpiritWhenTargetIsNotInAnyHand()
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-CHARMING-SPIRIT"],
                    Base = ["P1-CHARMING-SPIRIT-BASE-UNIT-001"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-CHARMING-SPIRIT-BASE-UNIT-001"] = new("P1-CHARMING-SPIRIT-BASE-UNIT-001", tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-charming-spirit-base-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-UNIT-CHARMING-SPIRIT",
                "UNL-121/219",
                ["P1-CHARMING-SPIRIT-BASE-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(3, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-CHARMING-SPIRIT"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-CHARMING-SPIRIT-BASE-UNIT-001"], result.State.PlayerZones["P1"].Base);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsCharmingSpiritWhenAnyHandTargetIsNotControlledByHandPlayer()
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-CHARMING-SPIRIT"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Hand = ["P2-DIRTY-P1-OWNED-HAND-CARD"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-CHARMING-SPIRIT"] = new(
                    "P1-UNIT-CHARMING-SPIRIT",
                    cardNo: "UNL-121/219",
                    tags: [CardObjectTags.UnitCard, "灵体"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-DIRTY-P1-OWNED-HAND-CARD"] = new(
                    "P2-DIRTY-P1-OWNED-HAND-CARD",
                    cardNo: "SFD·125/221",
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1")
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-charming-spirit-dirty-any-hand-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-UNIT-CHARMING-SPIRIT",
                "UNL-121/219",
                ["P2-DIRTY-P1-OWNED-HAND-CARD"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(3, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-CHARMING-SPIRIT"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-DIRTY-P1-OWNED-HAND-CARD"], result.State.PlayerZones["P2"].Hand);
        Assert.Empty(result.State.PlayerZones["P1"].Base);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEngineCharmingSpiritResolutionSkipsOpponentControlledHandTarget()
    {
        var engine = new CoreRuleEngine();
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-CHARMING-SPIRIT"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Hand = ["P2-CHARMING-SPIRIT-DISCARD-001", "P2-DIRTY-P1-OWNED-HAND-CARD"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-CHARMING-SPIRIT"] = new(
                    "P1-UNIT-CHARMING-SPIRIT",
                    cardNo: "UNL-121/219",
                    tags: [CardObjectTags.UnitCard, "灵体"],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P2-CHARMING-SPIRIT-DISCARD-001"] = new(
                    "P2-CHARMING-SPIRIT-DISCARD-001",
                    cardNo: "SFD·125/221",
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                ["P2-DIRTY-P1-OWNED-HAND-CARD"] = new(
                    "P2-DIRTY-P1-OWNED-HAND-CARD",
                    cardNo: "SFD·125/221",
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };

        var play = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-charming-spirit-dirty-resolution-play", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-UNIT-CHARMING-SPIRIT",
                "UNL-121/219",
                ["P2-CHARMING-SPIRIT-DISCARD-001"]),
            CancellationToken.None);
        Assert.True(play.Accepted);
        var dirtyStackItem = Assert.Single(play.State.StackItems) with
        {
            TargetObjectIds = ["P2-DIRTY-P1-OWNED-HAND-CARD"]
        };
        var dirtyStackState = play.State with
        {
            StackItems = [dirtyStackItem]
        };

        var p1Pass = await engine.ResolveAsync(
            dirtyStackState,
            new PlayerIntent("intent-charming-spirit-dirty-resolution-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-charming-spirit-dirty-resolution-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted);
        Assert.True(p2Pass.Accepted);
        Assert.Equal(["P1-UNIT-CHARMING-SPIRIT"], p2Pass.State.PlayerZones["P1"].Base);
        Assert.Equal(
            ["P2-CHARMING-SPIRIT-DISCARD-001", "P2-DIRTY-P1-OWNED-HAND-CARD"],
            p2Pass.State.PlayerZones["P2"].Hand);
        Assert.Empty(p2Pass.State.PlayerZones["P2"].Graveyard);
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "CARD_DISCARDED", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("p2-preflight-play-teemo-self-power-plus-three.fixture.json", "P1-UNIT-TEEMO")]
    [InlineData("p2-preflight-play-teemo-alt-a-self-power-plus-three.fixture.json", "P1-UNIT-TEEMO-A")]
    [InlineData("p2-preflight-play-teemo-alt-b-self-power-plus-three.fixture.json", "P1-UNIT-TEEMO-B")]
    [InlineData("p2-preflight-play-fnd-teemo-self-power-plus-three.fixture.json", "P1-UNIT-FND-TEEMO")]
    public async Task CoreRuleEnginePlaysTeemoSelfPowerPlusThree(string fixtureFileName, string sourceObjectId)
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", fixtureFileName),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal([sourceObjectId], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(4, result.FinalState.CardObjects[sourceObjectId].Power);
        Assert.Equal(3, result.FinalState.CardObjects[sourceObjectId].UntilEndOfTurnPowerModifier);
        Assert.Equal([CardObjectTags.UnitCard, CardObjectTags.Standby, "约德尔人"], result.FinalState.CardObjects[sourceObjectId].Tags);
        Assert.Empty(result.FinalState.TriggerQueue);
        Assert.Contains("TRIGGER_QUEUED", result.EventKinds);
        Assert.Contains("TRIGGER_RESOLVED", result.EventKinds);
    }

    [Theory]
    [InlineData(2, "P1-UNIT-TEEMO", "OGN·197/298", "P1-BASE-TEEMO-TARGET-001")]
    [InlineData(2, "P1-UNIT-TEEMO-A", "OGN·197a/298", "P1-BASE-TEEMO-A-TARGET-001")]
    [InlineData(2, "P1-UNIT-TEEMO-B", "OGN·197b/298", "P1-BASE-TEEMO-B-TARGET-001")]
    [InlineData(2, "P1-UNIT-FND-TEEMO", "FND-196/298", "P1-BASE-FND-TEEMO-TARGET-001")]
    public Task CoreRuleEngineRejectsTeemoWhenTargetsAreProvided(
        int mana,
        string sourceObjectId,
        string cardNo,
        string targetObjectId) =>
        AssertSourceUnitWithTargetRejectedAsync(
            mana,
            sourceObjectId,
            cardNo,
            targetObjectId);

    [Theory]
    [InlineData("p2-preflight-play-sett-self-boon.fixture.json", "P1-UNIT-SETT")]
    [InlineData("p2-preflight-play-sett-promo-self-boon.fixture.json", "P1-UNIT-SETT-PROMO")]
    [InlineData("p2-preflight-play-ogn-sett-self-boon.fixture.json", "P1-UNIT-OGN-SETT")]
    [InlineData("p2-preflight-play-ogn-sett-alt-a-self-boon.fixture.json", "P1-UNIT-OGN-SETT-A")]
    public async Task CoreRuleEnginePlaysSettSelfBoon(string fixtureFileName, string sourceObjectId)
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", fixtureFileName),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal([sourceObjectId], result.FinalState.PlayerZones["P1"].Base);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(5, result.FinalState.CardObjects[sourceObjectId].Power);
        Assert.Equal([CardObjectTags.UnitCard, CardObjectTags.Boon], result.FinalState.CardObjects[sourceObjectId].Tags);
    }

    [Theory]
    [InlineData(5, "P1-UNIT-SETT", "SFD·232/221", "P1-BASE-SETT-TARGET-001")]
    [InlineData(5, "P1-UNIT-SETT-PROMO", "SFD·232*/221", "P1-BASE-SETT-PROMO-TARGET-001")]
    [InlineData(5, "P1-UNIT-OGN-SETT", "OGN·164/298", "P1-BASE-OGN-SETT-TARGET-001")]
    [InlineData(5, "P1-UNIT-OGN-SETT-A", "OGN·164a/298", "P1-BASE-OGN-SETT-A-TARGET-001")]
    public Task CoreRuleEngineRejectsSettWhenTargetsAreProvided(
        int mana,
        string sourceObjectId,
        string cardNo,
        string targetObjectId) =>
        AssertSourceUnitWithTargetRejectedAsync(
            mana,
            sourceObjectId,
            cardNo,
            targetObjectId);

    [Fact]
    public async Task CoreRuleEnginePlaysSoulguardEquipmentGrantBoon()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-soulguard-equipment-boon.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(
            ["P1-SOULGUARD-BASE-UNIT-001", "P1-EQUIPMENT-SOULGUARD"],
            result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal([CardObjectTags.EquipmentCard], result.FinalState.CardObjects["P1-EQUIPMENT-SOULGUARD"].Tags);
        Assert.Equal(3, result.FinalState.CardObjects["P1-SOULGUARD-BASE-UNIT-001"].Power);
        Assert.Equal([CardObjectTags.UnitCard, CardObjectTags.Boon], result.FinalState.CardObjects["P1-SOULGUARD-BASE-UNIT-001"].Tags);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsSoulguardWhenTargetIsEnemyUnit()
    {
        var state = PunishmentState(mana: 2) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-EQUIPMENT-SOULGUARD"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Base = ["P2-SOULGUARD-ENEMY-UNIT-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-SOULGUARD-ENEMY-UNIT-001"] = new(
                    "P2-SOULGUARD-ENEMY-UNIT-001",
                    power: 2,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-soulguard-enemy-target", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-EQUIPMENT-SOULGUARD",
                "OGN·063/298",
                ["P2-SOULGUARD-ENEMY-UNIT-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-EQUIPMENT-SOULGUARD"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P2-SOULGUARD-ENEMY-UNIT-001"], result.State.PlayerZones["P2"].Base);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysCenterYourMindBaseDraw()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-center-your-mind-draw-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-DRAW-001", "P1-DRAW-002"], result.FinalState.PlayerZones["P1"].Hand);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysSpoilsOfWarFullCostDraw()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-spoils-of-war-draw-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-DRAW-001", "P1-DRAW-002"], result.FinalState.PlayerZones["P1"].Hand);
    }

    [Fact]
    public async Task CoreRuleEngineReducesSpoilsOfWarAfterEnemyUnitDestroyed()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-spoils-of-war-reduced-after-enemy-unit-destroyed.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P1-DRAW-001", "P1-DRAW-002"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Equal(["P2"], result.FinalState.DestroyedUnitOwnerIdsThisTurn);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysAbyssalHuntThroughStack()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-abyssal-hunt-damage-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task CoreRuleEngineBoostsAbyssalHuntWhenControllerHasFaceDownCard()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-abyssal-hunt-face-down-damage-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysDancingGrenadeAgainstBaseUnit()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-dancing-grenade-base-unit-damage.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(2, result.FinalState.CardObjects["P2-UNIT-001"].Damage);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysIncinerateThroughStack()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-incinerate-damage-stack.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
    }

    [Fact]
    public async Task CoreRuleEngineIncinerateResolutionSkipsOpponentControlledEnemyZoneTarget()
    {
        var state = PunishmentState(mana: 0) with
        {
            TimingState = TimingStates.NeutralClosed,
            ActivePlayerId = "P1",
            PriorityPlayerId = "P1",
            StackItems =
            [
                new StackItemState(
                    "STACK-INCINERATE-DIRTY-DAMAGE",
                    "P1",
                    "P1-SPELL-INCINERATE",
                    "INCINERATE_DAMAGE_2",
                    "OGS·003/024",
                    ["P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT"])
            ],
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty,
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT"] = new(
                    "P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT",
                    cardNo: "SFD·125/221",
                    manaCost: 3,
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1")
            }
        };
        var engine = new CoreRuleEngine();

        var p1Pass = await engine.ResolveAsync(
            state,
            new PlayerIntent("intent-incinerate-dirty-resolution-p1-pass", "P1", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);
        var p2Pass = await engine.ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-incinerate-dirty-resolution-p2-pass", "P2", "PASS_PRIORITY"),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p1Pass.Accepted);
        Assert.True(p2Pass.Accepted);
        Assert.Equal(["P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT"], p2Pass.State.PlayerZones["P2"].Battlefields);
        Assert.Equal(0, p2Pass.State.CardObjects["P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT"].Damage);
        Assert.Equal(["P1-SPELL-INCINERATE"], p2Pass.State.PlayerZones["P1"].Graveyard);
        Assert.DoesNotContain(p2Pass.Events, gameEvent => string.Equals(gameEvent.Kind, "DAMAGE_APPLIED", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CoreRuleEnginePlayCardRejectsGenericFieldTargetsWithMismatchedZoneControl()
    {
        var engine = new CoreRuleEngine();

        async Task AssertRejected(
            string scenarioId,
            string sourceObjectId,
            string cardNo,
            int mana,
            IReadOnlyList<string> p2Base,
            IReadOnlyList<string> p2Battlefields,
            string dirtyTargetObjectId,
            CardObjectState dirtyTargetState)
        {
            var state = PunishmentState(mana) with
            {
                PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
                {
                    ["P1"] = PlayerZones.Empty with
                    {
                        Hand = [sourceObjectId]
                    },
                    ["P2"] = PlayerZones.Empty with
                    {
                        Base = p2Base,
                        Battlefields = p2Battlefields
                    }
                },
                CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
                {
                    [sourceObjectId] = new(
                        sourceObjectId,
                        cardNo: cardNo,
                        tags: [CardObjectTags.SpellCard],
                        manaCost: mana,
                        ownerId: "P1",
                        controllerId: "P1"),
                    [dirtyTargetObjectId] = dirtyTargetState
                },
                PriorityPlayerId = null,
                StackItems = []
            };

            var result = await engine.ResolveAsync(
                state,
                new PlayerIntent($"intent-{scenarioId}", "P1", "PLAY_CARD"),
                new PlayCardCommand(sourceObjectId, cardNo, [dirtyTargetObjectId]),
                CancellationToken.None);

            Assert.False(result.Accepted);
            Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
            Assert.Contains(sourceObjectId, result.State.PlayerZones["P1"].Hand);
            Assert.Empty(result.State.StackItems);
        }

        await AssertRejected(
            "generic-dirty-battlefield",
            "P1-SPELL-INCINERATE",
            "OGS·003/024",
            2,
            [],
            ["P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT"],
            "P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT",
            new(
                "P2-DIRTY-P1-CONTROLLED-BATTLEFIELD-UNIT",
                cardNo: "SFD·125/221",
                tags: [CardObjectTags.UnitCard],
                ownerId: "P1",
                controllerId: "P1"));

        await AssertRejected(
            "generic-dirty-base-any-unit",
            "P1-SPELL-LOTUS-TRAP",
            "UNL-013/219",
            2,
            ["P2-DIRTY-P1-CONTROLLED-BASE-UNIT"],
            [],
            "P2-DIRTY-P1-CONTROLLED-BASE-UNIT",
            new(
                "P2-DIRTY-P1-CONTROLLED-BASE-UNIT",
                cardNo: "SFD·125/221",
                tags: [CardObjectTags.UnitCard],
                ownerId: "P1",
                controllerId: "P1"));

        await AssertRejected(
            "generic-dirty-equipment",
            "P1-SPELL-SIGIL-BURST",
            "SFD·005/221",
            1,
            ["P2-DIRTY-P1-CONTROLLED-EQUIPMENT"],
            [],
            "P2-DIRTY-P1-CONTROLLED-EQUIPMENT",
            new(
                "P2-DIRTY-P1-CONTROLLED-EQUIPMENT",
                cardNo: "SFD·022/221",
                tags: [CardObjectTags.EquipmentCard],
                ownerId: "P1",
                controllerId: "P1"));
    }

    [Fact]
    public async Task CoreRuleEnginePlaysLotusTrapAndDoublesNextDamage()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-lotus-trap-doubles-next-damage.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(4, result.FinalState.CardObjects["P2-UNIT-LOTUS-001"].Damage);
        Assert.Contains(
            "DAMAGE_RECEIVED_DOUBLED_THIS_TURN",
            result.FinalState.CardObjects["P2-UNIT-LOTUS-001"].UntilEndOfTurnEffects);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysNoxianGuillotineAndNextDamageDestroysUnit()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-noxian-guillotine-next-damage-destroys.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.DoesNotContain("P2-UNIT-NOXIAN-001", result.FinalState.CardObjects.Keys);
        Assert.Equal(["P2-UNIT-NOXIAN-001"], result.FinalState.PlayerZones["P2"].Graveyard);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysImperialDecreeAndDamageDestroysUnit()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-imperial-decree-damage-destroys-unit.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Empty(result.FinalState.CardObjects["P1-IMPERIAL-ALLY-001"].UntilEndOfTurnEffects);
        Assert.Single(result.FinalState.UntilEndOfTurnEffects,
            effect => effect.StartsWith("DAMAGE_DESTRUCTION|", StringComparison.Ordinal));
        Assert.DoesNotContain("P2-IMPERIAL-ENEMY-001", result.FinalState.CardObjects.Keys);
        Assert.Equal(["P2-IMPERIAL-ENEMY-001"], result.FinalState.PlayerZones["P2"].Graveyard);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysCounterstormAndPreventsNextDamage()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-counterstorm-prevent-next-damage.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.CardObjects["P2-UNIT-COUNTERSTORM-001"].Damage);
        Assert.DoesNotContain(
            "PREVENT_NEXT_DAMAGE_THIS_TURN",
            result.FinalState.CardObjects["P2-UNIT-COUNTERSTORM-001"].UntilEndOfTurnEffects);
        Assert.Equal(["P1-DRAW-001"], result.FinalState.PlayerZones["P1"].Hand);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysStandFirmAndPreventsSpellDamageThisTurn()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-stand-firm-prevent-spell-damage-this-turn.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.CardObjects["P2-UNIT-STAND-FIRM-001"].Damage);
        Assert.Contains(
            "PREVENT_SPELL_AND_SKILL_DAMAGE_THIS_TURN",
            result.FinalState.UntilEndOfTurnEffects);
        Assert.Equal(["P1-SPELL-INCINERATE"], result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal(["P2-SPELL-STAND-FIRM"], result.FinalState.PlayerZones["P2"].Graveyard);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysStandFirmAndPreventsXerathSkillDamageThisTurn()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p5-stand-firm-prevents-xerath-skill-damage.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(0, result.FinalState.CardObjects["P2-UNIT-STAND-FIRM-SKILL-001"].Damage);
        Assert.True(result.FinalState.CardObjects["P1-UNIT-XERATH"].IsExhausted);
        Assert.Contains(
            "PREVENT_SPELL_AND_SKILL_DAMAGE_THIS_TURN",
            result.FinalState.UntilEndOfTurnEffects);
    }

    [Fact]
    public async Task CoreRuleEngineCleansTriggerPowerAndPreventionAtEndTurn()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p5-end-turn-cleans-trigger-power-and-prevention.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Empty(result.FinalState.UntilEndOfTurnEffects);
        Assert.Equal(1, result.FinalState.CardObjects["P1-UNIT-TEEMO"].Power);
        Assert.Equal(0, result.FinalState.CardObjects["P1-UNIT-TEEMO"].UntilEndOfTurnPowerModifier);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsStandFirmWithTargets()
    {
        var state = PunishmentState(mana: 1) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-SPELL-STAND-FIRM"]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = ["P2-UNIT-STAND-FIRM-001"]
                }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P2-UNIT-STAND-FIRM-001"] = new("P2-UNIT-STAND-FIRM-001", power: 5)
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-stand-firm-target-rejected", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-SPELL-STAND-FIRM",
                "OGN·145/298",
                ["P2-UNIT-STAND-FIRM-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(1, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-SPELL-STAND-FIRM"], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
        Assert.Empty(result.State.UntilEndOfTurnEffects);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysJannaRemoveDamageAndMoveEnemyUnit()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-janna-remove-damage-move-enemy-unit.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Equal(["P2-UNIT-JANNA"], result.FinalState.PlayerZones["P2"].Base);
        Assert.Equal(["P1-UNIT-JANNA-ENEMY-001"], result.FinalState.PlayerZones["P1"].Base);
        Assert.Equal(2, result.FinalState.CardObjects["P2-UNIT-JANNA-FRIENDLY-001"].Damage);
        Assert.Equal(3, result.FinalState.CardObjects["P2-UNIT-JANNA"].Power);
        Assert.Equal([CardObjectTags.UnitCard], result.FinalState.CardObjects["P2-UNIT-JANNA"].Tags);
    }

    [Fact]
    public async Task CoreRuleEngineRejectsJannaWhenTargetIsFriendlyUnit()
    {
        var state = PunishmentState(mana: 3) with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = ["P1-UNIT-JANNA"],
                    Battlefields = ["P1-UNIT-JANNA-FRIENDLY-001"]
                },
                ["P2"] = PlayerZones.Empty
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                ["P1-UNIT-JANNA-FRIENDLY-001"] = new(
                    "P1-UNIT-JANNA-FRIENDLY-001",
                    power: 2,
                    tags: [CardObjectTags.UnitCard])
            }
        };

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-janna-friendly-target-rejected", "P1", "PLAY_CARD"),
            new PlayCardCommand(
                "P1-UNIT-JANNA",
                "SFD·053/221",
                ["P1-UNIT-JANNA-FRIENDLY-001"]),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.State.Tick);
        Assert.Equal(new RunePool(3, 0), result.State.RunePools["P1"]);
        Assert.Equal(["P1-UNIT-JANNA"], result.State.PlayerZones["P1"].Hand);
        Assert.Equal(["P1-UNIT-JANNA-FRIENDLY-001"], result.State.PlayerZones["P1"].Battlefields);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysWindWallCounterSpell()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-wind-wall-counter-spell.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Empty(result.FinalState.StackItems);
        Assert.Equal(0, result.FinalState.CardObjects["P2-UNIT-WIND-WALL-001"].Damage);
        Assert.Equal(["P1-SPELL-INCINERATE"], result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal(["P2-SPELL-WIND-WALL"], result.FinalState.PlayerZones["P2"].Graveyard);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysAbandonCounterSpellToHand()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-abandon-counter-spell-to-hand.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Empty(result.FinalState.StackItems);
        Assert.Equal(0, result.FinalState.CardObjects["P2-UNIT-ABANDON-001"].Damage);
        Assert.Equal(["P1-SPELL-INCINERATE"], result.FinalState.PlayerZones["P1"].Hand);
        Assert.Empty(result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal(["P2-SPELL-ABANDON"], result.FinalState.PlayerZones["P2"].Graveyard);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysFeatherstormCounterSpell()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-featherstorm-counter-spell.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Empty(result.FinalState.StackItems);
        Assert.Equal(0, result.FinalState.CardObjects["P2-UNIT-FEATHERSTORM-001"].Damage);
        Assert.Equal(["P1-SPELL-INCINERATE"], result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal(["P2-SPELL-FEATHERSTORM"], result.FinalState.PlayerZones["P2"].Graveyard);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysNightfallLullabyCounterSpell()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-nightfall-lullaby-counter-spell.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Empty(result.FinalState.StackItems);
        Assert.Equal(0, result.FinalState.CardObjects["P2-UNIT-NIGHTFALL-001"].Damage);
        Assert.Equal(["P1-SPELL-INCINERATE"], result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal(["P2-SPELL-NIGHTFALL-LULLABY"], result.FinalState.PlayerZones["P2"].Graveyard);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysHardBargainCounterSpellWhenTargetDeclinesPay()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-hard-bargain-counter-spell-decline-pay.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Empty(result.FinalState.StackItems);
        Assert.Equal(0, result.FinalState.CardObjects["P2-UNIT-HARD-BARGAIN-001"].Damage);
        Assert.Equal(["P1-SPELL-INCINERATE"], result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal(["P2-SPELL-HARD-BARGAIN"], result.FinalState.PlayerZones["P2"].Graveyard);
    }

    [Fact]
    public async Task CoreRuleEnginePlaysDefianceCounterSpellWithinCostLimits()
    {
        var fixture = await ConformanceFixture.LoadAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "p2-preflight-play-defiance-counter-spell-cost-limit.fixture.json"),
            CancellationToken.None);

        var result = await ConformanceFixtureRunner.RunAsync(
            fixture,
            new CoreRuleEngine(),
            CancellationToken.None);

        Assert.Empty(ConformanceFixtureRunner.CompareExpected(fixture, result));
        Assert.Empty(result.FinalState.StackItems);
        Assert.Equal(0, result.FinalState.CardObjects["P2-UNIT-DEFIANCE-001"].Damage);
        Assert.Equal(new RunePool(0, 2), result.FinalState.RunePools["P2"]);
        Assert.Equal(["P1-SPELL-INCINERATE"], result.FinalState.PlayerZones["P1"].Graveyard);
        Assert.Equal(["P2-SPELL-DEFIANCE"], result.FinalState.PlayerZones["P2"].Graveyard);
    }
}
