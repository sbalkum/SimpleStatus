using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Web.Script.Serialization;
using OpenPop.Pop3;

namespace SimpleStatus
{
	public class POP3StatusCheck : StatusCheckBase
	{
		protected bool IsResponseOK(StreamReader reader)
		{
			do
			{
				var str = reader.ReadLine();
				if (str == null || !str.StartsWith("+OK"))
				{
					return false;
				}
			} while (!reader.EndOfStream);

			return true;
		}

		protected override void ExecuteCheck(out bool isAlive, out string message)
		{
			isAlive = false;
			message = null;

			try
			{
				using (var client = new Pop3Client())
				{
					client.Connect(_settings["Host"], 110, false);

					client.Authenticate(_settings["Username"], _settings["Password"]);

					client.GetMessageCount();
				}

				isAlive = true;
			}
			catch (Exception e)
			{
				message = e.Message;
			}
		}
	}
	public class HTTPStatusCheck : StatusCheckBase
	{
		protected override void ExecuteCheck(out bool isAlive, out string message)
		{
			isAlive = false;
			message = null;
			try
			{
				var client = new WebClient();
				var content = client.DownloadString(_settings["Url"]);
				isAlive = content.Contains(_settings["Check Phrase"]);
				if (!isAlive)
					message = "Check Phrase not found";
			}
			catch (Exception e)
			{
				message = e.Message;
			}
		}
	}

	public abstract class StatusCheckBase : IStatusCheck
	{
		protected class Result
		{
			public long UnixTime { get; set; }
			public long ExecutionMilliseconds { get; set; }
			public string ErrorMessage { get; set; }
		}

		protected string _statFile;
		protected Dictionary<string, string> _settings;
		public string ErrorMessage { get; protected set; }
		public int ConsecutiveErrors { get; protected set; }

		public virtual void Initialize(string statFile, Dictionary<string, string> settings)
		{
			_statFile = statFile;
			_settings = settings;
		}

		protected void AppendToStatFile(DateTime time, long milliseconds)
		{
			string statfile = null;
			var jss = new JavaScriptSerializer();
			List<Result> stats;
			if (File.Exists(_statFile))
			{
				statfile = File.ReadAllText(_statFile);

				stats = jss.Deserialize<List<Result>>(statfile);
			}
			else
			{
				stats = new List<Result>();
			}

			var epoch = new DateTime(1970, 1, 1);

			var twoweeks = DateTime.UtcNow.AddDays(-14);
			var twoWeeksUnix = (long)((twoweeks - epoch).TotalSeconds);

			var i = 0;
			if (stats.Count > 0)
			{
				while (stats[i].UnixTime < twoWeeksUnix)
				{
					i++;
				}
				if (i > 0)
				{
					stats.RemoveRange(0, i);
				}
			}

			var unixTime = time.ToUniversalTime() - epoch;
			stats.Add(new Result()
				          {
					          UnixTime = (long)unixTime.TotalSeconds, 
							  ExecutionMilliseconds = milliseconds, 
							  ErrorMessage = ErrorMessage
				          });

			statfile = jss.Serialize(stats);
			File.WriteAllText(_statFile, statfile);

			i = stats.Count;
			while (i > 0 && stats[i - 1].ExecutionMilliseconds == 0)
			{
				i--;
			}
			ConsecutiveErrors = stats.Count - i;
		}

		public bool IsAlive()
		{
			var startTime = DateTime.Now;

			string message = null;
			var isAlive = false;
			ExecuteCheck(out isAlive, out message);
			ErrorMessage = message;

			var doneTime = DateTime.Now;
			var runTime = doneTime - startTime;

			AppendToStatFile(doneTime, isAlive ? (long)runTime.TotalMilliseconds : 0);

			return isAlive;
		}

		protected abstract void ExecuteCheck(out bool isAlive, out string message);
	}

	public interface IStatusCheck
	{
		string ErrorMessage { get; }
		int ConsecutiveErrors { get; }
		void Initialize(string statFile, Dictionary<string, string> settings);
		bool IsAlive();
	}
}