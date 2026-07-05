namespace Ventagram.Models;

public static class PublicationGroupExtensions
{
    public static string ToAdCodePrefix(this PublicationGroup group)
    {
        return group switch
        {
            PublicationGroup.Inmuebles => "INM",
            PublicationGroup.Rodados => "ROD",
            PublicationGroup.Generales => "GEN",
            PublicationGroup.Embarcaciones => "EMB",
            _ => "PUB"
        };
    }

    public static string ToAdCode(this Publication publication)
    {
        if (publication is null)
        {
            return string.Empty;
        }

        return $"{publication.Group.ToAdCodePrefix()}-{publication.Id:D3}";
    }

    public static string ToDisplayName(this PublicationGroup group)
    {
        return group switch
        {
            PublicationGroup.Rodados => "Rodados",
            PublicationGroup.Generales => "Generales",
            PublicationGroup.Embarcaciones => "Embarcaciones",
            _ => "Inmuebles"
        };
    }

    public static PublicationGroup ParseOrDefault(string? value, PublicationGroup fallback = PublicationGroup.Inmuebles)
    {
        if (byte.TryParse(value, out var byteValue) && Enum.IsDefined(typeof(PublicationGroup), byteValue))
        {
            return (PublicationGroup)byteValue;
        }

        if (Enum.TryParse<PublicationGroup>(value, true, out var byName))
        {
            return byName;
        }

        return value?.Trim() switch
        {
            "Rodados" => PublicationGroup.Rodados,
            "Generales" => PublicationGroup.Generales,
            "Embarcaciones" => PublicationGroup.Embarcaciones,
            "Lanchas" => PublicationGroup.Embarcaciones,
            "Inmuebles" => PublicationGroup.Inmuebles,
            _ => fallback
        };
    }
}
