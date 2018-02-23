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

        public string ItemName => "HTML";

        public string ProcessorID => "HTMLProcessor";

        public event ProgressHandler ProgressChanged;

        //public int LoadItems(OracleConnection conn, FilterConfig filters, int modifyThreshold, VersionState versionState)
        public int LoadItems(OracleConnection conn, FilterConfig filters)
        {
            //TODO: Store version and load last checked version
            _conn = conn;
            /* should we go by projects? */
            using (var itemLoad = new OracleCommand()) 
            {
                itemLoad.Connection = _conn;
                StringBuilder sb = new StringBuilder();
                if (filters.Projects != null && filters.Projects.Count > 0)
                {
                    sb.Append("select B.CONTNAME, B.LASTUPDOPRID from PSPROJECTITEM A, PSCONTDEFN B where A.OBJECTTYPE = 51 AND A.OBJECTVALUE1 = B.CONTNAME and A.OBJECTVALUE2 = 4 AND A.PROJECTNAME IN (");
                    for (var x = 0; x < filters.Projects.Count; x++)
                    {
                        sb.Append(":" + (x + 1) + ",");
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
                    itemLoad.CommandText = "SELECT CONTNAME, LASTUPDOPRID FROM PSCONTDEFN WHERE CONTTYPE = 4";
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
                changedItems.Add(new ChangedItem() { FilePath = fileName, OperatorId = item.Oprid});
                current++;
                ReportProgress(((int)(((current / total) * 10000))/(double)100));

            }
            return changedItems;
        }

        public void ProcessDeletes(string rootFolder)
        {
            var msgCatPath = Path.Combine(rootFolder, "HTML Objects");

            if (Directory.Exists(msgCatPath))
            {
                Directory.Delete(msgCatPath, true);
            }
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
