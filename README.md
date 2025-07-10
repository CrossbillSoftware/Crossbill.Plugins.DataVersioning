# Data versioning GIT plugin for Crossbill products

Crossbill.Plugins.DataVersioning.Git is the plugin written around the Git library used for internal files repository data versioning in Crossbill products (including Crossbill Cone and Crossbill Bone). The plugin implements a common interface used for managing changes in files repository. Installing the plugin on Cone or Bone enables versioning functionality including a list of the object versions and reversion of the file changes.

## Release version

The compiled version of the plugin can be obtained from [plugin's page on Crossbill Marketplace](https://marketplace.crossbillsoftware.com/en/Apps/Details/Crossbill.Plugins.DataVersioning.Git/) either by manually downloading a compiled package or by automatically installing it using Crossbill Central.

## Installation
### Manual installation using Crossbill Central user interface
1. Open Crossbill Central web page in a browser (common URL for the page is `http://central.example.com` where `example.com` is the domain name provided during the product installation);
2. In the Installed Apps list (left panel), click the Crossbill Central Agent app link to see the app's plugins;
3. Enter the plugin name Crossbill.Plugins.DataVersioning.Git in the search box (right panel) and click Search;
4. Click Install button;
5. Enter the plugin's settings in a popup window if required and click the Install button.

> [!NOTE]
> if the plugin did not appear in the search panel, make sure that
> 1. the Crossbill Marketplace is listed as a package source (Settings -> Package Sources)
> 2. the Central Agent is in Online mode (check the settings' value from Crossbill Central interface or see IsOnline flag in appsettings.json file located in /usr/share/CROSS/Central.Agent/appsettings.json )

### Installation using command line
1. Open a command line interface for the target server;
2. Locate a directory where Crossbill Agent is installed (by default /usr/share/CROSS/Central.Agent/ ) or the location of the Crossbill.Install utility (by default /usr/share/Crossbill.Install/ );
3. Make sure the Crossbill.Install.dll utility file is in the directory;
4. Edit an installation configuration file config.jsconf (either /usr/share/CROSS/Central.Agent/config.jsconf or /usr/share/Crossbill.Install/config.jsconf );
5. Provide the following configuration to install the plugin:
```
{
    "replacements": {
        //Environment
        "@(Environment)": "PROD",
		
		//Central
        "@(CentralAgentPort)": "5002",
        "@(CentralAgentURL)": "http://127.0.0.1:@(CentralAgentPort)/"
    },
    "apps": [
		{
            "Name": "Versioning Plugin for Crossbill Cone",
            "Environment": "@(Environment)",
            "CentralAgentURL": "@(CentralAgentURL)",
            "AppType": "Nest",
            "NestAppID": "Crossbill.Plugins.DataVersioning.Git", 
            "NestSource": "Local Repository",
            "ParentApplication": "Crossbill.Cone.Web",
            "ChildrenOf": "Crossbill Cone"
        }
	]
}
```
6. Save changes to config.jsconf
7. Run the installation utility
```
dotnet Crossbill.Install.dll
```

### Automated installation using Crossbill Seeder's or Crossbill Authority's project
1. Open Crossbill Bone web page in a browser (common URL for the page is `http://bone.example.com` where `example.com` is the domain name provided during the product installation);
2. Go to Crossbill Seeder's project page;
3. Either create a new project or use an existing one;
4. Under the Node apps section either locate an app in the target applications' lookup or enter the configuration manually:
```
{
  "Name": "Versioning Plugin for Crossbill Cone",
  "Environment": "@(Environment)",
  "CentralAgentURL": "@(CentralAgentURL)",
  "AppType": "Nest",
  "NestAppID": "Crossbill.Plugins.DataVersioning.Git", 
  "NestSource": "Crossbill Marketplace",
  "ParentApplication": "Crossbill.Cone.Web",
  "ChildrenOf": "Crossbill Cone",
  "Sources": [
	{
	  "ID": "Local Repository",
	  "AuthType": 3
	},
	{
	  "ID": "Crossbill Marketplace",
	  "URL": "@(AppSrc_Crossbill Marketplace_URL)",
	  "AuthType": 3
	}
  ]
}
```
5. Save the project;
6. Test the project execution by running it from the Crossbill Seeder interface or by providing a provisioning request from the customer-facing portal.


## Supported parameters
No parameters required.

## License

The Crossbill Software License Agreement is located in [plugins/Crossbill.Plugins.DataVersioning.Git/LICENSE.txt](plugins/Crossbill.Plugins.DataVersioning.Git/LICENSE.txt) file.

The Third Party Code in Crossbill Products notice is located in [plugins/Crossbill.Plugins.DataVersioning.Git/third-party-code.txt](plugins/Crossbill.Plugins.DataVersioning.Git/third-party-code.txt) file.

The copyright and license texts for the third party code can be found in [plugins/Crossbill.Plugins.DataVersioning.Git/third-party-notices.txt](plugins/Crossbill.Plugins.DataVersioning.Git/third-party-notices.txt) file.

