using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using akron.module;
using akronLog;
using akronConfig;
using  akronDB;
using System.IO.Compression;
/*   Author:Alrusix
*/
/*   正在实现：REST api
*/
/*   待实现:反向代理 负载均衡
*/
/*   无限期搁置:SSL/TLS   Http2/3  
*/
namespace akron
{
	public class TcpSocket
	{
		public readonly Socket Socket;
		public readonly string IpAddress;
		public readonly string Port;
		public List<HttpRequest> Converse;
		public TcpSocket(Socket socket)
		{
			Socket = socket;
			IpAddress = ((IPEndPoint)socket.RemoteEndPoint)?.Address.ToString() ?? "unknown";
			Port = ((IPEndPoint)socket.RemoteEndPoint)?.Port.ToString() ?? "unknown";
			Converse = new List<HttpRequest>();
		}
		public HttpRequest this[Index index]
		{
			get
			{
				if (index.GetOffset(Converse.Count) is int offset && offset >= 0 && offset < Converse.Count)
					return Converse[offset];
				throw new ArgumentOutOfRangeException(nameof(index), "索引超出范围");
			}
			set
			{
				if (index.GetOffset(Converse.Count) is int offset && offset >= 0 && offset < Converse.Count)
					Converse[offset] = value;
				else
					throw new ArgumentOutOfRangeException(nameof(index), "索引超出范围");
			}
		}
	}
	public class HttpRequest
	{
		public string Method { get; set; } = "Unkown";
		public string Url { get; set; } = "Unkown";
		public string HttpProtocol { get; set; } = "HTTP/1.1";
		public Dictionary<string, string> Headers { get; set; } = [];
		public string Connection { get; set; } = "close";
		public string? Body { get; set; }
		public string? QueryParameters { get; set; }
		public DateTime LastdateTime { get; set; }
		public string? Content_Type { get; set; } = "application/octet-stream";
		public string StatusCode { get; set; } = "Unkown";
		public byte[] Content { get; set; } = [];
	}
	public class Cache
	{
		public byte[] Data { get; private set; }
		public DateTime ExpiryTime { get; set; }
		public string ContentType { get; set; }
		public Cache(byte[] data, TimeSpan cacheDuration, string type)
		{
			Data = data;
			ExpiryTime = DateTime.Now.Add(cacheDuration);
			ContentType = type;
		}
		public bool IsValid()=> DateTime.Now < ExpiryTime;	
	}
	public class SocketServer
	{
		public static readonly Logger logger = new(Config.Get<string>("Log:Path"));
		static ConcurrentDictionary<string, Cache> _cache = new ConcurrentDictionary<string, Cache>();
		static readonly Socket listener = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
		static readonly CancellationTokenSource cancellationTokenSource = new();
		static readonly Dictionary<string, string> mimeTypes = Config.Parse();
		static string contentType = Config.Get<string>("Http:Type");
		static readonly string rootDirectory = Config.Get<string>("Http:Root");
		static readonly int port = Config.Get<int>("Server:Listen");
		static readonly int backlog = Config.Get<int>("Server: Worker_Connections");
		static readonly string IndexPath = Config.Get<string>("Http:Index");
		static readonly string[] separator = ["\r\n"];
		public static void Listener()
		{
			listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
			listener.Bind(new IPEndPoint(IPAddress.Any, port));
			listener.Listen(backlog);
			Check();
			Task.Run(() => AcceptHttpRequest(cancellationTokenSource.Token));			
		}
		public static async Task AcceptHttpRequest(CancellationToken cancellationToken)
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				try
				{
					//高负载下换成AcceptAsync(SocketAsyncEventArgs), 暂时搁置
					Socket clientSocket = await listener.AcceptAsync(cancellationToken);
					clientSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, 120000);
					_ = Task.Run(() => AcceptRequest(new TcpSocket(clientSocket)), cancellationToken);
				}
				catch (SocketException ex)
				{
					logger.Log($"{ex.ErrorCode} : {ex.SocketErrorCode} : {ex.Source} : {ex.Message} : {ex}");
				}
			}
		}
		public static void Check()
		{
			if (!DBEngine.Check("SocketLogs"))
			{
				string sql = @"
		          CREATE TABLE SocketLogs (
					  IPAdderss System.String,
		              Method System.String,
		              Url System.String,
		              LastdateTime System.DateTime,
		              StatusCode System.String	);				  
				";			
				switch (akronDB.DBEngine.ParseSQL(sql))
				{
					case 0x1:SocketServer.logger.Log($"表名解析失败，请检查 SQL 语句格式是否正确", 1); break;
					case 0x2:SocketServer.logger.Log($"表名已存在", 1);break;
					case 0x3:break;//创建成功
					default:break;
				}
			}			
		}
		public static void CloseSocket(TcpSocket tcpSocket)
		{
			try
			{
				tcpSocket.Socket.Shutdown(SocketShutdown.Both);
				tcpSocket.Socket.Close();
				foreach (var request in tcpSocket.Converse)
				{
					string sqlQuery = $"INSERT INTO SocketLogs ( IPAdderss,Method , Url,LastdateTime,StatusCode) VALUES ({tcpSocket.IpAddress},{request.Method},{request.Url},{request.LastdateTime.ToString("G")},{request.StatusCode})";
					akronDB.DBEngine.ParseSQL(sqlQuery);
				}
			}
			catch (SocketException ex)
			{
				logger.Log($"{ex.ErrorCode} : {ex.SocketErrorCode} : {ex.Source} : {ex.Message} : {ex}");
			}
		}
			/*   Timeout handle  2选1
			*/
			/*   //高开销
			*/
			/*   Socket.Poll(1000*2000, SelectMode.SelectRead); //wait 2s 
			*/
			/*   //会报异常
			*/
			/*   Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, 5000);
			*/
		public static void AcceptRequest(TcpSocket tcpSocket)
		{
			/*   Author:Alrusix
			*/	 https://github.com/Alrusix/			
			/*	 //int keepAliveTimeoutSeconds = Config.Get<int>("Keep-aliveOutTime", 120);
			*/   //while ((DateTime.Now - tcpSocket[^1].LastdateTime).Seconds < keepAliveTimeoutSeconds)
			/*   //{
			*/   //	if (tcpSocket.Socket.Available > 0)goto Http;
			/*   //	Thread.Sleep(100);
			*/   //}
			/*   如果你只关心低开销和实现高效读取，并希望知道有多少数据可以立即读入时，Available 是更好的选择
			*/
			try
			{
				tcpSocket.Converse.Add(new HttpRequest());
				switch (Parse(tcpSocket))
				{
					case 0x0:
						logger.Log($"{tcpSocket.IpAddress}close Socket");
						break;
					case 0x1:
						RouteProces(tcpSocket);
						if (tcpSocket[^1].Connection.Equals("keep-alive", StringComparison.OrdinalIgnoreCase))
							while (tcpSocket.Socket.Poll(1000 * 20000, SelectMode.SelectRead)) goto https;
						break;
					case 0x2:
						if (tcpSocket[^1].Headers.TryGetValue("Sec-WebSocket-Key", out string? _Key) && !string.IsNullOrEmpty(_Key))
						{
							MethodInfo? methodInfo = FindRouteHandlers<WebSocket>("ws");
							if (methodInfo != null)
							{
								_ = Task.Run(() => methodInfo.Invoke(null, new object?[] { tcpSocket.Socket, _Key }));
								logger.Log($"{tcpSocket.IpAddress}:{tcpSocket[^1].Method}:WebSocket");
							}
						}
						return;
					case 0x3:
						tcpSocket[^1].StatusCode = Config.Get<string>("StatusCode:HTTP Version not supported", "505 HTTP Version Not Supported");
						SendResponse(tcpSocket);
						break;
					default: break;
				}
				CloseSocket(tcpSocket);
			}
			catch (SocketException ex)
			{
				logger.Log($"{ex.ErrorCode} : {ex.SocketErrorCode} : {ex.Source} : {ex.Message} : {ex}");
			}
		}
		private static int Parse(TcpSocket tcpSocket)
		{		
			Span<byte> buffer = stackalloc byte[8192];
			int bytesRead = tcpSocket.Socket.Receive(buffer);
			if (bytesRead == 0) return 0x0;
			string requestText = Encoding.UTF8.GetString(buffer.ToArray(), 0, bytesRead);
			tcpSocket[^1].LastdateTime = DateTime.Now;
			Span<string> lines = requestText.Split(separator, StringSplitOptions.None);
			Span<string> requestLineParts = lines[0].Split(' ');
			tcpSocket[^1].Method = requestLineParts[0];
			string urlWithParams = requestLineParts[1];
			Span<string> urlParts = urlWithParams.Split('?');
			if (urlParts.Length > 1)
				tcpSocket[^1].QueryParameters = urlParts[1];
			tcpSocket[^1].Url = urlParts[0];
			if (!requestLineParts[2].Equals("HTTP/1.1"))
				return 0x3;
			int lineIndex = 1;
			while (!string.IsNullOrEmpty(lines[lineIndex]))
			{
				Span<string> headerParts = lines[lineIndex].Split([':'], 2);
				if (headerParts.Length == 2)
					tcpSocket[^1].Headers[headerParts[0].Trim()] = headerParts[1].Trim();
				lineIndex++;
			}
			if (tcpSocket[^1].Headers.TryGetValue("Connection", out string? _Connection))
				tcpSocket[^1].Connection = _Connection;
			if (tcpSocket[^1].Headers.TryGetValue("Content-Length", out string? contentLengthValue) &&
			int.TryParse(contentLengthValue, out int contentLength))
			{
				int bodyStartIndex = requestText.IndexOf("\r\n\r\n") + 4;
				tcpSocket[^1].Body = requestText.Substring(bodyStartIndex, contentLength);
			}
			if (tcpSocket[^1].Headers.TryGetValue("Upgrade", out string? _Upgrade) &&
				  tcpSocket[^1].Connection.Equals("Upgrade", StringComparison.OrdinalIgnoreCase) &&
								  _Upgrade.Equals("websocket", StringComparison.OrdinalIgnoreCase)) return 0x2;
			logger.Log($"{tcpSocket.IpAddress}:{tcpSocket[^1].Method}:{tcpSocket[^1].Url}");
			return 0x1;
		}
		//TODO  
		[AttributeUsage(AttributeTargets.Method)]
		public class RouteAttribute(string path) : Attribute
		{
			public string Path { get; } = path;
		}
		public static MethodInfo? FindRouteHandlers<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] T>(string route) =>
																	typeof(T).GetMethods(BindingFlags.Public | BindingFlags.Static)
																   .Where(m => m.GetCustomAttributes(typeof(RouteAttribute), false)
																   .Cast<RouteAttribute>()
																   .Any(attr => attr.Path == route))
																   .ToArray()
																   .FirstOrDefault();
		//路由处理
		public static void RouteProces(TcpSocket tcpSocket)
		{
			try
			{
				string filePath = Path.Combine(rootDirectory, IndexPath);
				if (tcpSocket[^1].Url != "/")
				{
					filePath = Path.Combine(rootDirectory, tcpSocket[^1].Url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
					if (!Path.GetFullPath(filePath).StartsWith(rootDirectory, StringComparison.OrdinalIgnoreCase))
					{
						logger.Log("非法操作");
						return;
					}
				}
				switch (tcpSocket[^1].Method)
				{
					case "GET":
						HandleGetRequest(tcpSocket, filePath);
						break;
					case "POST":
						HandlePostRequest(tcpSocket);
						break;
					//case "PUT":
					//	break;
					//case "DELETE":
					//	break;
					case "OPTIONS":
						tcpSocket[^1].StatusCode = Config.Get<string>("StatusCode:OK", "200 OK");
						tcpSocket[^1].Content = Encoding.UTF8.GetBytes("Allowed methods: " + Config.Get<string>("Http:Allow", "GET"));
						tcpSocket[^1].Content_Type = "text/plain";
						SendResponse(tcpSocket, $"{Config.Get<string>("Http:Allow", "GET,POST")}\r\n");
						break;
					//case "TRACE":
					//	break;
					//case "CONNECT":
					//	break;
					default:
						tcpSocket[^1].StatusCode = Config.Get<string>("StatusCode:Method Not Allowed", "405 Method Not Allowed");
						SendResponse(tcpSocket);
						break;
				}

			}
			catch (SocketException ex)
			{
				logger.Log($"{ex.ErrorCode} : {ex.SocketErrorCode} : {ex.Source} : {ex.Message} : {ex}");
			}
		}
		// 搁置
		private static void HandlePostRequest(TcpSocket tcpSocket)
		{
			
			MethodInfo? methodInfo = FindRouteHandlers<HttpAPI>(tcpSocket[^1].Url);
			if (methodInfo != null)
			{
				SendResponse(methodInfo.Invoke(null, [tcpSocket]) as TcpSocket);
			}
			else
			{
				logger.Log($"POST Body: {tcpSocket[^1].Body}");
				tcpSocket[^1].StatusCode = Config.Get<string>("StatusCode:Not Found", "404 Not Found");
				SendResponse(tcpSocket);
			}
		}
		private static void HandleGetRequest(TcpSocket tcpSocket, string filePath)
		{
			bool noCache = tcpSocket[^1].Connection.Equals("keep-alive", StringComparison.OrdinalIgnoreCase);
			if (noCache && _cache.TryGetValue(filePath, out Cache? cacheItem) && cacheItem.IsValid())
			{
				//logger.Log($"找到了{filePath}的缓存");
				Span<byte> cachedResponse =  cacheItem.Data;
				tcpSocket[^1].StatusCode = Config.Get<string>("StatusCode:OK", "200 OK");
				tcpSocket[^1].Content = cachedResponse.ToArray();
				tcpSocket[^1].Content_Type = cacheItem.ContentType;
				SendResponse(tcpSocket);
			}
			else
			{
				if (File.Exists(filePath))
				{
					byte[] fileContent = File.ReadAllBytes(filePath);
					string extension = Path.GetExtension(filePath);
					contentType = mimeTypes.TryGetValue(extension, out string? value) ? value : "application/octet-stream";
					_cache.AddOrUpdate(filePath, new Cache(fileContent, TimeSpan.FromHours(1), contentType), (k, v) => v = new Cache(data: fileContent, TimeSpan.FromHours(1), contentType));
					//_cache[filePath] = new Cache(fileContent, TimeSpan.FromHours(1), contentType);
					//logger.Log($"更新 {filePath}的缓存");
					tcpSocket[^1].StatusCode = Config.Get<string>("StatusCode:OK", "200 OK");
					tcpSocket[^1].Content = fileContent.ToArray();
					tcpSocket[^1].Content_Type = contentType;
					SendResponse(tcpSocket);
				}
				else
				{
					tcpSocket[^1].StatusCode = Config.Get<string>("StatusCode:Not Found", "404 Not Found");
					SendResponse(tcpSocket);
				}
			}
		}
		/// <returns>暂时搁置</returns>
		public static bool SendResponse(TcpSocket tcpSocket, string w = "")
		{
			try
			{
				if(tcpSocket==null) return false;
				if (tcpSocket[^1].Headers.TryGetValue("Accept-Encoding", out string? _Encoding) && _Encoding.Contains("gzip"))
				{
					tcpSocket[^1].Content = CompressWithGzip(tcpSocket[^1].Content);
					w = $"Content-Encoding: gzip\r\n{w}" ;
				}
				string responseString =
				$"{tcpSocket[^1].HttpProtocol} " +
				$"{tcpSocket[^1].StatusCode} " + "\r\n" +
				$"Content-Length: {tcpSocket[^1].Content.Length}\r\n" +
				$"Content-Type: {tcpSocket[^1].Content_Type}\r\n" +
				w + 
				$"Connection：{tcpSocket[^1].Connection}\r\n" +
				$"Server：Nginx/1.0.1\r\n" +
				$"Accept-Ranges: bytes\r\n" +
				$"Expires: {DateTime.UtcNow.AddHours(10):R}\r\n\r\n";

				Span<byte> headerBytes = System.Text.Encoding.UTF8.GetBytes(responseString);
				if (tcpSocket.Socket.Poll(5000 * 1000, SelectMode.SelectWrite))
				{
					tcpSocket.Socket.Send(headerBytes);									
					tcpSocket.Socket.Send(tcpSocket[^1].Content);
					//logger.Log($"等待下条消息,当前时间{DateTime.Now}\r\n");
					return true;
				}
				return false;
			}
			catch (SocketException ex)
			{
				logger.Log($"{ex.ErrorCode} : {ex.SocketErrorCode} : {ex.Source} : {ex.Message} : {ex}");
				return false;
			}
		}
		/// <summary>
		/// 使用 Gzip 动态压缩数据
		/// </summary>
		private static byte[] CompressWithGzip(byte[] data)
		{
			CompressionLevel compressionLevel;
			int dataSize = data.Length;
			if (dataSize < 1024)
				compressionLevel= CompressionLevel.Fastest; // 小文件使用最快速压缩
			else if (dataSize < 10240)
				compressionLevel = CompressionLevel.Optimal; // 中等文件使用优化压缩
			else
				compressionLevel = CompressionLevel.SmallestSize; // 大文件使用最大压缩率	
			using (MemoryStream output = new MemoryStream())
			{
				using (GZipStream gzip = new GZipStream(output, compressionLevel))
				{
					gzip.Write(data, 0, data.Length);
				}
				return output.ToArray();
			}
		}		
		// Stop server 
		public static void Stop()
		{			
			cancellationTokenSource.Cancel();
			listener.Close();
			logger.Log("Server stopped gracefully.");
		}
	}
}



