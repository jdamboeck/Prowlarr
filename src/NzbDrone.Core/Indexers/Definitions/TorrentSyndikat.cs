using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentValidation;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Indexers.Exceptions;
using NzbDrone.Core.Indexers.Settings;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.Indexers.Definitions
{
    public class TorrentSyndikat : TorrentIndexerBase<TorrentSyndikatSettings>
    {
        public override string Name => "TorrentSyndikat";
        public override string[] IndexerUrls => new[]
        {
            "https://torrent-syndikat.org/",
            "https://tee-stube.org/"
        };
        public override string Description => "A German general tracker";
        public override string Language => "de-DE";
        public override Encoding Encoding => Encoding.UTF8;
        public override IndexerPrivacy Privacy => IndexerPrivacy.Private;
        public override IndexerCapabilities Capabilities => SetCapabilities();
        public override bool SupportsPagination => true;
        public override int PageSize => 30;

        public TorrentSyndikat(IIndexerHttpClient httpClient, IEventAggregator eventAggregator, IIndexerStatusService indexerStatusService, IConfigService configService, Logger logger)
            : base(httpClient, eventAggregator, indexerStatusService, configService, logger)
        {
        }

        public override IIndexerRequestGenerator GetRequestGenerator()
        {
            return new TorrentSyndikatRequestGenerator() { Settings = Settings, Capabilities = Capabilities };
        }

        public override IParseIndexerResponse GetParser()
        {
            return new TorrentSyndikatParser(Settings, Capabilities.Categories, _logger);
        }

        private IndexerCapabilities SetCapabilities()
        {
            var caps = new IndexerCapabilities
            {
                LimitsDefault = PageSize,
                LimitsMax = PageSize,
                TvSearchParams = new List<TvSearchParam>
                                   {
                                       TvSearchParam.Q, TvSearchParam.Season, TvSearchParam.Ep
                                   },
                MovieSearchParams = new List<MovieSearchParam>
                                   {
                                       MovieSearchParam.Q, MovieSearchParam.ImdbId, MovieSearchParam.TmdbId
                                   },
                MusicSearchParams = new List<MusicSearchParam>
                                   {
                                       MusicSearchParam.Q
                                   },
                BookSearchParams = new List<BookSearchParam>
                                   {
                                       BookSearchParam.Q
                                   },
                Flags = new List<IndexerFlag>
                {
                    IndexerFlag.Scene
                }
            };

            // Tracker categories are "cat/subCat", optionally suffixed with a variant derived from the release
            // Variants have no description, as they are not tracker categories and map 1:1 to a standard category
            caps.Categories.AddCategoryMapping("apps/awindows", NewznabStandardCategory.PC0day, "Apps / Windows");
            caps.Categories.AddCategoryMapping("apps/alinux", NewznabStandardCategory.PC, "Apps / Linux");
            caps.Categories.AddCategoryMapping("apps/amacos", NewznabStandardCategory.PCMac, "Apps / MacOS");

            caps.Categories.AddCategoryMapping("games/gwindows", NewznabStandardCategory.PCGames, "Spiele / Windows");
            caps.Categories.AddCategoryMapping("games/gmacos", NewznabStandardCategory.PCGames, "Spiele / MacOS");
            caps.Categories.AddCategoryMapping("games/glinux", NewznabStandardCategory.PCGames, "Spiele / Linux");
            caps.Categories.AddCategoryMapping("games/playstation", NewznabStandardCategory.ConsoleOther, "Spiele / Playstation");
            caps.Categories.AddCategoryMapping("games/nintendo", NewznabStandardCategory.ConsoleOther, "Spiele / Nintendo");
            caps.Categories.AddCategoryMapping("games/xbox", NewznabStandardCategory.ConsoleOther, "Spiele / XBOX");

            caps.Categories.AddCategoryMapping("video/movie", NewznabStandardCategory.Movies, "Video / Filme");
            caps.Categories.AddCategoryMapping("video/movie/uhd", NewznabStandardCategory.MoviesUHD);
            caps.Categories.AddCategoryMapping("video/movie/hd", NewznabStandardCategory.MoviesHD);
            caps.Categories.AddCategoryMapping("video/movie/sd", NewznabStandardCategory.MoviesSD);
            caps.Categories.AddCategoryMapping("video/movie/foreign", NewznabStandardCategory.MoviesForeign);
            caps.Categories.AddCategoryMapping("video/series", NewznabStandardCategory.TV, "Video / Serien");
            caps.Categories.AddCategoryMapping("video/series/uhd", NewznabStandardCategory.TVUHD);
            caps.Categories.AddCategoryMapping("video/series/hd", NewznabStandardCategory.TVHD);
            caps.Categories.AddCategoryMapping("video/series/sd", NewznabStandardCategory.TVSD);
            caps.Categories.AddCategoryMapping("video/series/foreign", NewznabStandardCategory.TVForeign);
            caps.Categories.AddCategoryMapping("video/sports", NewznabStandardCategory.TVSport, "Video / Sport");
            caps.Categories.AddCategoryMapping("video/vmusic", NewznabStandardCategory.AudioVideo, "Video / Musik");

            caps.Categories.AddCategoryMapping("audio/amusic", NewznabStandardCategory.Audio, "Audio / Musik");
            caps.Categories.AddCategoryMapping("audio/amusic/flac", NewznabStandardCategory.AudioLossless);
            caps.Categories.AddCategoryMapping("audio/amusic/mp3", NewznabStandardCategory.AudioMP3);
            caps.Categories.AddCategoryMapping("audio/audiobook", NewznabStandardCategory.AudioAudiobook, "Audio / Hörbücher");
            caps.Categories.AddCategoryMapping("audio/podcast", NewznabStandardCategory.AudioOther, "Audio / Podcasts");

            caps.Categories.AddCategoryMapping("ebook/novel", NewznabStandardCategory.BooksEBook, "eBook / Belletristik");
            caps.Categories.AddCategoryMapping("ebook/nfiction", NewznabStandardCategory.BooksEBook, "eBook / Sachbücher");
            caps.Categories.AddCategoryMapping("ebook/comic", NewznabStandardCategory.BooksComics, "eBook / Comics");
            caps.Categories.AddCategoryMapping("ebook/npaper", NewznabStandardCategory.BooksMags, "eBook / Zeitungen");
            caps.Categories.AddCategoryMapping("ebook/magazine", NewznabStandardCategory.BooksMags, "eBook / Magazine");

            caps.Categories.AddCategoryMapping("misc/misc", NewznabStandardCategory.Other, "Misc / Misc");
            caps.Categories.AddCategoryMapping("misc/grabbel", NewznabStandardCategory.OtherMisc, "Misc / Grabbelkiste");

            return caps;
        }
    }

    public class TorrentSyndikatRequestGenerator : IIndexerRequestGenerator
    {
        public TorrentSyndikatSettings Settings { get; set; }
        public IndexerCapabilities Capabilities { get; set; }

        private static readonly Regex SeasonRegex = new(@"(^|\s)(s\d{2})(\s|$)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private IndexerPageableRequestChain GetRequestChain(SearchCriteriaBase searchCriteria, string term, string uniqueId = null)
        {
            var pageableRequests = new IndexerPageableRequestChain();

            if (uniqueId != null)
            {
                // uniqueId.php pages by the id of the last torrent instead of a page number, so only the first page can be requested
                // It does not support any filters, these are applied by the parser
                if (searchCriteria.Offset is not > 0)
                {
                    pageableRequests.Add(new[] { GetRequest("uniqueId.php", new NameValueCollection { { "id", uniqueId } }) });
                }

                return pageableRequests;
            }

            // releaseType, cat and subCat only accept a single value, so every combination gets its own request to keep the pages full
            var releaseTypes = Settings.ReleaseTypes.Select(TorrentSyndikatParser.ReleaseTypeName).Distinct().ToList();
            if (releaseTypes.Count == Enum.GetValues<TorrentSyndikatReleaseTypes>().Length)
            {
                releaseTypes.Clear();
            }

            var categoryFilters = GetCategoryFilters(searchCriteria.Categories);

            foreach (var releaseType in releaseTypes.DefaultIfEmpty())
            {
                foreach (var categoryFilter in categoryFilters.DefaultIfEmpty())
                {
                    var parameters = new NameValueCollection();

                    if (releaseType != null)
                    {
                        parameters.Add("releaseType", releaseType);
                    }

                    if (!string.IsNullOrWhiteSpace(term))
                    {
                        // Suffix the first occurrence of `s01` surrounded by whitespace with *
                        // That way we also search for single episodes in a whole season search
                        parameters.Add("searchTerm", SeasonRegex.Replace(term.Trim(), @"$1$2*$3"));
                    }

                    if (categoryFilter.Key != null)
                    {
                        parameters.Add(categoryFilter.Key, categoryFilter.Value);
                    }

                    if (searchCriteria.Limit is > 0 && searchCriteria.Offset is > 0)
                    {
                        var page = (int)(searchCriteria.Offset / searchCriteria.Limit) + 1;
                        parameters.Add("pageId", page.ToString());
                    }

                    pageableRequests.Add(new[] { GetRequest("all.php", parameters) });
                }
            }

            return pageableRequests;
        }

        private List<KeyValuePair<string, string>> GetCategoryFilters(int[] categories)
        {
            var trackerCats = Capabilities.Categories.GetTrackerCategories().Select(c => c.Split('/')).ToList();
            var requestedCats = Capabilities.Categories.MapTorznabCapsToTrackers(categories).Select(c => c.Split('/')).ToList();
            var filters = new List<KeyValuePair<string, string>>();

            foreach (var cat in requestedCats.GroupBy(c => c[0]))
            {
                var subCats = cat.Select(c => c[1]).Distinct().ToList();

                if (subCats.Count == trackerCats.Where(c => c[0] == cat.Key).Select(c => c[1]).Distinct().Count())
                {
                    filters.Add(new KeyValuePair<string, string>("cat", cat.Key));
                }
                else
                {
                    filters.AddRange(subCats.Select(subCat => new KeyValuePair<string, string>("subCat", subCat)));
                }
            }

            // Every category requested is the same as no filter at all
            if (filters.Count(f => f.Key == "cat") == trackerCats.Select(c => c[0]).Distinct().Count())
            {
                filters.Clear();
            }

            return filters;
        }

        private IndexerRequest GetRequest(string endpoint, NameValueCollection parameters)
        {
            var queryCollection = new NameValueCollection { { "apiKey", Settings.ApiKey } };
            queryCollection.Add(parameters);

            var searchUrl = $"{Settings.BaseUrl.TrimEnd('/')}/api_9djWe8Tb2NE3p6opyqnh/v2/torrents/{endpoint}?{queryCollection.GetQueryString()}";

            return new IndexerRequest(searchUrl, HttpAccept.Json);
        }

        public IndexerPageableRequestChain GetSearchRequests(MovieSearchCriteria searchCriteria)
        {
            string uniqueId = null;
            if (searchCriteria.ImdbId.IsNotNullOrWhiteSpace())
            {
                uniqueId = $"imdb:{searchCriteria.ImdbId}";
            }
            else if (searchCriteria.TmdbId is > 0)
            {
                uniqueId = $"tmdb:{searchCriteria.TmdbId}";
            }

            return GetRequestChain(searchCriteria, searchCriteria.SanitizedSearchTerm, uniqueId);
        }

        public IndexerPageableRequestChain GetSearchRequests(MusicSearchCriteria searchCriteria)
        {
            return GetRequestChain(searchCriteria, searchCriteria.SanitizedSearchTerm);
        }

        public IndexerPageableRequestChain GetSearchRequests(TvSearchCriteria searchCriteria)
        {
            return GetRequestChain(searchCriteria, searchCriteria.SanitizedTvSearchString);
        }

        public IndexerPageableRequestChain GetSearchRequests(BookSearchCriteria searchCriteria)
        {
            return GetRequestChain(searchCriteria, searchCriteria.SanitizedSearchTerm);
        }

        public IndexerPageableRequestChain GetSearchRequests(BasicSearchCriteria searchCriteria)
        {
            return GetRequestChain(searchCriteria, searchCriteria.SanitizedSearchTerm);
        }

        public Func<IDictionary<string, string>> GetCookies { get; set; }
        public Action<IDictionary<string, string>, DateTime?> CookiesUpdater { get; set; }
    }

    public class TorrentSyndikatParser : IParseIndexerResponse
    {
        private readonly TorrentSyndikatSettings _settings;
        private readonly IndexerCapabilitiesCategories _categories;
        private readonly Logger _logger;

        public TorrentSyndikatParser(TorrentSyndikatSettings settings, IndexerCapabilitiesCategories categories, Logger logger)
        {
            _settings = settings;
            _categories = categories;
            _logger = logger;
        }

        public static string ReleaseTypeName(int releaseType)
        {
            return ((TorrentSyndikatReleaseTypes)releaseType).ToString().Replace("_", "").ToLowerInvariant();
        }

        public IList<ReleaseInfo> ParseResponse(IndexerResponse indexerResponse)
        {
            var httpResponse = indexerResponse.HttpResponse;

            if (httpResponse.StatusCode != HttpStatusCode.OK)
            {
                STJson.TryDeserialize<TorrentSyndikatErrorResponse>(httpResponse.ResponseData, out var error);
                var message = error?.Msg ?? httpResponse.StatusCode.ToString();

                if (httpResponse.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                {
                    throw new IndexerAuthException($"API request failed: {message}");
                }

                throw new IndexerException(indexerResponse, $"Unexpected response status {httpResponse.StatusCode} code from indexer request: {message}");
            }

            if (!httpResponse.Headers.ContentType.Contains(HttpAccept.Json.Value))
            {
                throw new IndexerException(indexerResponse, $"Unexpected response header {httpResponse.Headers.ContentType} from indexer request, expected {HttpAccept.Json.Value}");
            }

            var releaseInfos = new List<ReleaseInfo>();
            var releaseTypes = _settings.ReleaseTypes.Select(ReleaseTypeName).ToList();
            var jsonResponse = STJson.Deserialize<TorrentSyndikatResponse>(httpResponse.ResponseData);

            foreach (var item in jsonResponse.Data)
            {
                // Skip single malformed rows instead of failing the whole page
                if (!STJson.TryDeserialize<TorrentSyndikatTorrent>(item.GetRawText(), out var row))
                {
                    _logger.Warn("Skipping release that could not be parsed: {0}", item.GetRawText());
                    continue;
                }

                if (releaseTypes.Any() && !releaseTypes.Contains(row.ReleaseType))
                {
                    continue;
                }

                var details = $"{_settings.BaseUrl}details.php?id={row.Id}";

                var release = new TorrentInfo
                {
                    MinimumRatio = 1,
                    MinimumSeedTime = 96 * 60 * 60,
                    DownloadVolumeFactor = 1,
                    UploadVolumeFactor = 1,
                    Guid = details,
                    InfoUrl = details,
                    DownloadUrl = $"{_settings.BaseUrl.TrimEnd('/')}/api_9djWe8Tb2NE3p6opyqnh/v2/torrents/download.php?id={row.Id}&apiKey={_settings.ApiKey}",
                    Title = row.Name,
                    Categories = ParseCategories(row),
                    PublishDate = DateTimeUtil.UnixTimestampToDateTime(row.Added),
                    Size = row.Size,
                    Files = row.Files,
                    Seeders = row.Seeders,
                    Peers = row.Seeders,
                    Grabs = row.Downloads,
                    Genres = row.Genres?.Select(g => g.Name).ToList() ?? new List<string>(),
                    IndexerFlags = row.ReleaseType == "scene" ? new HashSet<IndexerFlag> { IndexerFlag.Scene } : new HashSet<IndexerFlag>()
                };

                var descriptions = new List<string>();

                if (row.Product != null)
                {
                    release.ImdbId = row.Product.ImdbId.GetValueOrDefault();
                    release.TmdbId = row.Product.TmdbId.GetValueOrDefault();
                    release.TvdbId = row.Product.TvdbId.GetValueOrDefault();
                    release.TvMazeId = row.Product.TvmazeId.GetValueOrDefault();
                    release.Year = row.Product.Year.GetValueOrDefault();

                    if (row.Product.Title.IsNotNullOrWhiteSpace())
                    {
                        descriptions.Add("Title: " + row.Product.Title);
                    }

                    if (row.Product.TitleOrigin.IsNotNullOrWhiteSpace())
                    {
                        descriptions.Add("Original Title: " + row.Product.TitleOrigin);
                    }

                    descriptions.Add($"Product-Link: {_settings.BaseUrl}product.php?pid={row.Product.Pid}");
                }

                if (row.Tags is { Count: > 0 })
                {
                    descriptions.Add("Tags: " + string.Join(", ", row.Tags));
                }

                if (row.ReleaseType.IsNotNullOrWhiteSpace())
                {
                    descriptions.Add("Release Type: " + row.ReleaseType);
                }

                if (descriptions.Any())
                {
                    release.Description = string.Join(Environment.NewLine, descriptions);
                }

                releaseInfos.Add(release);
            }

            return releaseInfos.ToArray();
        }

        private List<IndexerCategory> ParseCategories(TorrentSyndikatTorrent row)
        {
            var trackerCat = $"{row.Cat}/{row.SubCat}";
            var categories = _categories.MapTrackerCatToNewznab(trackerCat).ToList();
            var tags = row.Tags ?? new List<string>();

            string variant = null;
            if (row.IsForeign == true)
            {
                variant = "foreign";
            }
            else if (tags.Contains("2160p") || tags.Contains("4320p"))
            {
                variant = "uhd";
            }
            else if (tags.Contains("1080p") || tags.Contains("1080i") || tags.Contains("720p"))
            {
                variant = "hd";
            }
            else if (tags.Contains("sd"))
            {
                variant = "sd";
            }
            else if (tags.Contains("flac"))
            {
                variant = "flac";
            }
            else if (tags.Contains("mp3"))
            {
                variant = "mp3";
            }

            if (variant != null)
            {
                categories.AddRange(_categories.MapTrackerCatToNewznab($"{trackerCat}/{variant}"));
            }

            return categories;
        }

        public Action<IDictionary<string, string>, DateTime?> CookiesUpdater { get; set; }
    }

    public class TorrentSyndikatSettingsValidator : NoAuthSettingsValidator<TorrentSyndikatSettings>
    {
        public TorrentSyndikatSettingsValidator()
        {
            RuleFor(c => c.ApiKey).NotEmpty();
        }
    }

    public class TorrentSyndikatSettings : NoAuthTorrentBaseSettings
    {
        private static readonly TorrentSyndikatSettingsValidator Validator = new();

        public TorrentSyndikatSettings()
        {
            ApiKey = "";
            ReleaseTypes = new List<int>();
        }

        [FieldDefinition(2, Label = "ApiKey", Privacy = PrivacyLevel.ApiKey, HelpText = "IndexerTorrentSyndikatSettingsApiKeyHelpText")]
        public string ApiKey { get; set; }

        [FieldDefinition(3, Label = "Release Types", Type = FieldType.Select, SelectOptions = typeof(TorrentSyndikatReleaseTypes))]
        public IEnumerable<int> ReleaseTypes { get; set; }

        public override NzbDroneValidationResult Validate()
        {
            return new NzbDroneValidationResult(Validator.Validate(this));
        }
    }

    public enum TorrentSyndikatReleaseTypes
    {
        [FieldOption]
        P2P = 1,
        [FieldOption]
        Scene = 2,
        [FieldOption]
        O_Scene = 3
    }

    internal class TorrentSyndikatResponse
    {
        public IReadOnlyCollection<JsonElement> Data { get; init; }
    }

    internal class TorrentSyndikatErrorResponse
    {
        public string Msg { get; init; }
    }

    internal class TorrentSyndikatTorrent
    {
        public int Id { get; init; }
        public string Name { get; init; }
        public long Added { get; init; }
        public bool? IsForeign { get; init; }
        public long Size { get; init; }
        public string Cat { get; init; }
        public string SubCat { get; init; }
        public int? Files { get; init; }
        public int? Downloads { get; init; }
        public int? Seeders { get; init; }
        public string ReleaseType { get; init; }
        public IReadOnlyCollection<TorrentSyndikatGenre> Genres { get; init; }
        public List<string> Tags { get; init; }
        public TorrentSyndikatProduct Product { get; init; }
    }

    internal class TorrentSyndikatGenre
    {
        public string Name { get; init; }
    }

    internal class TorrentSyndikatProduct
    {
        public int Pid { get; init; }
        public string Title { get; init; }
        public string TitleOrigin { get; init; }
        public int? Year { get; init; }
        public int? ImdbId { get; init; }
        public int? TmdbId { get; init; }
        public int? TvdbId { get; init; }
        public int? TvmazeId { get; init; }
    }
}
