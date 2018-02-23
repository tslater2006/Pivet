using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Oracle.ManagedDataAccess.Client;
using System.IO;
using Newtonsoft.Json;

namespace Pivet.Data.Processors
{
    internal class TranslateValueProcessor : IDataProcessor
    {
        private List<TranslateValueItem> _items = new List<TranslateValueItem>();
        OracleConnection _conn;
        public string ItemName => "Translate Value";
        public string ProcessorID => "TranslateValueProcessor";
        public event ProgressHandler ProgressChanged;

        public int LoadItems(OracleConnection conn, FilterConfig filters)
        {
            _conn = conn;
            using (var itemLoad = new OracleCommand())
            {
                itemLoad.Connection = conn;
                StringBuilder sb = new StringBuilder();
                if (filters.Projects != null && filters.Projects.Count > 0)
                {
                    sb.Append("select A.FIELDNAME from PSXLATITEM A, PSPROJECTITEM B WHERE B.OBJECTTYPE = 4 and B.OBJECTVALUE1 = A.FIELDNAME and B.OBJECTVALUE2 = A.FIELDVALUE and B.PROJECTNAME in (");
                    for (var x = 0; x < filters.Projects.Count; x++)
                    {
                        sb.Append(":" + (x + 1) + ",");
                    }
                    sb.Length--;
                    sb.Append(")");

                    
                    foreach (var proj in filters.Projects)
                    {
                        itemLoad.Parameters.Add(new OracleParameter() { OracleDbType = OracleDbType.Varchar2, Value = proj });
                    }
                }
                else
                {
                    //sb.Append("select A.FIELDNAME, A.FIELDVALUE, A.EFFDT, A.EFF_STATUS, A.XLATLONGNAME, A.XLATSHORTNAME, A.LASTUPDDTTM, A.LASTUPDOPRID from PSXLATITEM A");
                    sb.Append("select A.FIELDNAME FROM PSXLATITEM A");
                }
                itemLoad.CommandText = sb.ToString();
                using (var reader = itemLoad.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        _items.Add(new TranslateValueItem(reader.GetString(0)));
                    }
                }
            }
            ApplyFilters(filters);
            return _items.Count;
        }

        private void ApplyFilters(FilterConfig filters)
        {
            for (var x = _items.Count - 1; x >= 0; x--)
            {
                var shouldDiscard = true;

                if (filters.Prefixes == null || filters.Prefixes.Count == 0)
                {
                    shouldDiscard = false;
                }
                else
                {
                    foreach (var prfx in filters.Prefixes)
                    {
                        if (_items[x].FieldName.StartsWith(prfx))
                        {
                            shouldDiscard = false;
                            break;
                        }
                    }
                }

                if (!shouldDiscard && filters.IncludeOprids != null && filters.IncludeOprids.Count > 0)
                {
                    if (filters.IncludeOprids.Contains(_items[x].LastUpdateOprid))
                    {
                        shouldDiscard = false;
                    }
                    else
                    {
                        shouldDiscard = true;
                    }
                }

                if (!shouldDiscard && filters.ExcludeOprids != null && filters.ExcludeOprids.Count > 0)
                {
                    if (filters.ExcludeOprids.Contains(_items[x].LastUpdateOprid))
                    {
                        shouldDiscard = true;
                    }
                    else
                    {
                        shouldDiscard = false;
                    }
                }

                if (shouldDiscard)
                {
                    _items.RemoveAt(x);
                }
            }
        }
            

        public void ProcessDeletes(string rootFolder)
        {
            var xlatPath = Path.Combine(rootFolder, "Translate Values");

            if (Directory.Exists(xlatPath))
            {
                Directory.Delete(xlatPath, true);
            }

            
        }

        private void ReportProgress(double progress)
        {
            if (ProgressChanged != null)
            {
                ProgressChanged(new ProgressEvent() { Progress = progress });
            }
        }

