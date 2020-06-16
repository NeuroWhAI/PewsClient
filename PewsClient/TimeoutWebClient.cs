using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;

namespace PewsClient
{
    class TimeoutWebClient : WebClient
    {
		public TimeoutWebClient()
			: this(TimeSpan.FromSeconds(60))
		{ }

		public TimeoutWebClient(TimeSpan timeout)
		{
			Timeout = timeout;
			Headers.Add("user-agent", "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; .NET CLR 1.0.3705;)");
		}

		public TimeSpan Timeout { get; set; }

		protected override WebRequest GetWebRequest(Uri uri)
		{
			var w = base.GetWebRequest(uri);
			w.Timeout = (int)Timeout.TotalMilliseconds;
			return w;
		}

		public new async Task<string> DownloadStringTaskAsync(string address)
			=> await RunWithTimeout(base.DownloadStringTaskAsync(address));
		public new async Task<string> DownloadStringTaskAsync(Uri address)
			=> await RunWithTimeout(base.DownloadStringTaskAsync(address));

		public new async Task<byte[]> DownloadDataTaskAsync(string address)
			=> await RunWithTimeout(base.DownloadDataTaskAsync(address));
		public new async Task<byte[]> DownloadDataTaskAsync(Uri address)
			=> await RunWithTimeout(base.DownloadDataTaskAsync(address));

		public new async Task DownloadFileTaskAsync(string address, string fileName)
			=> await RunWithTimeout(base.DownloadFileTaskAsync(address, fileName));
		public new async Task DownloadFileTaskAsync(Uri address, string fileName)
			=> await RunWithTimeout(base.DownloadFileTaskAsync(address, fileName));

		private async Task RunWithTimeout(Task task)
		{
			if (task == await Task.WhenAny(task, Task.Delay(Timeout)))
			{
				await task;
			}
			else
			{
				CancelAsync();
				throw new TimeoutException();
			}
		}

		private async Task<T> RunWithTimeout<T>(Task<T> task)
		{
			if (task == await Task.WhenAny(task, Task.Delay(Timeout)))
			{
				return await task;
			}
			else
			{
				CancelAsync();
				throw new TimeoutException();
			}
		}
	}
}
