using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oracle.ManagedDataAccess.Client;
using Pivet.Data;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

namespace Pivet
{
    /// <summary>
    /// Database service for Raw Data configuration assistance
    /// Provides record validation, related table discovery, and data preview capabilities
    /// </summary>
    public class RawDataDatabaseService
    {
        /// <summary>
        /// Validates that a record exists in the database
        /// </summary>
        public static RecordValidationResult ValidateRecordExists(OracleConnection connection, string recordName)
        {
            try
            {
                using (var cmd = new OracleCommand("SELECT COUNT(FIELDNAME) FROM PSRECFIELD WHERE RECNAME = :1", connection))
                {
                    cmd.Parameters.Add(new OracleParameter() { OracleDbType = OracleDbType.Varchar2, Value = recordName });
                    
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            var fieldCount = reader.GetInt32(0);
                            if (fieldCount > 0)
                            {
                                return new RecordValidationResult(true, $"Record exists with {fieldCount} fields", fieldCount);
                            }
                            else
                            {
                                return new RecordValidationResult(false, "Record does not exist in database", 0);
                            }
                        }
                    }
                }
                return new RecordValidationResult(false, "Unable to validate record", 0);
            }
            catch (Exception ex)
            {
                return new RecordValidationResult(false, $"Error validating record: {ex.Message}", 0);
            }
        }

        /// <summary>
        /// Gets all fields available for a record
        /// </summary>
        public static List<RecordField> GetRecordFields(OracleConnection connection, string recordName)
        {
            var fields = new List<RecordField>();
            
            try
            {
                var query = @"
                    SELECT 
                        rf.FIELDNAME, 
                        MOD(rf.USEEDIT,2) as IS_KEY,
                        df.FIELDTYPE,
                        df.LENGTH,
                        df.DECIMALPOS
                    FROM PSRECFIELD rf
                    LEFT JOIN PSDBFIELD df ON rf.FIELDNAME = df.FIELDNAME
                    WHERE rf.RECNAME = :1 
                    ORDER BY rf.FIELDNUM";

                using (var cmd = new OracleCommand(query, connection))
                {
                    cmd.Parameters.Add(new OracleParameter() { OracleDbType = OracleDbType.Varchar2, Value = recordName });
                    
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            fields.Add(new RecordField
                            {
                                Name = reader.GetString("FIELDNAME"),
                                IsKey = reader.GetInt32("IS_KEY") == 1,
                                FieldType = reader.IsDBNull(reader.GetOrdinal("FIELDTYPE")) ? 0 : reader.GetInt32("FIELDTYPE"),
                                Length = reader.IsDBNull(reader.GetOrdinal("LENGTH")) ? 0 : reader.GetInt32("LENGTH"),
                                DecimalPositions = reader.IsDBNull(reader.GetOrdinal("DECIMALPOS")) ? 0 : reader.GetInt32("DECIMALPOS")
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Write($"Error getting record fields: {ex.Message}");
            }
            
            return fields;
        }

        /// <summary>
        /// Validates that a filter field exists in the specified record
        /// </summary>
        public static FieldValidationResult ValidateFilterField(OracleConnection connection, string recordName, string fieldName)
        {
            try
            {
                var query = @"
                    SELECT 
                        df.FIELDTYPE,
                        df.LENGTH,
                        df.DECIMALPOS,
                        MOD(rf.USEEDIT,2) as IS_KEY
                    FROM PSRECFIELD rf
                    LEFT JOIN PSDBFIELD df ON rf.FIELDNAME = df.FIELDNAME
                    WHERE rf.RECNAME = :1 AND rf.FIELDNAME = :2";

                using (var cmd = new OracleCommand(query, connection))
                {
                    cmd.Parameters.Add(new OracleParameter() { OracleDbType = OracleDbType.Varchar2, Value = recordName });
                    cmd.Parameters.Add(new OracleParameter() { OracleDbType = OracleDbType.Varchar2, Value = fieldName });
                    
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            var fieldType = reader.IsDBNull(reader.GetOrdinal("FIELDTYPE")) ? 0 : reader.GetInt32("FIELDTYPE");
                            var length = reader.IsDBNull(reader.GetOrdinal("LENGTH")) ? 0 : reader.GetInt32("LENGTH");
                            var isKey = reader.GetInt32("IS_KEY") == 1;
                            
                            return new FieldValidationResult(true, "Field exists", fieldType, length, isKey);
                        }
                        else
                        {
                            return new FieldValidationResult(false, "Field does not exist in record", 0, 0, false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return new FieldValidationResult(false, $"Error validating field: {ex.Message}", 0, 0, false);
            }
        }

        /// <summary>
        /// Gets key fields for a record (fields where MOD(USEEDIT,2) = 1)
        /// </summary>
        public static List<string> GetRecordKeyFields(OracleConnection connection, string recordName)
        {
            var keyFields = new List<string>();
            
            try
            {
                using (var cmd = new OracleCommand("SELECT FIELDNAME FROM PSRECFIELD WHERE RECNAME = :1 AND MOD(USEEDIT,2) = 1 ORDER BY FIELDNUM", connection))
                {
                    cmd.Parameters.Add(new OracleParameter() { OracleDbType = OracleDbType.Varchar2, Value = recordName });
                    
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            keyFields.Add(reader.GetString("FIELDNAME"));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Write($"Error getting key fields: {ex.Message}");
            }
            
            return keyFields;
        }

        /// <summary>
        /// Discovers related tables for a record using the same logic as RawDataProcessor
        /// </summary>
        public static RelatedTablesResult FindRelatedTables(OracleConnection connection, string recordName, List<string> excludeList = null)
        {
            try
            {
                // Get key fields first
                var keyFields = GetRecordKeyFields(connection, recordName);
                
                if (keyFields.Count == 0)
                {
                    return new RelatedTablesResult(false, "Record has no key fields", keyFields, new List<string>());
                }

                // Find related tables
                var keyFieldsForInClause = "'" + string.Join("', '", keyFields) + "'";
                var query = $@"
                    SELECT CASE WHEN SQLTABLENAME = ' ' THEN 'PS_' || RECNAME ELSE SQLTABLENAME END as TABLE_NAME, RECNAME
                    FROM PSRECDEFN 
                    WHERE RECNAME IN (
                        SELECT DISTINCT RECNAME 
                        FROM PSRECFIELD 
                        WHERE FIELDNAME IN({keyFieldsForInClause}) 
                          AND MOD(USEEDIT, 2) = 1 
                        GROUP BY RECNAME 
                        HAVING COUNT(FIELDNAME) = {keyFields.Count}
                    ) 
                    AND RECTYPE = 0 
                    AND RECNAME <> '{recordName}'
                    ORDER BY RECNAME";

                var relatedTables = new List<string>();
                
                using (var cmd = new OracleCommand(query, connection))
                {
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var tableName = reader.GetString("TABLE_NAME");
                            
                            // Apply exclusion list if provided
                            if (excludeList == null || !excludeList.Contains(tableName))
                            {
                                relatedTables.Add(tableName);
                            }
                        }
                    }
                }

                return new RelatedTablesResult(true, $"Found {relatedTables.Count} related tables", keyFields, relatedTables);
            }
            catch (Exception ex)
            {
                return new RelatedTablesResult(false, $"Error finding related tables: {ex.Message}", new List<string>(), new List<string>());
            }
        }

        /// <summary>
        /// Gets the actual SQL table name for a record
        /// </summary>
        public static string GetTableSQLName(OracleConnection connection, string recordName)
        {
            try
            {
                using (var cmd = new OracleCommand("SELECT CASE WHEN SQLTABLENAME = ' ' THEN 'PS_' || RECNAME ELSE SQLTABLENAME END FROM PSRECDEFN WHERE RECNAME = :1", connection))
                {
                    cmd.Parameters.Add(new OracleParameter() { OracleDbType = OracleDbType.Varchar2, Value = recordName });
                    
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return reader.GetString(0);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Write($"Error getting SQL table name: {ex.Message}");
            }
            
            return $"PS_{recordName}"; // Default fallback
        }

        /// <summary>
        /// Gets sample data for a Raw Data entry configuration
        /// </summary>
        public static SampleDataResult GetSampleData(OracleConnection connection, RawDataEntry entry, List<string> prefixes, int maxRows = 1)
        {
            try
            {
                var tableName = GetTableSQLName(connection, entry.Record);
                var keyFields = GetRecordKeyFields(connection, entry.Record);
                
                if (keyFields.Count == 0)
                {
                    return new SampleDataResult(false, "Record has no key fields", new List<RawDataItem>());
                }

                // Build WHERE clause
                var whereClause = BuildWhereClause(entry, prefixes);
                var query = $"SELECT * FROM {tableName} {whereClause}";
                
                if (maxRows > 0)
                {
                    query = $"SELECT * FROM ({query}) WHERE ROWNUM <= {maxRows}";
                }

                var items = new List<RawDataItem>();
                
                using (var cmd = new OracleCommand(query, connection))
                {
                    using (var reader = cmd.ExecuteReader())
                    {
                        var schemaTable = reader.GetSchemaTable();
                        
                        while (reader.Read() && items.Count < maxRows)
                        {
                            var item = RawDataProcessor.DataItemFromReader(schemaTable, reader);
                            
                            // Load related tables if requested
                            if (entry.IncludeRelated)
                            {
                                var relatedTablesResult = FindRelatedTables(connection, entry.Record, entry.RelatedBlacklist);
                                if (relatedTablesResult.IsSuccess)
                                {
                                    LoadRelatedTablesForItem(connection, keyFields, relatedTablesResult.RelatedTables, item);
                                }
                            }
                            
                            items.Add(item);
                        }
                    }
                }

                return new SampleDataResult(true, $"Retrieved {items.Count} sample record(s)", items);
            }
            catch (Exception ex)
            {
                return new SampleDataResult(false, $"Error getting sample data: {ex.Message}", new List<RawDataItem>());
            }
        }

        /// <summary>
        /// Generates a JSON preview for a Raw Data entry
        /// </summary>
        public static JsonPreviewResult GenerateJSONPreview(OracleConnection connection, RawDataEntry entry, List<string> prefixes)
        {
            try
            {
                var sampleResult = GetSampleData(connection, entry, prefixes, 1);
                
                if (!sampleResult.IsSuccess || sampleResult.Items.Count == 0)
                {
                    return new JsonPreviewResult(false, sampleResult.Message, "");
                }

                var sampleItem = sampleResult.Items[0];
                var jsonObject = JObject.FromObject(sampleItem);
                
                // Apply canonicalization like RawDataProcessor does
                Data.Formatters.JsonRawDataFormatter.CanonicalizeItem(jsonObject);
                
                var jsonPreview = jsonObject.ToString(Formatting.Indented);
                
                return new JsonPreviewResult(true, "JSON preview generated successfully", jsonPreview);
            }
            catch (Exception ex)
            {
                return new JsonPreviewResult(false, $"Error generating JSON preview: {ex.Message}", "");
            }
        }

        /// <summary>
        /// Tests a Raw Data entry configuration
        /// </summary>
        public static RawDataTestResult TestRawDataEntry(OracleConnection connection, RawDataEntry entry, List<string> prefixes)
        {
            var result = new RawDataTestResult();
            
            // Validate record exists
            var recordValidation = ValidateRecordExists(connection, entry.Record);
            result.RecordExists = recordValidation.IsValid;
            result.FieldCount = recordValidation.FieldCount;
            
            if (!recordValidation.IsValid)
            {
                result.IsValid = false;
                result.ErrorMessages.Add($"Record '{entry.Record}' does not exist");
                return result;
            }

            // Validate filter field if specified
            if (!string.IsNullOrWhiteSpace(entry.FilterField))
            {
                var fieldValidation = ValidateFilterField(connection, entry.Record, entry.FilterField);
                result.FilterFieldExists = fieldValidation.IsValid;
                
                if (!fieldValidation.IsValid)
                {
                    result.WarningMessages.Add($"Filter field '{entry.FilterField}' does not exist in record");
                }
            }

            // Test related tables discovery
            if (entry.IncludeRelated)
            {
                var relatedResult = FindRelatedTables(connection, entry.Record, entry.RelatedBlacklist);
                result.RelatedTablesFound = relatedResult.IsSuccess;
                result.RelatedTableCount = relatedResult.RelatedTables.Count;
                result.KeyFields = relatedResult.KeyFields;
                
                if (relatedResult.IsSuccess && relatedResult.RelatedTables.Count == 0)
                {
                    result.WarningMessages.Add("No related tables found for this record");
                }
            }

            // Test data retrieval
            try
            {
                var sampleResult = GetSampleData(connection, entry, prefixes, 1);
                result.SampleDataRetrieved = sampleResult.IsSuccess;
                result.SampleRowCount = sampleResult.Items.Count;
                
                if (!sampleResult.IsSuccess)
                {
                    result.ErrorMessages.Add($"Could not retrieve sample data: {sampleResult.Message}");
                }
                else if (sampleResult.Items.Count == 0)
                {
                    result.WarningMessages.Add("No data rows match the current filter criteria");
                }
            }
            catch (Exception ex)
            {
                result.SampleDataRetrieved = false;
                result.ErrorMessages.Add($"Error testing data retrieval: {ex.Message}");
            }

            result.IsValid = result.RecordExists && result.ErrorMessages.Count == 0;
            return result;
        }

        /// <summary>
        /// Gets row count for a Raw Data entry configuration
        /// </summary>
        public static RowCountResult GetRowCount(OracleConnection connection, RawDataEntry entry, List<string> prefixes)
        {
            try
            {
                var tableName = GetTableSQLName(connection, entry.Record);
                var whereClause = BuildWhereClause(entry, prefixes);
                var countQuery = $"SELECT COUNT(*) FROM {tableName} {whereClause}";

                using (var cmd = new OracleCommand(countQuery, connection))
                {
                    var rowCount = Convert.ToInt32(cmd.ExecuteScalar());
                    return new RowCountResult(true, "Row count retrieved successfully", rowCount);
                }
            }
            catch (Exception ex)
            {
                return new RowCountResult(false, $"Error getting row count: {ex.Message}", 0);
            }
        }

        #region Private Helper Methods

        private static string BuildWhereClause(RawDataEntry entry, List<string> prefixes)
        {
            var whereConditions = new List<string>();

            // Add prefix filtering
            if (!string.IsNullOrWhiteSpace(entry.FilterField) && prefixes != null && prefixes.Count > 0)
            {
                var prefixConditions = prefixes.Select(p => $"{entry.FilterField} LIKE '{p}%'");
                whereConditions.Add($"({string.Join(" OR ", prefixConditions)})");
            }

            // Add extra criteria
            if (!string.IsNullOrWhiteSpace(entry.ExtraCriteria))
            {
                whereConditions.Add($"({entry.ExtraCriteria})");
            }

            return whereConditions.Count > 0 ? "WHERE " + string.Join(" AND ", whereConditions) : "";
        }

        private static void LoadRelatedTablesForItem(OracleConnection connection, List<string> keyFields, List<string> relatedTables, RawDataItem item)
        {
            var whereClause = BuildRelatedTableWhereClause(keyFields);

            foreach (var tableName in relatedTables)
            {
                try
                {
                    using (var cmd = new OracleCommand($"SELECT * FROM {tableName} {whereClause}", connection))
                    {
                        // Set parameters for key fields
                        for (int i = 0; i < keyFields.Count; i++)
                        {
                            cmd.Parameters.Add(new OracleParameter() { Value = item.Fields[keyFields[i]] });
                        }

                        var relatedTable = new RawDataRelatedTable { TableName = tableName };

                        using (var reader = cmd.ExecuteReader())
                        {
                            var schemaTable = reader.GetSchemaTable();
                            
                            while (reader.Read())
                            {
                                var relatedRow = RawDataProcessor.RelatedRowFromReader(schemaTable, reader);
                                relatedTable.Rows.Add(relatedRow);
                            }
                        }

                        if (relatedTable.Rows.Count > 0)
                        {
                            item.RelatedTables.Add(relatedTable);
                        }
                    }
                }
                catch
                {
                    // Skip tables that can't be queried
                }
            }

            item.RelatedTables = item.RelatedTables.OrderBy(t => t.TableName).ToList();
        }

        private static string BuildRelatedTableWhereClause(List<string> keyFields)
        {
            var conditions = keyFields.Select((field, index) => $"{field} = :{index + 1}");
            return "WHERE " + string.Join(" AND ", conditions);
        }


        #endregion
    }

    #region Result Classes

    public class RecordValidationResult
    {
        public bool IsValid { get; }
        public string Message { get; }
        public int FieldCount { get; }

        public RecordValidationResult(bool isValid, string message, int fieldCount)
        {
            IsValid = isValid;
            Message = message;
            FieldCount = fieldCount;
        }
    }

    public class FieldValidationResult
    {
        public bool IsValid { get; }
        public string Message { get; }
        public int FieldType { get; }
        public int Length { get; }
        public bool IsKey { get; }

        public FieldValidationResult(bool isValid, string message, int fieldType, int length, bool isKey)
        {
            IsValid = isValid;
            Message = message;
            FieldType = fieldType;
            Length = length;
            IsKey = isKey;
        }
    }

    public class RelatedTablesResult
    {
        public bool IsSuccess { get; }
        public string Message { get; }
        public List<string> KeyFields { get; }
        public List<string> RelatedTables { get; }

        public RelatedTablesResult(bool isSuccess, string message, List<string> keyFields, List<string> relatedTables)
        {
            IsSuccess = isSuccess;
            Message = message;
            KeyFields = keyFields;
            RelatedTables = relatedTables;
        }
    }

    public class SampleDataResult
    {
        public bool IsSuccess { get; }
        public string Message { get; }
        public List<RawDataItem> Items { get; }

        public SampleDataResult(bool isSuccess, string message, List<RawDataItem> items)
        {
            IsSuccess = isSuccess;
            Message = message;
            Items = items;
        }
    }

    public class JsonPreviewResult
    {
        public bool IsSuccess { get; }
        public string Message { get; }
        public string JsonPreview { get; }

        public JsonPreviewResult(bool isSuccess, string message, string jsonPreview)
        {
            IsSuccess = isSuccess;
            Message = message;
            JsonPreview = jsonPreview;
        }
    }

    public class RawDataTestResult
    {
        public bool IsValid { get; set; }
        public bool RecordExists { get; set; }
        public int FieldCount { get; set; }
        public bool FilterFieldExists { get; set; }
        public bool RelatedTablesFound { get; set; }
        public int RelatedTableCount { get; set; }
        public List<string> KeyFields { get; set; } = new List<string>();
        public bool SampleDataRetrieved { get; set; }
        public int SampleRowCount { get; set; }
        public List<string> ErrorMessages { get; set; } = new List<string>();
        public List<string> WarningMessages { get; set; } = new List<string>();
    }

    public class RowCountResult
    {
        public bool IsSuccess { get; }
        public string Message { get; }
        public int RowCount { get; }

        public RowCountResult(bool isSuccess, string message, int rowCount)
        {
            IsSuccess = isSuccess;
            Message = message;
            RowCount = rowCount;
        }
    }

    public class RecordField
    {
        public string Name { get; set; }
        public bool IsKey { get; set; }
        public int FieldType { get; set; }
        public int Length { get; set; }
        public int DecimalPositions { get; set; }

        public string TypeDescription => GetFieldTypeDescription(FieldType);

        private string GetFieldTypeDescription(int fieldType)
        {
            return fieldType switch
            {
                0 => "Character",
                1 => "Number",
                2 => "Signed Number",
                3 => "Date",
                4 => "Time",
                5 => "DateTime",
                6 => "Image",
                8 => "Long Character",
                _ => $"Unknown ({fieldType})"
            };
        }

        public override string ToString()
        {
            var keyIndicator = IsKey ? " (Key)" : "";
            return $"{Name} - {TypeDescription}({Length}){keyIndicator}";
        }
    }

    #endregion
}