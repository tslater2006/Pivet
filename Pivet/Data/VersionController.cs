using NGit.Api;
using NGit.Transport;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NGit;
using System.Threading;

namespace Pivet.Data
{
    internal class VersionController
    {
        private Git _repository;
        UsernamePasswordCredentialsProvider _credentials;
        private string _repoBase;
        double lastProgress;
        internal void InitRepository(string path, RepositoryConfig config)
        {
            Logger.Write("Initializing Repository.");
            _repoBase = path;
            if (Directory.Exists(path) == false)
            {
                Directory.CreateDirectory(path);
            }

            if (Directory.Exists(path + Path.DirectorySeparatorChar + ".git"))
            {
                Logger.Write("Repository found, opening.");
                /* already a git repo */
                _repository = Git.Open(_repoBase);
                try
                {
                    if (config != null)
                    {
                        _credentials = new UsernamePasswordCredentialsProvider(config.User, config.Password);

                        Logger.Write("Issuing pull to get the latest changes.");
                        _repository.Reset().Call();
                        var repoConfig = _repository.GetRepository().GetConfig();
                        string branchName = "master";
                        string remoteName = "origin";
                        repoConfig.SetString(ConfigConstants.CONFIG_BRANCH_SECTION, branchName, ConfigConstants.CONFIG_KEY_REMOTE, remoteName);
                        repoConfig.SetString(ConfigConstants.CONFIG_BRANCH_SECTION, branchName, ConfigConstants.CONFIG_KEY_MERGE, Constants.R_HEADS + branchName);
                        repoConfig.Save();

                        _repository.Pull().SetCredentialsProvider(_credentials).Call();
                    }
                }
                catch (Exception e) { }

            } else
            {
                try
                {
                    if (config.Url.Length > 0)
                    {
                        _credentials = new UsernamePasswordCredentialsProvider(config.User, config.Password);
                        Logger.Write("No repository found on disk, trying to clone hosted version.");
                        var clone = Git.CloneRepository().SetCredentialsProvider(_credentials).SetDirectory(_repoBase).SetURI(config.Url);
                        _repository = clone.Call();

                        var repoConfig = _repository.GetRepository().GetConfig();
                        string branchName = "master";
                        string remoteName = "origin";
                        repoConfig.SetString(ConfigConstants.CONFIG_BRANCH_SECTION, branchName, ConfigConstants.CONFIG_KEY_REMOTE, remoteName);
                        repoConfig.SetString(ConfigConstants.CONFIG_BRANCH_SECTION, branchName, ConfigConstants.CONFIG_KEY_MERGE, Constants.R_HEADS + branchName);
                        repoConfig.Save();
                    } else
                    {
                        _repository = Git.Init().SetDirectory(path).Call();
                    }
                }
                catch (Exception ex)
                {
                    /* probably an empty repo */
                    Logger.Write("Problem cloneing hosted version (may not exist). Creating new repository.");
                    _repository = Git.Init().SetDirectory(path).Call();
                }
            }
            
        }

        private void ReportProgress(double progress)
        {
            if (lastProgress != progress)
            {
                Console.CursorLeft = 0;
                Console.CursorTop--;

                Console.WriteLine("Progress: " + string.Format("{0:N2}%", progress));
                lastProgress = progress;
            }
        }

        internal void ProcessChanges(List<ChangedItem> adds)
        {
            Logger.Write("Processing repository changes...");
            Logger.Write("");
            double total = adds.Count + 20;
            double current = 0;
            ReportProgress(0);
            var status = _repository.Status().Call();
            
            var opridGroups = adds.GroupBy(p => p.OperatorId);
            foreach (var opr in opridGroups)
            {
                var oprid = opr.Key;

                foreach (var item in opr)
                {
                    var fileName = item.FilePath.Replace(_repoBase + Path.DirectorySeparatorChar, "");
                    fileName = fileName.Replace("\\", "/");
                    _repository.Add().AddFilepattern(fileName).Call();
                    current++;
                    ReportProgress(((int)(((current / total) * 10000)) / (double)100));
                }
                status = _repository.Status().Call();
                if (status.GetAdded().Count > 0)
                {
                    _repository.Commit().SetAuthor(oprid, oprid).SetMessage("Changes made by " + oprid).Call();
                }
                
            }
            status = _repository.Status().Call();
            if (status.GetMissing().Count > 0)
            {
                var hasItems = false;
                /* process any file deletes as SYSTEM */
                foreach (var d in status.GetMissing())
                {
                    hasItems = true;
                    _repository.Rm().AddFilepattern(d).Call();
                }
                if (hasItems)
                {
                    _repository.Commit().SetAuthor("SYSTEM", "SYSTEM").SetMessage("Deleted Objects").Call();
                }
            }
            current += 10;

            ReportProgress(((int)(((current / total) * 10000)) / (double)100));

            status = _repository.Status().Call();
            if (status.GetUntracked().Count > 0 || status.GetModified().Count > 0)
            {
                foreach (var d in status.GetUntracked())
                {
                    _repository.Add().AddFilepattern(d).Call();
                }
                var hasItems = false;
                foreach (var d in status.GetModified())
                {
                    hasItems = true;
                    _repository.Add().AddFilepattern(d).Call();
                }
                if (hasItems)
                {
                    _repository.Commit()
                        .SetAuthor("SYSTEM", "SYSTEM")
                        .SetMessage("Commiting files with unknown authors.")
                        .Call();
                }
            }
            current += 10;

            ReportProgress(((int)(((current / total) * 10000)) / (double)100));
            status = _repository.Status().Call();
            Thread.Sleep(2000);
            if (_credentials != null)
            {
                _repository.Push().SetCredentialsProvider(_credentials).Call();
            }
         }
    }
}
