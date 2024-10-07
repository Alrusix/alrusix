using System;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using akronLog;
using akronConfig;
using System.IO.MemoryMappedFiles;
using System.Text.Json;
using akron;
using akron.module;
using System.Collections.Generic;
namespace akronDB
{
	[Serializable]
	public class Column(string columnName, DateType dataType, string constraints)
	{
		public string ColumnName { get; set; } = columnName;
		public DateType DataType { get; set; } = dataType;
		public string Constraints { get; set; } = constraints;
	}
	[Serializable]
	public class Tables
	{
		public string Name { get; set; } = "";
		public List<Column> Fields { get; set; } = [];
		public List<Dictionary<string, object>> Row { get; set; } = [];
	}
	public enum DateType : byte
	{
		Int32 = 0x1,
		Int64 = 0x2,
		Double = 0x3,
		Decimal = 0x4,
		String = 0x5,
		Array = 0x6,
		Boolean = 0x7,
		DateTime = 0x8
	}
	
	public static class DBEngine
	{
		
		public static readonly string dbPath = new(Config.Get<string>("DB:Path", "akron.adb"));
		public static List<Tables> tables = Init();
		public static ConcurrentQueue<string[]> tableData = new();
		readonly static int[] minWidths = [5, 5, 5, 10, 10];
		readonly static int[] maxWidths = [20, 40, 20, 20, 20];
		public static int CurrentPage { get; set; } = 0;
		public static int ItemsPerPage { get; set; } = Config.Get<int>("UI:Page", 6);
		public static int TotalPages { get; set; } = 0;
		private static readonly char[] separator = [','];
		public static DateType Type_to_DateType(string type)
		{
			if (string.IsNullOrEmpty(type)) throw new NotImplementedException();
			return type switch
			{
				"System.String" => DateType.String,
				"System.Int32" => DateType.Int32,
				"System.DateTime" => DateType.DateTime,
				"System.Int64" => DateType.Int64,
				"System.Double" => DateType.Double,
				"System.Decimal" => DateType.Decimal,
				"System.Boolean" => DateType.Boolean,
				"System.Array" => DateType.Array,
				_ => DateType.String,
			};
		}
		public static Type DateType_to_Type(DateType type) => type switch
		{
			DateType.String => typeof(String),
			DateType.DateTime => typeof(DateTime),
			DateType.Int32 => typeof(Int32),
			DateType.Int64 => typeof(Int64),
			DateType.Double => typeof(Double),
			DateType.Decimal => typeof(Decimal),
			DateType.Array => typeof(Array),
			DateType.Boolean => typeof(Boolean),
			_ => throw new NotImplementedException()
		};

		public static List<Tables> Init()
		{
			long fileSize = new FileInfo(dbPath).Length;
			if (fileSize == 0) return [];
			using (MemoryMappedFile mmf = MemoryMappedFile.CreateFromFile(dbPath, FileMode.OpenOrCreate))
			{
				using (MemoryMappedViewAccessor accessor = mmf.CreateViewAccessor(0, fileSize, MemoryMappedFileAccess.Read))
				{
					byte[] buffer = new byte[accessor.Capacity];
					accessor.ReadArray(0, buffer, 0, buffer.Length);
					string content = System.Text.Encoding.UTF8.GetString(buffer);
					return Deserialize(content) ?? new List<Tables>();
				}
			}
		}
		public static void Save()
		{
			byte[] buffer = System.Text.Encoding.UTF8.GetBytes(Serialize());
			using (MemoryMappedFile mmf = MemoryMappedFile.CreateFromFile(dbPath,FileMode.Open ,null,buffer.Length))
			{			
				using (MemoryMappedViewAccessor accessor = mmf.CreateViewAccessor(0, buffer.LongLength, MemoryMappedFileAccess.Write))
				{			
					accessor.WriteArray(0, buffer, 0, buffer.Length);
				}
			}
		}

