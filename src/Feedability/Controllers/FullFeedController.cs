using HtmlAgilityPack;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace Feedability.Controllers
{
	public class ArticleInfo
	{
		public string ArticleUrl { get; set; }

		public XElement ReplaceContents { get; set; }
	}

	public class CachedArticle
	{
		public string ArticleUrl { get; set; }

		public DateTime LastFetchedUTC { get; set; }
	}

	public class FullFeedOptions
	{
		public int MaxFeedEntriesProcessed { get; set; } = 5; // default to 5 if options not set
	}

    [Route("api/[controller]")]
    public class FullFeedController : Controller
    {
		public const string AtomNS = "http://www.w3.org/2005/Atom";

		// store hosting environment to get info on the path to find the phantomjs files
		private static IHostingEnvironment _env;

		// store injected options
		private readonly IOptions<FullFeedOptions> _optionsAccessor;

		public FullFeedController(IHostingEnvironment env, IOptions<FullFeedOptions> optionsAccessor)
		{
			_env = env;
			_optionsAccessor = optionsAccessor;
		}

		// debug method to show which articles are cached
		[HttpGet("cache")]
		public IEnumerable<CachedArticle> GetCache([FromQuery]string url)
		{
			var retval = new List<CachedArticle>();
			using (var conn = SqliteUtil.GetConn())
			{
				conn.Open();
				var cmd = new SqliteCommand("SELECT * FROM Articles WHERE FeedUrl=@feedurl", conn);
				cmd.Parameters.AddWithValue("@feedurl", url);
				var reader = cmd.ExecuteReader();
				while (reader.Read())
				{
					retval.Add(new CachedArticle
					{
						ArticleUrl = reader["ArticleUrl"].ToString(),
						LastFetchedUTC = DateTime.Parse(reader["LastFetchedUTC"].ToString())
					});
				}
				return retval;
			}
		}

		// debug method to clear the cache either by feed url or wholesale
		[HttpGet("clearcache")]
		public string ClearCache([FromQuery]string url)
		{
			using (var conn = SqliteUtil.GetConn())
			{
				conn.Open();
				var cmd = new SqliteCommand("DELETE FROM Articles WHERE @feedurl='' OR FeedUrl=@feedurl", conn);
				cmd.Parameters.AddWithValue("@feedurl", url ?? "");
				cmd.ExecuteNonQuery();
				return "Cache cleared";
			}
		}

		// preview the readability of an article (with white/black-list selectors)
		[HttpGet("article")]
		public ContentResult GetArticle([FromQuery]string url, [FromQuery]string whitelist, [FromQuery]string blacklist)
		{
			var retval = new ContentResult { ContentType = "text/html" };
			var pr = PhantomReadability.Get(_env.ContentRootPath, url, whitelist, blacklist);
			if (pr.error == null)
			{
				retval.Content = pr.content;
			}
			else
			{
				retval.Content = pr.error.message;
			}
			return retval;
		}

		// get the feed with description/content translated to readability versions of 
		// the articles (with white/black-list selectors) - only processes a few items at a time
		// to avoid super-long request times
		[HttpGet("feed")]
		public async Task<ContentResult> GetFeed([FromQuery]string url, [FromQuery]string whitelist, [FromQuery]string blacklist)
		{
			using (var client = new HttpClient())
			{
				var retval = new ContentResult { ContentType = "text/xml" };
				try
				{
					var feedResult = await client.GetAsync(url);
					var feedData = FixMarkup(await feedResult.Content.ReadAsStringAsync());
					var xmlReader = XmlReader.Create(new StringReader(feedData));
					var feedDoc = XDocument.Load(xmlReader);
					var nsManager = new XmlNamespaceManager(xmlReader.NameTable);
					nsManager.AddNamespace("atom", AtomNS);
					var articles = new List<ArticleInfo>();

					// fix RSS
					articles.AddRange(feedDoc.XPathSelectElements("/rss/channel/item")
						.Select(e => new ArticleInfo {
							ArticleUrl = e.Element("link").Value,
							ReplaceContents = e.Element("description")
						}));

					// fix ATOM
					articles.AddRange(feedDoc.XPathSelectElements("/atom:feed/atom:entry", nsManager)
						.Select(e => new ArticleInfo
						{
							ArticleUrl = e.Element(XName.Get("link", AtomNS)).Attribute("href").Value,
							ReplaceContents = e.Element(XName.Get("content", AtomNS))
						}));

					// remove PuSH link to avoid aggregators using pushed content
					feedDoc.XPathSelectElements("/rss/channel/atom:link[@rel='hub']", nsManager)?
						.ToList().ForEach(x => x.Remove()); // rss
					feedDoc.XPathSelectElements("/atom:feed/atom:link[@rel='hub']", nsManager)?
						.ToList().ForEach(x => x.Remove()); // atom

					TransformArticles(url, whitelist, blacklist, articles, _optionsAccessor.Value.MaxFeedEntriesProcessed);

					retval.Content = feedDoc.ToString();
				}
				catch (Exception ex)
				{
					string msg = ex.Message + "\n" + ex.StackTrace;
					retval.Content = $"<pre>{msg.Replace("<", "&gt;")}</pre>";
				}
				return retval;
			}
		}

		// performs the translation of articles, either using cache or calling the 
		// phantomJS helper to run readability on the article link url
		private static void TransformArticles(
			string feedUrl, 
			string whitelist, string blacklist, 
			IEnumerable<ArticleInfo> articles, 
			int maxFeedEntriesProcessed)
		{
			// check cache first
			using (var conn = SqliteUtil.GetConn())
			{
				conn.Open();
				int articleCount = 0;

				// update the feed contents (only process the latest few items)
				foreach (var article in articles)
				{
					string readableContent = "";

					var readCache = new SqliteCommand("SELECT * FROM Articles WHERE FeedUrl = @feedurl AND ArticleUrl = @articleurl", conn);
					readCache.Parameters.AddWithValue("@feedurl", feedUrl);
					readCache.Parameters.AddWithValue("@articleurl", article.ArticleUrl);
					using (var reader = readCache.ExecuteReader())
					{
						if (reader.Read())
						{
							readableContent = reader["Content"].ToString();
							readableContent += $"<!-- cached at {reader["LastFetchedUTC"]} -->";
						}
						else
						{
							// only process up to five items each time
							if (articleCount++ >= maxFeedEntriesProcessed) break;

							// if not cached, make it readable!
							var pr = PhantomReadability.Get(_env.ContentRootPath, article.ArticleUrl, whitelist, blacklist);
							if (pr.error != null)
							{
								switch (pr.error.message)
								{
									case "Empty result from Readability.js.":
										// if readability can't handle it, cache the original html
										readableContent = article.ReplaceContents.Value;
										// reset the error
										pr.error = null;
										break;
									default:
										// include the error message in CDATA comment
										article.ReplaceContents.Add(new XCData(pr.error.message));
										break;
								}
							}
							else if (pr.isProbablyReaderable)
							{
								readableContent = pr.content;
							}

							// cache the result (even if not able to be made readable)
							if (pr.error == null)
							{
								var writeCache = new SqliteCommand(@"
									INSERT INTO Articles (FeedUrl, ArticleUrl, LastFetchedUTC, Content, Readable)
									VALUES (@feedurl, @articleurl, @lastfetchedutc, @content, @readable)", conn);
								writeCache.Parameters.AddWithValue("@feedurl", feedUrl);
								writeCache.Parameters.AddWithValue("@articleurl", article.ArticleUrl);
								writeCache.Parameters.AddWithValue("@lastfetchedutc", DateTime.UtcNow);
								writeCache.Parameters.AddWithValue("@content", readableContent);
								writeCache.Parameters.AddWithValue("@readable", readableContent != "");
								writeCache.ExecuteNonQuery();
							}
						}
					}

					// if the article is readable, replace the contents
					if (readableContent != "")
					{
						readableContent += $"<!-- processed at {DateTime.UtcNow} -->";
						article.ReplaceContents.ReplaceNodes(new XCData(readableContent));
					}
				}

				// clean up the cache
				var parameters = string.Join(",", articles.Select((a, i) => "@a" + i));
				var cleanupCache = new SqliteCommand("", conn);
				cleanupCache.Parameters.AddRange(articles.Select((a, i) => new SqliteParameter("@a" + i, a.ArticleUrl)));
				cleanupCache.Parameters.AddWithValue("@feedurl", feedUrl);
				cleanupCache.CommandText = $"DELETE FROM Articles WHERE FeedUrl = @feedurl AND ArticleUrl NOT IN ({parameters})";
				cleanupCache.ExecuteNonQuery();
			}
		}

		// fixes invalid feed xml so that it can be parsed by XDocument
		private string FixMarkup(string xml)
		{
			HtmlNode.ElementsFlags.Remove("link");
			var doc = new HtmlDocument();
			doc.OptionFixNestedTags = true;
			doc.LoadHtml(xml);
			return doc.DocumentNode.InnerHtml;
		}
    }
}
