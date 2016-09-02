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
				conn.ExecuteNonQuery(@"
					CREATE TABLE IF NOT EXISTS Articles (
						FeedUrl TEXT, 
						ArticleUrl TEXT, 
						LastFetchedUTC DATETIME, 
						Content TEXT,
						Readable BOOLEAN
					);");

				// reset
				//conn.ExecuteNonQuery("DELETE FROM Articles");
			}
		}
    }

	// Reused and Modified Code - https://github.com/aspnet/Microsoft.Data.Sqlite/blob/dev/src/Microsoft.Data.Sqlite/Utilities/DbConnectionExtensions.cs  
	internal static class DbConnectionExtensions
	{
		public static int ExecuteNonQuery(this DbConnection connection,
			string commandText, int timeout = 30)
		{
			var command = connection.CreateCommand();
			command.CommandTimeout = timeout;
			command.CommandText = commandText;
			return command.ExecuteNonQuery();
		}

		public static T ExecuteScalar<T> (this DbConnection connection,
			 string commandText, int timeout = 30) =>  
        (T) connection.ExecuteScalar(commandText, timeout);  
  
    private static object ExecuteScalar(this DbConnection connection,
		string commandText, int timeout)
		{
			var command = connection.CreateCommand();
			command.CommandTimeout = timeout;
			command.CommandText = commandText;
			return command.ExecuteScalar();
		}

		public static DbDataReader ExecuteReader(this DbConnection connection,
			string commandText)
		{
			var command = connection.CreateCommand();
			command.CommandText = commandText;
			return command.ExecuteReader();
		}
	}
}
