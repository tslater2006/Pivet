using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Oracle.ManagedDataAccess.Client;
using System.IO;
using BasicSQLFormatter;

namespace Pivet.Data.Processors
{
    internal class SQLProcessor : IDataProcessor
    {
        public string ItemName => "SQL";
        public string ProcessorID => "SQLProcessor";
        public event ProgressHandler ProgressChanged;
        OracleConnection _conn;
        List<SQLItem> _items = new List<SQLItem>();

        public int LoadItems(OracleConnection conn, FilterConfig filters)
        {
            _conn = conn;
            using (var itemLoad = new OracleCommand())
            {
                itemLoad.Connection = conn;
                StringBuilder sb = new StringBuilder();
                if (filters.Projects != null && filters.Projects.Count > 0)
                {
                    sb.Append("select A.SQLID, A.SQLTYPE, A.LASTUPDOPRID, A.LASTUPDDTTM from PSSQLDEFN A, PSPROJECTITEM B WHERE B.OBJECTTYPE = 30 and B.OBJECTVALUE1 = A.SQLID and B.OBJECTVALUE2 = A.SQLTYPE AND B.PROJECTNAME in (");
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
                    sb.Append("select A.SQLID, A.SQLTYPE, A.LASTUPDOPRID, A.LASTUPDDTTM FROM PSSQLDEFN A");
                }
                itemLoad.CommandText = sb.ToString();

                using (var reader = itemLoad.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        if (reader.IsDBNull(3))
                        {
                            _items.Add(new SQLItem(reader.GetString(0), reader.GetString(1), reader.GetString(2), DateTime.MinValue));
                        }
                        else
                        {
                            _items.Add(new SQLItem(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetDateTime(3)));
                        }
                    }
                }

                ApplyFilters(filters);
            }

            return _items.Count;
        }

        private void ApplyFilters(FilterConfig filters)
        {
            /* project filter is handled by load on HTML Objects */

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
                        if (_items[x].SQLID.StartsWith(prfx))
                        {
                            shouldDiscard = false;
                            break;
                        }
                    }
                }

                if (!shouldDiscard && filters.IncludeOprids != null && filters.IncludeOprids.Count > 0)
                {
                    if (filters.IncludeOprids.Contains(_items[x].Oprid))
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
                    if (filters.ExcludeOprids.Contains(_items[x].Oprid))
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

        private string GetFilePathForSQL(string rootFolder, string sqlid, string sqltype)
        {
            var sqlRoot = Path.Combine(rootFolder, sqltype.Equals("6") ? "App Engine XSLT" :"SQL Objects");
            var path = "";
            switch (sqltype)
            {
                case "0":
                    path = Path.Combine(sqlRoot, "Standalone", sqlid + ".sql");
                    break;
                case "1":
                    path = Path.Combine(sqlRoot, "App Engine", sqlid + ".sql");
                    break;
                case "2":
                    path = Path.Combine(sqlRoot, "Views", sqlid + ".sql");
                    break;
                case "5":
                    path = Path.Combine(sqlRoot, "Audits", sqlid + ".sql");
                    break;
                case "6":
                    path = Path.Combine(sqlRoot, sqlid + ".xslt");
                    break;
                default:
                    path = Path.Combine(sqlRoot, "Type " + sqltype, sqlid + ".sql");
                    break;

            }
            return path;
        }

        public void ProcessDeletes(string rootFolder)
        {
            var sqlRoot = Path.Combine(rootFolder, "SQL Objects");
            if (Directory.Exists(sqlRoot))
            {
                Directory.Delete(sqlRoot, true);
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
            ReportProgress(0);
            var sqlRoot = Path.Combine(rootFolder, "SQL Objects");
            Directory.CreateDirectory(sqlRoot);
            List<ChangedItem> changedItems = new List<ChangedItem>();
            double total = _items.Count;
            double current = 0;
            if (total == 0)
            {
                ReportProgress(100);
            }
            foreach (var item in _items)
            {
                var fileName = GetFilePathForSQL(rootFolder, item.SQLID, item.SqlType).CleanFilePath();
                Directory.CreateDirectory(new FileInfo(fileName).Directory.FullName);

                var sqlText = item.GetContents(_conn);

                var formattedText = new SQLFormatter(sqlText).Format();

                File.WriteAllText(fileName, formattedText);

                changedItems.Add(new ChangedItem(fileName, item.Oprid));

                current++;
                ReportProgress(((int)(((current / total) * 10000)) / (double)100));

            }
            return changedItems;
        }
    }

    class SQLItem
    {
        internal string SQLID;
        internal string SqlType;
        internal string Oprid;
        internal DateTime LastUpdate;

        public SQLItem(string id, string type, string opr, DateTime dttm)
        {
            SQLID = id;
            SqlType = type;
            Oprid = opr;
            LastUpdate = dttm;
        }

        internal string GetContents(OracleConnection conn)
        {
            MemoryStream data = new MemoryStream();
            using (var cmd = new OracleCommand("SELECT SQLTEXT FROM PSSQLTEXTDEFN WHERE SQLID = :1 order by SEQNUM ASC", conn))
            {
                OracleParameter contName = new OracleParameter();
                contName.OracleDbType = OracleDbType.Varchar2;
                contName.Value = SQLID;

                cmd.Parameters.Add(contName);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var blob = reader.GetOracleClob(0);
                        blob.CopyTo(data);
                        blob.Close();
                    }
                }
            }

            return Encoding.Unicode.GetString(data.ToArray()).Split('\0')[0];
        }
    }
}
