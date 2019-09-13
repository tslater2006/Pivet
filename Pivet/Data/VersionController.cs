using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using LibGit2Sharp;
namespace Pivet.Data
{
    internal class VersionController
    {
        private Repository _repository;
        RepositoryConfig config;
        private string _repoBase;
        double lastProgress;
        internal void InitRepository(string path, RepositoryConfig config)
        {
            this.config = config;
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
                _repository = new Repository(_repoBase);

            }
            else
            {
                if (config.Url.Length > 0)
                {
                    Logger.Write("Repository URL is set, cloning repo to disk.");
                    /* repository is configured with a remote, try cloning from there */
                    var co = new CloneOptions();
                    co.CredentialsProvider = (_url, _user, _cred) => new UsernamePasswordCredentials { Username = config.User, Password = config.Password };
                    var result = Repository.Clone(config.Url, _repoBase, co);
                }
                else
                {
                    Repository.Init(_repoBase);
                }
                _repository = new Repository(_repoBase);
            }

            /* confirm this repository has an "origin" remote */
            if (_repository.Network.Remotes["origin"] != null)
            {
                /* there is an origin, make sure it points to the URL in config */
                if (_repository.Network.Remotes["origin"].Url.Equals(config.Url) == false)
                {
                    Logger.Write("Repository origin doesn't match config. Updating...");
                    _repository.Network.Remotes.Update("origin", r => r.Url = config.Url);
                }
            }
            else
            {
                Logger.Write("Repository doesn't have an origin. Adding...");
                /* no origin, lets set one if we have a URL */
                if (config.Url.Length > 0)
                {
                    _repository.Network.Remotes.Add("origin", config.Url);
                }
            }

        }

        private void ReportProgress(double progress)
        {
            if (Program.ShowProgress)
            {
                if (lastProgress != progress)
                {
                    Console.CursorLeft = 0;
                    Console.CursorTop--;

                    Console.WriteLine("Progress: " + string.Format("{0:N2}%", progress));
                    lastProgress = progress;
                }
            }
        }

        internal void ProcessChanges(List<ChangedItem> adds)
        {
            Logger.Write("Processing repository changes...");
            Logger.Write("");
            double current = 0;
            ReportProgress(0);

            List<string> changedOrNewItems = _repository.RetrieveStatus().Where(p => p.State == FileStatus.ModifiedInWorkdir || p.State == FileStatus.NewInWorkdir).Select(o => o.FilePath).ToList();
            List<string> deletedFiles = _repository.RetrieveStatus().Where(p => p.State == FileStatus.DeletedFromWorkdir || p.State == FileStatus.DeletedFromIndex).Select(o => o.FilePath).ToList();
            double total = 0;
            if (config.CommitByOprid)
            {
                List<ChangedItem> newOrModifiedFiles = new List<ChangedItem>();
                total = changedOrNewItems.Count;

                foreach (var f in changedOrNewItems)
                {
                    newOrModifiedFiles.Add(adds.Where(p => p.RepoPath == f).First());
                    current++;
                    ReportProgress(((int)(((current / total) * 10000)) / (double)100));
                }

                Logger.Write("Processing OPRID groups...");
                var opridGroups = newOrModifiedFiles.GroupBy(p => p.OperatorId);

                Logger.Write("Processing staged changes...");
                Logger.Write("");
                foreach (var opr in opridGroups)
                {
                    var staged = opr.Select(o => o.RepoPath).ToList();
                    Commands.Stage(_repository, staged);

                    if (newOrModifiedFiles.Count > 0)
                    {
                        var oprid = opr.Key;
                        Signature author = new Signature(oprid, oprid, DateTime.Now);
                        Signature committer = author;
                        Commit commit = _repository.Commit("Changes made by " + oprid, author, committer);
                    }
                }
            }
            else //not by oprid
            {
                if (changedOrNewItems.Count > 0)
                {
                    Commands.Stage(_repository, changedOrNewItems);
                    Signature author = new Signature("PIVET", "PIVET", DateTime.Now);
                    Signature committer = author;
                    String commitString = "Changes captured by Pivet";
                    if (Program.CustomCommitMessage.Length > 0)
                    {
                        commitString = Program.CustomCommitMessage;
                    }
                    Commit commit = _repository.Commit(commitString, author, committer);
                }
            }

            if (deletedFiles.Count > 0)
            {
                Commands.Stage(_repository, deletedFiles);
                Signature author = new Signature("SYSTEM", "SYSTEM", DateTime.Now);
                Signature committer = author;
                Commit commit = _repository.Commit("Deleted Objects", author, committer);
            }

            /* We have commited all changes, time to push if setup with a URL */
            if (this.config.Url.Length > 0)
            {
                Logger.Write("Pushing repository to origin.");
                Remote remote = _repository.Network.Remotes["origin"];
                var options = new PushOptions();
                options.CredentialsProvider = (_url, _user, _cred) =>
                    new UsernamePasswordCredentials { Username = this.config.User, Password = this.config.Password };
                options.OnPackBuilderProgress = (x, y, z) =>
                {
                    Console.CursorLeft = 0;
                    Console.CursorTop--;
                    Logger.Write(x + " (" + y + "/" + z + ")                              ");
                    return true;
                };

                options.OnPushTransferProgress = (x, y, z) =>
                {
                    Console.CursorLeft = 0;
                    Console.CursorTop--;
                    Logger.Write("Pushing objects (" + x + "/" + y + ")                                 ");
                    return true;
                };

                options.OnPushStatusError = (x) =>
                {
                    Logger.Write("Push Error: " + x.Message);
                };
                try
                {
                    _repository.Network.Push(remote, @"refs/heads/master", options);
                }
                catch (Exception ex)
                {
                    Logger.Write("Exception occured while pushing to remote: " + ex.ToString());
                }
            }
        }
    }
}
