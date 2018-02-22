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
    internal class MessageCatalogProcessor : IDataProcessor
    {
        List<MessageCatalogItem> selectedItems = new List<MessageCatalogItem>();
        OracleConnection _conn;

        public event ProgressHandler ProgressChanged;

        public int LoadItems(OracleConnection conn, FilterConfig filters)
        {
            _conn = conn;

            foreach (var msgFilter in filters.MessageCatalogs)
            {
                var set = msgFilter.Set;
                var min = msgFilter.Min;
                var max = msgFilter.Max;

                using (var cmd = new OracleCommand("SELECT MESSAGE_NBR, MESSAGE_TEXT,MSG_SEVERITY,LAST_UPDATE_DTTM,DESCRLONG FROM PSMSGCATDEFN WHERE MESSAGE_SET_NBR=:1 and MESSAGE_NBR >= :2 and MESSAGE_NBR <= :3 order by MESSAGE_NBR ASC", conn))
                {
                    cmd.Parameters.Add(new OracleParameter() { OracleDbType = OracleDbType.Int32, Value = set });
                    cmd.Parameters.Add(new OracleParameter() { OracleDbType = OracleDbType.Int32, Value = min });
                    cmd.Parameters.Add(new OracleParameter() { OracleDbType = OracleDbType.Int32, Value = max });

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var msgNumber = reader.GetInt32(0);
                            var msgText = reader.GetString(1);
                            var msgSev = reader.GetString(2);
                            var lastUpd = reader.GetDateTime(3);

                            var msgCatItem = new MessageCatalogItem();

                            msgCatItem.MessageNumber = msgNumber;
                            msgCatItem.MessageSet = set;
                            msgCatItem.MessageText = msgText;
                            msgCatItem.MessageSeverity = msgSev;
                            msgCatItem.LastUpdate = lastUpd;

                            selectedItems.Add(msgCatItem);
                        }
                    }
                }
            }
            return selectedItems.Count;
        }

        public void ProcessDeletes(string rootFolder)
        {
            var msgCatPath = Path.Combine(rootFolder, "Message Catalogs");

            if (Directory.Exists(msgCatPath))
            {
                Directory.Delete(msgCatPath, true);
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
            List<ChangedItem> changedItems = new List<ChangedItem>();

            var msgCatPath = Path.Combine(rootFolder, "Message Catalogs");
            if (Directory.Exists(msgCatPath) == false)
            {
                Directory.CreateDirectory(msgCatPath);
            }
            double total = selectedItems.Count;
            double current = 0;
            if (total == 0)
            {
                ReportProgress(100);
            }
            var setGroups = selectedItems.GroupBy(s => s.MessageSet);

            foreach (var set in setGroups)
            {
                var msgSet = new MessageCatalogSet();
                msgSet.MessageSet = set.Key;

                foreach (var item in set)
                {
                    FillLongAndTranslates(item);
                    msgSet.Messages.Add(item);
                    current++;
                    ReportProgress(((int)(((current / total) * 10000))/(double)100));
                }

                var jsonText = JsonConvert.SerializeObject(msgSet, Formatting.Indented);
                var filePath = Path.Combine(msgCatPath, set.Key + ".json");
                File.WriteAllText(filePath, jsonText);

                changedItems.Add(new ChangedItem() { FilePath = filePath, OperatorId = "MessageCats"});
            }
            return changedItems;
        }

        private void FillLongAndTranslates(MessageCatalogItem item)
        {
            using (var cmd = new OracleCommand("SELECT DESCRLONG FROM PSMSGCATDEFN WHERE MESSAGE_SET_NBR=:1 and MESSAGE_NBR = :2", _conn))
            {
                cmd.Parameters.Add(new OracleParameter() { OracleDbType = OracleDbType.Int32, Value = item.MessageSet });
                cmd.Parameters.Add(new OracleParameter() { OracleDbType = OracleDbType.Int32, Value = item.MessageNumber });

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        var clob = reader.GetOracleClob(0);
                        byte[] data = new byte[clob.Length];
                        clob.Read(data, 0, (int)clob.Length);
                        clob.Close();
                        item.Descrlong = Encoding.Unicode.GetString(data);
                    }
                    reader.Close();
                }
            }

            /* Get any translations */
            using (var translateCmd = new OracleCommand("SELECT LANGUAGE_CD, MESSAGE_TEXT, DESCRLONG FROM PSMSGCATLANG WHERE MESSAGE_SET_NBR = :1 AND MESSAGE_NBR = :2 ORDER BY LANGUAGE_CD ASC", _conn))
            {
                translateCmd.Parameters.Add(new OracleParameter() { OracleDbType = OracleDbType.Int32, Value = item.MessageSet });
                translateCmd.Parameters.Add(new OracleParameter() { OracleDbType = OracleDbType.Int32, Value = item.MessageNumber });
                using (var translateReader = translateCmd.ExecuteReader())
                {
                    while (translateReader.Read())
                    {
                        var translation = new MessageCatalogTranslation();
                        translation.LanguageCode = translateReader.GetString(0);
                        translation.MessageText = translateReader.GetString(1);

                        var translateClob = translateReader.GetOracleClob(2);
                        byte[] translateData = new byte[translateClob.Length];
                        translateClob.Read(translateData, 0, (int)translateClob.Length);
                        translateClob.Close();
                        translation.Descrlong = Encoding.Unicode.GetString(translateData);

                        item.Translations.Add(translation);
                    }
                    translateReader.Close();
                }
            }
        }
    }

    internal class MessageCatalogSet
    {
        public int MessageSet { get; set; }
        public List<MessageCatalogItem> Messages { get; set;}

        public MessageCatalogSet()
        {
            Messages = new List<MessageCatalogItem>();
        }
    }

    internal class MessageCatalogItem
    {
        [JsonIgnore]
        public int MessageSet { get; set; }

        public int MessageNumber { get; set; }
        public string MessageText { get; set; }
        public string MessageSeverity { get; set; }
        public DateTime LastUpdate { get; set; }
        public string Descrlong { get; set; }

        public List<MessageCatalogTranslation> Translations { get; set; }

        public MessageCatalogItem()
        {
            Translations = new List<MessageCatalogTranslation>();
        }

    }

    internal class MessageCatalogTranslation
    {
        public string LanguageCode { get; set; }
        public string MessageText { get; set; }
        public string Descrlong { get; set; }
    }
}
