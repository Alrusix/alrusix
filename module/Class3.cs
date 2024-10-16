using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using akronLog;
using System.Reflection.Emit;
using System.Net.Security;
using akron.HTTPS;
using System.Collections.Concurrent;
namespace akronWS
{
	// 聊天室demo
	public class WWW
	{
		string name = "频道一";
		List<Socket> clientSockets { get; set; } = new List<Socket>();
		public void Start() => _ = Task.Run(() => ww());
		List<Socket> checkRead = new List<Socket>();
		List<Socket> checkWrite = new List<Socket>();
		List<Socket> checkError = new List<Socket>();
		readonly string wwe = $"Welcome   {"\r\n"}";

		public void ss(Socket socket)
		{
			clientSockets.Add(socket);
			socket.Send(EncodeMessage(wwe));
		}
		async void ww()
		{
			while (true)
			{
				if (clientSockets.Count == 0)
				{
					Thread.Sleep(1000);
				}
				else
				{
					try
					{
						checkRead.Clear();
						checkRead.AddRange(clientSockets);
						Socket.Select(checkRead, null, null, 1000 * 1000);  // 等待1秒
						List<string> bytes = new();
						foreach (Socket socket in checkRead)
						{
							byte[] buffer = new byte[1024 * 4];
							int received = socket.Receive(buffer);
							if (received == 0)
							{
								clientSockets.Remove(socket);
								socket.Close();
							}
							else
							{
								string ww = "";
								DecodeMessage(buffer, received, ref ww);
								ww += "\r\n";
								bytes.Add(ww);
							}
						}
						if (bytes.Count > 0)
							AcceptMessage(bytes);


						checkError.Clear();
						checkError.AddRange(clientSockets);
						if (checkError.Count != 0)
						{
							Socket.Select(null, null, checkError, 1000 * 1000);
							foreach (Socket socket in checkError)
							{
								clientSockets.Remove(socket);
								socket.Close();
							}
						}
					}
					catch
					{
						//TODO
					}
				}
			}
		}
		private int DecodeMessage(byte[] buffer, int length, ref string ww)
		{
			bool isFinalFrame = (buffer[0] & 0x80) != 0;
			byte opCode = (byte)(buffer[0] & 0x0F);
			switch (opCode)
			{
				case 0x0://分片
					break;
				case 0x1: // 文本帧
					break;
				case 0x2: // 二进制帧
					break;
				case 0x8: // 关闭帧
						  //logger.Log("Received close frame from server.");
					return 0x8;
				case 0x9: // Ping 帧
						  //logger.Log("Received ping frame.");
					return 0x9;
				case 0xA: // Pong 帧
						  //logger.Log("Received pong frame.");
					return 0xA;
				default:
					return -1;
			}

			bool isMasked = (buffer[1] & 0x80) != 0;
			int payloadLength = buffer[1] & 0x7F;
			int offset = 2;

			if (payloadLength == 126)
			{
				if (length < offset + 2)
				{

					return 0x8;
				}
				payloadLength = buffer[2] << 8 | buffer[3];
				offset += 2;

			}
			else if (payloadLength == 127)
			{
				if (length < offset + 8)
				{

					return 0x8;
				}
				//理论上可能会出现溢出，实际上不会，不管
				payloadLength = (int)(
					(ulong)buffer[2] << 56 |
					(ulong)buffer[3] << 48 |
					(ulong)buffer[4] << 40 |
					(ulong)buffer[5] << 32 |
					(ulong)buffer[6] << 24 |
					(ulong)buffer[7] << 16 |
					(ulong)buffer[8] << 8 |
					buffer[9]
				);
				offset += 8;
			}

			if (length < offset + (isMasked ? 4 : 0) + payloadLength)
			{

				return 0x8;
			}

			byte[] maskingKey = new byte[4];
			if (isMasked)
			{
				Array.Copy(buffer, offset, maskingKey, 0, 4);
				offset += 4;
			}
			byte[] payloadData = new byte[payloadLength];
			Array.Copy(buffer, offset, payloadData, 0, payloadLength);

			if (isMasked)
			{
				for (int i = 0; i < payloadLength; i++)
				{
					payloadData[i] ^= maskingKey[i % 4];
				}
			}
			ww = Encoding.UTF8.GetString(payloadData);

			if (!isFinalFrame && opCode == 0x0)
			{
				return 0xAA;
			}
			else if (!isFinalFrame && opCode != 0x0)
			{

				return 0xAA;
			}

			return opCode != 0x0 ? opCode : 1;
		}
		public void AcceptMessage(List<string> bytes)
		{

			checkWrite.Clear();
			checkWrite.AddRange(clientSockets);
			Socket.Select(null, checkWrite, null, 1000 * 1000);
			foreach (Socket socket in checkWrite)
			{
				foreach (var buffer in bytes)
				{
					socket.Send(EncodeMessage(buffer));
				}
			}
		}
		private byte[] EncodeMessage(string message, int opcode = 0x1)
		{
			byte[] messageBytes = Encoding.UTF8.GetBytes(message);
			byte[] frame;

			if (messageBytes.Length < 126)
			{
				frame = new byte[2 + messageBytes.Length];
				frame[1] = (byte)messageBytes.Length;
			}
			else if (messageBytes.Length <= ushort.MaxValue)
			{
				frame = new byte[4 + messageBytes.Length]; // 扩展到两字节长度
				frame[1] = 126; // 标记负载长度使用 2 字节
				frame[2] = (byte)(messageBytes.Length >> 8 & 0xFF); // 长度的高字节
				frame[3] = (byte)(messageBytes.Length & 0xFF); // 长度的低字节
			}
			else
			{
				//logger.Log("Buffer length does not match the payload length.4");
				return new byte[2];
			}
			// 设置帧头的 FIN 位和 Opcode
			frame[0] = (byte)(0x80 | (byte)opcode); // FIN = 1, Opcode 由传入的值决定

			Array.Copy(messageBytes, 0, frame, frame.Length - messageBytes.Length, messageBytes.Length);

			return frame;
		}
	}
	public class WWWs
	{
		private  Dictionary<Socket, SslStream> clients = new ();
		private readonly object lockObject = new object();
		string name = "频道二";
		List<Socket> clientSockets { get; set; } = new ();
		public void Start() => _ = Task.Run(() => ww());
		List<Socket> checkRead = new ();
		List<Socket> checkWrite = new ();
		List<Socket> checkError = new ();
		readonly string wwe = $"Welcome   {"\r\n"}";