		public static string Serialize()=> JsonSerializer.Serialize(DBEngine.tables,new JsonSerializerOptions { WriteIndented =true});	
		public static List<Tables>? Deserialize(string json)
		{
			var deserializedTables = JsonSerializer.Deserialize<List<Tables>>(json);
			if (deserializedTables != null)
				return deserializedTables;
			return null;
		}

		public static List<Column>? CheckFields(string tableName)
		{
			if (string.IsNullOrEmpty(tableName)) return null;
			Tables? table = tables.SingleOrDefault(t => t.Name == tableName);
			if(table!=null)
			return table.Fields;
			return null;
		}
		public static Tables? GetTable(string tableName) => tables.SingleOrDefault(t => t.Name == tableName);
		public static bool Check(string tableName) => tables.Any(t => t.Name == tableName);
		static byte InsertIntoDatabase(string sqlQuery)
		{
			string pattern = @"INSERT INTO\s+([a-zA-Z_][a-zA-Z0-9_]*)\s*\(([^)]+)\)\s*VALUES\s*\(([^)]+)\)";
			Match match = Regex.Match(sqlQuery, pattern, RegexOptions.IgnoreCase);
			if (match.Success)
			{
				string tableName = match.Groups[1].Value;
				Tables? table = tables.SingleOrDefault(t => t.Name == tableName);
				if (table == null)
					return 0x1;
				string[] columns = match.Groups[2].Value.Split(separator, StringSplitOptions.RemoveEmptyEntries);
				for (int i = 0; i < columns.Length; i++)
					columns[i] = columns[i].Trim();
				string[] values = match.Groups[3].Value.Split(separator, StringSplitOptions.RemoveEmptyEntries);
				for (int i = 0; i < values.Length; i++)
					values[i] = values[i].Trim().Trim('\'');
				if (columns.Length != values.Length)		
					return 0x2;
				Dictionary<string, object> newRow = [];
				foreach (var column in table.Fields)
				{
					newRow[column.ColumnName] = GetDefaultValue(DateType_to_Type(column.DataType));
				}
				for (int i = 0; i < columns.Length; i++)
				{
					string normalizedColumnName = columns[i].Trim().ToLower();
					var column = table.Fields.SingleOrDefault(c => c.ColumnName.Trim().ToLower() == normalizedColumnName);				
					if (column == null)
						return 0x3;
					try
					{
						object typedValue = Convert.ChangeType(values[i], DateType_to_Type(column.DataType));
						newRow[columns[i]] = typedValue;
					}
					catch (Exception ex)
					{
						//SocketServer.logger.Log($"列<{columns[i]}>的值<{values[i]}>类型转换失败: {ex.Message}");
						return 0x4;
					}
				}
				// 如果表中有 ID 字段（例如自增 ID），需要自动为其生成值
				var idColumn = table.Fields.SingleOrDefault(f => f.ColumnName == "id");
				if (idColumn != null && newRow.ContainsKey("id"))
				{
					// 假设 ID 是自增值，根据表中已有行数生成新 ID（可以自定义 ID 生成逻辑）
					int newId = table.Row.Count > 0 ? table.Row.Max(r => Convert.ToInt32(r["id"])) + 1 : 1;
					newRow["id"] = newId;
				}
				// 将新行插入到表的行列表中
				table.Row.Add(newRow);
				//SocketServer.logger.Log($"<{tableName}>插入一条新记录");
				Save();
				return 0x0;
			}
			//SocketServer.logger.Log($"SQL 语句格式错误或未匹配到 INSERT INTO 语句");
			return 0x5;
		}
		// 辅助方法：根据列的数据类型返回默认值
		static object? GetDefaultValue(Type type)
		{
			if (type.IsValueType)
			{
				return Activator.CreateInstance(type);
			}
			return null; // 引用类型默认为 null
		}
		/// <summary>
		/// 
		/// </summary>
		/// <param name="sql">要求列名以字母或下划线开头，并由字母、数字或下划线组成。</param>
		/// <returns></returns>
		static byte ParseCreateTableStatement(string sql)
		{
			try
			{	
				sql = sql.Replace("\n", "").Replace("\r", "");
				string tableNamePattern = @"CREATE\s+TABLE\s+([a-zA-Z_][a-zA-Z0-9_]*)\s*\((.*?)\);";
				Match tableNameMatch = Regex.Match(sql, tableNamePattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
				if (!tableNameMatch.Success)
					return 0x1;
				string tableName = tableNameMatch.Groups[1].Value;
				if (Check(tableName))
					return 0x2;
				Tables tabless = new Tables();
				tabless.Name = tableName;
				string columnsPart = tableNameMatch.Groups[2].Value;
				//解析及调整说明
				//([a - zA - Z_][a - zA - Z0 - 9_] *)：
				//匹配列名，要求列名以字母或下划线开头，并由字母、数字或下划线组成。
				//([\w\.] +\([0 - 9,] +\)| [\w\.] +)：
				//支持匹配可能包含.的数据类型，比如 DECIMAL(10, 2)。
				//[\w\.] +：
				//匹配数据类型名（如 INT、DECIMAL）。
				//\([0 - 9,] +\)：
				//匹配括号内参数（如 VARCHAR(255) 或 DECIMAL(10, 2)）。
				//(.*?)：
				//匹配约束部分（非贪婪匹配），如 PRIMARY KEY、NOT NULL。
				//(?:,|$)：
				//匹配逗号 , 或行尾 $，表示列定义的结束。
				string columnPattern = @"\s*([a-zA-Z_][a-zA-Z0-9_]*)\s+([\w\.]+\([0-9,]+\)|[\w\.]+)\s*(.*?)(?:,|$)";
				MatchCollection columnMatches = Regex.Matches(columnsPart, columnPattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
				foreach (Match columnMatch in columnMatches)
				{
					tabless.Fields.Add(new Column(columnMatch.Groups[1].Value, Type_to_DateType(columnMatch.Groups[2].Value), columnMatch.Groups[3].Value.Trim()));
				}
				tables.Add(tabless);
				Save();
				return 0x3;
			}
			catch (Exception ex)
			{
				SocketServer.logger.Log(""+ex.ToString(),2);
				return 0x4;
			}			
		}
		//TODO 
		/// <param name="sql">
		/// <c>SQL 语句的末端必须使用分号 </c>
		/// <code>SQL 语句忽略大小写</code>
		/// </param>
		/// <returns></returns>
		public static byte ParseSQL(string sql)
		{
			string[] strings = sql.Split(';');
			foreach (string s in strings)
			{
				switch(s.Split(" ")[0].ToUpper())
				{
					case "CREATE":
						return ParseCreateTableStatement(sql);
					case "INSERT":
						return  InsertIntoDatabase(sql);					
					case "UPDATE":
						break;
					case "DELETE":
						break;
					case "CREAT":
						break;
					case "ALTER":
						break;
					case "DROP":
						break;
					default:
						break;
				}			
			}
			return 0x0;
		}
		//TODO
		private static bool IsConstraintKeyword(string word)
		{
			string[] constraintKeywords = new[] { "PRIMARY", "NOT", "UNIQUE", "CHECK", "FOREIGN", "DEFAULT" };
			return Array.Exists(constraintKeywords, keyword => keyword.Equals(word, StringComparison.OrdinalIgnoreCase));
		}
		public static void ShowTable(string tableName)
		{
			Tables? table = tables.SingleOrDefault(t => t.Name == tableName);
			if (table == null) {return; }
			var tableData = table.Row;
			string[] head = table.Fields.Select(c => c.ColumnName).ToArray();
			if (tableData.Count > 0)
			{				
				Program.Options.Add(new UP());
				Program.Options.Add(new Down());
						
				TotalPages = (int)Math.Ceiling((double)tableData.Count / ItemsPerPage);
				Console.ForegroundColor = ConsoleColor.DarkYellow;
				Console.WriteLine($"  Catalog  {CurrentPage + 1}/{TotalPages}");
				Console.WriteLine(@"");
				Console.ForegroundColor = ConsoleColor.White;

				var pageData = GetPageData(CurrentPage, tableData, head);

				int[] columnWidths = GetMaxColumnWidths(pageData, minWidths, maxWidths, head);
				PrintLine(columnWidths);
				PrintRow(columnWidths, head);
				PrintLine(columnWidths);
				if (pageData != null)
					foreach (var row in pageData)
						PrintRow(columnWidths, row);
				PrintLine(columnWidths);
			}
		}
		// 获取指定页的数据，并将每行的数据转换为 string 数组格式
		static List<string[]> GetPageData(int pageIndex, List<Dictionary<string, object>> tableData, string[] head)
		{
			var pageData = new List<string[]>();
			int startIndex = pageIndex * ItemsPerPage;
			int endIndex = Math.Min(startIndex + ItemsPerPage, tableData.Count);

			for (int i = startIndex; i < endIndex; i++)
			{
				// 将 Dictionary<string, object> 转换为 string[] 数组
				var rowArray = head.Select(columnName => tableData[i].ContainsKey(columnName) ? tableData[i][columnName]?.ToString() : "").ToArray();
				pageData.Add(rowArray);
			}
			return pageData;
		}

		// 计算每列的最大宽度
		static int[] GetMaxColumnWidths(List<string[]> table, int[] minWidths, int[] maxWidths, string[] head)
		{
			if (table == null || table.Count == 0)
				return minWidths;
			// 根据表头的列数创建一个初始的列宽数组
			int[] maxWidthsResult = new int[head.Length];
			foreach (var row in table)
			{
				for (int i = 0; i < row.Length; i++)
					maxWidthsResult[i] = Math.Max(maxWidthsResult[i], row[i]?.Length ?? 0);
			}
			// 将表头的长度也计算在内
			for (int i = 0; i < head.Length; i++)
			{
				maxWidthsResult[i] = Math.Max(maxWidthsResult[i], head[i].Length);
			}
			// 应用最小和最大宽度限制
			for (int i = 0; i < head.Length; i++)
			{
				maxWidthsResult[i] = Math.Max(maxWidthsResult[i], FillArrayUsingLinq(minWidths, maxWidthsResult.Length)[i]);
				maxWidthsResult[i] = Math.Min(maxWidthsResult[i], FillArrayUsingLinq(maxWidths, maxWidthsResult.Length)[i]);
			}
			return maxWidthsResult;
		}
		/// <summary>
		/// 数组自动补全
		/// </summary>
		/// <param name="originalArray">原数组</param>
		/// <param name="desiredLength">目标长度</param>
		/// <param name="fillValue">补充值</param>
		/// <returns></returns>
		static int[] FillArrayUsingLinq(int[] originalArray, int desiredLength, int fillValue = 30)
		{
			if (desiredLength <= originalArray.Length)
				return originalArray;
			return originalArray.Concat(Enumerable.Repeat(fillValue, desiredLength - originalArray.Length)).ToArray();
		}
		static void PrintLine(int[] columnWidths)
		{
			Console.Write("  +");
			foreach (var width in columnWidths)
				Console.Write(new string('-', width + 2) + "+");
			Console.WriteLine();
		}
		static void PrintRow(int[] columnWidths, string[] row)
		{
			Console.Write("  |");
			for (int i = 0; i < row.Length; i++)
			{
				if (row[i].Length > columnWidths[i])
					Console.Write($" {string.Concat(row[i].AsSpan(0, columnWidths[i] - 2), "…")} |");
				else
					Console.Write($" {row[i].PadRight(columnWidths[i])} |");
			}				
			Console.WriteLine();
		}
	}
}

