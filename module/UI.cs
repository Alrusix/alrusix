using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using akronDB;

namespace akron.module
{
	static class UI
	{
		public static bool OpenAccessLog { get; set; } = false;
		public static bool OpenLog { get; set; } = false;
		public static HashSet<IOption> Options = [
			//new Test(),
			new OpenAccessRecord(),
			new OpenLogs(),
			new Stop(),
			new Quit()
		];

		public static void Start()
		{
			Console.OutputEncoding = Encoding.UTF8;
		/*
		*/	https://github.com/Alrusix/

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
			Console.WriteLine(@"*	version : 1.0.1	        	by : https://github.com/Alrusix/					 ");
			Console.WriteLine(@"*");

			if (OpenAccessLog)
				Program.engine.ParseSQL("SELECT * FROM SocketLogs;");
			if (OpenLog)
				Program.engine.ParseSQL("SELECT * FROM Logs;");
			if (Options.Contains(new OpenAccessRecord()) && Options.Contains(new OpenLogs()))
			{
				Options.Remove(new UP());
				Options.Remove(new Down());
			}
			Console.WriteLine();
			Console.ForegroundColor = ConsoleColor.DarkYellow;
			Console.WriteLine($"   Options");
			Console.ForegroundColor = ConsoleColor.White;
		}
	}
}
