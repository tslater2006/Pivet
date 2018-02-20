using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Oracle.ManagedDataAccess.Client;
using System.Collections;

namespace Pivet.Data.Processors
{
    internal class RegistryProcessor : IDataProcessor
    {
        public event ProgressHandler ProgressChanged;
        OracleConnection _conn;
        VersionState _versionState;

        HashSet<string> SelectedItems = new HashSet<string>();

        public int LoadItems(OracleConnection conn, FilterConfig filters, VersionState versionState)
        {
            _versionState = versionState;
            _conn = conn;
            /* TODO: Add Project filtering support */

            using (var itemLoad = new OracleCommand())
            {
                itemLoad.Parameters.Add(new OracleParameter() { OracleDbType = OracleDbType.Int32, Value = _versionState.PRSM.LastVersion });
                itemLoad.Parameters.Add(new OracleParameter() { OracleDbType = OracleDbType.Int32, Value = _versionState.PRSM.CurrentVersion });
                //itemLoad.Parameters.Add(new OracleParameter() { OracleDbType = OracleDbType.Int32, Value = modifyThreshold });
                itemLoad.Connection = conn;
                StringBuilder sb = new StringBuilder();

                /* Base query */
                sb.Append("SELECT PORTAL_OBJNAME, PORTAL_PRNTOBJNAME FROM PSPRSMDEFN WHERE VERSION > :1 AND VERSION <= :2 ");

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

                HashSet<string> parentsToResolve = new HashSet<string>();
                using (var reader = itemLoad.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        SelectedItems.Add(reader.GetString(0));
                    }
                }
            }
            return SelectedItems.Count;
        }

        public List<ChangedItem> ProcessDeletes(string rootFolder)
        {
            throw new NotImplementedException();
        }

        public List<ChangedItem> SaveToDisk(string rootFolder)
        {
            throw new NotImplementedException();
        }
    }

    internal class RegistryDefinition
    {
    }
}
