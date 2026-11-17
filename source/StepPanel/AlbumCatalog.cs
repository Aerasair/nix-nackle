namespace StepPanel;

internal static class AlbumCatalog
{
    public const int Count = 57;
    private static readonly string[] Titles =
    [
        "A Coin After Dark", "Shade and Spiced Tea", "The Candlelit Map", "A Deal in the Silk Bazaar",
        "Gold Saucer Roll", "The Chocobo Took My Hat", "Into the Aether Cavern", "Above the Clouds",
        "Snowfall in Ishgard", "A Catch in Limsa", "Lanterns in the Shroud", "After Hours in Sharlayan",
        "Dust of Sil'dihn", "One Little Festival Cake", "Fireworks over Costa del Sol", "A Hand at the Tavern",
        "The Jeweler's Loupe", "Moon over Thanalan", "Rain over Kugane", "An Armful of Moogles",
        "Paladin", "Warrior", "Dark Knight", "Gunbreaker",
        "White Mage", "Scholar", "Astrologian", "Sage",
        "Monk", "Dragoon", "Ninja", "Samurai", "Reaper", "Viper",
        "Bard", "Machinist", "Dancer", "Blue Mage", "Beastmaster",
        "Black Mage", "Summoner", "Red Mage", "Pictomancer",
        "Carpenter", "Blacksmith", "Armorer", "Goldsmith",
        "Leatherworker", "Weaver", "Alchemist", "Culinarian",
        "A Realm Reborn · Ul'dah", "Heavensward · Ishgard", "Stormblood · Kugane",
        "Shadowbringers · Il Mheg", "Endwalker · Old Sharlayan", "Dawntrail · Tuliyollal",
    ];

    public static string Title(int index) => Titles[index];

    public static int? Neighbor(ulong unlocked, int index, bool forward)
    {
        var direction = forward ? 1 : -1;
        for (var i = index + direction; i >= 0 && i < Count; i += direction)
            if ((unlocked & (1UL << i)) != 0)
                return i;

        return null;
    }
}