		public void AddUser(TcpSockets tcpSockets)
		{
			lock (lockObject)
			{
				clients[tcpSockets.TCP_Client.Client] = tcpSockets.SSL_Stream;
				clientSockets.Add(tcpSockets.TCP_Client.Client);
			}
			
			tcpSockets.SSL_Stream.Write(EncodeMessage(wwe));
			tcpSockets.SSL_Stream.Flush();
		}
		async void ww()
		{
			while (true)
			{
				if (clientSockets.Count == 0)
				{
					Thread.Sleep(1000);
				}
				else
				{
					try
					{
						checkRead.Clear();
						checkRead.AddRange(clientSockets);
						Socket.Select(checkRead, null, null, 1000 * 1000);  // 等待1秒
						List<string> bytes = new();
						foreach (Socket socket in checkRead)
						{
							byte[] buffer = new byte[1024 * 4];
							int received = clients[socket].Read(buffer);
							if (received == 0)
							{
								clientSockets.Remove(socket);
								clients.Remove(socket);
								socket.Close();
								
							}
							else
							{
								string ww = "";
								DecodeMessage(buffer, received, ref ww);
								ww += "\r\n";
								bytes.Add(ww);
							}
						}
						if (bytes.Count > 0)
							AcceptMessage(bytes);


						checkError.Clear();
						checkError.AddRange(clientSockets);
						if (checkError.Count != 0)
						{
							Socket.Select(null, null, checkError, 1000 * 1000);
							foreach (Socket socket in checkError)
							{
								clientSockets.Remove(socket);
								socket.Close();
							}
						}
					}
					catch
					{
						//TODO
					}
				}
			}
		}
		private int DecodeMessage(byte[] buffer, int length, ref string ww)
		{
			bool isFinalFrame = (buffer[0] & 0x80) != 0;
			byte opCode = (byte)(buffer[0] & 0x0F);
			switch (opCode)
			{
				case 0x0://分片
					break;
				case 0x1: // 文本帧
					break;
				case 0x2: // 二进制帧
					break;
				case 0x8: // 关闭帧
						  //logger.Log("Received close frame from server.");
					return 0x8;
				case 0x9: // Ping 帧
						  //logger.Log("Received ping frame.");
					return 0x9;
				case 0xA: // Pong 帧
						  //logger.Log("Received pong frame.");
					return 0xA;
				default:
					return -1;
			}

			bool isMasked = (buffer[1] & 0x80) != 0;
			int payloadLength = buffer[1] & 0x7F;
			int offset = 2;

			if (payloadLength == 126)
			{
				if (length < offset + 2)
				{

					return 0x8;
				}
				payloadLength = buffer[2] << 8 | buffer[3];
				offset += 2;

			}
			else if (payloadLength == 127)
			{
				if (length < offset + 8)
				{

					return 0x8;
				}
				//理论上可能会出现溢出，实际上不会，不管
				payloadLength = (int)(
					(ulong)buffer[2] << 56 |
					(ulong)buffer[3] << 48 |
					(ulong)buffer[4] << 40 |
					(ulong)buffer[5] << 32 |
					(ulong)buffer[6] << 24 |
					(ulong)buffer[7] << 16 |
					(ulong)buffer[8] << 8 |
					buffer[9]
				);
				offset += 8;
			}

			if (length < offset + (isMasked ? 4 : 0) + payloadLength)
			{

				return 0x8;
			}

			byte[] maskingKey = new byte[4];
			if (isMasked)
			{
				Array.Copy(buffer, offset, maskingKey, 0, 4);
				offset += 4;
			}
			byte[] payloadData = new byte[payloadLength];
			Array.Copy(buffer, offset, payloadData, 0, payloadLength);

			if (isMasked)
			{
				for (int i = 0; i < payloadLength; i++)
				{
					payloadData[i] ^= maskingKey[i % 4];
				}
			}
			ww = Encoding.UTF8.GetString(payloadData);

			if (!isFinalFrame && opCode == 0x0)
			{
				return 0xAA;
			}
			else if (!isFinalFrame && opCode != 0x0)
			{

				return 0xAA;
			}

			return opCode != 0x0 ? opCode : 1;
		}
		public void AcceptMessage(List<string> bytes)
		{

			checkWrite.Clear();
			checkWrite.AddRange(clientSockets);
			Socket.Select(null, checkWrite, null, 1000 * 1000);
			foreach (Socket socket in checkWrite)
			{
				foreach (var buffer in bytes)
				{
					
					clients[socket].Write(EncodeMessage(buffer));
					clients[socket].Flush();
				}
			}
		}
		private byte[] EncodeMessage(string message, int opcode = 0x1)
		{
			byte[] messageBytes = Encoding.UTF8.GetBytes(message);
			byte[] frame;

			if (messageBytes.Length < 126)
			{
				frame = new byte[2 + messageBytes.Length];
				frame[1] = (byte)messageBytes.Length;
			}
			else if (messageBytes.Length <= ushort.MaxValue)
			{
				frame = new byte[4 + messageBytes.Length]; // 扩展到两字节长度
				frame[1] = 126; // 标记负载长度使用 2 字节
				frame[2] = (byte)(messageBytes.Length >> 8 & 0xFF); // 长度的高字节
				frame[3] = (byte)(messageBytes.Length & 0xFF); // 长度的低字节
			}
			else
			{
				//logger.Log("Buffer length does not match the payload length.4");
				return new byte[2];
			}
			// 设置帧头的 FIN 位和 Opcode
			frame[0] = (byte)(0x80 | (byte)opcode); // FIN = 1, Opcode 由传入的值决定

			Array.Copy(messageBytes, 0, frame, frame.Length - messageBytes.Length, messageBytes.Length);

			return frame;
		}
	}
}

