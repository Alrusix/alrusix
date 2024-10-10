using akron;
using akronConfig;
using System.Collections.Concurrent;
using akronDB;
using akron.module;
using System.Text;
namespace akron
{
	public class Program
	{
		public static bool OpenAccessLog {  get; set; }=false;
		public static bool OpenLog { get; set; } = false;
		public static HashSet<IOption> Options = [
			//new Test(),
			new OpenAccessRecord(),
			new OpenLogs(),
			new Stop(),
			new Quit()
		];
		static void Main(string[] args)
		{
			Console.OutputEncoding = Encoding.UTF8;
			string dbPath = Config.Get<string>("DB:Path", "akron.adb");
			if (!File.Exists(dbPath))
				File.WriteAllText(dbPath, "[]");//不要动这行，浪费一天时间在这了
			SocketServer.Listener();
			/*
			*/https://github.com/Alrusix/
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
				DBEngine.ParseSQL("SELECT * FROM SocketLogs;");
			if (OpenLog)
				DBEngine.ParseSQL("SELECT * FROM Logs;");
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

