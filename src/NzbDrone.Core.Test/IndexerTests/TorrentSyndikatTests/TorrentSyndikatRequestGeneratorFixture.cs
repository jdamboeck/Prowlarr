using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Indexers.Definitions;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.IndexerTests.TorrentSyndikatTests
{
    public class TorrentSyndikatRequestGeneratorFixture : CoreTest<TorrentSyndikatRequestGenerator>
    {
        [SetUp]
        public void Setup()
        {
            Subject.Settings = new TorrentSyndikatSettings
            {
                BaseUrl = "https://torrent-syndikat.org/",
                ApiKey = "abcd"
            };

            Subject.Capabilities = Mocker.Resolve<TorrentSyndikat>().Capabilities;
        }

        private static List<IndexerRequest> GetRequests(IndexerPageableRequestChain chain)
        {
            return chain.GetAllTiers().SelectMany(r => r).ToList();
        }

        private static string GetParameter(IndexerRequest request, string name)
        {
            return Regex.Match(request.Url.Query, $@"(?:^|&){name}=([^&]*)").Groups[1].Value;
        }

        [Test]
        public void should_search_all_endpoint_with_api_key()
        {
            var requests = GetRequests(Subject.GetSearchRequests(new BasicSearchCriteria { SearchTerm = "Spider-Man" }));

            requests.Should().ContainSingle();
            requests[0].Url.FullUri.Should().Be("https://torrent-syndikat.org/api_9djWe8Tb2NE3p6opyqnh/v2/torrents/all.php?apiKey=abcd&searchTerm=Spider-Man");
        }

        [Test]
        public void should_only_send_api_key_for_rss()
        {
            var requests = GetRequests(Subject.GetSearchRequests(new BasicSearchCriteria()));

            requests.Single().Url.FullUri.Should().Be("https://torrent-syndikat.org/api_9djWe8Tb2NE3p6opyqnh/v2/torrents/all.php?apiKey=abcd");
        }

        [Test]
        public void should_request_json()
        {
            var requests = GetRequests(Subject.GetSearchRequests(new BasicSearchCriteria { SearchTerm = "Spider-Man", Categories = [5000] }))
                .Concat(GetRequests(Subject.GetSearchRequests(new MovieSearchCriteria { ImdbId = "22084616" })))
                .ToList();

            requests.Should().HaveCount(3);
            requests.Should().OnlyContain(r => r.HttpRequest.Headers.Accept == "application/json");
        }

        [Test]
        public void should_use_alternative_domain()
        {
            Subject.Settings.BaseUrl = "https://tee-stube.org/";

            var requests = GetRequests(Subject.GetSearchRequests(new BasicSearchCriteria { SearchTerm = "Spider-Man" }));

            requests.Single().Url.FullUri.Should().StartWith("https://tee-stube.org/api_9djWe8Tb2NE3p6opyqnh/v2/torrents/all.php?");
        }

        [Test]
        public void should_request_each_category_filter()
        {
            // Default sync categories of the *arr applications and some single categories
            var cases = new Dictionary<string, (int[] Categories, string[] Filters)>
            {
                { "Radarr", (new[] { 2000, 2010, 2020, 2030, 2040, 2045, 2050, 2060, 2070, 2080, 2090 }, new[] { "subCat=movie" }) },
                { "Sonarr", (new[] { 5000, 5010, 5020, 5030, 5040, 5045, 5050, 5090 }, new[] { "subCat=series", "subCat=sports" }) },
                { "Lidarr", (new[] { 3000, 3010, 3030, 3040, 3050, 3060 }, new[] { "cat=audio", "subCat=vmusic" }) },
                { "Readarr", (new[] { 3030, 7000, 7010, 7020, 7030, 7040, 7050, 7060 }, new[] { "cat=ebook", "subCat=audiobook" }) },
                { "Mylar", (new[] { 7030 }, new[] { "subCat=comic" }) },
                { "Movies/UHD", (new[] { 2045 }, new[] { "subCat=movie" }) },
                { "TV/UHD", (new[] { 5045 }, new[] { "subCat=series" }) },
                { "PC", (new[] { 4000 }, new[] { "cat=apps", "subCat=gwindows", "subCat=gmacos", "subCat=glinux" }) },
                { "All", (new[] { 1000, 2000, 3000, 4000, 5000, 7000, 8000 }, new[] { "" }) },
                { "None", (Array.Empty<int>(), new[] { "" }) }
            };

            var actual = cases.ToDictionary(c => c.Key, c => GetRequests(Subject.GetSearchRequests(new BasicSearchCriteria { SearchTerm = "test", Categories = c.Value.Categories }))
                .Select(r => Regex.Match(r.Url.Query, "(?:subCat|cat)=[^&]+").Value).ToArray());

            actual.Should().BeEquivalentTo(cases.ToDictionary(c => c.Key, c => c.Value.Filters));
        }

        [Test]
        public void should_filter_single_release_type()
        {
            Subject.Settings.ReleaseTypes = new List<int> { (int)TorrentSyndikatReleaseTypes.O_Scene };

            var requests = GetRequests(Subject.GetSearchRequests(new MovieSearchCriteria { SearchTerm = "test", Categories = [2000] }));

            requests.Select(r => GetParameter(r, "releaseType")).Should().BeEquivalentTo("oscene");
        }

        [Test]
        public void should_request_each_selected_release_type_and_category()
        {
            Subject.Settings.ReleaseTypes = new List<int> { (int)TorrentSyndikatReleaseTypes.P2P, (int)TorrentSyndikatReleaseTypes.Scene };

            var requests = GetRequests(Subject.GetSearchRequests(new TvSearchCriteria { SearchTerm = "test", Categories = [5000] }));

            requests.Select(r => $"{GetParameter(r, "releaseType")}/{GetParameter(r, "subCat")}")
                .Should().BeEquivalentTo("p2p/series", "p2p/sports", "scene/series", "scene/sports");
        }

        [Test]
        public void should_not_filter_release_type_if_all_are_selected()
        {
            Subject.Settings.ReleaseTypes = new List<int> { (int)TorrentSyndikatReleaseTypes.P2P, (int)TorrentSyndikatReleaseTypes.Scene, (int)TorrentSyndikatReleaseTypes.O_Scene };

            var requests = GetRequests(Subject.GetSearchRequests(new BasicSearchCriteria { SearchTerm = "test" }));

            requests.Single().Url.Query.Should().NotContain("releaseType");
        }

        [Test]
        public void should_add_wildcard_to_season_search()
        {
            var requests = GetRequests(Subject.GetSearchRequests(new TvSearchCriteria { SearchTerm = "Dig 2026", Season = 1, Categories = [5000] }));

            requests.Should().OnlyContain(r => GetParameter(r, "searchTerm") == "Dig+2026+S01*");
        }

        [Test]
        public void should_not_add_wildcard_to_episode_search()
        {
            var requests = GetRequests(Subject.GetSearchRequests(new TvSearchCriteria { SearchTerm = "Dig 2026", Season = 1, Episode = "6", Categories = [5000] }));

            requests.Should().OnlyContain(r => GetParameter(r, "searchTerm") == "Dig+2026+S01E06");
        }

        [Test]
        public void should_search_daily_episode_by_date()
        {
            var requests = GetRequests(Subject.GetSearchRequests(new TvSearchCriteria { SearchTerm = "The Daily Show", Season = 2026, Episode = "11/24", Categories = [5000] }));

            requests.Should().HaveCount(2);
            requests.Should().OnlyContain(r => GetParameter(r, "searchTerm") == "The+Daily+Show+2026.11.24");
        }

        [Test]
        public void should_not_search_tv_by_id()
        {
            var requests = GetRequests(Subject.GetSearchRequests(new TvSearchCriteria { SearchTerm = "Game of Thrones", ImdbId = "0944947", TvdbId = 121361, TmdbId = 1399, TvMazeId = 82, Season = 1, Categories = [5000] }));

            requests.Should().HaveCount(2);
            requests.Should().OnlyContain(r => r.Url.Path.EndsWith("/all.php") && GetParameter(r, "searchTerm") == "Game+of+Thrones+S01*" && GetParameter(r, "id") == "");
        }

        [TestCase("Spider-Man", 2000)]
        [TestCase("Rolling Stones", 3000)]
        [TestCase("Carnival of Twisted Shadows", 7000)]
        public void should_use_search_term_for_search_types(string term, int category)
        {
            var criteria = new List<SearchCriteriaBase>
            {
                new MovieSearchCriteria { SearchTerm = term, Categories = [category] },
                new MusicSearchCriteria { SearchTerm = term, Categories = [category] },
                new BookSearchCriteria { SearchTerm = term, Categories = [category] },
                new BasicSearchCriteria { SearchTerm = term, Categories = [category] }
            };

            var requests = criteria.SelectMany(c => GetRequests(c switch
            {
                MovieSearchCriteria movie => Subject.GetSearchRequests(movie),
                MusicSearchCriteria music => Subject.GetSearchRequests(music),
                BookSearchCriteria book => Subject.GetSearchRequests(book),
                _ => Subject.GetSearchRequests((BasicSearchCriteria)c)
            })).ToList();

            requests.Should().OnlyContain(r => r.Url.Path.EndsWith("/all.php") && GetParameter(r, "searchTerm") == term.Replace(' ', '+'));
        }

        [Test]
        public void should_search_by_imdbid_without_filters()
        {
            Subject.Settings.ReleaseTypes = new List<int> { (int)TorrentSyndikatReleaseTypes.Scene };

            var requests = GetRequests(Subject.GetSearchRequests(new MovieSearchCriteria { SearchTerm = "Spider-Man", ImdbId = "22084616", TmdbId = 969681, Categories = [2000] }));

            requests.Single().Url.FullUri.Should().Be("https://torrent-syndikat.org/api_9djWe8Tb2NE3p6opyqnh/v2/torrents/uniqueId.php?apiKey=abcd&id=imdb%3A22084616");
        }

        [Test]
        public void should_search_by_tmdbid_if_imdbid_is_missing()
        {
            var requests = GetRequests(Subject.GetSearchRequests(new MovieSearchCriteria { TmdbId = 969681, Categories = [2000] }));

            requests.Single().Url.FullUri.Should().Be("https://torrent-syndikat.org/api_9djWe8Tb2NE3p6opyqnh/v2/torrents/uniqueId.php?apiKey=abcd&id=tmdb%3A969681");
        }

        [Test]
        public void should_request_page_by_offset()
        {
            var requests = GetRequests(Subject.GetSearchRequests(new TvSearchCriteria { SearchTerm = "test", Categories = [5000], Limit = 30, Offset = 60 }));

            requests.Select(r => GetParameter(r, "pageId")).Should().BeEquivalentTo("3", "3");
        }

        [Test]
        public void should_not_request_next_page_for_id_search()
        {
            var requests = GetRequests(Subject.GetSearchRequests(new MovieSearchCriteria { ImdbId = "22084616", Limit = 30, Offset = 30 }));

            requests.Should().BeEmpty();
        }
    }
}
