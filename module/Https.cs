using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Net.Security;
using System.Threading;
using akronLog;
using akronConfig;
using System.Collections.Concurrent;
using System.Reflection;
using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Net.Http;
using akronWS;
using static akron.HTTPS.WebSockets;
namespace akron.HTTPS
{
	public class TcpSockets
	{
		public NetworkStream NetworkStream;
		public TcpClient TCP_Client;
		public SslStream SSL_Stream;
		public readonly string IpAddress;
		public readonly string Port;
		public List<HttpRequest> Converse;
		public TcpSockets(TcpClient client, NetworkStream networkStream, SslStream socket, string _IpAddress, string _Port)
		{
			NetworkStream = networkStream;
			TCP_Client = client;
			SSL_Stream = socket;
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
	
	public class Https
	{
		ConcurrentDictionary<string, Cache> cache = new();
		readonly string[] separator = ["\r\n"];

		
		Dictionary<string, string> MimeTypes { get; set; }
		Dictionary<int, string> StatusCode { get; set; }
		string DefaultContentType { get; set; }
		string RootDirectory { get; set; }
		ushort HttpPort { get; set; }
		int Backlog { get; set; }
		string HomePage { get; set; }
		
		TcpListener Httpslistener { get; set; }
		X509Certificate2 Certificate { get; set; }
		string CertPath { get; set; }
		string PassWord { get; set; }
		ushort Port { get; set; }
		CancellationTokenSource cancellationTokenSource { get; set; }
		Log Logger { get; set; }
		#region SetSocket
		public Https()
		{		
			Backlog = 20;
			HomePage = "index.html";
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

			Port = 443;
			Logger = new("akronHttps.log");
			cancellationTokenSource = new CancellationTokenSource();
			
			
		}
		public Https(Config config) : this()
		{
			SetPort(config.Get<ushort>("Https:Listen"))
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
		public Https SetBacklog(int _backlog)
		{
			//Backlog = _backlog;
			return this;
		}
		public Https SetCertPath(string _CertPath)
		{
			CertPath =  _CertPath;
			return this;
		}
		public Https SetPassWord(string _PassWord)
		{
			PassWord = _PassWord;
			return this;
		}
		/// <param name="_logger">日志文件不要设置为默认路径akron.log</param>
		public Https SetLog(Log _logger)
		{
			Logger = _logger;
			return this;
		}
		public Https SetPort(ushort _Port)
		{
			Port = _Port;
			return this;
		}
		public Https SetHomePage(string _HomePage)
		{
			HomePage = _HomePage;
			return this;
		}
		public Https SetRootDirectory(string _RootDirectory)
		{
			RootDirectory = _RootDirectory;
			return this;
		}
		public Https SetDefaultContentType(string _DefaultContentType)
		{
			DefaultContentType = _DefaultContentType;
			return this;
		}
		public Https SetCancellationTokenSource(CancellationTokenSource _cancellationTokenSource)
		{
			cancellationTokenSource = _cancellationTokenSource;
			return this;
		}
		public Https SetMimeTypes(Dictionary<string, string> _MimeTypes)
		{
			MimeTypes = _MimeTypes;
			return this;
		}
		public Https SetStatusCode(Dictionary<int, string> _StatusCode)
		{
			StatusCode = _StatusCode;
			return this;
		}
		#endregion
		//聊天室
		public static WWWs wWWs = new WWWs();
		public void Listen()
		{
			try
			{
				wWWs.Start();
				Httpslistener = new TcpListener(IPAddress.Any, Port);
				Certificate = new X509Certificate2(CertPath, PassWord);
				Httpslistener.Start();
				_ = Task.Run(() => AcceptHttpsRequest(cancellationTokenSource.Token));
			}
			catch (SocketException e)
			{
				Logger.log($"Message:{e.Message}|SocketErrorCode:{e.SocketErrorCode}", 2);
			}
		}
		async Task AcceptHttpsRequest(CancellationToken cancellationToken)
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				try
				{
					TcpClient client = await Httpslistener.AcceptTcpClientAsync();
					client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, 120000);
					
					_ = Task.Run(() => Filter(client));
				}
				catch (SocketException ex)
				{
					Logger.log($"{ex.ErrorCode} : {ex.SocketErrorCode} : {ex.Source} : {ex.Message} : {ex}", 2);
				}
			}
		}
		async void Filter(TcpClient client)
		{
			//List<string> Blacklist = new List<string>();
			//List<string> Whitelist = new List<string>();
			string IpAddress = ((IPEndPoint)client.Client.RemoteEndPoint!).Address.ToString() ?? "unknown";
			string Port = ((IPEndPoint)client.Client.RemoteEndPoint)?.Port.ToString() ?? "unknown";
			//if (Blacklist.Contains(IpAddress))
			//	return;
			//if (!Whitelist.Contains(IpAddress))
			//	return;
			if (IpAddress.Equals("unknown") || Port.Equals("unknown"))
				return;
			NetworkStream networkStream = client.GetStream();
			SslStream sslStream = new SslStream(networkStream, false);
			
				try
				{
					// 进行 SSL/TLS 握手，使用服务器证书
					sslStream.AuthenticateAsServer(Certificate, clientCertificateRequired: false, checkCertificateRevocation: true);
					
					await Task.Run(() => AcceptRequest(new TcpSockets(client,networkStream, sslStream,IpAddress,Port)));
					
				}
				catch (Exception ex)
				{
					Logger.log("处理客户端连接时出错: " + ex.Message,2);
					networkStream.Close();
					sslStream.Close();				
					client.Close();
			}			
						
		}
		public async void AcceptRequest(TcpSockets tcpSocket)
		{
			/*   Time:2024.10.16
			*/	 https://github.com/			
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
							while (tcpSocket.TCP_Client.Client.Poll(1000 * 20000, SelectMode.SelectRead)) goto https;
						break;
					case 0x2:
						if (tcpSocket[^1].Headers.TryGetValue("Sec-WebSocket-Key", out string? _Key) && !string.IsNullOrEmpty(_Key))
						{

							MethodInfo? methodInfo = FindRouteHandlers<WebSockets>("wss");
							if (methodInfo != null)
							{								
								Logger.log($"{tcpSocket.IpAddress}:{tcpSocket[^1].Method}:WebSockets");
								_= Task.Run(() => methodInfo.Invoke(null, new object?[] { tcpSocket, _Key }));
							}
						}
						return;
					case 0x3:
						tcpSocket[^1].StatusCode = StatusCode.TryGetValue(505, out string? value) ? value : "505 HTTP Version Not Supported";
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
		void CloseSocket(TcpSockets tcpSocket)
		{
			try
			{
				tcpSocket.NetworkStream.Close();
				tcpSocket.SSL_Stream.Close();
				//tcpSocket.TCP_Client.GetStream().Close();
				tcpSocket.TCP_Client.Close();
				
				foreach (var request in tcpSocket.Converse)
				{
					string sqlQuery = $"INSERT INTO HttpsLogs ( IPAdderss,Method , Url,LastdateTime,StatusCode) VALUES ({tcpSocket.IpAddress},{request.Method},{request.Url},{request.LastdateTime.ToString("G")},{request.StatusCode})";
					Program.engine.ParseSQL(sqlQuery);
				}
			}
			catch (SocketException ex)
			{
				Logger.log($"{ex.ErrorCode} : {ex.SocketErrorCode} : {ex.Source} : {ex.Message} : {ex}", 2);
			}
		}
		private int Parse(TcpSockets tcpSocket)
		{
			Span<byte> buffer = stackalloc byte[8192];
			int bytesRead = tcpSocket.SSL_Stream.Read(buffer);

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
		public void RouteProces(TcpSockets tcpSocket)
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
		private void HandlePostRequest(TcpSockets tcpSocket)
		{

			MethodInfo? methodInfo = FindRouteHandlers<HttpAPI>(tcpSocket[^1].Url);
			if (methodInfo != null)
			{
				SendResponse(methodInfo.Invoke(null, [tcpSocket]) as TcpSockets);
			}
			else
			{
				Logger.log($"{tcpSocket.IpAddress}POST Body: {tcpSocket[^1].Body}", 0);
				tcpSocket[^1].StatusCode = StatusCode.TryGetValue(404, out string? _value) ? _value : "404 Not Found";
				SendResponse(tcpSocket);
			}
		}
		private void HandleGetRequest(TcpSockets tcpSocket, string filePath)
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
		public bool SendResponse(TcpSockets tcpSocket, string w = "")
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

				Span<byte> headerBytes = Encoding.UTF8.GetBytes(responseString);
				if (tcpSocket.TCP_Client.Client.Poll(5000 * 1000, SelectMode.SelectWrite))
				{
					
					//logger.Log($"等待下条消息,当前时间{DateTime.Now}\r\n");
					tcpSocket.SSL_Stream.Write(headerBytes);
					tcpSocket.SSL_Stream.Write(tcpSocket[^1].Content);
					tcpSocket.SSL_Stream.Flush();

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
			Httpslistener.Stop();

			Logger.log("Server stopped gracefully.", 0);
		}
	}
}
