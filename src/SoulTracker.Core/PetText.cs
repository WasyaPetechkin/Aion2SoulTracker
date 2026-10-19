using System.Globalization;

namespace SoulTracker.Core;

/// <summary>Display language never changes the catalog ID or the protocol's counters.</summary>
public static class PetText
{
    private static readonly CompareInfo RussianCompare=CultureInfo.GetCultureInfo("ru-RU").CompareInfo;
    public static string Name(PetProgress pet, string language) => language == "ru" &&
        !string.IsNullOrWhiteSpace(pet.NameRu) ? pet.NameRu : pet.Name;
    public static string Category(string genus, string language) => language != "ru" ? genus : genus switch
    {
        "Cogni" => "Разумные", "Natura" => "Природные", "Fera" => "Дикие",
        "Varian" => "Мутанты", "Special" => "Диковинные", _ => "Неизвестная"
    };
    public static bool Matches(PetProgress pet, string search)
    {
        search=search.Trim();
        return search.Length==0 || Contains(pet.Name,search) || Contains(pet.NameRu ?? "",search) ||
            pet.Id.ToString(CultureInfo.InvariantCulture).Contains(search,StringComparison.Ordinal);
    }
    private static bool Contains(string text,string query) => RussianCompare.IndexOf(text,query,
        CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;
    public static string Level(PetProgress pet,string language) => pet.Level==0
        ? language=="ru" ? "Не получен" : "Not owned"
        : language=="ru" ? $"Ур. {pet.Level}" : $"Lv. {pet.Level}";
    public static string Remaining(PetProgress pet,string language) => pet.Remaining?.ToString(CultureInfo.InvariantCulture) ??
        (pet.AwaitingLevelConfirmation ? language=="ru" ? "Нужна синхронизация" : "Sync required" : "—");
}
