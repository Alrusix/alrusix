using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.IO;
namespace akronConfig
{

	public class Config
	{
		private Dictionary<string, string> _config = new();
		private readonly object _lock = new();
		private bool _loaded = false;
		private string ConfigFilePath { get; set; }
		public Config(string _configFilePath)
		{
			if (!File.Exists(_configFilePath))
			{
				File.WriteAllText(_configFilePath, "[Server]\\r\\nListen  = 8080\\r\\nWorker_Connections = 20\\r\\n\\r\\n[Http]\\r\\nRoot = /website\\r\\nType = application/octet-stream\\r\\nIndex = index.htm\\r\\n\\r\\n[MimeTypes]\\r\\n.html = text/html\\r\\n.htm = text/html\\r\\n.txt = text/plain\\r\\n.jpg = image/jpeg\\r\\n.jpeg = image/jpeg\\r\\n.png = image/png\\r\\n.gif = image/gif\\r\\n\\r\\n[Log]\\r\\nPath = Log.txt\\r\\nAccess_Log = ww");
				//StreamWriter sw = new StreamWriter(_configFilePath);
				//sw.Write("");
				//sw.Flush();
				//sw.Close();
				ConfigFilePath = _configFilePath.Trim();
			}
			else
			{
				ConfigFilePath = _configFilePath.Trim();
			}
			Load();
		}
		public  void Load()
		{
			lock (_lock)
			{
				if (_loaded)
				{
					return;
				}
				string[] lines = File.ReadAllLines(ConfigFilePath);
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
		/// <param name="defaultValue">在配置中找不到 key 时，提供一个默认值。</param>
		public  T Get<T>(string key, T defaultValue = default)
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
			try
			{
				return (T)Convert.ChangeType(value, typeof(T));
			}
			catch (KeyNotFoundException ex)
			{
				throw new KeyNotFoundException($"KeyNotFound", ex);
			}
			catch (InvalidCastException ex)
			{
				throw new InvalidCastException($"Failed to convert config value '{value}' to type '{typeof(T)}'.", ex);
			}
		}
		public Dictionary<string, string> GetMimeTypes(string section = "MimeTypes")
		{
			string filePath = ConfigFilePath;
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
		public Dictionary<int, string> GetStatusCode(string section= "StatusCode")
		{
			string filePath = ConfigFilePath;
			Dictionary<int, string> keyValuePairs = [];

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
						//俺寻思没必要捕获，次数多了就换回原来的Dictionary<string, string>
						try
						{ 
						int key = int.Parse(parts[0].Trim());				
						string value = parts[1].Trim();
						keyValuePairs[key] = value;
						}
						catch (FormatException ex) { throw new FormatException($"FormatException", ex); }
						catch (ArgumentNullException ex) { throw new ArgumentNullException($"ArgumentNull", ex); }
						catch (OverflowException ex) { throw new OverflowException($"Overflow", ex); }
					}
				}
			}
			return keyValuePairs;
		}
	}
}
