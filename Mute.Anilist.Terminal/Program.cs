using Mute.Anilist;

var client = new AniListClient(new HttpClient());

var season = client.GetSeasonalMediaAsync(MediaSeason.Winter, 2026);
await foreach (var media in season)
{
    Console.WriteLine(media.Title?.Romaji);
    Console.WriteLine(media.CoverImage?.Color?.ToString() ?? "none");
}

var characters = client.SearchCharactersAsync("Saitama");
await foreach (var character in characters)
{
    Console.WriteLine(character.Name);
    Console.WriteLine(character.Description);
}

var search = client.SearchMediaAsync("One punch");
await foreach (var anime in search)
{
    Console.WriteLine(anime.Title?.English ?? anime.Title?.Romaji ?? anime.Title?.Native ?? "null");
}

var characterById = await client.GetCharacterByIdAsync(1);
if (characterById != null)
{
    Console.WriteLine(characterById.Name?.Full);
    Console.WriteLine(characterById.Description);
}