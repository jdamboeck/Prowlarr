using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Http;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Indexers.Definitions;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.IndexerTests.TorrentSyndikatTests
{
    [TestFixture]
    public class TorrentSyndikatFixture : CoreTest<TorrentSyndikat>
    {
        [SetUp]
        public void Setup()
        {
            Subject.Definition = new IndexerDefinition
            {
                Name = "TorrentSyndikat",
                Settings = new TorrentSyndikatSettings
                {
                    BaseUrl = "https://torrent-syndikat.org/",
                    ApiKey = "someapikey"
                }
            };
        }

        private void GivenResponse(string content, HttpStatusCode statusCode = HttpStatusCode.OK, string contentType = "application/json")
        {
            Mocker.GetMock<IIndexerHttpClient>()
                .Setup(o => o.ExecuteProxiedAsync(It.Is<HttpRequest>(v => v.Method == HttpMethod.Get), Subject.Definition))
                .ReturnsAsync((HttpRequest r, IndexerDefinition _) => new HttpResponse(r, new HttpHeader { { "Content-Type", contentType } }, new CookieCollection(), content, 0, statusCode));
        }

        private void GivenRecentFeed(string fileName = "recentfeed.json")
        {
            GivenResponse(ReadAllText($"Files/Indexers/TorrentSyndikat/{fileName}"));
        }

        private static ReleaseInfo GetRelease(IList<ReleaseInfo> releases, int id)
        {
            return releases.Single(r => r.Guid == $"https://torrent-syndikat.org/details.php?id={id}");
        }

        [Test]
        public async Task should_parse_recent_feed_from_TorrentSyndikat()
        {
            GivenRecentFeed();

            var releases = (await Subject.Fetch(new BasicSearchCriteria())).Releases;

            releases.Should().HaveCount(29);
            releases.First().Should().BeOfType<TorrentInfo>();

            var torrentInfo = GetRelease(releases, 3851097) as TorrentInfo;

            torrentInfo.Title.Should().Be("Spider-Man.Brand.New.Day.2026.German.DL.2160p.UHD.BluRay.x265-NEXUS");
            torrentInfo.DownloadProtocol.Should().Be(DownloadProtocol.Torrent);
            torrentInfo.DownloadUrl.Should().Be("https://torrent-syndikat.org/api_9djWe8Tb2NE3p6opyqnh/v2/torrents/download.php?id=3851097&apiKey=someapikey");
            torrentInfo.InfoUrl.Should().Be("https://torrent-syndikat.org/details.php?id=3851097");
            torrentInfo.CommentUrl.Should().BeNullOrEmpty();
            torrentInfo.Indexer.Should().Be(Subject.Definition.Name);
            torrentInfo.PublishDate.ToUniversalTime().Should().Be(new DateTime(2026, 11, 24, 20, 40, 14, DateTimeKind.Utc));
            torrentInfo.Size.Should().Be(34138685807);
            torrentInfo.Files.Should().Be(2);
            torrentInfo.Grabs.Should().Be(20);
            torrentInfo.InfoHash.Should().Be(null);
            torrentInfo.MagnetUrl.Should().Be(null);
            torrentInfo.Peers.Should().Be(20);
            torrentInfo.Seeders.Should().Be(20);
            torrentInfo.ImdbId.Should().Be(22084616);
            torrentInfo.TmdbId.Should().Be(969681);
            torrentInfo.TvdbId.Should().Be(0);
            torrentInfo.TvMazeId.Should().Be(0);
            torrentInfo.Year.Should().Be(2026);
            torrentInfo.Genres.Should().BeEquivalentTo("Action", "Adventure", "Fantasy", "Science Fiction");
            torrentInfo.IndexerFlags.Should().BeEmpty();
            torrentInfo.Description.Should().Contain("Product-Link: https://torrent-syndikat.org/product.php?pid=84271");
            torrentInfo.DownloadVolumeFactor.Should().Be(1);
            torrentInfo.UploadVolumeFactor.Should().Be(1);
            torrentInfo.MinimumRatio.Should().Be(1);
            torrentInfo.MinimumSeedTime.Should().Be(345600);
            torrentInfo.Languages.Should().BeEmpty();
            torrentInfo.Subs.Should().BeEmpty();
        }

        [Test]
        public async Task should_parse_series_release_from_TorrentSyndikat()
        {
            GivenRecentFeed();

            var releases = (await Subject.Fetch(new BasicSearchCriteria())).Releases;

            var torrentInfo = GetRelease(releases, 3851064) as TorrentInfo;

            torrentInfo.Title.Should().Be("The.Detective.Is.Already.Dead.S01E12.Die.drei.gleissenden.Jahre.die.ich.mit.dir.verbracht.habe.waren.GERMAN.ANiME.WEB.H264-GERTv");
            torrentInfo.PublishDate.ToUniversalTime().Should().Be(new DateTime(2026, 11, 24, 19, 44, 58, DateTimeKind.Utc));
            torrentInfo.Size.Should().Be(387803745);
            torrentInfo.Files.Should().Be(2);
            torrentInfo.Grabs.Should().Be(2);
            torrentInfo.Seeders.Should().Be(3);
            torrentInfo.Peers.Should().Be(3);
            torrentInfo.ImdbId.Should().Be(13884052);
            torrentInfo.TmdbId.Should().Be(117061);
            torrentInfo.TvdbId.Should().Be(397197);
            torrentInfo.TvMazeId.Should().Be(55377);
            torrentInfo.Year.Should().Be(2021);
            torrentInfo.IndexerFlags.Should().BeEquivalentTo(new[] { IndexerFlag.Scene });
            torrentInfo.Description.Should().Be(string.Join(Environment.NewLine, "Original Title: The Detective Is Already Dead", "Product-Link: https://torrent-syndikat.org/product.php?pid=62555", "Tags: sd, untouched, web, avc, sdr, sl, aac, c2", "Release Type: scene"));
        }

        [Test]
        public void should_report_capabilities()
        {
            Subject.SupportsPagination.Should().BeTrue();
            Subject.PageSize.Should().Be(30);
            Subject.Capabilities.LimitsDefault.Should().Be(30);
            Subject.Capabilities.LimitsMax.Should().Be(30);
            Subject.Capabilities.MovieSearchParams.Should().BeEquivalentTo(new[] { MovieSearchParam.Q, MovieSearchParam.ImdbId, MovieSearchParam.TmdbId });
            Subject.Capabilities.TvSearchParams.Should().BeEquivalentTo(new[] { TvSearchParam.Q, TvSearchParam.Season, TvSearchParam.Ep });
            Subject.Capabilities.MusicSearchParams.Should().BeEquivalentTo(new[] { MusicSearchParam.Q });
            Subject.Capabilities.BookSearchParams.Should().BeEquivalentTo(new[] { BookSearchParam.Q });
        }

        [Test]
        public void should_offer_main_and_alternative_domain()
        {
            Subject.IndexerUrls.Should().Equal("https://torrent-syndikat.org/", "https://tee-stube.org/");
        }

        [Test]
        public void should_only_create_custom_categories_for_tracker_categories()
        {
            var trackerCategories = new[]
            {
                "Apps / Windows", "Apps / Linux", "Apps / MacOS",
                "Spiele / Windows", "Spiele / MacOS", "Spiele / Linux", "Spiele / Playstation", "Spiele / Nintendo", "Spiele / XBOX",
                "Video / Filme", "Video / Serien", "Video / Sport", "Video / Musik",
                "Audio / Musik", "Audio / Hörbücher", "Audio / Podcasts",
                "eBook / Belletristik", "eBook / Sachbücher", "eBook / Comics", "eBook / Zeitungen", "eBook / Magazine",
                "Misc / Misc", "Misc / Grabbelkiste"
            };

            Subject.Capabilities.Categories.GetTorznabCategoryList().Where(c => c.Id >= 100000).Select(c => c.Name).Should().BeEquivalentTo(trackerCategories);
        }

        [Test]
        public async Task should_map_categories()
        {
            GivenRecentFeed();

            var releases = (await Subject.Fetch(new BasicSearchCriteria())).Releases;

            var expected = new Dictionary<int, int[]>
            {
                { 3851097, new[] { 2000, 2045 } }, // video/movie 2160p
                { 3850809, new[] { 2000, 2040 } }, // video/movie 1080p
                { 3850669, new[] { 2000, 2030 } }, // video/movie sd
                { 3851111, new[] { 2000, 2010 } }, // video/movie sd foreign
                { 3851177, new[] { 5000, 5040 } }, // video/series 1080p
                { 3851064, new[] { 5000, 5030 } }, // video/series sd
                { 3851030, new[] { 5000, 5020 } }, // video/series 720p foreign
                { 3851155, new[] { 5060 } },       // video/sports foreign
                { 3851142, new[] { 3020 } },       // video/vmusic tagged 1080p and flac
                { 3851191, new[] { 3000, 3040 } }, // audio/amusic flac
                { 3851193, new[] { 3000, 3010 } }, // audio/amusic mp3
                { 3851090, new[] { 3030 } },       // audio/audiobook mp3
                { 3758974, new[] { 3050 } },       // audio/podcast
                { 3835149, new[] { 7020 } },       // ebook/novel
                { 3846848, new[] { 7020 } },       // ebook/nfiction
                { 3677177, new[] { 7030 } },       // ebook/comic
                { 3750111, new[] { 7010 } },       // ebook/npaper
                { 3850793, new[] { 7010 } },       // ebook/magazine
                { 3851163, new[] { 4050 } },       // games/gwindows
                { 3851118, new[] { 4050 } },       // games/glinux
                { 3851122, new[] { 4050 } },       // games/gmacos
                { 3816723, new[] { 1090 } },       // games/playstation
                { 3851046, new[] { 1090 } },       // games/nintendo
                { 3327237, new[] { 1090 } },       // games/xbox
                { 3851181, new[] { 4010 } },       // apps/awindows
                { 3848076, new[] { 4000 } },       // apps/alinux
                { 3838484, new[] { 4030 } },       // apps/amacos foreign
                { 3795177, new[] { 8000 } },       // misc/misc
                { 3328283, new[] { 8010 } },       // misc/grabbel
            };

            releases.ToDictionary(r => int.Parse(r.Guid.Split('=').Last()), r => r.Categories.Select(c => c.Id).Where(c => c < 100000).ToArray())
                .Should().BeEquivalentTo(expected);
        }

        [Test]
        public async Task should_set_scene_flag_only_for_scene_releases()
        {
            GivenRecentFeed();

            var releases = (await Subject.Fetch(new BasicSearchCriteria())).Releases;

            releases.Where(r => r.IndexerFlags.Contains(IndexerFlag.Scene)).Select(r => r.Guid.Split('=').Last())
                .Should().BeEquivalentTo("3851181", "3851155", "3851142", "3851111", "3851064", "3851030", "3850809", "3850793", "3850669", "3848076", "3795177", "3750111", "3677177", "3327237");
        }

        [Test]
        public async Task should_parse_release_without_product()
        {
            GivenRecentFeed();

            var releases = (await Subject.Fetch(new BasicSearchCriteria())).Releases;

            var torrentInfo = GetRelease(releases, 3851191) as TorrentInfo;

            torrentInfo.Title.Should().Be("The_Rolling_Stones-Out_Of_Control-PROMO-CDS-FLAC-1998-MAHOU");
            torrentInfo.ImdbId.Should().Be(0);
            torrentInfo.Year.Should().Be(0);
            torrentInfo.Genres.Should().BeEquivalentTo("Rock");
            torrentInfo.Description.Should().NotContain("Product-Link");
        }

        [Test]
        public async Task should_filter_release_types_on_id_search()
        {
            ((TorrentSyndikatSettings)Subject.Definition.Settings).ReleaseTypes = new List<int> { (int)TorrentSyndikatReleaseTypes.Scene, (int)TorrentSyndikatReleaseTypes.O_Scene };

            GivenRecentFeed();

            var releases = (await Subject.Fetch(new MovieSearchCriteria { ImdbId = "22084616" })).Releases;

            releases.Should().HaveCount(21);
            releases.Select(r => r.Description).Should().OnlyContain(d => d.Contains("Release Type: scene") || d.Contains("Release Type: oscene"));
        }

        [Test]
        public async Task should_skip_malformed_release()
        {
            GivenRecentFeed("recentfeed-malformed.json");

            var releases = (await Subject.Fetch(new BasicSearchCriteria())).Releases;

            var torrentInfo = releases.Should().ContainSingle().Subject as TorrentInfo;

            torrentInfo.Title.Should().Be("Germany.Shore.2021.S06E01.German.EAC3.1080p.AMZN.WEB.H264-AR0NA");
            torrentInfo.Categories.Select(c => c.Id).Where(c => c < 100000).Should().BeEquivalentTo(new[] { 5000, 5040 });
            torrentInfo.Files.Should().BeNull();
            torrentInfo.Grabs.Should().BeNull();
            torrentInfo.Seeders.Should().BeNull();
            torrentInfo.Year.Should().Be(0);
            torrentInfo.Genres.Should().BeEmpty();

            ExceptionVerification.ExpectedWarns(1);
        }

        [Test]
        public async Task should_not_error_if_empty_response()
        {
            GivenResponse("{\"totalResults\":0,\"maxResults\":0,\"perPage\":0,\"nextPageId\":0,\"data\":[]}");

            var releases = (await Subject.Fetch(new BasicSearchCriteria())).Releases;

            releases.Should().BeEmpty();
        }

        [Test]
        public async Task should_warn_on_invalid_api_key()
        {
            GivenResponse("{\"msg\":null}", HttpStatusCode.Unauthorized);

            var result = await Subject.Fetch(new BasicSearchCriteria());

            result.Releases.Should().BeEmpty();

            // Auth failures are reported as invalid credentials instead of a generic indexer error with the response attached
            result.Queries.Should().BeEmpty();

            ExceptionVerification.ExpectedWarns(2);
        }

        [Test]
        public async Task should_warn_on_bad_request()
        {
            GivenResponse("{\"msg\":\"Ungültige Torrent-ID\"}", HttpStatusCode.BadRequest);

            var result = await Subject.Fetch(new BasicSearchCriteria());

            result.Releases.Should().BeEmpty();
            result.Queries.Should().ContainSingle().Which.Response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            ExceptionVerification.ExpectedWarns(2);
        }

        [Test]
        public async Task should_warn_on_non_json_response()
        {
            GivenResponse("<?xml version='1.0' encoding='UTF-8'?><rss version='2.0'><channel><title>TS-RSS</title></channel></rss>", contentType: "application/xml");

            var result = await Subject.Fetch(new BasicSearchCriteria());

            result.Releases.Should().BeEmpty();
            result.Queries.Should().ContainSingle();

            ExceptionVerification.ExpectedWarns(1);
        }
    }
}
