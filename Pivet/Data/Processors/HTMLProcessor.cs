using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Oracle.ManagedDataAccess.Client;
using System.IO;

namespace Pivet.Data.Processors
{
    internal class HTMLProcessor : IDataProcessor
    {
        private OracleConnection _conn;
        private List<HTMLItem> _items = new List<HTMLItem>();
        private VersionState _versionState;

        public event ProgressHandler ProgressChanged;

        //public int LoadItems(OracleConnection conn, FilterConfig filters, int modifyThreshold, VersionState versionState)
        public int LoadItems(OracleConnection conn, FilterConfig filters, VersionState versionState)
        {
            //TODO: Store version and load last checked version
            _conn = conn;
            _versionState = versionState;
            /* should we go by projects? */
            using (var itemLoad = new OracleCommand()) 
            {
                itemLoad.Parameters.Add(new OracleParameter() { OracleDbType = OracleDbType.Int32, Value = _versionState.CRM.LastVersion });
                itemLoad.Parameters.Add(new OracleParameter() { OracleDbType = OracleDbType.Int32, Value = _versionState.CRM.CurrentVersion });
                itemLoad.Connection = _conn;
                StringBuilder sb = new StringBuilder();
                if (filters.Projects != null && filters.Projects.Count > 0)
                {
                    //sb.Append("select B.CONTNAME, B.LASTUPDOPRID from PSPROJECTITEM A, PSCONTDEFN B where A.OBJECTTYPE = 51 AND A.OBJECTVALUE1 = B.CONTNAME and A.OBJECTVALUE2 = 4 and B.VERSION > :1 AND B.VERSION <= :2 AND B.LASTUPDDTTM < SYSDATE - :3/(24*60) AND A.PROJECTNAME IN (");
                    sb.Append("select B.CONTNAME, B.LASTUPDOPRID from PSPROJECTITEM A, PSCONTDEFN B where A.OBJECTTYPE = 51 AND A.OBJECTVALUE1 = B.CONTNAME and A.OBJECTVALUE2 = 4 and B.VERSION > :1 AND B.VERSION <= :2 AND A.PROJECTNAME IN (");
                    for (var x = 0; x < filters.Projects.Count; x++)
                    {
                        sb.Append(":" + (x + 3) + ",");
                    }
                    sb.Length--;
                    sb.Append(")");
                    itemLoad.CommandText = sb.ToString();
                    foreach (var proj in filters.Projects)
                    {
                        itemLoad.Parameters.Add(new OracleParameter() { OracleDbType = OracleDbType.Varchar2, Value = proj });
                    }
                }
                else
                {
                    //itemLoad.CommandText = "SELECT CONTNAME, LASTUPDOPRID FROM PSCONTDEFN WHERE CONTTYPE = 4 AND VERSION > :1 AND VERSION <= :2 AND LASTUPDDTTM < SYSDATE - :3/(24*60)";
                    itemLoad.CommandText = "SELECT CONTNAME, LASTUPDOPRID FROM PSCONTDEFN WHERE CONTTYPE = 4 AND VERSION > :1 AND VERSION <= :2";
                }

                using (var reader = itemLoad.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        _items.Add(new HTMLItem() { HtmlName = reader.GetString(0), Oprid = reader.GetString(1) });
                    }
                }
            }
            ApplyFilters(filters);
            return _items.Count;
        }
        public List<ChangedItem> ProcessDeletes(string rootFolder)
        {
            List<ChangedItem> deletes = new List<ChangedItem>();

            using (var cmd = new OracleCommand("SELECT CONTNAME FROM PSCONTDEL WHERE CONTTYPE = 4 and VERSION > :1 and VERSION <= :2", _conn))
            {
                cmd.Parameters.Add(new OracleParameter() { OracleDbType = OracleDbType.Int32, Value = _versionState.CRM.LastVersion });
                cmd.Parameters.Add(new OracleParameter() { OracleDbType = OracleDbType.Int32, Value = _versionState.CRM.CurrentVersion });

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        deletes.Add(new ChangedItem() { FilePath = GetFilePathForHTML(rootFolder, reader.GetString(0)), State = ChangedItemState.DELETE });
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
            var htmlRoot = Path.Combine(rootFolder, "HTML Objects");
            Directory.CreateDirectory(htmlRoot);
            List<ChangedItem> changedItems = new List<ChangedItem>();
            double total = _items.Count;
            double current = 0;
            if (total == 0)
            {
                ReportProgress(100);
            }
            foreach (var item in _items)
            {
                var fileName = GetFilePathForHTML(rootFolder, item.HtmlName);

                File.WriteAllText(fileName,item.GetContents(_conn));
                changedItems.Add(new ChangedItem() { FilePath = fileName, OperatorId = item.Oprid, State = ChangedItemState.CREATE });

                current++;
                ReportProgress(((int)(((current / total) * 10000))/(double)100));

            }
            return changedItems;
        }

        private string GetFilePathForHTML(string rootFolder, string htmlName)
        {
            var htmlRoot = Path.Combine(rootFolder, "HTML Objects");
            return Path.Combine(htmlRoot, htmlName + ".html");
        }

        private void ApplyFilters(FilterConfig filters)
        {
            /* project filter is handled by load on HTML Objects */

            for (var x = _items.Count-1; x>= 0; x--)
            {
                var shouldDiscard = true;

                if (filters.Prefixes == null || filters.Prefixes.Count == 0)
                {
                    shouldDiscard = false;
                } else
                {
                    foreach (var prfx in filters.Prefixes)
                    {
                        if (_items[x].HtmlName.StartsWith(prfx))
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
                    } else
                    {
                        shouldDiscard = true;
                    }
                }

                if (!shouldDiscard && filters.ExcludeOprids != null && filters.ExcludeOprids.Count > 0)
                {
                    if (filters.ExcludeOprids.Contains(_items[x].Oprid))
                    {
                        shouldDiscard = true;
                    } else
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
    }

    class HTMLItem
    {
        internal string HtmlName;
        internal string Oprid;

        internal string GetContents(OracleConnection conn)
        {
            MemoryStream data = new MemoryStream();
            using (var cmd = new OracleCommand("SELECT CONTDATA FROM PSCONTENT WHERE CONTTYPE = 4 AND CONTNAME = :1 order by SEQNUM ASC",conn))
            {
                OracleParameter contName = new OracleParameter();
                contName.OracleDbType = OracleDbType.Varchar2;
                contName.Value = HtmlName;

                cmd.Parameters.Add(contName);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var blob = reader.GetOracleBlob(0);
                        blob.CopyTo(data);
                        blob.Close();
                    }
                }
            }

            return Encoding.Unicode.GetString(data.ToArray()).Replace('\0', ' ').Trim(); ;
        }
    }
}
