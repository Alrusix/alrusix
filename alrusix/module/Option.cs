
using akronDB;

namespace akron.module
{
	public interface IOption
	{
		public ConsoleKey Key { get; }
		public string Name { get; }
		public void Execute() { }
	}
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
			SocketServer.Stop();
			Program.Options.Remove(new Stop());
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
			Program.OpenAccessLog = true;
			Program.Options.Add(new CloseAccessRecord());
			Program.Options.Remove(new OpenAccessRecord());
			Program.Options.Remove(new OpenLogs());

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
		public ConsoleKey Key { get; } = ConsoleKey.C;
		public string Name { get; } = "Close AccessRecord";
		public void Execute()
		{
			Program.OpenAccessLog = false;
			Program.Options.Add(new OpenAccessRecord());
			Program.Options.Remove(new CloseAccessRecord());
			Program.Options.Add(new OpenLogs());
			DBEngine.CurrentPage = 0;
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
			Program.OpenLog = true;
			Program.Options.Add(new CloseLogs());
			Program.Options.Remove(new OpenLogs());
			Program.Options.Remove(new OpenAccessRecord());
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
		public ConsoleKey Key { get; } = ConsoleKey.X;
		public string Name { get; } = "Close Logs";
		public void Execute()
		{
			Program.OpenLog = false;
			Program.Options.Add(new OpenLogs());
			Program.Options.Remove(new CloseLogs());
			Program.Options.Add(new OpenAccessRecord());
			DBEngine.CurrentPage = 0;
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