        public List<ChangedItem> SaveToDisk(string rootFolder)
        {
            var xlatPath = Path.Combine(rootFolder, "Translate Values");

            if (Directory.Exists(xlatPath) == false)
            {
                Directory.CreateDirectory(xlatPath);
            }

            List<ChangedItem> changedItems = new List<ChangedItem>();

            var setGroups = _items.GroupBy(s => s.FieldName);

            double total = _items.Count;
            double current = 0;

            if (total == 0)
            {
                ReportProgress(100);
            }

            foreach (var set in setGroups)
            {
                var xlatField = new TranslateValueField();
                xlatField.FieldName = set.Key;

                foreach (var item in set)
                {
                    PopulateItemFromDB(item);
                    xlatField.Translates.Add(item);
                    current++;
                    ReportProgress(((int)(((current / total) * 10000))/(double)100));
                }

                var jsonText = JsonConvert.SerializeObject(xlatField, Formatting.Indented);
                var filePath = Path.Combine(xlatPath, set.Key + ".json");
                File.WriteAllText(filePath, jsonText);

                changedItems.Add(new ChangedItem() { FilePath = filePath, OperatorId = "Translate Values"});
            }
            return changedItems;
        }

        private void PopulateItemFromDB(TranslateValueItem item)
        {
            using (var cmd = new OracleCommand("select A.FIELDVALUE, A.EFFDT, A.EFF_STATUS, A.XLATLONGNAME, A.XLATSHORTNAME, A.LASTUPDDTTM, A.LASTUPDOPRID from PSXLATITEM A WHERE A.FIELDNAME = :1", _conn))
            {
                cmd.Parameters.Add(new OracleParameter() { OracleDbType = OracleDbType.Varchar2, Value = item.FieldName });
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        item.FieldValue = reader.GetString(0);
                        item.EffectiveDate = reader.GetDateTime(1);
                        item.EffectiveStatus = reader.GetString(2);
                        item.XlatLongName = reader.GetString(3);
                        item.XlatShortName = reader.GetString(4);
                        item.LastUpdate = reader.GetDateTime(5);
                        item.LastUpdateOprid = reader.GetString(6);
                    }
                    reader.Close();
                }


                /* translations */
                item.Translations = new List<TranslateValueTranslation>();

                using (var translateCmd = new OracleCommand("SELECT EFFDT, LANGUAGE_CD, XLATLONGNAME, XLATSHORTNAME FROM PSXLATITEMLANG WHERE FIELDNAME = :1 AND FIELDVALUE = :2", _conn))
                {
                    translateCmd.Parameters.Add(new OracleParameter() { OracleDbType = OracleDbType.Varchar2, Value = item.FieldName });
                    translateCmd.Parameters.Add(new OracleParameter() { OracleDbType = OracleDbType.Varchar2, Value = item.FieldValue });
                    using (var translateReader = translateCmd.ExecuteReader())
                    {
                        while (translateReader.Read())
                        {
                            var translation = new TranslateValueTranslation();
                            translation.EffectiveDate = translateReader.GetDateTime(0);
                            translation.LanguageCode = translateReader.GetString(1);
                            translation.XlatLongName = translateReader.GetString(2);
                            translation.XlatShortName = translateReader.GetString(3);

                            item.Translations.Add(translation);
                        }
                    }
                }
            }


        }
    }

    internal class TranslateValueField
    {
        [JsonProperty(Order = 1)]
        public string FieldName { get; set; }
        [JsonProperty(Order = 2)]
        public List<TranslateValueItem> Translates = new List<TranslateValueItem>();
    }
    internal class TranslateValueTranslation
    {
        public DateTime EffectiveDate { get; set; }
        public string LanguageCode { get; set; }
        public string XlatLongName { get; set; }
        public string XlatShortName { get; set; }
    }
    internal class TranslateValueItem
    {
        [JsonIgnore]
        public string FieldName { get; set; }
        public string FieldValue { get; set; }
        public DateTime EffectiveDate { get; set; }
        public string EffectiveStatus { get; set; }
        public string XlatLongName { get; set; }
        public string XlatShortName { get; set; }
        public DateTime LastUpdate { get; set; }
        public string LastUpdateOprid { get; set; }

        public List<TranslateValueTranslation> Translations { get; set; }

        public TranslateValueItem(String fieldName)
        {
            FieldName = fieldName;
        }
    }
}
