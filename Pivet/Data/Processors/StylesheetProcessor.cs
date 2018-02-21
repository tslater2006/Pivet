using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Oracle.ManagedDataAccess.Client;
using System.IO;

namespace Pivet.Data.Processors
{
    internal class StylesheetProcessor : IDataProcessor
    {
        private OracleConnection _conn;
        private List<StylesheetItem> _items = new List<StylesheetItem>();

        public event ProgressHandler ProgressChanged;

        //public int LoadItems(OracleConnection conn, FilterConfig filters, int modifyThreshold, VersionState versionState)
        public int LoadItems(OracleConnection conn, FilterConfig filters)
        {
            //TODO: Store version and load last checked version
            _conn = conn;
            /* should we go by projects? */
            using (var itemLoad = new OracleCommand())
            {
                //itemLoad.Parameters.Add(new OracleParameter() { OracleDbType = OracleDbType.Int32, Value = modifyThreshold });
                itemLoad.Connection = _conn;
                StringBuilder sb = new StringBuilder();
                if (filters.Projects != null && filters.Projects.Count > 0)
                {
                    sb.Append("select B.STYLESHEETNAME, B.LASTUPDOPRID from PSPROJECTITEM A, PSSTYLSHEETDEFN B where A.OBJECTTYPE = 50 AND A.OBJECTVALUE1 = B.STYLESHEETNAME AND A.PROJECTNAME IN (");
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
                    itemLoad.CommandText = "SELECT STYLESHEETNAME, LASTUPDOPRID FROM PSSTYLSHEETDEFN";
                }

                using (var reader = itemLoad.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        _items.Add(new StylesheetItem() { StylesheetName = reader.GetString(0), Oprid = reader.GetString(1) });
                    }
                }
            }
            ApplyFilters(filters);
            return _items.Count;
        }
        public void ProcessDeletes(string rootFolder)
        {
            var htmlRoot = Path.Combine(rootFolder, "Stylesheets");
            
            if (Directory.Exists(htmlRoot))
            {
                Directory.Delete(htmlRoot, true);
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
            var htmlRoot = Path.Combine(rootFolder, "Stylesheets");
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

                var fileName = GetFilePathForCSS(rootFolder, item.StylesheetName);

                File.WriteAllText(fileName, item.GetContents(_conn));
                changedItems.Add(new ChangedItem() { FilePath = fileName, OperatorId = item.Oprid});

                current++;
                ReportProgress(((int)(((current / total) * 10000))/(double)100));
            }
            return changedItems;
        }

        private string GetFilePathForCSS(string rootFolder, string htmlName)
        {
            var htmlRoot = Path.Combine(rootFolder, "Stylesheets");
            return Path.Combine(htmlRoot, htmlName + ".css");
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
                        if (_items[x].StylesheetName.StartsWith(prfx))
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
    }

    class StylesheetItem
    {
        internal string StylesheetName;
        internal string Oprid;

        internal string GetContents(OracleConnection conn)
        {
            MemoryStream data = new MemoryStream();
            using (var cmd = new OracleCommand("SELECT CONTDATA FROM PSCONTENT WHERE CONTTYPE = 9 AND CONTNAME = :1 order by SEQNUM ASC", conn))
            {
                OracleParameter contName = new OracleParameter();
                contName.OracleDbType = OracleDbType.Varchar2;
                contName.Value = StylesheetName;

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
