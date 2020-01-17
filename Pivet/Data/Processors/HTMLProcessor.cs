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

        public string ItemName => "HTML";

        public string ProcessorID => "HTMLProcessor";

        public event ProgressHandler ProgressChanged;

        public int SaveItems(OracleConnection conn, FilterConfig filters, string outputFolder)
        {
            _conn = conn;

            /* Prep working directory for processor */
            ReportProgress(0);
            var htmlRoot = Path.Combine(outputFolder, "HTML Objects");
            Directory.CreateDirectory(htmlRoot);
            var savedItemCount = 0;
            /* Determine SQL to load definitions */
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
                    itemLoad.CommandText = "SELECT CONTNAME, LASTUPDOPRID FROM PSCONTDEFN WHERE CONTTYPE = 4";
                }
                using (var reader = itemLoad.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var htmlName = reader.GetString(0);
                        var oprid = reader.GetString(1);
                        if (ApplyFilters(filters, htmlName,oprid))
                        {
                            /* save it to disk */
                            savedItemCount++;

                            var fileName = GetFilePathForHTML(outputFolder, reader.GetString(0));


                            using (MemoryStream data = new MemoryStream())
                            {
                                using (var cmd = new OracleCommand("SELECT CONTDATA FROM PSCONTENT WHERE CONTTYPE = 4 AND CONTNAME = :1 order by SEQNUM ASC", conn))
                                {
                                    OracleParameter contName = new OracleParameter();
                                    contName.OracleDbType = OracleDbType.Varchar2;
                                    contName.Value = htmlName;

                                    cmd.Parameters.Add(contName);
                                    using (var dataReader = cmd.ExecuteReader())
                                    {
                                        while (dataReader.Read())
                                        {
                                            var blob = dataReader.GetOracleBlob(0);
                                            blob.CopyTo(data);
                                            blob.Close();
                                        }
                                    }
                                }

                                var htmlText = Encoding.Unicode.GetString(data.ToArray()).Replace('\0', ' ').Trim();
                                File.WriteAllText(fileName, htmlText);
                            }

                        }
                    }
                }
            }
            return savedItemCount;
        }

        private void ReportProgress(double progress)
        {
            if (ProgressChanged != null)
            {
                ProgressChanged(new ProgressEvent() { Progress = progress });
            }
        }

        public void Cleanup(string rootFolder)
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

        private bool ApplyFilters(FilterConfig filters, string htmlName, string oprid)
        {
            /* project filter is handled by load on HTML Objects */
            var shouldDiscard = true;
            if (filters.Prefixes == null || filters.Prefixes.Count == 0)
            {
                shouldDiscard = false;
            }
            else
            {
                foreach (var prfx in filters.Prefixes)
                {
                    if (htmlName.StartsWith(prfx))
                    {
                        shouldDiscard = false;
                        break;
                    }
                }
            }

            if (!shouldDiscard && filters.IncludeOprids != null && filters.IncludeOprids.Count > 0)
            {
                if (filters.IncludeOprids.Contains(oprid))
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
                if (filters.ExcludeOprids.Contains(oprid))
                {
                    shouldDiscard = true;
                }
                else
                {
                    shouldDiscard = false;
                }
            }
            return !shouldDiscard;
        }
    }
}
