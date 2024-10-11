using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using static System.Net.Mime.MediaTypeNames;
using static akron.SocketServer;
using akronWS;
namespace akron.module
{
	public class WebSocket
	{	
		[Route("ws")]
		public static void HandleHandshake(Socket clientSocket, string wsKey)
		{
			string response = "HTTP/1.1 101 Switching Protocols\r\n" +
							  "Upgrade: websocket\r\n" +
							  "Connection: Upgrade\r\n" +
							  $"Sec-WebSocket-Accept: {Convert.ToBase64String(System.Security.Cryptography.SHA1.HashData(Encoding.UTF8.GetBytes(wsKey + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")))}\r\n\r\n";
			clientSocket.Send(Encoding.UTF8.GetBytes(response));
			HandleClient(clientSocket);
		}
		private static void HandleClient(Socket clientSocket)
		{
			List<byte> messageBuffer = [];
			int opcode = 0;
			Span<byte> buffer = new byte[16 * 1024];
			while (true)
			{
				try
				{
					int received = clientSocket.Receive(buffer);
					if (received == 0) break;
					int frameType = DecodeMessage(buffer.ToArray(), received, ref messageBuffer, ref opcode,clientSocket);
					switch (frameType)
					{
						case 0x1:
							string message = Encoding.UTF8.GetString(messageBuffer.ToArray());
							//logger.Log($"Received: {message}",0);
							messageBuffer.Clear();
							opcode = 0;

							string responseMessage = message+"\r\n";
							clientSocket.Send(EncodeMessage(responseMessage, 0x1));
							break;
						//binary // 暂时搁置
						case 0x2:
							messageBuffer.Clear();
							opcode = 0;
							break;
						case 0xAA:
							//等下一帧
							break;
						case 0x8: goto http;
						case 0x9:
							//ping
							clientSocket.Send(EncodeMessage(string.Empty, 0xA));
							break;
						case 0xA:
							//pong							
							break;
						case 0xFF:
							return;
						default: goto http;
					}
				}
				catch (SocketException ex)
				{
					////logger.Log($"ErrorCode:{ex.ErrorCode}|SocketErrorCode:{ex.SocketErrorCode}|Message:{ex.Message}|Source:{ex.Source}",2);
					break;
				}
			}
		http://baidu.com;
			////logger.Log("Client disconnected.");
			clientSocket.Close();
		}
		// 0               1               2               3
		// 1 2 3 4 5 6 7 8 1 2 3 4 5 6 7 8 1 2 3 4 5 6 7 8 1 2 3 4 5 6 7 8
		//+-+-+-+-+-------+-+-------------+-------------------------------+
		//|F|R|R|R| opcode|M| Payload len |    Extended payload length    |
		//|I|S|S|S|  (4)  |A|     (7)     |             (16/64)           |
		//|N|V|V|V|       |S|             |   (if payload len==126/127)   |
		//| |1|2|3|       |K|             |                               |
		//+-+-+-+-+-------+-+-------------+ - - - - - - - - - - - - - - - +
		//|     Extended payload length continued, if payload len == 127  |
		//+ - - - - - - - - - - - - - - - +-------------------------------+
		//|                               |Masking-key, if MASK set to 1  |
		//+-------------------------------+-------------------------------+
		//| Masking-key(continued)        |          Payload Data         |
		//+-------------------------------- - - - - - - - - - - - - - - - +
		//:                     Payload Data continued...                 :
		//+ - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - +
		//|                     Payload Data continued...                 |
		//+---------------------------------------------------------------+
		private static int DecodeMessage(byte[] buffer, int length, ref List<byte> message, ref int _opcode,Socket socket)
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
					////logger.Log("Received close frame from server.");
					return 0x8;
				case 0x9: // Ping 帧
					////logger.Log("Received ping frame.");
					return 0x9;
				case 0xA: // Pong 帧
					////logger.Log("Received pong frame.");
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
					//logger.Log("Buffer length does not match the payload length.1",0);
					return 0x8;
				}
				payloadLength = buffer[2] << 8 | buffer[3];
				offset += 2;

			}
			else if (payloadLength == 127)
			{
				if (length < offset + 8)
				{
					//logger.Log("Buffer length does not match the payload length.2",0);
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
				//logger.Log("Buffer length does not match the payload length.3",0);
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
			//暂时搁置，原路返回
			string Text = Encoding.UTF8.GetString(payloadData);
			var w = Text.Split("=");
			if (w.Length== 2 && w[0] == "name")
			{
				
				WWW.ss(socket);
				return 0xFF;
			}
			else
			{
				message.AddRange(payloadData);
			}
			//logger.Log(Text,0);

			if (!isFinalFrame && opCode == 0x0)
			{
				return 0xAA;
			}
			else if (!isFinalFrame && opCode != 0x0)
			{
				_opcode = opCode;
				return 0xAA;
			}

			return opCode != 0x0 ? opCode : _opcode;
		}
		private static byte[] EncodeMessage(string message, int opcode)
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
		public enum WebSocketOpcode
		{
			Continuation = 0x0,
			Text = 0x1,
			Binary = 0x2,
			Close = 0x8,
			Ping = 0x9,
			Pong = 0xA
		}
	}
}
