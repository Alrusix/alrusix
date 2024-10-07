using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.IO;
namespace akronConfig
{

	public static class Config
	{
		private static Dictionary<string, string> _config = new();
		private static readonly object _lock = new();
		private static bool _loaded = false;
		private static string _configFilePath = "config.ini";
		static Config()
		{
			if (!File.Exists(_configFilePath))
			{
				if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				{
					StreamWriter sw = new StreamWriter("config.ini");
					sw.Write("[Server]\r\nListen  = 8080\r\nWorker_Connections = 20\r\n\r\n[Http]\r\nRoot = C:\\WebSite\r\nType = application/octet-stream\r\nIndex = index.htm\r\n\r\n[MimeTypes]\r\n.html = text/html\r\n.htm = text/html\r\n.txt = text/plain\r\n.jpg = image/jpeg\r\n.jpeg = image/jpeg\r\n.png = image/png\r\n.gif = image/gif\r\n\r\n[Log]\r\nPath = Log.txt\r\nAccess_Log = ww");
					sw.Flush();
					sw.Close();
				}
				else
				{
					StreamWriter sw = new StreamWriter("config.ini");
					sw.Write("[Server]\r\nListen  = 8080\r\nWorker_Connections = 20\r\n\r\n[Http]\r\nRoot = /website\r\nType = application/octet-stream\r\nIndex = index.htm\r\n\r\n[MimeTypes]\r\n.html = text/html\r\n.htm = text/html\r\n.txt = text/plain\r\n.jpg = image/jpeg\r\n.jpeg = image/jpeg\r\n.png = image/png\r\n.gif = image/gif\r\n\r\n[Log]\r\nPath = Log.txt\r\nAccess_Log = ww");
					sw.Flush();
					sw.Close();
				}
			}
			Load();
		}
		public static void Load()
		{
			lock (_lock)
			{
				if (_loaded)
				{
					return;
				}
				string[] lines = File.ReadAllLines(_configFilePath);
				string section = "";
				foreach (string line in lines)
				{
					string trimmedLine = line.Trim();

					if (trimmedLine.StartsWith('[') && trimmedLine.EndsWith(']'))
					{
						section = trimmedLine.Substring(1, trimmedLine.Length - 2);
					}
					else if (!string.IsNullOrWhiteSpace(trimmedLine))
					{
						string[] parts = trimmedLine.Split('=');
						if (parts.Length == 2)
						{
							string key = parts[0].Trim();
							string value = parts[1].Trim();
							string fullKey = section + ":" + key;
							_config[fullKey] = value;
						}
					}
				}
				_loaded = true;
			}
		}
		/// <summary>
		/// 获取配置项值
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="key"></param>
		/// <param name="defaultValue">在配置中找不到 key 时，提供一个默认值。</param>
		/// <exception cref="KeyNotFoundException"></exception>
		/// <exception cref="InvalidCastException"></exception>
		public static T Get<T>(string key, T defaultValue = default)
		{
			string fullKey = key;
			if (!_config.ContainsKey(fullKey) && defaultValue.Equals(default))
			{
				throw new KeyNotFoundException($"Key '{key}' not found in config.");
			}
			string? value = _config.TryGetValue(fullKey, out string stringValue) ? stringValue : null;
			if (value == null)
			{
				return defaultValue;
			}
			// 转换配置值为指定类型
			try
			{
				return (T)Convert.ChangeType(value, typeof(T));
			}
			catch (Exception ex)
			{
				throw new InvalidCastException($"Failed to convert config value '{value}' to type '{typeof(T)}'.", ex);
			}
		}
		public static Dictionary<string, string> Parse(string section = "MimeTypes")
		{
			string filePath = _configFilePath;
			Dictionary<string, string> keyValuePairs = [];

			string? currentSection = null;
			foreach (string line in File.ReadLines(filePath))
			{
				string trimmedLine = line.Trim();
				if (trimmedLine.StartsWith('[') && trimmedLine.EndsWith(']'))
				{
					currentSection = trimmedLine.Substring(1, trimmedLine.Length - 2);
				}
				else if (currentSection == section && !string.IsNullOrWhiteSpace(trimmedLine))
				{
					string[] parts = trimmedLine.Split(['='], 2);
					if (parts.Length == 2)
					{
						string key = parts[0].Trim();
						string value = parts[1].Trim();
						keyValuePairs[key] = value;
					}
				}
			}
			return keyValuePairs;
		}
	}
}
