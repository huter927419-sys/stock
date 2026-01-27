using System;
using System.Collections.Generic;
using System.Text;

namespace MQReceiver.Helpers
{
    /// <summary>
    /// JSON解析辅助类
    /// 提供轻量级的JSON解析功能，无需外部依赖
    /// </summary>
    public static class JsonParserHelper
    {
        /// <summary>
        /// 从JSON中提取records数组并逐个处理
        /// </summary>
        /// <typeparam name="T">记录类型</typeparam>
        /// <param name="json">JSON字符串</param>
        /// <param name="recordParser">单条记录解析函数</param>
        /// <returns>解析后的记录列表</returns>
        public static List<T> ParseRecordsArray<T>(string json, Func<string, T> recordParser) where T : class
        {
            var records = new List<T>();
            try
            {
                int recordsStart = json.IndexOf("\"records\":[");
                if (recordsStart < 0) return records;

                int arrayStart = json.IndexOf('[', recordsStart);
                if (arrayStart < 0) return records;

                int braceCount = 0;
                var currentRecord = new StringBuilder();

                for (int i = arrayStart + 1; i < json.Length; i++)
                {
                    char c = json[i];
                    if (c == '{')
                    {
                        braceCount++;
                        currentRecord.Append(c);
                    }
                    else if (c == '}')
                    {
                        currentRecord.Append(c);
                        braceCount--;
                        if (braceCount == 0)
                        {
                            T record = recordParser(currentRecord.ToString());
                            if (record != null) records.Add(record);
                            currentRecord.Clear();
                        }
                    }
                    else if (braceCount > 0)
                    {
                        currentRecord.Append(c);
                    }
                    else if (c == ']')
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("解析JSON失败: " + ex.Message);
            }
            return records;
        }

        /// <summary>
        /// 从JSON对象中提取字符串值
        /// </summary>
        public static string ExtractStringValue(string json, string key)
        {
            string searchKey = "\"" + key + "\":";
            int keyPos = json.IndexOf(searchKey);
            if (keyPos < 0) return "";

            int valueStart = keyPos + searchKey.Length;
            while (valueStart < json.Length && char.IsWhiteSpace(json[valueStart]))
            {
                valueStart++;
            }

            if (valueStart >= json.Length) return "";

            if (json[valueStart] == '"')
            {
                valueStart++;
                int valueEnd = json.IndexOf('"', valueStart);
                if (valueEnd < 0) return "";
                return json.Substring(valueStart, valueEnd - valueStart);
            }
            else
            {
                int valueEnd = valueStart;
                while (valueEnd < json.Length && json[valueEnd] != ',' && json[valueEnd] != '}')
                {
                    valueEnd++;
                }
                return json.Substring(valueStart, valueEnd - valueStart).Trim();
            }
        }

        /// <summary>
        /// 从JSON对象中提取整数值
        /// </summary>
        public static int ExtractIntValue(string json, string key, int defaultValue = 0)
        {
            string value = ExtractStringValue(json, key);
            if (string.IsNullOrEmpty(value) || value == "null")
                return defaultValue;
            return int.TryParse(value, out int result) ? result : defaultValue;
        }

        /// <summary>
        /// 从JSON对象中提取ushort值
        /// </summary>
        public static ushort ExtractUShortValue(string json, string key, ushort defaultValue = 0)
        {
            string value = ExtractStringValue(json, key);
            if (string.IsNullOrEmpty(value) || value == "null")
                return defaultValue;
            return ushort.TryParse(value, out ushort result) ? result : defaultValue;
        }

        /// <summary>
        /// 从JSON对象中提取decimal值
        /// </summary>
        public static decimal ExtractDecimalValue(string json, string key, decimal defaultValue = 0)
        {
            string value = ExtractStringValue(json, key);
            if (string.IsNullOrEmpty(value) || value == "null")
                return defaultValue;
            return decimal.TryParse(value, out decimal result) ? result : defaultValue;
        }

        /// <summary>
        /// 从JSON对象中提取日期值
        /// </summary>
        public static DateTime ExtractDateValue(string json, string key, string format = "yyyy-MM-dd")
        {
            string value = ExtractStringValue(json, key);
            if (string.IsNullOrEmpty(value))
                return default(DateTime);
            return DateTime.TryParseExact(value, format, null, System.Globalization.DateTimeStyles.None, out DateTime result)
                ? result : default(DateTime);
        }

        /// <summary>
        /// 从JSON对象中提取日期时间值
        /// </summary>
        public static DateTime ExtractDateTimeValue(string json, string key, string format = "yyyy-MM-dd HH:mm:ss")
        {
            string value = ExtractStringValue(json, key);
            if (string.IsNullOrEmpty(value))
                return default(DateTime);
            return DateTime.TryParseExact(value, format, null, System.Globalization.DateTimeStyles.None, out DateTime result)
                ? result : default(DateTime);
        }

        /// <summary>
        /// 从JSON对象中提取decimal数组
        /// </summary>
        public static decimal[] ExtractDecimalArray(string json, string key, int arraySize = 5)
        {
            decimal[] result = new decimal[arraySize];
            string searchKey = "\"" + key + "\":[";
            int keyPos = json.IndexOf(searchKey);
            if (keyPos < 0) return result;

            int arrayStart = keyPos + searchKey.Length;
            int arrayEnd = json.IndexOf(']', arrayStart);
            if (arrayEnd < 0)
            {
                arrayEnd = json.IndexOf('}', arrayStart);
                if (arrayEnd < 0) return result;
            }

            string arrayContent = json.Substring(arrayStart, arrayEnd - arrayStart);
            string[] values = arrayContent.Split(',');

            for (int i = 0; i < Math.Min(arraySize, values.Length); i++)
            {
                string value = values[i].Trim();
                if (!string.IsNullOrEmpty(value))
                {
                    decimal.TryParse(value, out result[i]);
                }
            }

            return result;
        }

        /// <summary>
        /// 从JSON对象中提取可空ushort值
        /// </summary>
        public static ushort? ExtractNullableUShortValue(string json, string key)
        {
            string value = ExtractStringValue(json, key);
            if (string.IsNullOrEmpty(value) || value == "null")
                return null;
            return ushort.TryParse(value, out ushort result) ? result : (ushort?)null;
        }

        /// <summary>
        /// 从JSON对象中提取可空decimal值
        /// </summary>
        public static decimal? ExtractNullableDecimalValue(string json, string key)
        {
            string value = ExtractStringValue(json, key);
            if (string.IsNullOrEmpty(value) || value == "null")
                return null;
            return decimal.TryParse(value, out decimal result) ? result : (decimal?)null;
        }
    }
}
