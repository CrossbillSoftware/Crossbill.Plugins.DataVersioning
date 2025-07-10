using Crossbill.Common;
using Crossbill.Common.Plugins;
using Crossbill.Common.Resources;
using Crossbill.Plugins.DataVersioning.Git.Processors;
using System.ComponentModel.Composition;

namespace Crossbill.Plugins.DataVersioning.Git
{
    [Export(typeof(IPlugin))]
    public class DataVersioningGitPlugin : APlugin
    {
		public override void RegisterApplicationContextTypes(ICrossContainer container)
        {
            base.RegisterApplicationContextTypes(container);

            container.RegisterType<IRepoProcessor, RepoProcessor>(CrossTypeLifetime.Hierarchical);
        }
    }
}
