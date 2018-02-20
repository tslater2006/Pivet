using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Oracle.ManagedDataAccess.Client;
using System.IO;

namespace Pivet.Data.Processors
{
    internal class SQLProcessor : IDataProcessor
    {
        public event ProgressHandler ProgressChanged;
        OracleConnection _conn;
        List<SQLItem> _items = new List<SQLItem>();
        VersionState _versionState;

        public int LoadItems(OracleConnection conn, FilterConfig filters, VersionState versionState)
        {
            _versionState = versionState;
            _conn = conn;
            using (var itemLoad = new OracleCommand())
            {
                itemLoad.Connection = conn;

                itemLoad.Parameters.Add(new OracleParameter() { OracleDbType = OracleDbType.Int32, Value = _versionState.SRM.LastVersion });
                itemLoad.Parameters.Add(new OracleParameter() { OracleDbType = OracleDbType.Int32, Value = _versionState.SRM.CurrentVersion });

                StringBuilder sb = new StringBuilder();
                if (filters.Projects != null && filters.Projects.Count > 0)
                {
                    //sb.Append("select A.FIELDNAME , A.FIELDVALUE, A.EFFDT, A.EFF_STATUS, A.XLATLONGNAME, A.XLATSHORTNAME, A.LASTUPDDTTM, A.LASTUPDOPRID from PSXLATITEM A, PSPROJECTITEM B WHERE B.OBJECTTYPE = 4 and B.OBJECTVALUE1 = A.FIELDNAME and B.OBJECTVALUE2 = A.FIELDVALUE and B.PROJECTNAME in (");
                    sb.Append("select A.SQLID, A.SQLTYPE, A.LASTUPDOPRID, A.LASTUPDDTTM from PSSQLDEFN A, PSPROJECTITEM B WHERE B.OBJECTTYPE = 30 and B.OBJECTVALUE1 = A.SQLID and B.OBJECTVALUE2 = A.SQLTYPE AND A.VERSION > :1 AND A.VERSION <= :2 AND B.PROJECTNAME in (");
                    for (var x = 0; x < filters.Projects.Count; x++)
                    {
                        sb.Append(":" + (x + 3) + ",");
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
                    sb.Append("select A.SQLID, A.SQLTYPE, A.LASTUPDOPRID, A.LASTUPDDTTM FROM PSSQLDEFN A WHERE A.VERSION > :1 AND A.VERSION <= :2");
                }
                itemLoad.CommandText = sb.ToString();

                using (var reader = itemLoad.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        _items.Add(new SQLItem(reader.GetString(0),reader.GetString(1),reader.GetString(2),reader.GetDateTime(3)));
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
            var sqlRoot = Path.Combine(rootFolder, "SQL Objects");
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
                    path = Path.Combine(sqlRoot, "App Engine XML", sqlid + ".sql");
                    break;
                default:
                    path = Path.Combine(sqlRoot, "Type " + sqltype, sqlid + ".sql");
                    break;

            }
            return path;
        }

        public List<ChangedItem> ProcessDeletes(string rootFolder)
        {
            List<ChangedItem> deletes = new List<ChangedItem>();

            using (var cmd = new OracleCommand("SELECT SQLID, SQLTYPE FROM PSSQLDEL WHERE VERSION > :1 and VERSION <= :2", _conn))
            {
                cmd.Parameters.Add(new OracleParameter() { OracleDbType = OracleDbType.Int32, Value = _versionState.SRM.LastVersion });
                cmd.Parameters.Add(new OracleParameter() { OracleDbType = OracleDbType.Int32, Value = _versionState.SRM.CurrentVersion });

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        deletes.Add(new ChangedItem() { FilePath = GetFilePathForSQL(rootFolder, reader.GetString(0),reader.GetString(1)), State = ChangedItemState.DELETE });
                    }
                }
            }

            return deletes;
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
                var fileName = GetFilePathForSQL(rootFolder, item.SQLID, item.SqlType);
                Directory.CreateDirectory(new FileInfo(fileName).Directory.FullName);

                 File.WriteAllText(fileName, item.GetContents(_conn));

                changedItems.Add(new ChangedItem() { FilePath = fileName, OperatorId = item.Oprid, State = ChangedItemState.CREATE });

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
