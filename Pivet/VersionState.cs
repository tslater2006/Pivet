using Newtonsoft.Json;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pivet
{
    public class VersionHolder
    {
        public int CurrentVersion { get; set; }
        public int LastVersion { get; set; }

        public VersionHolder(int current, int last)
        {
            CurrentVersion = current;
            LastVersion = last;
        }
    }
    public class VersionState
    {
        public string Description { get; set; }
        public VersionHolder PCM { get; set; }
        public VersionHolder CRM { get; set; }
        public VersionHolder SSM { get; set; }
        public VersionHolder SRM { get; set; }
        public VersionHolder PRSM { get; set; }
        /* Item1 of each Tuple is "current version" */
        /* Item2 of each Tuple is "last processed version" */

        public VersionState()
        {
            Description = "This file is used internally, please do not modify!";
            PCM = new VersionHolder(0,0);
            CRM = new VersionHolder(0,0);
            SSM = new VersionHolder(0, 0);
            SRM = new VersionHolder(0, 0);
            PRSM = new VersionHolder(0, 0);
        }

        public void UpdateAndSaveFromDB(OracleConnection conn, string rootDirectory)
        {
            using (var cmd = new OracleCommand("select OBJECTTYPENAME, VERSION from PSVERSION WHERE OBJECTTYPENAME in ('PCM','CRM', 'SSM', 'SRM', 'PRSM')", conn))
            {
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var newVersion = reader.GetInt32(1);
                        switch (reader.GetString(0))
                        {
                            case "PCM":
                                PCM.LastVersion = PCM.CurrentVersion;
                                PCM.CurrentVersion = newVersion;
                                break;
                            case "CRM":
                                CRM.LastVersion = CRM.CurrentVersion;
                                CRM.CurrentVersion = newVersion;
                                break;
                            case "SSM":
                                SSM.LastVersion = SSM.CurrentVersion;
                                SSM.CurrentVersion = newVersion;
                                break;
                            case "SRM":
                                SRM.LastVersion = SRM.CurrentVersion;
                                SRM.CurrentVersion = newVersion;
                                break;
                            case "PRSM":
                                PRSM.LastVersion = PRSM.CurrentVersion;
                                PRSM.CurrentVersion = newVersion;
                                break;
                        }
                    }
                }
            }
            var versionFilePath = Path.Combine(rootDirectory, "version.txt");

            File.WriteAllText(versionFilePath, JsonConvert.SerializeObject(this));
        }

    }
}
