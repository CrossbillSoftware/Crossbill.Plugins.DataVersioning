using Crossbill.Common;
using Crossbill.Common.Resources;
using Crossbill.Users.Lib;
using Crossbill.Users.Lib.Models;
using Crossbill.Users.Lib.Processors;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Crossbill.Plugins.DataVersioning.Git.Processors
{
    public class RepoProcessor : IRepoProcessor
    {
        private static object _lock = new object();
        protected ICrossContainer _container;
        public RepoProcessor(ICrossContainer container)
        {
            this._container = container;
        }

        public bool CheckRepo(bool isDraft = false)
        {
            var svc = _container.Resolve<IPathProcessor>();
            string dir = svc.GetPhysicalDataDir(isDraft);
            GIT helper = new GIT(new GitSettings(dir));
            return helper.IsRepoInitialized();
        }

        public void Initialize()
        {
            if (!CheckRepo(false))
            {
                var svc = _container.Resolve<IPathProcessor>();
                string dir = svc.GetPhysicalDataDir(false);
                GIT helper = new GIT(new GitSettings(dir));
                helper.Init(false);

                string[] files = Directory.GetFiles(dir, "*", SearchOption.AllDirectories);
                helper.Commit("Application initialize", null, files.ToList());
            }
        }

        public void Commit(string comment, bool isDraft = false)
        {
            var svc = _container.Resolve<IPathProcessor>();
            string dir = svc.GetPhysicalDataDir(isDraft);
            lock (_lock)
            {
                GIT helper = new GIT(UserSettings(dir));
                helper.Commit(comment);
            }
        }

        protected GitSettings UserSettings(string dir)
        {
            if(!_container.IsRegistered<BaseContext>())
            {
                return new GitSettings(dir);
            }
            BaseContext context = _container.Resolve<BaseContext>();
            CrossbillWebSecurity security = _container.Resolve<CrossbillWebSecurity>();

            if ((context != null && context.HttpContext == null) || !security.IsAuthenticated || security.CurrentUserId == 0)
            {
                return new GitSettings(dir);
            }
            var svc = _container.Resolve<IUserProcessor>();
            cb_user profile = svc.Get(security.CurrentUserId);
            return new GitSettings(dir, profile.GetFullName(), profile.UserName);
        }

        public void CreateDraftBranch(string sourceCommitSha)
        {
            var svc = _container.Resolve<IPathProcessor>();
            string dir = svc.GetPhysicalDataDir(true);
            GIT helper = new GIT(new GitSettings(dir) { ProjectURL = svc.GetPhysicalDataDir(false) });
            helper.CloneBranchAndCheckout(sourceCommitSha, svc.GetDraftFolderName());
        }

        public List<ContentConflictDM> MergeDraftBranch(bool isResolved)
        {
            var svc = _container.Resolve<IPathProcessor>();
            string branchName = svc.GetDraftFolderName();
            string draftDir = svc.GetPhysicalDataDir(true);
            string dir = svc.GetPhysicalDataDir(false);
            bool isSeparateDirs = draftDir != dir;

            List<ContentConflictDM> conflicts;
            try
            {
                GitSettings st = UserSettings(draftDir);
                st.ProjectURL = dir;
                GIT helper = new GIT(st);
                helper.CheckoutBranch();
                if (isSeparateDirs)
                {
                    helper.Fetch();
                }
            
                conflicts = helper.Merge(branchName, isResolved ? "Theirs" : "Normal");
                if (conflicts != null && conflicts.Count > 0)
                {
                    if (CheckIsMinorConflict(conflicts))
                    {
                        helper.ResolveToOurs(conflicts);
                        conflicts = helper.Merge(branchName, "Theirs");
                    }
                }

                //Clean master
                if (!isSeparateDirs && conflicts != null && conflicts.Count > 0)
                {
                    helper.ForceCheckoutBranch();
                }

                if (isSeparateDirs && (conflicts == null || conflicts.Count == 0))
                {
                    GitSettings repoSettings = UserSettings(dir);
                    repoSettings.ProjectURL = draftDir;
                    lock (_lock)
                    {
                        GIT repoHelper = new GIT(repoSettings);
                        repoHelper.InitRemote();
                        repoHelper.Pull();
                        repoHelper.RemoveRemote();
                    }
                }
            }
            finally
            {
                if (isSeparateDirs)
                {
                    ClearDir(draftDir);
                }
            }
            return conflicts;
        }

        protected bool CheckIsMinorConflict(List<ContentConflictDM> conflicts)
        {
            bool isMinor = true;
            foreach (var conflict in conflicts)
            {
                if (String.IsNullOrEmpty(conflict.OurPath) || Path.GetExtension(conflict.OurPath) != ".jsconf")
                {
                    isMinor = false;
                    continue;
                }
                if (String.IsNullOrEmpty(conflict.TheirPath) || Path.GetExtension(conflict.TheirPath) != ".jsconf")
                {
                    isMinor = false;
                    continue;
                }
                if (String.IsNullOrEmpty(conflict.Their) || String.IsNullOrEmpty(conflict.Our))
                {
                    isMinor = false;
                    continue;
                }

                string[] lines1 = conflict.Our.Split(new string[]{ Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                string[] lines2 = conflict.Their.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                if(lines1.Length != lines2.Length){
                    isMinor = false;
                    continue;
                }
                bool isEqual = true;
                for(int i = 0; i < lines1.Length; i++){
                    if (lines1[i] != lines2[i] && !(lines1[i].Contains("UpdateDate") || lines1[i].Contains("CreateDate")))
                    {
                        isEqual = false;
                        break;
                    }
                }
                if(!isEqual){
                    isMinor = false;
                    continue;
                }
                conflict.IsMinor = true;
            }
            return isMinor;
        }

        protected void ClearDir(string dir)
        {
            try
            {
                DeleteReadOnlyDirectory(dir);
            }
            catch
            {
            }
        }

        protected void DeleteReadOnlyDirectory(string directory)
        {
            foreach (var subdirectory in Directory.EnumerateDirectories(directory))
            {
                DeleteReadOnlyDirectory(subdirectory);
            }
            foreach (var fileName in Directory.EnumerateFiles(directory))
            {
                var fileInfo = new FileInfo(fileName);
                fileInfo.Attributes = FileAttributes.Normal;
                fileInfo.Delete();
            }
            Directory.Delete(directory);
        }

        public string GetCurrentCommit()
        {
            GIT helper = GetHelper();
            return helper.CurrentCommit();
        }


        public List<ResourceVersion> GetVersions(string path, SearchVersionDM filter)
        {
            GIT helper = GetHelper();
            PageInfo paging;
            if (filter != null && filter.PageInfo != null)
            {
                paging = filter.PageInfo;
            }
            else
            {
                paging = new PageInfo() { CountPerPage = 10 };
            }
            var commits = helper.GetCommits(
                path, 
                paging.PageNumber,
                paging.CountPerPage
            );
            return commits;
        }

        public ResourceVersionMeta GetVersionMeta(string path, VersionMode versionMode)
        {
            ResourceVersionMeta result = new ResourceVersionMeta();
            if (versionMode.HasFlag(VersionMode.LastCommit))
            {
                result.SourceCommit = this.GetCurrentCommit();
            }
            if(versionMode.HasFlag(VersionMode.VersionLog)){
                result.Versions = this.GetVersions(path, null);
            }
            return result;
        }


        public void Revert(string path, string commit)
        {
            lock (_lock)
            {
                GIT helper = GetHelper();
                helper.Revert(path, commit);
            }
        }

        public void ResetToVersion(string path, string commit)
        {
            lock (_lock)
            {
                GIT helper = GetHelper();
                helper.ResetToVersion(path, commit);
            }
        }
        
        public string GetVersionContent(string path, string commit)
        {
            GIT helper = GetHelper();
            string content = helper.GetVersionContent(path, commit);
            return content;
        }

        protected GIT GetHelper()
        {
            var svc = _container.Resolve<IPathProcessor>();
            string dir = svc.GetPhysicalDataDir(false);
            GIT helper = new GIT(new GitSettings(dir));
            return helper;
        }
    }
}
