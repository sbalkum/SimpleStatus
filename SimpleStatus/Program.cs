using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Microsoft.Practices.ServiceLocation;
using StructureMap;

namespace SimpleStatus
{
	public class StatusCheckSetting
	{
		public string Name { get; set; }
		public string Kind { get; set; }
		public string StatFile { get; set; }
		public Dictionary<string, string> Settings { get; set; }
		public string Alert { get; set; }
		public int AlertOnConsecutiveErrors { get; set; }
	}

	public class Settings
	{
		public string SMTPHost { get; set; }
		public int SMTPPort { get; set; }
		public bool SMTPIsSsl { get; set; }
		public string SMTPUsername { get; set; }
		public string SMTPPassword { get; set; }
		public List<StatusCheckSetting> Checks { get; set; }
	}

	public class Program
	{
		static void Main(string[] args)
		{
			IoC.Bootstrapper.InitializeStructureMap();

			var checkfile = File.ReadAllText(args[0]);

			var jss = new JavaScriptSerializer();
			var settings = jss.Deserialize<Settings>(checkfile);

			foreach (var check in settings.Checks)
			{
				var typename = "SimpleStatus." + check.Kind + "StatusCheck";
				var statusCheck = (IStatusCheck)ServiceLocator.Current.GetInstance(Type.GetType(typename));
				statusCheck.Initialize(check.StatFile, check.Settings);
				if (!statusCheck.IsAlive() && statusCheck.ConsecutiveErrors == check.AlertOnConsecutiveErrors)
				{
					var client = new SmtpClient(settings.SMTPHost, settings.SMTPPort);
					client.EnableSsl = settings.SMTPIsSsl;
					if (!string.IsNullOrEmpty(settings.SMTPUsername))
					{
						client.Credentials = new NetworkCredential(settings.SMTPUsername, settings.SMTPPassword);
					}
					client.Send(settings.SMTPUsername, check.Alert, check.Name + " Status Check Failed", statusCheck.ErrorMessage);
				}
			}
		}
	}
}
