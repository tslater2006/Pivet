using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Pivet.Data
{
    /* This is NOT in the Pivet.Data.Processors namespace because it does not implement IDataProcessor and cannot be used in the DataProviders section of the Config */

    class RawDataProcessor
    {
        public event ProgressHandler ProgressChanged;

        RawDataEntry _item;
        string _outputPath;
        OracleConnection _conn;
        List<string> _prefixes;
        public RawDataProcessor(OracleConnection conn, string baseOutputPath, RawDataEntry item, List<string> prefixes)
        {
            _item = item;
            _outputPath = baseOutputPath;
            _conn = conn;
            _prefixes = prefixes;
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
            string outputFolder = rootFolder + Path.DirectorySeparatorChar + _item.Folder;

            if (Directory.Exists(outputFolder))
            {
                Directory.Delete(outputFolder, true);
            }
        }

        public List<ChangedItem> Process()
        {
            string outputFolder = _outputPath + Path.DirectorySeparatorChar + _item.Folder;

            Logger.Write($"Processing Table: {_item.Record}");

            List<RawDataItem> items = GetItems();
            
            Directory.CreateDirectory(outputFolder);
            List<ChangedItem> changedItems = new List<ChangedItem>();

            foreach (RawDataItem item in items)
            {
                var changedItem = SaveRawDataItem(outputFolder, item);
                
                changedItems.Add(changedItem);
            }
            
            return changedItems;
        }


        private ChangedItem SaveRawDataItem(string path, RawDataItem item)
        {
            ChangedItem CI = new ChangedItem();
            /* build out file name */
            Regex placeHolder = new Regex("{([^}]+)}");
            var filePath = path + Path.DirectorySeparatorChar + _item.NamePattern;

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
           

            CI.FilePath = filePath;
            if (item.Fields.ContainsKey("LASTUPDOPRID"))
            {
                CI.OperatorId = item.Fields["LASTUPDOPRID"].ToString();
            } else
            {
                CI.OperatorId = "RAWDATA";
            }
            

            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            File.WriteAllText(filePath, JsonConvert.SerializeObject(item,Formatting.Indented));

            return CI;
        }

        private List<RawDataItem> GetItems()
        {
            List<RawDataItem> returnedItems = new List<RawDataItem>();

            Logger.Write("Finding Key Fields for record.");

            List<string> recordKeyFields = new List<string>();
            List<string> relatedTables = new List<string>();

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
                        relatedTables.Add(relatedTable);
                    }
                }
            }

            Logger.Write($"Found {relatedTables.Count} Related Tables: {string.Join(", ", relatedTables)}");

            Logger.Write("Processing top level rows.");

            var likeString = $"{_item.FilterField} LIKE '" + string.Join($"%' OR {_item.FilterField} LIKE '", _prefixes) + "%'";

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

            using (OracleCommand topLevelRows = new OracleCommand($"SELECT * FROM {topTableName} WHERE {likeString} ", _conn))
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
                        returnedItems.Add(rawItem);
                    }
                }
            }
            Logger.Write("Loaded " + returnedItems.Count + " Items...");

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
                using (var secondaryLevel = new OracleCommand($"SELECT * FROM {relatedTable} {whereClause}",_conn))
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
            }

            


        }

        public RawDataItem DataItemFromReader(DataTable dataTable, OracleDataReader reader)
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
    }

    class RawDataRelatedTable
    {
        public string TableName;
        public List<RawDataItem> Rows = new List<RawDataItem>();
    }

}
