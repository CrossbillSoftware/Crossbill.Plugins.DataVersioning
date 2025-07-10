using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Crossbill.Plugins.DataVersioning.Git
{
    public class GitSettings
    {
        public string ProjectURL { get; set; }

        public string Directory { get; set; }

        public string Token { get; set; }

        public string Name { get; set; }

        public string Email { get; set; }

        public GitSettings(string directory, string name = "Crossbill System", string email = "info@crossbill.com")
        {
            this.Directory = directory;
            this.Name = name;
            this.Email = email;
        }
    }
}
