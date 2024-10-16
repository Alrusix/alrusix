using System;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using akronLog;
using akronConfig;
using System.IO.MemoryMappedFiles;
using System.Text.Json;
using akron;
using akron.HTTPS;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics.Metrics;
using System.Linq;
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
		public List<Dictionary<string, object?>> Row { get; set; } = [];
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

	public class DBEngine
	{

		Config config { get; set; } 
		Log logger { get; set; } 
		string DBPath { get; set; }
		List<Tables> TableList { get; set; }
		//readonly  int[] minWidths = [5, 5, 5, 10, 10];
		//readonly  int[] maxWidths = [20, 40, 20, 20, 20];
		public static int MinWidth { get; set; } = 5;
		public static int MaxWidth { get; set; } = 20;
		public static int CurrentPage { get; set; } = 0;
		public static int ItemsPerPage { get; set; }
		public static int TotalPages { get; set; } = 0;
		readonly char[] separator = [','];
		public DBEngine()
		{
			config = new Config("config.ini");
			logger = new Log("db.log");	
			ItemsPerPage = config.Get<int>("UI:Page", 6);
		}
		public DBEngine SetDBPath(string dbPath)
		{
			DBPath = dbPath;
			return this;
		}

		DateType Type_to_DateType(string type)
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
		Type DateType_to_Type(DateType type) => type switch
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
		public void Init()
		{
			string dbPath = config.Get<string>("DB:Path", "akron.adb");
			if (!File.Exists(dbPath))
				File.WriteAllText(dbPath, "[]");//不要动这行，浪费一天时间在这了

			long fileSize = new FileInfo(DBPath).Length;
			//if (fileSize == 0)
			//{
			//	TableList = new List<Tables>();
			//	return;
			//}
				
			using (MemoryMappedFile mmf = MemoryMappedFile.CreateFromFile(DBPath, FileMode.OpenOrCreate))
			{
				using (MemoryMappedViewAccessor accessor = mmf.CreateViewAccessor(0, fileSize, MemoryMappedFileAccess.Read))
				{
					byte[] buffer = new byte[accessor.Capacity];
					accessor.ReadArray(0, buffer, 0, buffer.Length);
					string content = System.Text.Encoding.UTF8.GetString(buffer);

					TableList = Deserialize(content) ?? new List<Tables>();
				}
			}
		}
		void Save()
		{
			byte[] buffer = System.Text.Encoding.UTF8.GetBytes(Serialize());
			using (MemoryMappedFile mmf = MemoryMappedFile.CreateFromFile(DBPath, FileMode.Open, null, buffer.Length))
			{
				using (MemoryMappedViewAccessor accessor = mmf.CreateViewAccessor(0, buffer.LongLength, MemoryMappedFileAccess.Write))
				{
					accessor.WriteArray(0, buffer, 0, buffer.Length);
				}
			}
		}
		public string Serialize() => JsonSerializer.Serialize(TableList, new JsonSerializerOptions { WriteIndented = true });
		static List<Tables>? Deserialize(string json)
		{
			var deserializedTables = JsonSerializer.Deserialize<List<Tables>>(json);
			if (deserializedTables != null)
				return deserializedTables;
			return null;
		}

		List<Column>? CheckFields(string tableName)
		{
			if (string.IsNullOrEmpty(tableName)) return null;
			Tables? table = TableList.SingleOrDefault(t => t.Name == tableName);
			if (table != null)
				return table.Fields;
			return null;
		}
		public Tables? GetTable(string tableName) => TableList.SingleOrDefault(t => t.Name == tableName);
		public bool Check(string tableName)
		{
			if (TableList != null)
				return TableList.Any(t => t.Name == tableName);
			return false;
		}
		byte InsertIntoDatabase(string sqlQuery)
		{
			string pattern = @"INSERT INTO\s+([a-zA-Z_][a-zA-Z0-9_]*)\s*\(([^)]+)\)\s*VALUES\s*\(([^)]+)\)";
			Match match = Regex.Match(sqlQuery, pattern, RegexOptions.IgnoreCase);
			if (match.Success)
			{
				string tableName = match.Groups[1].Value;
				Tables? table = TableList.SingleOrDefault(t => t.Name == tableName);
				if (table == null)
				{
					return 0x1;
				}

				string[] columns = match.Groups[2].Value.Split(separator, StringSplitOptions.RemoveEmptyEntries);
				for (int i = 0; i < columns.Length; i++)
					columns[i] = columns[i].Trim();
				string[] values = match.Groups[3].Value.Split(separator, StringSplitOptions.RemoveEmptyEntries);
				for (int i = 0; i < values.Length; i++)
					values[i] = values[i].Trim().Trim('\'');
				if (columns.Length != values.Length)
				{
					return 0x2;
				}

				Dictionary<string, object?> newRow = [];
				foreach (var column in table.Fields)
				{
					newRow[column.ColumnName] = GetDefaultValue(DateType_to_Type(column.DataType));
				}
				for (int i = 0; i < columns.Length; i++)
				{
					string normalizedColumnName = columns[i].Trim().ToLower();
					var column = table.Fields.SingleOrDefault(c => c.ColumnName.Trim().ToLower() == normalizedColumnName);
					if (column == null)
					{
						return 0x3;
					}
					try
					{
						object typedValue = Convert.ChangeType(values[i], DateType_to_Type(column.DataType));
						newRow[columns[i]] = typedValue;
					}
					catch (Exception ex)
					{
						logger.log($"列<{columns[i]}>的值<{values[i]}>类型转换失败: {ex.Message}", 2, false);
						return 0x4;
					}
				}
				// 如果表中有 ID 字段（例如自增 ID），需要自动为其生成值
				var idColumn = table.Fields.SingleOrDefault(f => f.ColumnName == "id" && f.Constraints == "AUTO_INCREMENT");
				if (idColumn != null && newRow.ContainsKey("id"))
				{
					// 假设 ID 是自增值，根据表中已有行数生成新 ID（可以自定义 ID 生成逻辑）
					int newId = table.Row.Count > 0 ? table.Row.Max(r => Convert.ToInt32(r["id"])) + 1 : 1;
					newRow["id"] = newId;
				}
				table.Row.Add(newRow);
				Save();
				return 0x0;
			}
			logger.log($"SQL 语句格式错误或未匹配到 INSERT INTO 语句", 2, false);
			return 0xF;
		}
		// 辅助方法：根据列的数据类型返回默认值
		object? GetDefaultValue(Type type)
		{
			if (type.IsValueType)
			{
				return Activator.CreateInstance(type);
			}
			return null; // 引用类型默认为 null
		}

		/// <param name="sql">要求列名以字母或下划线开头，并由字母、数字或下划线组成。</param>
		/// <returns></returns>
		byte ParseCreateTableStatement(string sql)
		{
			try
			{
				sql = sql.Replace("\n", "").Replace("\r", "");
				string tableNamePattern = @"CREATE\s+TABLE\s+([a-zA-Z_][a-zA-Z0-9_]*)\s*\((.*?)\);";
				Match tableNameMatch = Regex.Match(sql, tableNamePattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
				if (!tableNameMatch.Success)
					return 0x1;
				string tableName = tableNameMatch.Groups[1].Value;
				//Console.WriteLine(tableName);
				if (TableList != null)
				{
					if (Check(tableName))
						return 0x2;
				}
				else
				{
					Init();
					if (Check(tableName))
						return 0x2;
				}
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
				TableList.Add(tabless);
				Save();
				return 0x3;
			}
			catch (Exception ex)
			{
				//Console.WriteLine(ex.ToString());
				logger.log($"{ex.StackTrace}|{ex.Message}|{ex.Source}", 2, true);
				return 0x4;
			}
		}
		//TODO  不要问元组有什么用，自有他的道理
		///	<summary>
		/// <c>SQL 语句的末端必须使用分号 </c>
		/// <code>SQL 语句忽略大小写</code>
		/// </summary>
		public (byte, string) ParseSQL(string sql)
		{
			string[] w = Regex.Split(sql, @"(?<=;)");
			foreach(string s in w)
			{
				switch (s.Trim().Split(' ')[0].ToUpper())
				{
					case "CREATE":					
						ParseCreateTableStatement(s); break;
					case "INSERT":
						InsertIntoDatabase(s); break;
					case "UPDATE":
						ParseUPDATE(s); break;
					case "DELETE":
						ParseDELETE(s); break;
					case "ALTER":
						ParseALTER(s); break;
					case "DROP":
						ParseDROP(s); break;
					case "SELECT":
						ParseSELECT(s); break;
					default:
						return (0xFF, "");
				}
			}
			return (0xFF, "");
		}
		/// <summary>
		/// 语法：<code>UPDATE table_name SET column1 = value1, column2 = value2, ... WHERE condition;</code>
		/// 示例：<code>UPDATE SocketLogs SET Url=/admin,Method=POST WHERE IPAdderss=127.0.0.1;</code>
		/// 解析步骤：
		/// 匹配表名。
		/// 匹配列和值的设置部分（SET 语句）。
		/// 解析 WHERE 条件，并筛选符合条件的行。
		/// 更新符合条件的行中的指定字段。
		/// </summary>
		/// <param name="sql"></param>
		/// <returns></returns>
		(byte, string) ParseUPDATE(string sql)
		{
			try
			{
				string pattern = @"UPDATE\s+([a-zA-Z_][a-zA-Z0-9_]*)\s+SET\s+(.*?)\s+WHERE\s+(.*);?";
				Match match = Regex.Match(sql, pattern, RegexOptions.IgnoreCase);
				if (!match.Success)
				{
					return ((byte)SqlCode.FormatError, "");
				}
				string tableName = match.Groups[1].Value;
				string setPart = match.Groups[2].Value;
				string wherePart = match.Groups[3].Value;
				//Console.WriteLine($"{tableName}|{setPart}|{wherePart}|{match.Groups[3]}");
				Tables? table = GetTable(tableName);
				if (table == null)
				{
					return ((byte)SqlCode.TableDoesNotExist, "");
				}
				Dictionary<string, object?> setValues = new();
				string[] setAssignments = setPart.Split(separator, StringSplitOptions.RemoveEmptyEntries);
				foreach (string assignment in setAssignments)
				{
					string[] pair = assignment.Split('=');
					if (pair.Length == 2)
					{
						string column = pair[0].Trim();
						object? value = pair[1].Trim('\'');
						setValues[column] = value;
					}
				}
				foreach (var row in table.Row)
				{
					if (row.ContainsKey(wherePart.Split('=')[0].Trim()) && row[wherePart.Split('=')[0].Trim()].ToString() == wherePart.Split('=')[1].Trim('\''))
					{
						foreach (var set in setValues)
						{
							if (row.ContainsKey(set.Key)) row[set.Key] = set.Value;
						}
					}
				}
				Save();

				return ((byte)SqlCode.OK, "");
			}
			catch (Exception ex)
			{
				logger.log($"StackTrace:{ex.StackTrace}|Message:{ex.Message}|Source:{ex.Source}", 2,true);
				return ((byte)SqlCode.Exception, "");
			}
		}
		/// <summary>
		/// 语法：<code>DELETE FROM table_name WHERE condition;</code>
		/// 解析步骤：<code>
		/// 匹配表名。
		/// 解析 WHERE 条件，并筛选符合条件的行。
		/// 删除符合条件的行。</code>
		/// </summary>
		/// <param name="sql"></param>
		/// <returns></returns>
		byte ParseDELETE(string sql)
		{
			try
			{
				string pattern = @"DELETE\s+FROM\s+([a-zA-Z_][a-zA-Z0-9_]*)\s+WHERE\s+(.*);?";
				Match match = Regex.Match(sql, pattern, RegexOptions.IgnoreCase);
				if (!match.Success)
				{
					return (byte)SqlCode.FormatError;
				}

				string tableName = match.Groups[1].Value;
				string wherePart = match.Groups[2].Value;

				Tables? table = GetTable(tableName);
				if (table == null) return (byte)SqlCode.TableDoesNotExist;

				table.Row = table.Row.Where(row => !(row.ContainsKey(wherePart.Split('=')[0].Trim()) && row[wherePart.Split('=')[0].Trim()].ToString() == wherePart.Split('=')[1].Trim('\''))).ToList();

				Save();
				return (byte)SqlCode.OK;
			}
			catch (Exception ex)
			{
				logger.log($"StackTrace:{ex.StackTrace}|Message:{ex.Message}|Source:{ex.Source}", 2);
				return (byte)SqlCode.Exception;
			}
		}
		/// <summary>
		/// 语法：
		/// <code>ALTER TABLE table_name ADD column_name column_type;（增加列）
		/// ALTER TABLE table_name DROP COLUMN column_name;（删除列）
		/// ALTER TABLE table_name MODIFY COLUMN column_name column_type;（修改列类型）暂时无法使用</code>
		/// 示例：
		/// <code>ALTER TABLE SocketLogs ADD ID System.Int32;
		/// ALTER TABLE SocketLogs DROP COLUMN ID;
		/// ALTER TABLE SocketLogs MODIFY COLUMN ID System.Int64;</code>
		/// 解析步骤：
		/// 匹配表名和操作类型（ADD、DROP、MODIFY）。
		/// 根据操作类型对表结构进行增、删或改。
		/// </summary>
		/// <param name="sql"></param>
		/// <returns></returns>
		byte ParseALTER(string sql)
		{
			try
			{
				string pattern = @"ALTER\s+TABLE\s+([a-zA-Z_][a-zA-Z0-9_]*)\s+(ADD|DROP COLUMN|MODIFY COLUMN)\s+(.*);?";
				Match match = Regex.Match(sql, pattern, RegexOptions.IgnoreCase);
				if (!match.Success)
				{
					return (byte)SqlCode.FormatError;
				}
				string tableName = match.Groups[1].Value;
				string operation = match.Groups[2].Value.ToUpper();
				string columnDefinition = match.Groups[3].Value;

				Tables? table = GetTable(tableName);
				if (table == null) return (byte)SqlCode.TableDoesNotExist;

				switch (operation)
				{
					case "ADD":
						string[] addParts = columnDefinition.Split(' ');
						if (addParts.Length >= 2)
						{
							string columnName = addParts[0].Trim();
							DateType columnType = Type_to_DateType(addParts[1].Trim());
							table.Fields.Add(new Column(columnName, columnType, ""));

							Save();
						}
						else
						{
							return 0x3;
						}
						break;
					case "DROP COLUMN":
						string columnToDrop = columnDefinition.Trim();
						foreach (var field in table.Fields.Where(f => f.ColumnName == columnToDrop))
						{
							field.Constraints = "Delete";
						}
						//table.Fields.RemoveAll(f => f.ColumnName == columnToDrop);
						//table.Row.RemoveAll(f => f.ContainsKey(columnToDrop));					
						Save();
						break;
					case "MODIFY COLUMN":
						string[] modifyParts = columnDefinition.Split(' ');
						if (modifyParts.Length >= 2)
						{
							string columnName = modifyParts[0].Trim();
							DateType newColumnType = Type_to_DateType(modifyParts[1].Trim());
							Column? columnToModify = table.Fields.SingleOrDefault(f => f.ColumnName == columnName);
							if (columnToModify != null)
							{
								columnToModify.DataType = newColumnType;
								Save();
							}
							else
							{
								return 0x4;
							}
						}
						else
						{
							return 0x5;
						}
						break;
					default:
						return 0xFF;
				}

				return 0x0;
			}
			catch (Exception ex)
			{
				logger.log($"StackTrace:{ex.StackTrace}|Message:{ex.Message}|Source:{ex.Source}", 2);
				return 0xA;
			}
		}
		/// <summary>
		/// 语法：<code>DROP TABLE table_name;</code>
		/// 解析步骤：
		/// 匹配表名。
		/// 删除指定的表对象。
		/// </summary>
		/// <param name="sql"></param>
		/// <returns></returns>
		byte ParseDROP(string sql)
		{
			try
			{
				string pattern = @"DROP\s+TABLE\s+([a-zA-Z_][a-zA-Z0-9_]*);?";
				Match match = Regex.Match(sql, pattern, RegexOptions.IgnoreCase);
				if (!match.Success)
				{
					//logger.Log("SQL语法错误: 无法匹配到DROP语句", 2);
					return (byte)SqlCode.FormatError;
				}
				string tableName = match.Groups[1].Value;
				Tables? table = GetTable(tableName);
				if (table == null) return 0x2;
				TableList.RemoveAll(t => t.Name == tableName);
				Save();
				return (byte)SqlCode.OK; ;
			}
			catch (Exception ex)
			{
				logger.log($"StackTrace:{ex.StackTrace}|Message:{ex.Message}|Source:{ex.Source}", 2);
				return 0xA;
			}
		}
		/// <summary>
		/// 语法：<code>
		/// SELECT column1, column2, ... FROM table_name;
		///	SELECT* FROM table_name;</code>
		/// 示例：<code>SELECT * FROM SocketLogs;
		/// SELECT IPAdderss,Url FROM SocketLogs;</code>
		/// 解析步骤：
		/// 匹配 SELECT 语句中的字段名称和表名。
		/// 判断通配符* 是否存在，若存在则表示选择所有列。
		/// 根据表名获取表对象。
		/// 根据列名获取每一行数据中对应的字段。
		/// 将选中的字段返回给用户。
		/// </summary>
		byte ParseSELECT(string sql)
		{
			try
			{
				sql = sql.Replace("\n", "").Replace("\r", "").Trim();
				string pattern = @"SELECT\s+(.*?)\s+FROM\s+([a-zA-Z_][a-zA-Z0-9_]*);?";
				Match match = Regex.Match(sql, pattern, RegexOptions.IgnoreCase);
				if (!match.Success)
				{
					return (byte)SqlCode.FormatError;
				}
				string columnsPart = match.Groups[1].Value;
				string tableName = match.Groups[2].Value;

				Tables? table = GetTable(tableName);
				if (table == null)
				{
					return (byte)SqlCode.TableDoesNotExist;
				}
				List<string> selectedColumns = new();

				if (columnsPart.Trim() == "*")
				{
					selectedColumns.AddRange(table.Fields.Where(f => f.Constraints != "Delete").Select(f => f.ColumnName));
				}
				else
				{
					Span<string> columns = columnsPart.Split(separator, StringSplitOptions.RemoveEmptyEntries);
					foreach (string column in columns)
					{
						string trimmedColumn = column.Trim();
						if (table.Fields.Any(f => f.ColumnName == trimmedColumn))
						{
							selectedColumns.Add(trimmedColumn);
						}
						else
						{
							return (byte)SqlCode.FieldsDoesNotExist;
						}
					}
				}
				List<Dictionary<string, object?>> resultRows = new();
				foreach (var row in table.Row)
				{
					Dictionary<string, object?> selectedRow = new();
					foreach (string column in selectedColumns)
					{
						if (row.TryGetValue(column, out object? value))
						{
							selectedRow[column] = value;
						}
					}
					resultRows.Add(selectedRow);
				}
				ShowTable(selectedColumns.ToArray(), resultRows);
				return 0x0;
			}
			catch (Exception ex)
			{
				logger.log($"StackTrace:{ex.StackTrace}|Message:{ex.Message}|Source:{ex.Source}", 2);
				return (byte)SqlCode.Exception;
			}
		}
		//TODO
		bool IsConstraintKeyword(string word)
		{
			string[] constraintKeywords = new[] { "PRIMARY", "NOT", "UNIQUE", "CHECK", "FOREIGN", "DEFAULT" };
			return Array.Exists(constraintKeywords, keyword => keyword.Equals(word, StringComparison.OrdinalIgnoreCase));
		}
		//public  void ShowTable(string tableName)
		//{
		//	Tables? table = tables.SingleOrDefault(t => t.Name == tableName);
		//	if (table == null) {return; }
		//	var tableData = table.Row;
		//	string[] head = table.Fields.Select(c => c.ColumnName).ToArray();
		//	if (tableData.Count > 0)
		//	{				
		//		Options.Add(new UP());
		//		Options.Add(new Down());

		//		TotalPages = (int)Math.Ceiling((double)tableData.Count / ItemsPerPage);
		//		Console.ForegroundColor = ConsoleColor.DarkYellow;
		//		Console.WriteLine($"  Catalog  {CurrentPage + 1}/{TotalPages}");
		//		Console.WriteLine(@"");
		//		Console.ForegroundColor = ConsoleColor.White;

		//		var pageData = GetPageData(CurrentPage, tableData, head);
		//		int[] columnWidths = GetMaxColumnWidths(pageData, minWidth, maxWidth, head);
		//		PrintLine(columnWidths);
		//		PrintRow(columnWidths, head);
		//		PrintLine(columnWidths);
		//		if (pageData != null)
		//			foreach (var row in pageData)
		//				PrintRow(columnWidths, row);
		//		PrintLine(columnWidths);
		//	}
		//	else {
		//		Console.ForegroundColor = ConsoleColor.DarkYellow;
		//		Console.WriteLine($"  暂无日志");
		//		Console.WriteLine(@"");
		//		Console.ForegroundColor = ConsoleColor.White;
		//	}
		//}
		/// <summary>
		/// 
		/// </summary>
		/// <param name="head"></param>
		/// <param name="tableData"></param>
		void ShowTable(string[] head, List<Dictionary<string, object?>> tableData)
		{
			if (tableData.Count > 0)
			{
				UI.Options.Add(new UP());
				UI.Options.Add(new Down());

				TotalPages = (int)Math.Ceiling((double)tableData.Count / ItemsPerPage);
				Console.ForegroundColor = ConsoleColor.DarkYellow;
				Console.WriteLine($"  Catalog  {CurrentPage + 1}/{TotalPages}");
				Console.WriteLine(@"");
				Console.ForegroundColor = ConsoleColor.White;

				var pageData = GetPageData(CurrentPage, tableData, head);
				int[] columnWidths = GetMaxColumnWidths(pageData, MinWidth, MaxWidth, head);
				PrintLine(columnWidths);
				PrintRow(columnWidths, head);
				PrintLine(columnWidths);
				if (pageData != null)
					foreach (var row in pageData)
						PrintRow(columnWidths, row);
				PrintLine(columnWidths);
			}
			else
			{
				Console.ForegroundColor = ConsoleColor.DarkYellow;
				Console.WriteLine($"  暂无日志");
				Console.WriteLine(@"");
				Console.ForegroundColor = ConsoleColor.White;
			}
		}
		// 获取指定页的数据，并将每行的数据转换为 string 数组格式
		List<string[]> GetPageData(int pageIndex, List<Dictionary<string, object?>> tableData, string[] head)
		{
			var pageData = new List<string[]>();
			int startIndex = pageIndex * ItemsPerPage;
			int endIndex = Math.Min(startIndex + ItemsPerPage, tableData.Count);

			for (int i = startIndex; i < endIndex; i++)
			{
				// 将 Dictionary<string, object> 转换为 string[] 数组
				var rowArray = head.Select(columnName => tableData[i].ContainsKey(columnName) ? tableData[i][columnName]?.ToString() : "").ToArray();
				if (rowArray != null)
					pageData.Add(rowArray!);
			}
			return pageData;
		}
		// 计算每列的最大宽度
		int[] GetMaxColumnWidths(List<string[]> table, int minWidths, int maxWidths, string[] head)
		{
			if (table == null || table.Count == 0)
				throw new ArgumentNullException();
			int[] maxWidthsResult = new int[head.Length];
			foreach (var row in table)
			{
				for (int i = 0; i < row.Length; i++)
					maxWidthsResult[i] = Math.Max(maxWidthsResult[i], row[i]?.Length ?? 0);
			}
			for (int i = 0; i < head.Length; i++)
			{
				maxWidthsResult[i] = Math.Max(maxWidthsResult[i], head[i].Length);
			}
			for (int i = 0; i < head.Length; i++)
			{
				maxWidthsResult[i] = Math.Max(maxWidthsResult[i], FillArrayUsingLinq(new int[] { }, maxWidthsResult.Length, minWidths)[i]);
				maxWidthsResult[i] = Math.Min(maxWidthsResult[i], FillArrayUsingLinq(new int[] { }, maxWidthsResult.Length, maxWidths)[i]);
			}
			return maxWidthsResult;
		}
		//// 计算每列的最大宽度
		// int[] GetMaxColumnWidths(List<string[]> table, int[] minWidths, int[] maxWidths, string[] head)
		//{
		//	if (table == null || table.Count == 0)
		//		return minWidths;
		//	int[] maxWidthsResult = new int[head.Length];
		//	foreach (var row in table)
		//	{
		//		for (int i = 0; i < row.Length; i++)
		//			maxWidthsResult[i] = Math.Max(maxWidthsResult[i], row[i]?.Length ?? 0);
		//	}
		//	for (int i = 0; i < head.Length; i++)
		//	{
		//		maxWidthsResult[i] = Math.Max(maxWidthsResult[i], head[i].Length);
		//	}
		//	for (int i = 0; i < head.Length; i++)
		//	{
		//		maxWidthsResult[i] = Math.Max(maxWidthsResult[i], FillArrayUsingLinq(minWidths, maxWidthsResult.Length,5)[i]);
		//		maxWidthsResult[i] = Math.Min(maxWidthsResult[i], FillArrayUsingLinq(maxWidths, maxWidthsResult.Length)[i]);
		//	}
		//	return maxWidthsResult;
		//}
		/// <summary>
		/// 数组自动补全
		/// </summary>
		/// <param name="originalArray">原数组</param>
		/// <param name="desiredLength">目标长度</param>
		/// <param name="fillValue">补充值</param>
		/// <returns></returns>
		int[] FillArrayUsingLinq(int[] originalArray, int desiredLength, int fillValue = 30)
		{
			if (desiredLength <= originalArray.Length)
				return originalArray;
			return originalArray.Concat(Enumerable.Repeat(fillValue, desiredLength - originalArray.Length)).ToArray();
		}
		void PrintLine(int[] columnWidths)
		{
			Console.Write("  +");
			foreach (var width in columnWidths)
				Console.Write(new string('-', width + 2) + "+");
			Console.WriteLine();
		}
		void PrintRow(int[] columnWidths, string[] row)
		{
			Console.Write("  |");
			for (int i = 0; i < row.Length; i++)
			{
				int displayWidth = GetDisplayWidth(row[i]);
				if (displayWidth > columnWidths[i])
					Console.Write($" {GetTruncatedString(row[i], columnWidths[i] - 2)}… |");
				else
					Console.Write($" {PadString(row[i], columnWidths[i])} |");
			}
			Console.WriteLine();
		}
		/// <summary>
		/// 获取字符串在控制台的显示宽度（一个中文字符按2个宽度计算）
		/// </summary>
		int GetDisplayWidth(string text)
		{
			int width = 0;
			foreach (char c in text)
			{
				if (IsFullWidth(c))
					width += 2;
				else
					width += 1;
			}
			return width;
		}

		/// <summary>
		/// 判断字符是否是全角字符（中文、日文、韩文等）
		/// </summary>
		bool IsFullWidth(char c)
		{
			// 全角字符一般位于Unicode范围 U+3000 ~ U+FF60, 以及其他汉字区块
			return c >= 0x3000 && c <= 0xFF60 || c >= 0xFF61 && c <= 0xFF9F || c >= 0x4E00 && c <= 0x9FFF;
		}

		/// <summary>
		/// 获取截取后的字符串，确保显示宽度不超过指定宽度
		/// </summary>
		string GetTruncatedString(string text, int maxDisplayWidth)
		{
			int currentWidth = 0;
			int endIndex = 0;
			for (int i = 0; i < text.Length; i++)
			{
				currentWidth += IsFullWidth(text[i]) ? 2 : 1;
				if (currentWidth > maxDisplayWidth)
					break;
				endIndex++;
			}
			return text[..endIndex];
		}

		/// <summary>
		/// 根据显示宽度进行填充，确保显示时对齐
		/// </summary>
		string PadString(string text, int totalWidth)
		{
			int currentDisplayWidth = GetDisplayWidth(text);
			int padLength = totalWidth - currentDisplayWidth;
			return text + new string(' ', padLength);
		}
	}
	public enum SqlCode : byte
	{
		OK = 0x0,
		TableDoesNotExist = 0x1,
		FormatError = 0x2,
		ColumnDoesNotExist = 0x3,
		FieldsDoesNotExist = 0x4,
		Exception = 0xA,
		NotSupported = 0xF,

	}
}

