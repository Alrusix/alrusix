using akronConfig;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static akron.SocketServer;
using akronDB;

namespace akron.module
{
	//暂时搁置
	internal class HttpAPI
	{
		public HttpAPI() { }

		//[Route("/phpinfo.php")]
		[Route("/phpinfo.cpp")]
		public static TcpSocket LoginUser(TcpSocket tcpSocket)
		{
			logger.Log(tcpSocket[^1].Body??"");
			//TODO
			tcpSocket[^1].Content = System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(DBEngine.GetTable("SocketLogs")));
			tcpSocket[^1].Content_Type = "application/json";
			tcpSocket[^1].StatusCode = Config.Get<string>("StatusCode:OK", "200 OK");
			return tcpSocket;
		}
		//解析参数 TODO
		public static Dictionary<string, string> ParsingParameters(string ww)
		{
			var QueryParameters = new Dictionary<string, string>();
			if (ww.Length > 1)
			{
				string[] parameters = ww.Split('&');
				foreach (string parameter in parameters)
				{
					string[] keyValue = parameter.Split('=');
					if (keyValue.Length == 2)
					{
						QueryParameters[keyValue[0]] = keyValue[1];
					}
				}
			}
			return QueryParameters;
		}

	}
}
