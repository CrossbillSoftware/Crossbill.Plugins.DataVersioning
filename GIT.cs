using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using Crossbill.Common.Resources;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Crossbill.Plugins.DataVersioning.Git
{
    public class GIT
    {
        private GitSettings _settings;
        private Signature _signature;

        public GIT(GitSettings settings)
        {
            this._settings = settings;
            this._signature = new Signature(settings.Name, settings.Email, DateTimeOffset.Now);
        }

        public Credentials GetCredentials(string url, string usernameFromUrl, SupportedCredentialTypes types)
        {
            return new UsernamePasswordCredentials() { Username = this._settings.Token, Password = "" };
        }

        public void Init(bool isBare)
        {
            if (!this.IsRepoInitialized())
            {
                Repository.Init(_settings.Directory, isBare);
            }
        }

        public void Clone(string branch = null)
        {
            CloneOptions opts = new CloneOptions() {
                Checkout = true
            };
            opts.FetchOptions.CredentialsProvider = this.GetCredentials;
            if (branch != null){
                opts.BranchName = branch;
            }
            Repository.Clone(
                this._settings.ProjectURL,
                this._settings.Directory,
                opts
            );
        }

        public void Pull(){
            using (Repository repo = new Repository(this._settings.Directory))
            {
                PullOptions opt = new PullOptions()
                {
                    FetchOptions = new FetchOptions()
                    {
                        CredentialsProvider = this.GetCredentials
                    }
                };
                Commands.Pull(repo, this._signature, opt);
            }            
        }

        public string Commit(string comment, string subDir = null, List<string> filePaths = null)
        {
            using (Repository repo = new Repository(this._settings.Directory))
            {
                return CommitInternal(repo, comment, subDir, filePaths);
            }
        }

        private string CommitInternal(Repository repo, string comment, string subDir, List<string> filePaths)
        {
            RepositoryStatus status;
            if (filePaths == null)
            {
                status = repo.RetrieveStatus();
                var paths = status
                            .Modified
                            .Concat(status.Untracked)
                            .Concat(status.Removed)
                            .Concat(status.Missing)
                            .Concat(status.Added)
                            .Select(mods => mods.FilePath);
                if(!String.IsNullOrEmpty(subDir)){
                    subDir = subDir.ToLower();
                    paths = paths
                            .Where(f => f.ToLower().StartsWith(subDir));
                }
                filePaths = paths
                            .ToList();
            }

            if (filePaths == null || filePaths.Count == 0)
            {
                return null;
            }

            Commands.Stage(repo, filePaths);
            status = repo.RetrieveStatus();
            if (!status.IsDirty)
            {
                return null;
            }
            Commit commit = repo.Commit(comment, this._signature, this._signature);
            return commit.Sha;
        }

        public void Push(string branch = "master")
        {
            using (Repository repo = new Repository(this._settings.Directory))
            {
                PushInternal(repo, branch);
            }
        }

        private void PushInternal(Repository repo, string branch)
        {            
            PushOptions opt = new PushOptions() 
            {
                CredentialsProvider = this.GetCredentials
            };

            repo.Network.Push(repo.Branches[branch], opt);
        }

        public string CommitAndPush(string comment, string subDir, string branch, List<string> filePaths = null)
        {
            using (Repository repo = new Repository(this._settings.Directory))
            {
                string sha = this.CommitInternal(repo, comment, subDir, filePaths);
                this.PushInternal(repo, branch);
                return sha;
            }
        }

        public bool IsRepoInitialized()
        {
            string gitDir = Path.Combine(this._settings.Directory, ".git");
            return Directory.Exists(gitDir);
        }

        public void Branch(string name, string sourceCommitSha = null, string repositoryUrl = null)
        {
            repositoryUrl = repositoryUrl ?? this._settings.Directory;
            using (Repository repo = new Repository(repositoryUrl))
            {
                if (repo.Branches[name] != null)
                {
                    repo.Branches.Remove(name);
                }

                if (String.IsNullOrEmpty(sourceCommitSha))
                {
                    repo.CreateBranch(name);
                }
                else
                {
                    var commit = repo.Lookup<Commit>(sourceCommitSha);
                    repo.CreateBranch(name, commit);
                }
            }
        }

        public void CloneBranchAndCheckout(string sourceCommitSha, string name)
        {
            if (String.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException("name");
            }

            if (this._settings.ProjectURL != this._settings.Directory)
            {
                this.Clone();
            }
            this.BranchAndCheckout(sourceCommitSha, name);
        }

        public void BranchAndCheckout(string sourceCommitSha, string name)
        {
            if (String.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException("name");
            }

            this.Branch(name, sourceCommitSha);
            this.CheckoutBranch(name);
        }

        public void ResolveToOurs(List<ContentConflictDM> conflicts)
        {
            using (Repository repo = new Repository(this._settings.Directory))
            {
                foreach(var conflict in conflicts){
                    string ourPath = Path.Combine(this._settings.Directory, conflict.OurPath);
                    using (FileStream stream = new FileStream(ourPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                    {
                        using (TextWriter wr = new StreamWriter(stream, Encoding.UTF8))
                        {
                            wr.Write(conflict.Our);
                        }
                    }
                    repo.Index.Add(conflict.OurPath);
                }
            }
        }

        public List<ContentConflictDM> Merge(string branchName, string strategy = "Normal")
        {
            using (Repository repo = new Repository(this._settings.Directory))
            {
                var branch = repo.Branches[branchName];
                MergeOptions opts = new MergeOptions() { 
                    FileConflictStrategy = CheckoutFileConflictStrategy.Merge,
                    MergeFileFavor = (MergeFileFavor)Enum.Parse(typeof(MergeFileFavor), strategy)
                };
                repo.Merge(branch, _signature, opts);

                if (repo.Index.Conflicts.Count() > 0)
                {
                    return repo
                            .Index
                            .Conflicts
                            .Select(c => new ContentConflictDM()
                            {
                                OurPath = c.Ours.Path,
                                TheirPath = c.Theirs.Path,
                                Our = GetContent(repo, c.Ours),
                                Their = GetContent(repo, c.Theirs)
                            })
                            .ToList();
                }
            }
            return null;
        }

        private string GetContent(Repository repo, IndexEntry entry)
        {
            if (entry == null)
            {
                return null;
            }
            var blob = repo.Lookup<Blob>(entry.Id);
            if(blob == null)
            {
                return null;
            }
            FilteringOptions opts = new FilteringOptions(entry.Path);
            return blob.GetContentText(opts, Encoding.UTF8);
        }

        public void RemoveBranch(string name)
        {
            using (Repository repo = new Repository(this._settings.Directory))
            {
                repo.Branches.Remove(name);
            }
        }

        public string CurrentCommit()
        {
            using (Repository repo = new Repository(this._settings.Directory))
            {
                if (repo.Head == null || repo.Head.Tip == null || repo.Head.Tip.Id == null || repo.Head.Tip.Id.Sha == null)
                {
                    return null;
                }
                return repo.Head.Tip.Id.Sha;
            }
        }

        public void InitRemote()
        {
            this.Init(false);
            using (Repository repo = new Repository(this._settings.Directory))
            {
                string remoteName = "origin";
                var remote = repo.Network.Remotes[remoteName];
                if (remote != null)
                {
                    repo.Network.Remotes.Remove(remoteName);
                }
                repo.Network.Remotes.Add(remoteName, this._settings.ProjectURL);
                repo.Branches.Update(repo.Head,
                     b => b.Remote = remoteName,
                     b => b.UpstreamBranch = "refs/heads/master");
            }
        }

        public void CheckoutCommit(string commitSha)
        {
            using (Repository repo = new Repository(this._settings.Directory))
            {
                Commands.Checkout(repo, commitSha);
            }
        }

        public void CheckoutBranch(string branchName = "master", bool force = false)
        {
            using (Repository repo = new Repository(this._settings.Directory))
            {
                var branch = repo.Branches[branchName];
                Commands.Checkout(repo, branch);
            }
        }

        public void ForceCheckoutBranch(string branchName = "master")
        {
            using (Repository repo = new Repository(this._settings.Directory))
            {
                var branch = repo.Branches[branchName];
                Commands.Checkout(repo, branch, new CheckoutOptions()
                {
                    CheckoutModifiers = CheckoutModifiers.Force
                });
            }
        }

        public void RemoveRemote()
        {
            using (Repository repo = new Repository(this._settings.Directory))
            {
                string remoteName = "origin";
                repo.Branches.Update(repo.Head,
                     b => b.Remote = null,
                     b => b.UpstreamBranch = null);
                if (repo.Network.Remotes[remoteName] != null)
                {
                    repo.Network.Remotes.Remove(remoteName);
                }
            }
        }

        public void Fetch()
        {
            using (Repository repo = new Repository(this._settings.Directory))
            {
                Remote remote = repo.Network.Remotes["origin"];
                var refSpecs = remote.FetchRefSpecs.Select(x => x.Specification);
                Commands.Fetch(repo, remote.Name, refSpecs, null, "Fetch");
            }
        }

        protected static Regex _revert = new Regex(@"\{reset: ""([^""]*)"".*");
        public List<ResourceVersion> GetCommits(string path, int pageNumber, int countPerPage)
        {
            using (Repository repo = new Repository(this._settings.Directory))
            {
                List<LogEntry> result = GetLog(repo, path);

                int cnt = result.Count;
                if (countPerPage > 0)
                {
                    result = result
                                .Skip(pageNumber * countPerPage)
                                .Take(countPerPage)
                                .ToList();
                }

                return result
                        .Select((c,i) => {
                            string message = c.Commit.MessageShort;
                            if (message.Contains('{'))
                            {
                                message = message.Substring(0, message.IndexOf('{'));
                            }
                            return new ResourceVersion()
                            {
                                Order = cnt - i,
                                Commit = c.Commit.Id.Sha,
                                Title = message,
                                Author = c.Commit.Author.Name
                            };
                        })
                        .ToList();
            }
        }

        private List<LogEntry> GetLog(Repository repo, string path)
        {
            string relativePath = GetRelativePath(path);
            if (relativePath == null)
            {
                return new List<LogEntry>();
            }

            CommitFilter filter = new CommitFilter() { SortBy = CommitSortStrategies.Time };
            List<LogEntry> result = repo
                                            .Commits
                                            .QueryBy(relativePath, filter)
                                            .ToList();
            string ignore = null;
            foreach (var item in result.ToList())
            {
                if (ignore != null)
                {
                    if (item.Commit.Id.Sha == ignore)
                    {
                        ignore = null;
                    }
                    else
                    {
                        result.Remove(item);
                        continue;
                    }
                }

                if (item.Commit.MessageShort.StartsWith("{reset"))
                {
                    ignore = _revert.Replace(item.Commit.MessageShort, "$1");
                    result.Remove(item);
                    continue;
                }
            }
            return result;
        }

        private string GetRelativePath(string path)
        {
            if (String.IsNullOrEmpty(path) || !path.StartsWith(this._settings.Directory))
            {
                return null;
            }
            return path.Substring(this._settings.Directory.Length);
        }

        public string GetVersionContent(string path, string commit)
        {
            string relativePath = GetRelativePath(path);
            if (relativePath == null)
            {
                return null;
            }

            using (Repository repo = new Repository(this._settings.Directory))
            {
                var target = repo.Lookup<Commit>(commit);
                Blob file = (Blob)target.Tree[relativePath].Target;

                FilteringOptions opts = new FilteringOptions(relativePath);
                return file.GetContentText(opts, Encoding.UTF8);
            }
        }

        public void ResetToVersion(string path, string commit)
        {
            string content = GetVersionContent(path, commit);
            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                using (TextWriter wr = new StreamWriter(stream, Encoding.UTF8))
                {
                    wr.Write(content);
                }
            }
            this.Commit(String.Format("{{reset: \"{0}\"}}", commit));
        }

        public void Revert(string path, string commit)
        {
            using (Repository repo = new Repository(this._settings.Directory))
            {
                var target = repo.Lookup<Commit>(commit);
                repo.Revert(target, _signature, new RevertOptions());
            }
        }
    }
}
