using akron;
using System.Collections.Concurrent;
namespace akronLog
{
	public enum LogLevel
	{
		Info,
		Warning,
		Error,
		Debug
	}
	public class Logger
	{
		private ConcurrentQueue<string> _logQueue = new ConcurrentQueue<string>();
		private AutoResetEvent _logSignal = new AutoResetEvent(false);
		private string LogFilePath;
		private bool _isLogging = true;
		private readonly StreamWriter _writer;
		public Logger(string logFilePath)
		{
			LogFilePath = logFilePath;
			_writer = new StreamWriter(LogFilePath, true)
			{
				AutoFlush = false
			};
			string sql = @"
		          CREATE TABLE Logs (
					  Level System.String,
		              Message System.String,
					  Time System.DateTime
				);
				";
			akronDB.DBEngine.ParseSQL(sql);
			Task.Run(() => StartLogging());		
		}
		private void StartLogging()
		{
			while (_isLogging || !_logQueue.IsEmpty)
			{
				_logSignal.WaitOne();
				while (_logQueue.TryDequeue(out var logMessage))
				{
					_writer.WriteLine(logMessage);
				}
				_writer.Flush();
			}
		}
		public void StopLogging()
		{
			_isLogging = false;
			_logSignal.Set();
			while (!_logQueue.IsEmpty)
			{
			}
			_writer.Flush();
			_writer.Close();
		}
		/// <param name="message"> </param>
		/// <param name="level">0:Info;  1:Warning;  2:Error</param>
		public void Log(string message, int level = 0)
		{
			switch (level)
			{
				case 0: Info(message); break;
				case 1: Warning(message); break;
				case 2: Error(message); break;
				default: Debug(message); break;
			}
			string sqlQuery = $"INSERT INTO Logs ( Level,Message,Time ) VALUES ({(LogLevel)level},{message.Replace(",","，")},{DateTime.Now.ToString("G")})";
			akronDB.DBEngine.ParseSQL(sqlQuery);
		}
		//暂时搁置
		 void Info(string message)
		{
			_logQueue.Enqueue($"Info:{DateTime.Now:yyyy-MM-dd HH:mm:ss}: {message}");
			_logSignal.Set();		
		}

		 void Warning(string message)
		{
			_logQueue.Enqueue($"Warning:{DateTime.Now:yyyy-MM-dd HH:mm:ss}: {message}");
			_logSignal.Set();

		}
		 void Error(string message)
		{
			_logQueue.Enqueue($"Error:{DateTime.Now:yyyy-MM-dd HH:mm:ss}: {message}");
			_logSignal.Set();

		}
		 void Debug(string message)
		{
			_logQueue.Enqueue($"Warning:{DateTime.Now:yyyy-MM-dd HH:mm:ss}: {message}");
			_logSignal.Set();

		}
	}
}
