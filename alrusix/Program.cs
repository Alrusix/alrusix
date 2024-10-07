using akron;
using akronConfig;
using System.Collections.Concurrent;
using akronDB;
using akron.module;
namespace akron
{
	public class Program
	{
		public static bool OpenAccessLog {  get; set; }=false;
		public static bool OpenLog { get; set; } = false;
		public static HashSet<IOption> Options = [
			new OpenAccessRecord(),
			new OpenLogs(),
			new Stop(),
			new Quit()
		];
		static void Main(string[] args)
		{
			string dbPath = Config.Get<string>("DB:Path", "akron.adb");
			if (!File.Exists(dbPath))
				File.WriteAllText(dbPath, "[\r\n  {\r\n    \"Name\": \"Logs\",\r\n    \"Fields\": [\r\n      {\r\n        \"ColumnName\": \"Level\",\r\n        \"DataType\": 5,\r\n        \"Constraints\": \"\"\r\n      },\r\n      {\r\n        \"ColumnName\": \"Message\",\r\n        \"DataType\": 5,\r\n        \"Constraints\": \"\"\r\n      }\r\n    ],\r\n    \"Row\": []\r\n  },\r\n  {\r\n    \"Name\": \"SocketLogs\",\r\n    \"Fields\": [\r\n      {\r\n        \"ColumnName\": \"IPAdderss\",\r\n        \"DataType\": 5,\r\n        \"Constraints\": \"\"\r\n      },\r\n      {\r\n        \"ColumnName\": \"Method\",\r\n        \"DataType\": 5,\r\n        \"Constraints\": \"\"\r\n      },\r\n      {\r\n        \"ColumnName\": \"Url\",\r\n        \"DataType\": 5,\r\n        \"Constraints\": \"\"\r\n      },\r\n      {\r\n        \"ColumnName\": \"LastdateTime\",\r\n        \"DataType\": 8,\r\n        \"Constraints\": \"\"\r\n      },\r\n      {\r\n        \"ColumnName\": \"StatusCode\",\r\n        \"DataType\": 5,\r\n        \"Constraints\": \"\"\r\n      }\r\n    ],\r\n    \"Row\": [\r\n      {\r\n        \"IPAdderss\": \"127.0.0.1\",\r\n        \"Method\": \"GET\",\r\n        \"Url\": \"/\",\r\n        \"LastdateTime\": \"2024-10-06T22:04:51\",\r\n        \"StatusCode\": \"200 OK\"\r\n      }\r\n    ]\r\n  }\r\n]");					
			SocketServer.Listener();
			https://github.com/Alrusix/
			DisplayTable();
			Console.WriteLine();
			foreach (var w in Options)
			{
				Console.ForegroundColor = ConsoleColor.Blue;
				Console.Write($"  [{w.Key}] ");
				Console.ResetColor();
				Console.Write(w.Name + "\r\n");
			}
			while (true)
			{
				var key = Console.ReadKey(true).Key;
				switch (key)
				{
					case ConsoleKey.Enter:
						goto https;
					default:
						foreach (var w in Options)
							if (key == w.Key)
							{
								w.Execute();
								break;
							}						
						goto https;
				}
			}
		}
		static void DisplayTable()
		{
			Console.Clear();
			Console.WriteLine(@"");
			Console.ForegroundColor = ConsoleColor.DarkMagenta;
			Console.WriteLine(@"*      /######  /##       /#######  /##   /##  /######  /###### /##   /##			 ");
			Console.WriteLine(@"*     /##__  ##| ##      | ##__  ##| ##  | ## /##__  ##|_  ##_/| ##  / ##			 ");
			Console.WriteLine(@"*    | ##  \ ##| ##      | ##  \ ##| ##  | ##| ##  \__/  | ##  |  ##/ ##/			 ");
			Console.WriteLine(@"*    | ########| ##      | #######/| ##  | ##|  ######   | ##   \  ####/			 ");
			Console.WriteLine(@"*    | ##__  ##| ##      | ##__  ##| ##  | ## \____  ##  | ##    >##  ##			 ");
			Console.WriteLine(@"*    | ##  | ##| ##      | ##  \ ##| ##  | ## /##  \ ##  | ##   /##/\  ##			 ");
			Console.WriteLine(@"*    | ##  | ##| ########| ##  | ##|  ######/|  ######/ /######| ##  \ ##			 ");
			Console.WriteLine(@"*    |__/  |__/|________/|__/  |__/ \______/  \______/ |______/|__/  |__/			 ");
			Console.WriteLine(@"*");
			Console.WriteLine(@"*	version : 0.0.1	        	by : https://github.com/Alrusix/					 ");
			Console.WriteLine(@"*");
			if (OpenAccessLog)
				DBEngine.ShowTable("SocketLogs");
			if (OpenLog)
				DBEngine.ShowTable("Logs");
			if (Program.Options.Contains(new OpenAccessRecord()) && Program.Options.Contains(new OpenLogs()))
			{
				Program.Options.Remove(new UP());
				Program.Options.Remove(new Down());
				
			}
			Console.WriteLine();
			Console.ForegroundColor = ConsoleColor.DarkYellow;
			Console.WriteLine($"   Options");
			Console.ForegroundColor = ConsoleColor.White;
		}
	}
}

