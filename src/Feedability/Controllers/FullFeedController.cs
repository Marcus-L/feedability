using HtmlAgilityPack;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

    [Route("api/[controller]")]
    public class FullFeedController : Controller
    {
		private static IHostingEnvironment _env;

		public FullFeedController(IHostingEnvironment env)
		{
			_env = env;
		}

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
					var feedDoc = XDocument.Parse(feedData);
					var articles = new List<ArticleInfo>();

					// fix RSS
					articles.AddRange(feedDoc.XPathSelectElements("/rss/channel/item")
						.Select(e => new ArticleInfo {
							ArticleUrl = e.Element("link").Value,
							ReplaceContents = e.Element("description")
						}));

					// fix ATOM
					articles.AddRange(feedDoc.XPathSelectElements("/feed/entry")
						.Select(e => new ArticleInfo
						{
							ArticleUrl = e.Element("link").Value,
							ReplaceContents = e.Element("content")
						}));

					TransformArticles(url, whitelist, blacklist, articles);

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

		private static void TransformArticles(string feedUrl, string whitelist, string blacklist, IEnumerable<ArticleInfo> articles)
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
							if (articleCount++ >= 5) break;

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
