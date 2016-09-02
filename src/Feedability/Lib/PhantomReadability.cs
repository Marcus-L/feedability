using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Feedability
{
	public class Uri
	{
		public string host { get; set; }
		public string pathBase { get; set; }
		public string prePath { get; set; }
		public string scheme { get; set; }
		public string spec { get; set; }
	}

	public class ReadabilityError
	{
		public string message { get; set; }
	}

	public class ReadabilityObject
	{
		public ReadabilityError error { get; set; }
		public string byline { get; set; }
		public string content { get; set; }
		public string dir { get; set; }
		public string excerpt { get; set; }
		public bool isProbablyReaderable { get; set; }
		public int length { get; set; }
		public string textContent { get; set; }
		public string title { get; set; }
		public Uri uri { get; set; }
		public string userAgent { get; set; }
		public List<string> consoleLogs { get; set; }
	}

	public class PhantomReadability
    {
		public static ReadabilityObject Get(string contentRoot, string url, string whitelist, string blacklist, int timeout = 15000)
		{
			// current chrome UA
			string useragent = "Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/52.0.2743.116 Safari/537.36";
			string inject = @"
function() {
	function feedability_el(q,hide) {
		var els = document.querySelectorAll(q);
		for (i=0; i<els.length; i++) {
			els[i].id = 'neutral';
			els[i].className = hide ? 'hidden' : 'content';
			if (hide) {
				els[i].innerHTML = '';
			} else {
				els[i].appendChild(document.createTextNode(',,,,,,,,,,'));
			}
		}
	}
";
			if (whitelist != null && whitelist != "")
				inject += string.Join("\n", whitelist.Split(',').Select(wl => "feedability_el('" + wl + "',false);"));
			if (blacklist != null && blacklist != "")
				inject += string.Join("\n", blacklist.Split(',').Select(bl => "feedability_el('" + bl + "',true);"));

			inject += "\n}";

			using (var process = new Process
			{
				StartInfo = new ProcessStartInfo
				{
					WorkingDirectory = contentRoot + "\\Vendor\\",
					RedirectStandardError = true,
					RedirectStandardOutput = true,
					UseShellExecute = false,
					CreateNoWindow = true,
					FileName = contentRoot + "\\Vendor\\phantomjs.exe",
					Arguments = $"phantom-scrape.js \"{url}\" readability.js \"{useragent}\" \"{inject}\""
				}
			})
			{
				// stream buffers
				var output = new StringBuilder();
				var error = new StringBuilder();

				using (AutoResetEvent outputWaitHandle = new AutoResetEvent(false))
				using (AutoResetEvent errorWaitHandle = new AutoResetEvent(false))
				{
					process.OutputDataReceived += (sender, e) => {
						if (e.Data == null)
						{
							outputWaitHandle.Set();
						}
						else
						{
							output.AppendLine(e.Data);
						}
					};
					process.ErrorDataReceived += (sender, e) =>
					{
						if (e.Data == null)
						{
							errorWaitHandle.Set();
						}
						else
						{
							error.AppendLine(e.Data);
						}
					};

					process.Start();
					process.BeginOutputReadLine();
					process.BeginErrorReadLine();

					if (process.WaitForExit(timeout) &&
						outputWaitHandle.WaitOne(timeout) &&
						errorWaitHandle.WaitOne(timeout))
					{
						// Process completed. Check process.ExitCode here.
						var json = output.ToString().Replace(",,,,,,,,,,", "");
						var retval = JsonConvert.DeserializeObject<ReadabilityObject>(json);
						if (retval == null)
						{
							return new ReadabilityObject
							{
								error = new ReadabilityError
								{
									message = error.ToString()
								}
							};
						}
						return retval;
					}
					else
					{
						// Timed out.
						throw new Exception("Timed out: " + error.ToString());
					}
				}
			}
		}
	}
}
