using System.Text;

// The native perk leads; active shared perks of the same kind appear only once.
public static class CityPerkIcons
{
    public static string IconFor(City city, CityPerkKind kind)
    {
        if (city == null) return "";
        if (city.data != null && city.data.perk != null && city.data.perk.kind == kind)
            return city.data.perk.IconTag;
        foreach (var bond in TurnManager.Instance.Bonds.All)
        {
            City partner = bond.Other(city);
            if (bond.Active && partner != null && partner.data != null &&
                partner.data.perk != null && partner.data.perk.kind == kind) return partner.data.perk.IconTag;
        }
        return "";
    }

    public static string Row(City city)
    {
        var text = new StringBuilder();
        int shown = 0;
        var bonds = TurnManager.Instance.Bonds;
        Append(city.data != null ? city.data.perk : null);
        foreach (var bond in bonds.All)
        {
            City partner = bond.Other(city);
            if (bond.Active && partner != null && partner.data != null) Append(partner.data.perk);
        }
        return text.ToString();

        void Append(CityPerkData perk)
        {
            if (perk == null || perk.kind == CityPerkKind.None) return;
            int bit = 1 << (int)perk.kind;
            if ((shown & bit) != 0) return;
            shown |= bit;
            if (text.Length > 0) text.Append("  ");
            text.Append(perk.icon != null ? perk.IconTag.TrimEnd() : perk.perkName);
            text.Append($"<sup>{bonds.GetLevel(city, perk.kind)}</sup>");
        }
    }
}
