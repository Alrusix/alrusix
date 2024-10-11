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
		public static DBEngine engine { get; set; }

		static void Main(string[] args)
		{
			Config config = new("config.ini");
			engine = new DBEngine().SetDBPath(new(config.Get<string>("DB:Path", "akron.adb")));
			engine.Init();

			string sql = @"
		          CREATE TABLE Logs (
					  Level System.String,
		              Message System.String,
					  Time System.DateTime
				);
				 CREATE TABLE SocketLogs (
					  IPAdderss System.String,
		              Method System.String,
		              Url System.String,
		              LastdateTime System.DateTime,
		              StatusCode System.String	);";
			Program.engine.ParseSQL(sql);
						
			//使用默认配置
			//var s = new SocketServer();
			//使用配置文件配置
			//var s = new SocketServer(config);
			//手动配置某项  
			var s = new SocketServer().SetPort(config.Get<ushort>("Server:Listen"))
									  .SetHomePage(config.Get<string>("Http:Index"))
									  .SetLog(new(config.Get<string>("Log:Path")))
									  .SetRootDirectory(config.Get<string>("Http:Root"))
									  .SetBacklog(config.Get<int>("Server: Worker_Connections"))
									  .SetMimeTypes(config.GetMimeTypes())
									  .SetStatusCode(config.GetStatusCode())
									  .SetLog(new akronLog.Log("test.log"));

			s.Listen();

			UI.Start();
		}
	}
}