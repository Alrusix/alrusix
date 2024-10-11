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
using akronDB;
using System.IO.Compression;
using akronWS;
using System;
using static System.Net.Mime.MediaTypeNames;
using System.Net.NetworkInformation;
using System.Threading;
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
		public TcpSocket(Socket socket,string _IpAddress,string _Port)
		{
			Socket = socket;
			IpAddress = _IpAddress;
			Port = _Port;
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
		public bool IsValid() => DateTime.Now < ExpiryTime;
	}
	public class SocketServer
	{	
		ConcurrentDictionary<string, Cache> cache = new();
		Socket listener = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);		
		readonly string[] separator = ["\r\n"];

		Log Logger { get; set; }
		Dictionary<string, string> MimeTypes { get; set; }
		Dictionary<int, string> StatusCode { get; set; }
		string DefaultContentType { get; set; }
		string RootDirectory { get; set; }
		ushort Port { get; set; }
		int Backlog { get; set; }
		string HomePage { get; set; }
		CancellationTokenSource cancellationTokenSource { get; set; }

		#region SetSocket
		public SocketServer()
		{
			Port = 8080;
			Backlog  = 20;
			HomePage  = "index.html";
			DefaultContentType = "application/octet-stream";
			RootDirectory = "/www";
			MimeTypes = new Dictionary<string, string>
			{
				{ ".html", "text/html" },
				{ ".htm", "text/html" }				
			};
			StatusCode = new Dictionary<int, string>
			{
				{ 200, "200 OK" },
				{ 404, "404 Not Found" }
			};
			Logger = new("akron.log");
			cancellationTokenSource = new CancellationTokenSource();
		}
		public SocketServer(Config config):this()
		{
			this.SetPort(config.Get<ushort>("Server:Listen"))
									  .SetHomePage(config.Get<string>("Http:Index"))
									  .SetLog(new(config.Get<string>("Log:Path")))
									  .SetRootDirectory(config.Get<string>("Http:Root"))
									  .SetBacklog(config.Get<int>("Server: Worker_Connections"))
									  .SetMimeTypes(config.GetMimeTypes())
									  .SetStatusCode(config.GetStatusCode())
									  .SetLog(new Log(config.Get<string>("Log:Path")));
		}
		/// <summary>
		/// 指定可排队等待接受的传入连接数
		/// </summary>
		public SocketServer SetBacklog(int _backlog)
		{
			Backlog = _backlog;
			return this;
		}

		/// <param name="_logger">日志文件不要设置为默认路径akron.log</param>
		public SocketServer SetLog(Log _logger)
		{
			Logger = _logger;
			return this;
		}
		public SocketServer SetPort(ushort _Port)
		{
			Port = _Port;
			return this;
		}
		public SocketServer SetHomePage(string _HomePage)
		{
			HomePage = _HomePage;
			return this;
		}
		public SocketServer SetRootDirectory(string _RootDirectory)
		{
			RootDirectory = _RootDirectory;
			return this;
		}
		public SocketServer SetDefaultContentType(string _DefaultContentType)
		{
			DefaultContentType = _DefaultContentType;
			return this;
		}
		public SocketServer SetCancellationTokenSource(CancellationTokenSource _cancellationTokenSource)
		{
			cancellationTokenSource = _cancellationTokenSource;
			return this;
		}
		public SocketServer SetMimeTypes(Dictionary<string, string> _MimeTypes)
		{
			MimeTypes = _MimeTypes;
			return this;
		}
		public SocketServer SetStatusCode(Dictionary<int, string> _StatusCode)
		{
			StatusCode = _StatusCode;
			return this;
		}
		#endregion
		public void Listen()
		{
			try
			{
				listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
				listener.Bind(new IPEndPoint(IPAddress.Any, Port));
				listener.Listen(Backlog);
				Task.Run(() => AcceptHttpRequest(cancellationTokenSource.Token));
				WWW.Start();
			}
			catch (SocketException e)
			{
				Logger.log($"Message:{e.Message}|SocketErrorCode:{e.SocketErrorCode}",2);
			}		
		}
		async Task AcceptHttpRequest(CancellationToken cancellationToken)
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				try
				{
					//高负载下换成AcceptAsync(SocketAsyncEventArgs), 暂时搁置
					Socket clientSocket = await listener.AcceptAsync(cancellationToken);
					clientSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, 120000);
					_ = Task.Run(() => ww(clientSocket, cancellationToken));
				}
				catch (SocketException ex)
				{
					Logger.log($"{ex.ErrorCode} : {ex.SocketErrorCode} : {ex.Source} : {ex.Message} : {ex}", 2);
				}
			}
		}
		void ww(Socket socket, CancellationToken cancellationToken)
		{

			string IpAddress = ((IPEndPoint)socket.RemoteEndPoint!).Address.ToString() ?? "unknown";
			string Port = ((IPEndPoint)socket.RemoteEndPoint)?.Port.ToString() ?? "unknown";
			if (IpAddress.Equals("unknown") || Port.Equals("unknown"))
				return;
			_ = Task.Run(() => AcceptRequest(new TcpSocket(socket,IpAddress,Port)), cancellationToken);
		}

		public void CloseSocket(TcpSocket tcpSocket)
		{
			try
			{
				tcpSocket.Socket.Shutdown(SocketShutdown.Both);
				tcpSocket.Socket.Close();
				foreach (var request in tcpSocket.Converse)
				{
					string sqlQuery = $"INSERT INTO SocketLogs ( IPAdderss,Method , Url,LastdateTime,StatusCode) VALUES ({tcpSocket.IpAddress},{request.Method},{request.Url},{request.LastdateTime.ToString("G")},{request.StatusCode})";
					Program.engine.ParseSQL(sqlQuery);
				}
			}
			catch (SocketException ex)
			{
				Logger.log($"{ex.ErrorCode} : {ex.SocketErrorCode} : {ex.Source} : {ex.Message} : {ex}", 2);
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

		public void AcceptRequest(TcpSocket tcpSocket)
		{
		/*   Author:Alrusix
		*/
		https://github.com/Alrusix/			
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
						Logger.log($"{tcpSocket.IpAddress}close Socket");
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
								Logger.log($"{tcpSocket.IpAddress}:{tcpSocket[^1].Method}:WebSocket");
							}
						}
						return;
					case 0x3:
						tcpSocket[^1].StatusCode = StatusCode.TryGetValue(505, out string? value)? value:"505 HTTP Version Not Supported";
						SendResponse(tcpSocket);
						break;
					default: break;
				}
				CloseSocket(tcpSocket);
			}
			catch (SocketException ex)
			{
				Logger.log($"{ex.ErrorCode} : {ex.SocketErrorCode} : {ex.Source} : {ex.Message} : {ex}", 2);
			}
		}
		private int Parse(TcpSocket tcpSocket)
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
			Logger.log($"{tcpSocket.IpAddress}:{tcpSocket[^1].Method}:{tcpSocket[^1].Url}", 0);
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
		public void RouteProces(TcpSocket tcpSocket)
		{
			try
			{
				string filePath = Path.Combine(RootDirectory, HomePage);
				if (tcpSocket[^1].Url != "/")
				{
					filePath = Path.Combine(RootDirectory, tcpSocket[^1].Url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
					if (!Path.GetFullPath(filePath).StartsWith(RootDirectory, StringComparison.OrdinalIgnoreCase))
					{
						Logger.log($"{tcpSocket.IpAddress}试图访问{filePath}", 1);
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
						tcpSocket[^1].StatusCode = StatusCode.TryGetValue(200, out string? _value) ? _value : "200 OK";
						tcpSocket[^1].Content_Type = "text/plain";
						SendResponse(tcpSocket, "Allow: GET,POST\r\n");
						break;
					//case "TRACE":
					//	break;
					//case "CONNECT":
					//	break;
					default:
						tcpSocket[^1].StatusCode = StatusCode.TryGetValue(405, out string? value) ? value : "405 Method Not Allowed";
						SendResponse(tcpSocket);
						break;
				}

			}
			catch (SocketException ex)
			{
				Logger.log($"{ex.ErrorCode} : {ex.SocketErrorCode} : {ex.Source} : {ex.Message} : {ex}", 2);
			}
		}
		// 搁置
		private void HandlePostRequest(TcpSocket tcpSocket)
		{

			MethodInfo? methodInfo = FindRouteHandlers<HttpAPI>(tcpSocket[^1].Url);
			if (methodInfo != null)
			{
				SendResponse(methodInfo.Invoke(null, [tcpSocket]) as TcpSocket);
			}
			else
			{
				Logger.log($"{tcpSocket.IpAddress}POST Body: {tcpSocket[^1].Body}", 0);
				tcpSocket[^1].StatusCode = StatusCode.TryGetValue(404, out string? _value) ? _value : "404 Not Found";
				SendResponse(tcpSocket);
			}
		}
		private void HandleGetRequest(TcpSocket tcpSocket, string filePath)
		{
			bool noCache = tcpSocket[^1].Connection.Equals("keep-alive", StringComparison.OrdinalIgnoreCase);
			if (noCache && cache.TryGetValue(filePath, out Cache? cacheItem) && cacheItem.IsValid())
			{
				//logger.Log($"找到了{filePath}的缓存");
				Span<byte> cachedResponse = cacheItem.Data;
				tcpSocket[^1].StatusCode = StatusCode.TryGetValue(200, out string? _value) ? _value : "200 OK";
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
					DefaultContentType = MimeTypes.TryGetValue(extension, out string? value) ? value : "application/octet-stream";
					cache.AddOrUpdate(filePath, new Cache(fileContent, TimeSpan.FromHours(1), DefaultContentType), (k, v) => v = new Cache(data: fileContent, TimeSpan.FromHours(1), DefaultContentType));
					//_cache[filePath] = new Cache(fileContent, TimeSpan.FromHours(1), contentType);
					//logger.Log($"更新 {filePath}的缓存");
					tcpSocket[^1].StatusCode = StatusCode.TryGetValue(200, out string? _value) ? _value : "200 OK";
					tcpSocket[^1].Content = fileContent.ToArray();
					tcpSocket[^1].Content_Type = DefaultContentType;
					SendResponse(tcpSocket);
				}
				else
				{
					tcpSocket[^1].StatusCode = StatusCode.TryGetValue(404, out string? _value) ? _value : "404 Not Found";
					SendResponse(tcpSocket);
				}
			}
		}
		/// <returns>暂时搁置</returns>
		public bool SendResponse(TcpSocket tcpSocket, string w = "")
		{
			try
			{
				if (tcpSocket == null) return false;
				if (tcpSocket[^1].Headers.TryGetValue("Accept-Encoding", out string? _Encoding) && _Encoding.Contains("gzip"))
				{
					tcpSocket[^1].Content = CompressWithGzip(tcpSocket[^1].Content);
					w = $"Content-Encoding: gzip\r\n{w}";
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
				Logger.log($"{ex.ErrorCode} : {ex.SocketErrorCode} : {ex.Source} : {ex.Message} : {ex}", 2);
				return false;
			}
		}
		/// <summary>
		/// 使用 Gzip 动态压缩数据
		/// </summary>
		private byte[] CompressWithGzip(byte[] data)
		{
			CompressionLevel compressionLevel;
			int dataSize = data.Length;
			if (dataSize < 1024)
				compressionLevel = CompressionLevel.Fastest; // 小文件使用最快速压缩
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
		public void Stop()
		{
			cancellationTokenSource.Cancel();
			listener.Close();
			Logger.log("Server stopped gracefully.", 0);
		}
	}
}



