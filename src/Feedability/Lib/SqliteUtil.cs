using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;

namespace Feedability
{
    public class SqliteUtil
    {
		public static SqliteConnection GetConn()
		{
			return new SqliteConnection("Data Source=feedability.db");
		}

		public static void Init()
		{
			using (var conn = SqliteUtil.GetConn())
			{
				conn.Open();

				// create database if it doesn't exist
				var cmd = new SqliteCommand(@"
					CREATE TABLE IF NOT EXISTS Articles (
						FeedUrl TEXT, 
						ArticleUrl TEXT, 
						LastFetchedUTC DATETIME, 
						Content TEXT,
						Readable BOOLEAN
					);", conn);
				cmd.ExecuteNonQuery();

				//// reset (if you pollute the cache by mistake)
				//var delCmd = new SqliteCommand("DELETE FROM Articles",conn);
				//delCmd.ExecuteNonQuery();
			}
		}
    }
}
