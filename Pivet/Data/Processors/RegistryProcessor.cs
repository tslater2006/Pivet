using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Oracle.ManagedDataAccess.Client;
using System.Collections;
using System.IO;
using Newtonsoft.Json;

namespace Pivet.Data.Processors
{
    internal class ContentReference
    {
        internal string ID;
        internal string Portal;
        internal string Path;
        internal string LastUpdOprid;
        /* data goes here */
        /* lots of fields, probably easier just to reuse RawData structures/methods for it */
        internal RawDataItem Data;
    }

    internal class RegistryProcessor : IDataProcessor
    {
        public string ItemName => "Registry";
        public string ProcessorID => "RegistryProcessor";
        public event ProgressHandler ProgressChanged;
        OracleConnection _conn;

        List<ContentReference> SelectedCREFs = new List<ContentReference>();
        public int LoadItems(OracleConnection conn, FilterConfig filters)
        {
            _conn = conn;

            using (var itemLoad = new OracleCommand())
            {
                itemLoad.Connection = conn;
                StringBuilder sb = new StringBuilder();

                /* Base query */

                /* This SQL is taken from http://jjmpsj.blogspot.com/2012/10/query-for-component-andor-cref.html (and slightly adjusted) */
                sb.Append("WITH PORTAL_REGISTRY (PORTAL_NAME, PORTAL_REFTYPE, PORTAL_OBJNAME, PORTAL_LABEL, PORTAL_URI_SEG1, PORTAL_URI_SEG2, PORTAL_URI_SEG3, LASTUPDOPRID, PATH) AS (SELECT P.PORTAL_NAME , P.PORTAL_REFTYPE , P.PORTAL_OBJNAME , P.PORTAL_LABEL , PORTAL_URI_SEG1 , PORTAL_URI_SEG2 , PORTAL_URI_SEG3 , LASTUPDOPRID, P.PORTAL_LABEL AS PATH FROM PSPRSMDEFN P WHERE P.PORTAL_PRNTOBJNAME = ' ' UNION ALL SELECT P_ONE.PORTAL_NAME , P_ONE.PORTAL_REFTYPE , P_ONE.PORTAL_OBJNAME , P_ONE.PORTAL_LABEL , P_ONE.PORTAL_URI_SEG1 , P_ONE.PORTAL_URI_SEG2 , P_ONE.PORTAL_URI_SEG3 , P_ONE.LASTUPDOPRID, PATH || ' -> ' || P_ONE.PORTAL_OBJNAME AS PATH FROM PORTAL_REGISTRY P INNER JOIN PSPRSMDEFN P_ONE ON P.PORTAL_NAME = P_ONE.PORTAL_NAME AND P.PORTAL_REFTYPE = 'F' AND P.PORTAL_OBJNAME = P_ONE.PORTAL_PRNTOBJNAME WHERE P_ONE.PORTAL_PRNTOBJNAME != ' ') SELECT PORTAL_NAME , PORTAL_OBJNAME , PATH, LASTUPDOPRID FROM PORTAL_REGISTRY WHERE PORTAL_REFTYPE != 'F' ");

                if (filters.Prefixes.Count > 0)
                {
                    sb.Append(" AND (");

                    for (var x = 0; x < filters.Prefixes.Count; x++)
                    {
                        sb.Append("PORTAL_OBJNAME like '");
                        sb.Append(filters.Prefixes[x]);
                        sb.Append("%'");
                        if (x < filters.Prefixes.Count - 1)
                        {
                            sb.Append(" OR ");
                        }
                    }

                    sb.Append(")");
                }

                itemLoad.CommandText = sb.ToString();
                using (var reader = itemLoad.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var newCref = new ContentReference() { Portal = reader.GetString(0), ID = reader.GetString(1), Path = reader.GetString(2), LastUpdOprid = reader.GetString(3) };

                        /* Todo: fetch CREF data as JSON */

                        using (OracleCommand crefDataSql = new OracleCommand($"SELECT * FROM PSPRSMDEFN WHERE PORTAL_OBJNAME = :1 and PORTAL_NAME = :2", _conn))
                        {
                            OracleParameter crefID = new OracleParameter();
                            crefID.OracleDbType = OracleDbType.Varchar2;
                            crefID.Value = newCref.ID;
                            crefDataSql.Parameters.Add(crefID);

                            OracleParameter portalName = new OracleParameter();
                            portalName.OracleDbType = OracleDbType.Varchar2;
                            portalName.Value = newCref.Portal;
                            crefDataSql.Parameters.Add(portalName);

                            using (var dataReader = crefDataSql.ExecuteReader())
                            {
                                var dataTable = dataReader.GetSchemaTable();
                                while (dataReader.Read())
                                {
                                    newCref.Data = RawDataProcessor.DataItemFromReader(dataTable, dataReader);
                                }
                            }
                        }

                        SelectedCREFs.Add(newCref);
                    }
                }

                

            }
            return SelectedCREFs.Count;
        }

        public void ProcessDeletes(string rootFolder)
        {
            var crefDirectory = Path.Combine(rootFolder, "Content References");

            if (Directory.Exists(crefDirectory))
            {
                Directory.Delete(crefDirectory, true);
            }
        }

        public List<ChangedItem> SaveToDisk(string rootFolder)
        {
            List<ChangedItem> changedItems = new List<ChangedItem>();
            var crefDirectory = Path.Combine(rootFolder, "Content References");

            foreach (ContentReference cref in SelectedCREFs)
            {
                /* get relative path */
                var crefPath = string.Join(Path.DirectorySeparatorChar, cref.Path.Split(" -> ").Skip(1).ToArray());
                crefPath += ".cref";


                crefPath = Path.Combine(crefDirectory, crefPath);

                /* ensure the folder exists */
                var directory = crefPath.Substring(0, crefPath.LastIndexOf(Path.DirectorySeparatorChar));

                Directory.CreateDirectory(directory);

                File.WriteAllText(crefPath, JsonConvert.SerializeObject(cref.Data, Formatting.Indented));

                changedItems.Add(new ChangedItem(crefPath, cref.LastUpdOprid));
            }
            return changedItems;
        }

        private void ReportProgress(double progress)
        {
            ProgressChanged?.Invoke(new ProgressEvent() { Progress = progress });
        }
    }

    internal class RegistryDefinition
    {
    }
}
