using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using akronDB;
using Microsoft.VisualBasic.FileIO;
using static akron.HTTPS.UI;
namespace akron.HTTPS
{
	public static class UI
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
				Program.engine.ParseSQL("SELECT * FROM HttpLogs;");
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
	public interface IOption
	{
		public ConsoleKey Key { get; }
		public string Name { get; }
		public void Execute() { }
	}
	public class Test : IOption
	{
		public ConsoleKey Key { get; } = ConsoleKey.L;
		public string Name { get; } = "Test";
		public override int GetHashCode() => Key.GetHashCode();
		public override bool Equals(object? obj)
		{
			if (obj != null && obj is IOption)
				return Key.GetHashCode() == obj.GetHashCode();
			return false;
		}
		public void Execute()
		{

		}
	}
	//TODO
	public class Stop : IOption
	{
		public ConsoleKey Key { get; } = ConsoleKey.S;
		public string Name { get; } = "Stop";
		public override int GetHashCode() => Key.GetHashCode();
		public override bool Equals(object? obj)
		{
			if (obj != null && obj is IOption)
				return Key.GetHashCode() == obj.GetHashCode();
			return false;
		}
		public void Execute()
		{
			//SocketServer.Stop();
			UI.Options.Remove(new Stop());
		}
	}
	public class Quit : IOption
	{
		public ConsoleKey Key { get; } = ConsoleKey.Q;
		public string Name { get; } = "Quit";
		public void Execute() => Environment.Exit(0);
	}
	public class OpenAccessRecord : IOption
	{
		public ConsoleKey Key { get; } = ConsoleKey.W;
		public string Name { get; } = "Open AccessRecord";
		public void Execute()
		{
			OpenAccessLog = true;
			Options.Remove(new OpenAccessRecord());
			Options.Add(new CloseAccessRecord());
			Options.Remove(new OpenLogs());
			Options.Add(new IncreaseWidth());
			Options.Add(new ReduceWidth());

		}
		public override int GetHashCode() => Key.GetHashCode();
		public override bool Equals(object? obj)
		{
			if (obj != null && obj is IOption)
				return Key.GetHashCode() == obj.GetHashCode();
			return false;
		}
	}
	public class CloseAccessRecord : IOption
	{
		public ConsoleKey Key { get; } = ConsoleKey.W;
		public string Name { get; } = "Close AccessRecord";
		public void Execute()
		{
			OpenAccessLog = false;
			Options.Remove(new CloseAccessRecord());
			Options.Add(new OpenAccessRecord());
			Options.Add(new OpenLogs());
			DBEngine.CurrentPage = 0;
			Options.Remove(new IncreaseWidth());
			Options.Remove(new ReduceWidth());
		}
		public override int GetHashCode() => Key.GetHashCode();
		public override bool Equals(object? obj)
		{
			if (obj != null && obj is IOption)
				return Key.GetHashCode() == obj.GetHashCode();
			return false;
		}
	}
	public class OpenLogs : IOption
	{
		public ConsoleKey Key { get; } = ConsoleKey.B;
		public string Name { get; } = "Open Logs";
		public void Execute()
		{
			OpenLog = true;
			Options.Remove(new OpenLogs());
			Options.Add(new CloseLogs());

			Options.Remove(new OpenAccessRecord());
			Options.Add(new IncreaseWidth());
			Options.Add(new ReduceWidth());
		}
		public override int GetHashCode() => Key.GetHashCode();
		public override bool Equals(object? obj)
		{
			if (obj != null && obj is IOption)
				return Key.GetHashCode() == obj.GetHashCode();
			return false;
		}
	}
	public class CloseLogs : IOption
	{
		public ConsoleKey Key { get; } = ConsoleKey.B;
		public string Name { get; } = "Close Logs";
		public void Execute()
		{
			OpenLog = false;
			Options.Remove(new CloseLogs());
			Options.Add(new OpenLogs());

			Options.Add(new OpenAccessRecord());
			DBEngine.CurrentPage = 0;
			Options.Remove(new IncreaseWidth());
			Options.Remove(new ReduceWidth());
		}
		public override int GetHashCode() => Key.GetHashCode();
		public override bool Equals(object? obj)
		{
			if (obj != null && obj is IOption)
				return Key.GetHashCode() == obj.GetHashCode();
			return false;
		}
	}
	public class IncreaseWidth : IOption
	{
		public ConsoleKey Key { get; } = ConsoleKey.T;

		public string Name { get; } = "Increase the width of the table";
		public override int GetHashCode() => Key.GetHashCode();
		public void Execute()
		{
			DBEngine.MaxWidth += 1;
			DBEngine.MinWidth += 1;
		}
		public override bool Equals(object? obj)
		{
			if (obj != null && obj is IOption)
				return Key.GetHashCode() == obj.GetHashCode();
			return false;
		}
	}
	public class ReduceWidth : IOption
	{
		public ConsoleKey Key { get; } = ConsoleKey.R;

		public string Name { get; } = "Reduce the table width";
		public void Execute()
		{
			DBEngine.MaxWidth -= 1;
			DBEngine.MinWidth -= 1;
		}

		public override int GetHashCode() => Key.GetHashCode();
		public override bool Equals(object? obj)
		{
			if (obj != null && obj is IOption)
				return Key.GetHashCode() == obj.GetHashCode();
			return false;
		}
	}

	public class UP : IOption
	{
		public ConsoleKey Key { get; } = ConsoleKey.U;
		public string Name { get; } = "Page UP";
		public void Execute()
		{
			if (DBEngine.CurrentPage > 0)
				DBEngine.CurrentPage--;
		}
		public override int GetHashCode() => Key.GetHashCode();
		public override bool Equals(object? obj)
		{
			if (obj != null && obj is IOption)
				return Key.GetHashCode() == obj.GetHashCode();
			return false;
		}
	}
	public class Down : IOption
	{
		public ConsoleKey Key { get; } = ConsoleKey.D;
		public string Name { get; } = "Page Down";
		public void Execute()
		{
			if (DBEngine.CurrentPage < DBEngine.TotalPages - 1)
				DBEngine.CurrentPage++;
		}
		public override int GetHashCode() => Key.GetHashCode();
		public override bool Equals(object? obj)
		{
			if (obj != null && obj is IOption)
				return Key.GetHashCode() == obj.GetHashCode();
			return false;
		}
	}
}
