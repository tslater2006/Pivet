using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Pivet.Data
{
    /* This is NOT in the Pivet.Data.Processors namespace because it does not implement IDataProcessor and cannot be used in the DataProviders section of the Config */

    class RawDataProcessor : IDataProcessor
    {
        public event ProgressHandler ProgressChanged;
        public string ItemName => "Raw Data";
        public string ProcessorID => "RawDataProcessor";
        OracleConnection _conn;
        List<string> _prefixes;
        List<RawDataEntry> _entries;
        Dictionary<RawDataEntry,List<RawDataItem>> _selectedItems;
        public int LoadItems(OracleConnection conn, FilterConfig filters)
        {
            _conn = conn;
            _prefixes = filters.Prefixes;
            _entries = filters.RawData;

            _selectedItems = GetItems();

            return _selectedItems.Sum(k => k.Value.Count);
        }

        private void ReportProgress(double progress)
        {
            if (ProgressChanged != null)
            {
                ProgressChanged(new ProgressEvent() { Progress = progress });
            }
        }
        
        public void ProcessDeletes(string rootFolder)
        {
            foreach (var _item in _entries)
            {
                string outputFolder = rootFolder + Path.DirectorySeparatorChar + _item.Folder;
                if (outputFolder.Contains("{"))
                {
                    /* Likely this raw data is using a variable in its path, ignore that and everything after it */
                    outputFolder = outputFolder.Substring(0, outputFolder.IndexOf("{"));
                }
                if (Directory.Exists(outputFolder))
                {
                    Directory.Delete(outputFolder, true);
                }
            }
        }

        public List<ChangedItem> SaveToDisk(string _outputPath)
        {
            List<ChangedItem> changedItems = new List<ChangedItem>();
            foreach (var _item in _selectedItems)
            {
                string outputFolder = _outputPath + Path.DirectorySeparatorChar + _item.Key.Folder;

                Logger.Write($"Saving Table: {_item.Key.Record}");
                

                foreach (RawDataItem item in _item.Value)
                {
                    var changedItem = SaveRawDataItem(outputFolder, _item.Key.NamePattern, item);

                    changedItems.Add(changedItem);
                }
            }

            return changedItems;
        }


        private ChangedItem SaveRawDataItem(string path, string pattern, RawDataItem item)
        {
            /* build out file name */
            Regex placeHolder = new Regex("{([^}]+)}");
            var filePath = path + Path.DirectorySeparatorChar + pattern;

            var matches = placeHolder.Matches(filePath);
            string invalid = new string(Path.GetInvalidFileNameChars());

            foreach (Match m in matches)
            {
                var col = m.Groups[1].Value;
                var colValue = item.Fields[col].ToString();
                foreach (char c in invalid)
                {
                    colValue = colValue.Replace(c.ToString(), "_");
                }
                filePath = filePath.Replace("{" + col + "}", colValue);
            }
           
	        string oprID;
            if (item.Fields.ContainsKey("LASTUPDOPRID"))
            {
                oprID = item.Fields["LASTUPDOPRID"].ToString();
            } else
            {
                oprID = "RAWDATA";
            }
            

            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            var itemAsJObject = JObject.FromObject(item);

            CanonicalizeItem(itemAsJObject);

            File.WriteAllText(filePath, itemAsJObject.ToString(Formatting.Indented));

            return new ChangedItem(filePath, oprID);
        }

        private static void CanonicalizeItem(JToken token)
        {
            switch (token.Type)
            {
                case JTokenType.Object:
                    var obj = (JObject)token;
                    var properties = obj.Properties().ToList();
                    properties.Sort((p1, p2) => string.CompareOrdinal(p1.Name, p2.Name));

                    obj.RemoveAll();

                    foreach (var property in properties)
                    {
                        CanonicalizeItem(property.Value);
                        obj.Add(property);
                    }
                    break;

                case JTokenType.Array:
                    var array = (JArray)token;
                    foreach (var item in array)
                    {
                        CanonicalizeItem(item);
                    }
                    var sortedArray = new JArray(array.OrderBy(t => t.ToString(), StringComparer.Ordinal));
                    array.Replace(sortedArray);
                    break;
            }
        }

        private Dictionary<RawDataEntry, List<RawDataItem>> GetItems()
        {
            Dictionary<RawDataEntry, List<RawDataItem>> returnedItems = new Dictionary<RawDataEntry, List<RawDataItem>>();

            foreach(var _item in _entries)
            {
                List<RawDataItem> entryItems = new List<RawDataItem>();
                Logger.Write($"Finding Key Fields for record: {_item.Record}");

                List<string> recordKeyFields = new List<string>();
                List<string> relatedTables = new List<string>();
                bool tableExists = true;
                /* Ensure the table exists */
                using (OracleCommand fieldCount = new OracleCommand("SELECT COUNT(FIELDNAME) FROM PSRECFIELD WHERE RECNAME = :1", _conn))
                {
                    fieldCount.Parameters.Add(new OracleParameter() { OracleDbType = OracleDbType.Varchar2, Value = _item.Record });
                    using (var reader = fieldCount.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            if (reader.GetInt32(0) == 0)
                            {
                                tableExists = false;
                            }
                        }
                    }
                }
                if (tableExists == false)
                {
                    Logger.Write($"Skipping record: {_item.Record} since it doesn't exist in target database.");
                    /* table doesn't exist! */
                    continue;
                }
                using (OracleCommand keyCommand = new OracleCommand("SELECT FIELDNAME FROM PSRECFIELD WHERE RECNAME = :1 AND MOD(USEEDIT,2) = 1", _conn))
                {
                    keyCommand.Parameters.Add(new OracleParameter() { OracleDbType = OracleDbType.Varchar2, Value = _item.Record });

                    using (var reader = keyCommand.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var keyFieldName = reader.GetString(0);
                            recordKeyFields.Add(keyFieldName);
                        }
                    }

                }

                Logger.Write($"Record has {recordKeyFields.Count} keys: {string.Join(", ", recordKeyFields)}");

                Logger.Write("Finding related tables.");

                var keyFieldsForInClause = "'" + string.Join("', '", recordKeyFields) + "'";

                using (OracleCommand relatedTableCommand = new OracleCommand($"SELECT CASE WHEN SQLTABLENAME = ' ' THEN 'PS_' || RECNAME ELSE SQLTABLENAME END FROM PSRECDEFN WHERE RECNAME IN  (SELECT DISTINCT RECNAME FROM PSRECFIELD WHERE FIELDNAME IN({keyFieldsForInClause}) AND MOD(USEEDIT, 2) = 1 GROUP BY RECNAME HAVING COUNT(FIELDNAME) = {recordKeyFields.Count}) AND RECTYPE = 0 AND RECNAME <> '{_item.Record}'", _conn))
                {
                    using (var reader = relatedTableCommand.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var relatedTable = reader.GetString(0);
                            if (_item.RelatedBlacklist.Contains(relatedTable))
                            {
                                continue;
                            }
                            else
                            {
                                relatedTables.Add(relatedTable);
                            }
                        }
                    }
                }

                Logger.Write($"Found {relatedTables.Count} Related Tables.");

                Logger.Write($"Processing top level rows for {_item.Record}");

                string likeString = "";
                if (_item.FilterField.Length > 0)
                {
                    likeString = $"WHERE ({_item.FilterField} LIKE '" + string.Join($"%' OR {_item.FilterField} LIKE '", _prefixes) + "%')";
                }

                /* get the actual table name */
                var topTableName = "";
                using (OracleCommand actualTableName = new OracleCommand($"SELECT CASE WHEN SQLTABLENAME = ' ' THEN 'PS_' || RECNAME ELSE SQLTABLENAME END FROM PSRECDEFN WHERE RECNAME ='{_item.Record}'", _conn))
                {
                    using (var reader = actualTableName.ExecuteReader())
                    {
                        reader.Read();
                        topTableName = reader.GetString(0);
                    }
                }

                if (_item.ExtraCriteria != null)
                {
                    if (likeString.Length > 0) {
                        likeString += $" AND {_item.ExtraCriteria}";
                    } else
                    {
                        likeString = $" WHERE {_item.ExtraCriteria}";
                    }
                }
                
                using (OracleCommand topLevelRows = new OracleCommand($"SELECT * FROM {topTableName} {likeString} ", _conn))
                {
                    using (var reader = topLevelRows.ExecuteReader())
                    {
                        var dataTable = reader.GetSchemaTable();
                        while (reader.Read())
                        {
                            var rawItem = DataItemFromReader(dataTable, reader);

                            if (_item.IncludeRelated)
                            {
                                LoadRelatedTables(recordKeyFields, relatedTables, rawItem);
                            }
                            entryItems.Add(rawItem);
                        }
                    }
                }

                returnedItems.Add(_item, entryItems);
            }

            return returnedItems;
        }

        public void LoadRelatedTables(List<string> relatedKeys, List<string> relatedTables, RawDataItem topLevelItem)
        {
            /* build the where clause for the selects */
            StringBuilder sb = new StringBuilder();
            sb.Append("WHERE ");
            for (var x = 1; x <= relatedKeys.Count; x++)
            {
                sb.Append(relatedKeys[x - 1]).Append(" = :").Append(x);

                if (x < relatedKeys.Count)
                {
                    sb.Append(" AND ");
                }
            }

            var whereClause = sb.ToString();
            sb.Clear();


            foreach (string relatedTable in relatedTables)
            {
                try
                {
                    using (var secondaryLevel = new OracleCommand($"SELECT * FROM {relatedTable} {whereClause}", _conn))
                    {
                        RawDataRelatedTable relatedTableData = new RawDataRelatedTable();
                        relatedTableData.TableName = relatedTable;
                        /* setup the parameters */
                        foreach (string relatedKey in relatedKeys)
                        {
                            secondaryLevel.Parameters.Add(new OracleParameter() { Value = topLevelItem.Fields[relatedKey] });
                        }

                        using (var reader = secondaryLevel.ExecuteReader())
                        {
                            var dataTable = reader.GetSchemaTable();
                            while (reader.Read())
                            {
                                var rawItem = DataItemFromReader(dataTable, reader);
                                relatedTableData.Rows.Add(rawItem);
                            }
                        }

                        if (relatedTableData.Rows.Count > 0)
                        {
                            topLevelItem.RelatedTables.Add(relatedTableData);
                        }
                    }
                } catch (Exception ex)
                {
                    /* Table probably hasn't been built yet... */
                }
            }

            topLevelItem.RelatedTables = topLevelItem.RelatedTables.OrderBy(t => t.TableName).ToList();


        }

        public static RawDataItem DataItemFromReader(DataTable dataTable, OracleDataReader reader)
        {
            RawDataItem item = new RawDataItem();

            object[] vals = new object[dataTable.Rows.Count];
            int foo = reader.GetValues(vals);

            for(var x = 0; x < dataTable.Rows.Count; x++)
            {
                var columnName = dataTable.Rows[x].ItemArray[0];
                item.Fields.Add(columnName.ToString(), vals[x]);
            }

            return item;
        }

    }

    class RawDataItem
    {
        public Dictionary<string, object> Fields = new Dictionary<string, object>();
        public List<RawDataRelatedTable> RelatedTables = new List<RawDataRelatedTable>();

        public bool ShouldSerializeRelatedTables()
        {
            return RelatedTables.Count > 0;
        }

        public override int GetHashCode()
        {
            var encoder = new UTF8Encoding();
            var hash = SHA256.Create();
            var sb = new StringBuilder();

            foreach (var item in Fields.Values)
            {
                sb.Append(item);
            }

            return
                BitConverter.ToInt32(
                    new Rfc2898DeriveBytes(sb.ToString(),
                        hash.ComputeHash(encoder.GetBytes(sb.ToString()))).GetBytes(4),0);
        }

    }

    class RawDataRelatedTable
    {
        public string TableName;
        public List<RawDataItem> Rows = new List<RawDataItem>();
    }

}
