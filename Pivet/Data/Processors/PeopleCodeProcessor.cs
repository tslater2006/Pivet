using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Oracle.ManagedDataAccess.Client;
using System.IO;
using PeopleCodeLib.Decoder;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace Pivet.Data.Processors
{
    internal class PeopleCodeProcessor : IDataProcessor
    {
        private OracleConnection _conn;
        private List<PeopleCodeItem> selectedItems = new List<PeopleCodeItem>();

        public event ProgressHandler ProgressChanged;

        //public int LoadItems(OracleConnection conn, FilterConfig filters, int modifyThreshold, VersionState versionState)
        public int LoadItems(OracleConnection conn, FilterConfig filters)
        {
            Logger.Write("Loading Peoplecode Definitions.");
            _conn = conn;
            LoadAllItemsFromDB();

            if (filters.Projects != null && filters.Projects.Count > 0)
            {
                FilterByProjects(filters.Projects);
            }
            if (filters.Prefixes != null && filters.Prefixes.Count > 0)
            {
                FilterByPrefix(filters.Prefixes);
            }
            if (filters.IncludeOprids != null && filters.IncludeOprids.Count > 0)
            {
                FilterByIncludeOprid(filters.IncludeOprids);
            }
            if (filters.ExcludeOprids != null && filters.ExcludeOprids.Count > 0)
            {
                FilterByExcludeOprid(filters.ExcludeOprids);
            }

            return selectedItems.Count;
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
            List<ChangedItem> changes = new List<ChangedItem>();

            var ppcDirectory = Path.Combine(rootFolder, "PeopleCode");

            double total = selectedItems.Count;
            double current = 0;

            if (total == 0)
            {
                ReportProgress(100);
            }

            foreach (var i in selectedItems)
            {
                if (i.SaveToDirectory(_conn, ppcDirectory)) {
                    changes.Add(new ChangedItem() { FilePath = i.FilePath, OperatorId = i.lastOprid});
                }

                current++;
                ReportProgress(((int)(((current / total) * 10000))/(double)100));
            }
            return changes;
        }

        public void ProcessDeletes(string rootFolder)
        {
            var ppcDirectory = Path.Combine(rootFolder, "PeopleCode");

            if (Directory.Exists(ppcDirectory))
            {
                Directory.Delete(ppcDirectory, true);
            }
        }

        private void LoadAllItemsFromDB()
        {
            using (var cmd = new OracleCommand("SELECT OBJECTID1, OBJECTVALUE1, OBJECTID2, OBJECTVALUE2, OBJECTID3, OBJECTVALUE3, OBJECTID4, OBJECTVALUE4, OBJECTID5, OBJECTVALUE5, OBJECTID6, OBJECTVALUE6, OBJECTID7, OBJECTVALUE7, LASTUPDDTTM, LASTUPDOPRID from PSPCMPROG WHERE PROGSEQ = 0", _conn))
            {

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        List<Tuple<int, string>> keys = new List<Tuple<int, string>>();
                        keys.Add(new Tuple<int, string>(reader.GetInt32(0), reader.GetString(1)));
                        keys.Add(new Tuple<int, string>(reader.GetInt32(2), reader.GetString(3)));
                        keys.Add(new Tuple<int, string>(reader.GetInt32(4), reader.GetString(5)));
                        keys.Add(new Tuple<int, string>(reader.GetInt32(6), reader.GetString(7)));
                        keys.Add(new Tuple<int, string>(reader.GetInt32(8), reader.GetString(9)));
                        keys.Add(new Tuple<int, string>(reader.GetInt32(10), reader.GetString(11)));
                        keys.Add(new Tuple<int, string>(reader.GetInt32(12), reader.GetString(13)));

                        var lastUpdateTime = reader.GetDateTime(14);
                        var lastOprid = reader.GetString(15);

                        selectedItems.Add(new PeopleCodeItem(keys, lastOprid, lastUpdateTime));
                    }
                }
            }
        }

        private void FilterByProjects(List<string> projectNames)
        {
            List<ProjectItem> allItems = new List<ProjectItem>();
            foreach (var p in projectNames)
            {
                var proj = new Project(_conn, p);

                var ppcProjectItems = proj.GetPeopleCodeEntries();
                allItems.AddRange(ppcProjectItems);
            }

            for (var x = selectedItems.Count - 1; x >= 0; x--)
            {
                var itemFound = false;
                var currSelectedItem = selectedItems[x];
                foreach (var projectItem in allItems)
                {

                    if (currSelectedItem.EqualTo(projectItem))
                    {
                        itemFound = true;
                        break;
                    }
                }

                if (itemFound == false)
                {
                    selectedItems.RemoveAt(x);
                }
            }
        }
        
        private void FilterByPrefix(List<string> prefixList)
        {
            for (var x = selectedItems.Count - 1; x >= 0; x--)
            {
                var shouldRemove = true;
                foreach(var prfx in prefixList)
                {
                    if (selectedItems[x].MatchesPrefix(prfx)) {
                        shouldRemove = false;
                        break;
                    }
                }
                
                if (shouldRemove)
                {
                    selectedItems.RemoveAt(x);
                }
            }
        }

        private void FilterByIncludeOprid(List<string> opridList)
        {
            for (var x = selectedItems.Count - 1; x >= 0; x--)
            {
                var shouldRemove = true;
                foreach (var oprid in opridList)
                {
                    if (selectedItems[x].MatchesOprid(oprid))
                    {
                        shouldRemove = false;
                        break;
                    }
                }

                if (shouldRemove)
                {
                    selectedItems.RemoveAt(x);
                }
            }
        }

        private void FilterByExcludeOprid(List<string> opridList)
        {
            for (var x = selectedItems.Count - 1; x >= 0; x--)
            {
                var shouldRemove = false;
                foreach (var oprid in opridList)
                {
                    if (selectedItems[x].MatchesOprid(oprid))
                    {
                        shouldRemove = true;
                        break;
                    }
                }

                if (shouldRemove)
                {
                    selectedItems.RemoveAt(x);
                }
            }
        }
    }

    class PeopleCodeItem
    {
        private string _programText;
        internal List<Tuple<int, string>> Keys;
        public string lastOprid;
        DateTime lastUpdate;
        private bool _saved;
        public bool Saved { get { return _saved; } }
        public string FilePath;
        public string Name
        {
            get
            {
                var sb = new StringBuilder();
                foreach (var i in Keys)
                {
                    if (i.Item2.Length > 0)
                    {
                        sb.Append(i.Item2).Append(".");
                    }
                }
                sb.Length--;
                return sb.ToString();
            }
        }

        public string ProgramText
        {
            get
            {
                return _programText;
            }
        }

        public PeopleCodeItem(List<Tuple<int, string>> keys, string oprid, DateTime lastUpdate)
        {
            this.Keys = keys;
            this.lastOprid = oprid;
            this.lastUpdate = lastUpdate;
        }

        public void LoadFromDB(OracleConnection conn)
        {
            _programText = Parser.GetProgramByKeys(conn, Keys);
        }
        public string GetFullFilePath(string rootPath)
        {
            var rootFolder = "";
            var relativePath = new StringBuilder();
            var keyOffset = 0;
            var fileName = "";
            var separator = Path.DirectorySeparatorChar;
            switch (Keys[0].Item1)
            {
                case 1:
                    rootFolder = "Records";
                    break;
                case 3:
                    rootFolder = "Menus";
                    break;
                case 9:
                    rootFolder = "Pages";
                    break;
                case 10:
                    rootFolder = "Components";
                    break;
                case 60:
                    rootFolder = "Messages";
                    break;
                case 66:
                    rootFolder = "App Engines";
                    break;
                case 74:
                    rootFolder = "Component Interfaces";
                    break;
                case 104:
                    rootFolder = "Application Packages";
                    keyOffset = 1;
                    break;
                default:
                    rootFolder = "Unknown";
                    break;

            }
            relativePath.Append(rootFolder).Append(separator);
            var populatedKeyCount = 0;
            for (var x = 0; x < 7; x++)
            {
                if (Keys[x].Item1 == 0)
                {
                    break;
                }
                else
                {
                    populatedKeyCount++;
                }
            }

            for (var x = 0; x < populatedKeyCount - (1 + keyOffset); x++)
            {
                relativePath.Append(MakeValidDirectory(Keys[x].Item2)).Append(separator);
            }

            relativePath.Length--;

            fileName = Keys[populatedKeyCount - (1 + keyOffset)].Item2 + ".pcode";

            var fullPath = rootPath + separator + relativePath;
            return fullPath + separator + fileName;
        }
        internal bool SaveToDirectory(OracleConnection conn, string outputPath)
        {
            var separator = Path.DirectorySeparatorChar;

           
            /* load the PPC from the DB */
            LoadFromDB(conn);

            
            this.FilePath = GetFullFilePath(outputPath);

            /* get the directory */
            var directory = this.FilePath.Substring(0, this.FilePath.LastIndexOf(Path.DirectorySeparatorChar));

            Directory.CreateDirectory(directory);
            

            File.WriteAllText(this.FilePath, _programText);
            _programText = null;
            _saved = true;
            return true;
        }
        internal string MakeValidDirectory(string directory)
        {
            string invalidChars = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
            string invalidRegStr = string.Format(@"([{0}]*\.+$)|([{0}]+)", invalidChars);

            return Regex.Replace(directory, invalidRegStr, "_");
        }
        internal bool MatchesPrefix(string prfx)
        {
            var prefixes = prfx.Split(',');
            var matches = false;
            foreach (string s in prefixes)
            {
                if (Keys[0].Item2.StartsWith(s))
                {
                    matches = true;
                    break;
                }
            }
            return matches;
        }

        internal bool MatchesOprid(string oprid)
        {
            var operators = oprid.Split(',');
            var matches = false;
            foreach (string s in operators)
            {
                if (lastOprid.Equals(s))
                {
                    matches = true;
                    break;
                }
            }
            return matches;
        }

        internal bool EqualTo(ProjectItem projectItem)
        {
            if (projectItem.ObjectIDs[0] != this.Keys[0].Item1)
            {
                return false;
            }

            var objectType = this.Keys[0].Item1;

            if (objectType == 104)
            {
                var isMatch = true;

                for (var x = 0; x < 4; x++)
                {
                    if (projectItem.ObjectValues[x].Equals(Keys[x].Item2) == false && Keys[x].Item2.Equals("OnExecute") == false)
                    {
                        isMatch = false;
                        break;
                    }
                }

                return isMatch;
            }
            else if (objectType == 66)
            {
                if (projectItem.ObjectIDs[0] != Keys[0].Item1 || projectItem.ObjectValues[0].Equals(Keys[0].Item2) == false)
                {
                    return false;
                }

                var ppcItemComposite = Keys[1].Item2;
                while (ppcItemComposite.Length < 8)
                {
                    ppcItemComposite = ppcItemComposite + " ";
                }

                ppcItemComposite += Keys[2].Item2;
                while (ppcItemComposite.Length < 11)
                {
                    ppcItemComposite = ppcItemComposite + " ";
                }
                ppcItemComposite += Keys[3].Item2;
                while (ppcItemComposite.Length < 20)
                {
                    ppcItemComposite = ppcItemComposite + " ";
                }
                ppcItemComposite += Keys[4].Item2;

                if (projectItem.ObjectValues[1].Equals(ppcItemComposite) == false)
                {
                    return false;
                }

                if (projectItem.ObjectIDs[2] != Keys[5].Item1 || projectItem.ObjectValues[2].Equals(Keys[5].Item2) == false)
                {
                    return false;
                }

                if (projectItem.ObjectIDs[3] != Keys[6].Item1 || projectItem.ObjectValues[3].Equals(Keys[6].Item2) == false)
                {
                    return false;
                }

                return true;
            }
            else if (objectType == 10 && (this.Keys[4].Item2.Equals(" ") == false))
            {
                var idsMatch = true;
                for (var x = 0; x < 3; x++)
                {
                    if (projectItem.ObjectIDs[x] != this.Keys[x].Item1)
                    {
                        idsMatch = false;
                        break;
                    }
                }

                if (idsMatch == false)
                {
                    return false;
                }

                var valuesMatch = true;
                for (var x = 0; x < 3; x++)
                {
                    if (projectItem.ObjectValues[x].Equals(this.Keys[x].Item2))
                    {
                        valuesMatch = false;
                        break;
                    }
                }

                if (valuesMatch == false) { return false; }

                if (projectItem.ObjectIDs[3] != this.Keys[3].Item1)
                {
                    return false;
                }

                if (projectItem.ObjectValues[3].Length <= 18)
                {
                    if (projectItem.ObjectValues[3].Equals(this.Keys[3].Item2) == false)
                    {
                        return false;
                    }
                    else
                    {
                        return true;
                    }
                }
                else
                {
                    var key4 = projectItem.ObjectValues[3].Substring(0, 18).Trim();
                    var key5 = projectItem.ObjectValues[3].Substring(18).Trim();
                    if (key4.Equals(this.Keys[3].Item2) == false || key5.Equals(this.Keys[4].Item2) == false)
                    {
                        return false;
                    }
                    else { return true; }
                }
            }
            else
            {
                var idsMatch = true;
                for (var x = 0; x < 4; x++)
                {
                    if (projectItem.ObjectIDs[x] != this.Keys[x].Item1)
                    {
                        idsMatch = false;
                        break;
                    }
                }

                if (idsMatch == false)
                {
                    return false;
                }

                var valuesMatch = true;
                for (var x = 0; x < 4; x++)
                {
                    if (projectItem.ObjectValues[x].Equals(this.Keys[x].Item2) == false)
                    {
                        valuesMatch = false;
                        break;
                    }
                }

                return valuesMatch;
            }

            throw new NotImplementedException();
        } 
    }

    internal class Project
    {
        internal List<ProjectItem> Items = new List<ProjectItem>();

        internal Project(OracleConnection conn, string projectName)
        {
            using (var projItems = new OracleCommand("SELECT OBJECTTYPE, OBJECTID1, OBJECTVALUE1, OBJECTID2, OBJECTVALUE2, OBJECTID3, OBJECTVALUE3, OBJECTID4, OBJECTVALUE4 FROM PSPROJECTITEM WHERE PROJECTNAME like :1 AND UPGRADEACTION <> 1", conn))
            {
                OracleParameter projName = new OracleParameter();
                projName.OracleDbType = OracleDbType.Varchar2;
                projName.Value = projectName;
                projItems.Parameters.Add(projName);

                using (var reader = projItems.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        ProjectItem item = new ProjectItem();
                        item.ObjectType = reader.GetInt32(0);
                        item.ObjectIDs = new int[] { reader.GetInt32(1), reader.GetInt32(3), reader.GetInt32(5), reader.GetInt32(7) };
                        item.ObjectValues = new string[] { reader.GetString(2), reader.GetString(4), reader.GetString(6), reader.GetString(8) };

                        Items.Add(item);
                    }
                }
            }
        }

        internal List<ProjectItem> GetPeopleCodeEntries()
        {
            var ppcTypes = new int[] { 8, 9, 39, 40, 42, 43, 44, 45, 46, 47, 48, 58, 66 };

            return Items.Where(i => Array.IndexOf(ppcTypes, i.ObjectType) > -1).ToList();
        }

    }

    internal class ProjectItem
    {
        public int ObjectType;
        public int[] ObjectIDs = new int[4];
        public string[] ObjectValues = new string[4];
    }
}
