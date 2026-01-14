using System.Drawing;
using JetBrains.Annotations;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mute.Anilist
{
    public class AniListClient
    {
        private readonly HttpClient _httpClient;
        private const string ApiUrl = "https://graphql.anilist.co";

        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };

        /// <summary>
        /// Amount of allowed requests per minute
        /// </summary>
        public int? RateLimitLimit { get; private set; }

        /// <summary>
        /// How many requests remain in the current minute
        /// </summary>
        public int? RateLimitRemaining { get; private set; }

        private int? _rateLimitReset;
        /// <summary>
        /// Unix timestamp of when a request can next be made
        /// </summary>
        public DateTimeOffset? RateLimitReset => _rateLimitReset.HasValue ? DateTimeOffset.FromUnixTimeSeconds(_rateLimitReset.Value) : null;

        public AniListClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <summary>
        /// Get details for a specific Media by its AniList ID.
        /// </summary>
        public async Task<Media?> GetMediaByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            const string query = """
                                 query ($id: Int) {
                                     Media (id: $id) {
                                         id
                                         type
                                         title { romaji english native }
                                         description
                                         siteUrl
                                         startDate { year month day }
                                         endDate { year month day }
                                         isAdult
                                         episodes
                                         status
                                         genres
                                         averageScore
                                         season
                                         seasonYear
                                         coverImage { extraLarge large medium color }
                                         relations {
                                             edges {
                                                 relationType(version: 2)
                                                 node {
                                                     id
                                                     type
                                                     title { romaji english native }
                                                 }
                                             }
                                         }
                                         characters {
                                             edges {
                                                 node {
                                                     id
                                                     name {
                                                         full
                                                     }
                                                     image {
                                                         large
                                                     }
                                                 }
                                                 role
                                             }
                                         }
                                     }
                                 }
                                 """;

            var variables = new { id = id };
            var response = await SendRequestAsync<MediaData>(query, variables, cancellationToken);
            return response?.Media;
        }

        /// <summary>
        /// Search for anime by title string.
        /// Yields results page by page.
        /// </summary>
        public async IAsyncEnumerable<Media> SearchAnimesAsync(string searchString, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            const string query = """
                                 query ($search: String, $page: Int) {
                                     Page (page: $page, perPage: 50) {
                                         pageInfo {
                                             currentPage
                                             hasNextPage
                                         }
                                         media (search: $search) {
                                             id
                                             type
                                             title { romaji english native }
                                             description
                                             siteUrl
                                             startDate { year month day }
                                             endDate { year month day }
                                             isAdult
                                             episodes
                                             status
                                             genres
                                             averageScore
                                             season
                                             seasonYear
                                             coverImage { extraLarge large medium color }
                                             relations {
                                                 edges {
                                                     relationType(version: 2)
                                                     node {
                                                         id
                                                         type
                                                         title { romaji english native }
                                                     }
                                                 }
                                             }
                                             characters {
                                                 edges {
                                                     node {
                                                         id
                                                         name {
                                                             full
                                                         }
                                                         image {
                                                             large
                                                         }
                                                     }
                                                     role
                                                 }
                                             }
                                         }
                                     }
                                 }
                                 """;

            var currentPage = 1;
            var hasNextPage = true;

            while (hasNextPage)
            {
                // Check for cancellation before fetching the next page
                cancellationToken.ThrowIfCancellationRequested();

                var variables = new { search = searchString, page = currentPage };
                var response = await SendRequestAsync<PageData>(query, variables, cancellationToken);

                if (response?.Page == null)
                    break;

                // Yield return items from the current page
                if (response.Page.Media != null)
                    foreach (var media in response.Page.Media)
                        yield return media;

                // Setup next iteration
                hasNextPage = response.Page.PageInfo?.HasNextPage ?? false;
                currentPage++;
            }
        }

        /// <summary>
        /// Get all anime airing in a specific season and year.
        /// Yields results page by page.
        /// </summary>
        public async IAsyncEnumerable<Media> GetSeasonalAnimesAsync(MediaSeason season, int year, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            const string query = """
                                 query ($season: MediaSeason, $year: Int, $page: Int) {
                                     Page (page: $page, perPage: 50) {
                                         pageInfo {
                                             currentPage
                                             hasNextPage
                                         }
                                         media (season: $season, seasonYear: $year, sort: POPULARITY_DESC) {
                                             id
                                             type
                                             title { romaji english native }
                                             description
                                             siteUrl
                                             startDate { year month day }
                                             endDate { year month day }
                                             isAdult
                                             episodes
                                             status
                                             genres
                                             averageScore
                                             season
                                             seasonYear
                                             coverImage { extraLarge large medium color }
                                             relations {
                                                 edges {
                                                     relationType(version: 2)
                                                     node {
                                                         id
                                                         type
                                                         title { romaji english native }
                                                     }
                                                 }
                                             }
                                             characters {
                                                 edges {
                                                     node {
                                                         id
                                                         name {
                                                             full
                                                         }
                                                         image {
                                                             large
                                                         }
                                                     }
                                                     role
                                                 }
                                             }
                                         }
                                     }
                                 }
                                 """;

            var currentPage = 1;
            var hasNextPage = true;

            while (hasNextPage)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var variables = new { season = season.ToString().ToUpper(), year = year, page = currentPage };
                var response = await SendRequestAsync<PageData>(query, variables, cancellationToken);

                if (response?.Page == null) break;

                if (response.Page.Media != null)
                    foreach (var media in response.Page.Media)
                        yield return media;

                hasNextPage = response.Page.PageInfo?.HasNextPage ?? false;
                currentPage++;
            }
        }

        /// <summary>
        /// Get all related media (e.g. sequels, prequels, adaptations)
        /// </summary>
        public async Task<IReadOnlyList<MediaEdge>> GetRelatedMediaAsync(Media media, CancellationToken cancellationToken = default)
        {
            return await GetRelatedMediaAsync(media.Id, cancellationToken);
        }

        /// <summary>
        /// Get all related media (e.g. sequels, prequels, adaptations)
        /// </summary>
        public async Task<IReadOnlyList<MediaEdge>> GetRelatedMediaAsync(int id, CancellationToken cancellationToken = default)
        {
            const string query = """
                                 query ($id: Int) {
                                     Media (id: $id) {
                                         relations {
                                             edges {
                                                 relationType(version: 2)
                                                 node {
                                                     id
                                                     type
                                                     title { romaji english native }
                                                     description
                                                     siteUrl
                                                     startDate { year month day }
                                                     endDate { year month day }
                                                     isAdult
                                                     episodes
                                                     status
                                                     genres
                                                     averageScore
                                                     season
                                                     seasonYear
                                                     coverImage { extraLarge large medium color }
                                                     relations {
                                                         edges {
                                                             relationType(version: 2)
                                                             node {
                                                                 id
                                                                 type
                                                                 title { romaji english native }
                                                             }
                                                         }
                                                     }
                                                     characters {
                                                         edges {
                                                             node {
                                                                 id
                                                                 name {
                                                                     full
                                                                 }
                                                                 image {
                                                                     large
                                                                 }
                                                             }
                                                             role
                                                         }
                                                     }
                                                 }
                                             }
                                         }
                                     }
                                 }
                                 """;

            var variables = new { id = id };
            var response = await SendRequestAsync<MediaData>(query, variables, cancellationToken);

            return response?.Media?.Relations?.Edges ?? [ ];
        }

        /// <summary>
        /// Get all characters for a given media ID
        /// </summary>
        public async Task<IReadOnlyList<CharacterEdge>> GetCharactersAsync(int id, CancellationToken cancellationToken = default)
        {
            const string query = """
                                 query ($id: Int) {
                                     Media (id: $id) {
                                         characters {
                                             edges {
                                                 node {
                                                     id
                                                     name {
                                                         first
                                                         middle
                                                         last
                                                         full
                                                         native
                                                         alternative
                                                     }
                                                     description
                                                     siteUrl
                                                     image {
                                                         large
                                                         medium
                                                     }
                                                 }
                                                 role
                                             }
                                         }
                                     }
                                 }
                                 """;

            var variables = new { id = id };
            var response = await SendRequestAsync<MediaData>(query, variables, cancellationToken);

            return response?.Media?.Characters?.Edges ?? [ ];
        }

        /// <summary>
        /// Search for characters by name.
        /// Yields results page by page.
        /// </summary>
        public async IAsyncEnumerable<Character> SearchCharactersAsync(string searchString, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            const string query = """
                                 query ($search: String, $page: Int) {
                                     Page (page: $page, perPage: 50) {
                                         pageInfo {
                                             currentPage
                                             hasNextPage
                                         }
                                         characters (search: $search) {
                                             id
                                             name {
                                                 first
                                                 middle
                                                 last
                                                 full
                                                 native
                                                 alternative
                                             }
                                             description
                                             siteUrl
                                             image {
                                                 large
                                                 medium
                                             }
                                         }
                                     }
                                 }
                                 """;

            var currentPage = 1;
            var hasNextPage = true;

            while (hasNextPage)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var variables = new { search = searchString, page = currentPage };
                var response = await SendRequestAsync<CharacterPageData>(query, variables, cancellationToken);

                if (response?.Page == null)
                    break;

                if (response.Page.Characters != null)
                    foreach (var character in response.Page.Characters)
                        yield return character;

                hasNextPage = response.Page.PageInfo?.HasNextPage ?? false;
                currentPage++;
            }
        }

        private async Task<T?> SendRequestAsync<T>(string query, object variables, CancellationToken cancellation = default)
            where T : class
        {
            // Wait for the rate limit to reset if we have hit the limit
            if (RateLimitRemaining is 0 && RateLimitReset.HasValue)
            {
                var reset = RateLimitReset.Value;
                var delay = reset - DateTimeOffset.UtcNow;
                if (delay > TimeSpan.Zero)
                    await Task.Delay(delay + TimeSpan.FromSeconds(0.5f), cancellation);
            }

            // Serialize payload to JSON
            var jsonPayload = JsonSerializer.Serialize(
                new
                {
                    query,
                    variables
                },
                _jsonOptions
            );

            // Retry request up to 3 times
            for (var i = 0; i < 3; i++)
            {
                // Send request
                using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                using var response = await _httpClient.PostAsync(ApiUrl, content, cancellation);

                // Read rate limit headers
                if (response.Headers.TryGetValues("X-RateLimit-Limit", out var limitValues) && int.TryParse(limitValues.FirstOrDefault(), out var limit))
                    RateLimitLimit = limit;
                if (response.Headers.TryGetValues("X-RateLimit-Remaining", out var remainingValues) && int.TryParse(remainingValues.FirstOrDefault(), out var remaining))
                    RateLimitRemaining = remaining;
                if (response.Headers.TryGetValues("X-RateLimit-Reset", out var resetValues) && int.TryParse(resetValues.FirstOrDefault(), out var reset))
                    _rateLimitReset = reset;

                // If the request fails due to the rate limit, wait for the specified amount of time and then try again
                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    var retry = response.Headers.RetryAfter?.Delta;
                    if (retry.HasValue)
                    {
                        await Task.Delay(retry.Value, cancellation);
                        continue;
                    }
                }

                // Read response
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<GraphQLResponse<T>>(_jsonOptions, cancellation);
                return result?.Data;
            }

            return null;
        }
    }

    #region Models
    public enum MediaSeason { Winter, Spring, Summer, Fall }

    public enum MediaStatus
    {
        Finished,
        Releasing,

        [JsonStringEnumMemberName("NOT_YET_RELEASED")]
        NotYetReleased,

        Cancelled,
        Hiatus
    }

    public enum MediaType
    {
        /// <summary>
        /// Japanese Anime
        /// </summary>
        Anime,

        /// <summary>
        /// Asian Manga, Manhwa, Manhua
        /// </summary>
        Manga,

        Unknown
    }

    public enum CharacterRole
    {
        Main,
        Supporting,
        Background
    }

    public enum MediaRelation
    {
        /// <summary>
        /// An adaption of this media into a different format 
        /// </summary>
        Adaptation,

        /// <summary>
        /// Released before the relation 
        /// </summary>
        Prequel,

        /// <summary>
        /// Released after the relation 
        /// </summary>
        Sequel,

        /// <summary>
        /// The media a side story is from 
        /// </summary>
        Parent,

        /// <summary>
        /// A side story of the parent media 
        /// </summary>
        [JsonStringEnumMemberName("SIDE_STORY")]
        SideStory,

        /// <summary>
        /// Shares at least 1 character 
        /// </summary>
        Character,

        /// <summary>
        /// A shortened and summarized version 
        /// </summary>
        Summary,

        /// <summary>
        /// An alternative version of the same media 
        /// </summary>
        Alternative,

        /// <summary>
        /// An alternative version of the media with a different primary focus 
        /// </summary>
        [JsonStringEnumMemberName("SPIN_OFF")]
        SpinOff,

        /// <summary>
        /// Other 
        /// </summary>
        Other,

        /// <summary>
        /// The source material the media was adapted from 
        /// </summary>
        Source,

        Compilation,

        Contains
    }

    [UsedImplicitly]
    internal class GraphQLResponse<T> { public T? Data { get; init; } }

    [UsedImplicitly]
    internal class MediaData { public Media? Media { get; init; } }

    [UsedImplicitly]
    internal class PageData { public Page? Page { get; init; } }

    [UsedImplicitly]
    internal class CharacterPageData { public Page? Page { get; init; } }

    [UsedImplicitly]
    internal class Page
    {
        public PageInfo? PageInfo { get; init; }
        public List<Media>? Media { get; init; }
        public List<Character>? Characters { get; init; }
    }

    [UsedImplicitly]
    internal class PageInfo
    {
        public int CurrentPage { get; init; }
        public bool HasNextPage { get; init; }
    }

    [UsedImplicitly]
    public class Media
    {
        public int Id { get; init; }
        public MediaType? Type { get; init; }
        public MediaTitle? Title { get; init; }
        public string? Description { get; init; }
        public string? SiteUrl { get; init; }
        public FuzzyDate? StartDate { get; init; }
        public FuzzyDate? EndDate { get; init; }
        public bool IsAdult { get; init; }
        public int? Episodes { get; init; }
        public MediaStatus? Status { get; init; }
        public List<string>? Genres { get; init; }
        public int? AverageScore { get; init; }
        public MediaSeason? Season { get; init; }
        public int? SeasonYear { get; init; }
        public CoverImage? CoverImage { get; init; }
        public MediaConnection? Relations { get; init; }
        public CharacterConnection? Characters { get; init; }
    }

    [UsedImplicitly]
    public class MediaConnection
    {
        public List<MediaEdge>? Edges { get; init; }
    }

    [UsedImplicitly]
    public class MediaEdge
    {
        public MediaRelation RelationType { get; init; }
        public MediaEdgeNode? Node { get; init; }
    }

    [UsedImplicitly]
    public class MediaEdgeNode
    {
        public int Id { get; init; }
        public MediaType? Type { get; init; }
        public MediaTitle? Title { get; init; }
    }

    [UsedImplicitly]
    public class CharacterConnection
    {
        public List<CharacterEdge>? Edges { get; init; }
    }

    [UsedImplicitly]
    public class CharacterEdge
    {
        public CharacterRole Role { get; init; }
        public Character? Node { get; init; }
    }

    [UsedImplicitly]
    public class FuzzyDate
    {
        public int? Year { get; init; }
        public int? Month { get; init; }
        public int? Day { get; init; }
    }

    [UsedImplicitly]
    public class MediaTitle
    {
        public string? Romaji { get; init; }
        public string? English { get; init; }
        public string? Native { get; init; }
    }

    [UsedImplicitly]
    public class CoverImage
    {
        public string? ExtraLargeUrl { get; init; }
        public string? LargeUrl { get; init; }
        public string? MediumUrl { get; init; }

        [JsonPropertyName("color")]
        public string? ColorHex { get; init; }

        [JsonIgnore]
        public Color? Color => !string.IsNullOrEmpty(ColorHex) ? ColorTranslator.FromHtml(ColorHex) : null;
    }

    [UsedImplicitly]
    public class Character
    {
        public int Id { get; init; }
        public CharacterName? Name { get; init; }
        public string? Description { get; init; }
        public string? SiteUrl { get; init; }
        public CharacterImage? Image { get; init; }
    }

    [UsedImplicitly]
    public class CharacterName
    {
        public string? First { get; init; }
        public string? Middle { get; init; }
        public string? Last { get; init; }
        public string? Full { get; init; }
        public string? Native { get; init; }
        public List<string>? Alternative { get; init; }
    }

    [UsedImplicitly]
    public class CharacterImage
    {
        public string? Large { get; init; }
        public string? Medium { get; init; }
    }
    #endregion
}