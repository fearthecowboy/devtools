//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2010-2012 Garrett Serack and CoApp Contributors. 
//     Contributors can be discovered using the 'git log' command.
//     All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

namespace CoApp.UniversalFileAccess.Azure {
    using System.Collections.Generic;
    using System.Linq;
    using System.Management.Automation;
    using Developer.Toolkit.Scripting.Languages.PropertySheet;
    using Microsoft.WindowsAzure.StorageClient;
    using Toolkit.Collections;
    using Toolkit.Exceptions;
    using Toolkit.Extensions;
    using Utility;

    public class AzureDriveInfo : PSDriveInfo {
        internal const string ProviderScheme = "azure";
        internal const string ProviderDescription = "azure blob storage";

        internal Path Path;
        internal string Secret;
        private CloudFileSystem _cloudFileSystem;
        private readonly IDictionary<string, CloudBlobContainer> _containerCache = new XDictionary<string, CloudBlobContainer>();

        internal string Account {
            get {
                return Path.Account;
            }
        }

        internal string ContainerName {
            get {
                return Path.Container;
            }
        }

        internal string RootPath {
            get {
                return Path.SubPath;
            }
        }

        internal CloudFileSystem CloudFileSystem {
            get {
                return _cloudFileSystem ?? (_cloudFileSystem = new CloudFileSystem(Account, Secret));
            }
        }

        internal CloudBlobContainer GetContainer(string containerName) {
            // Console.WriteLine("Getting Container {0}", containerName);
            return _containerCache.GetOrAdd(containerName, () => CloudFileSystem.ContainerExists(containerName) ? CloudFileSystem[containerName] : null);
        }

        public AzureDriveInfo(Rule aliasRule, ProviderInfo providerInfo, PSCredential psCredential = null)
            : base(GetDriveInfo(aliasRule, providerInfo, psCredential)) {
            Path = new Path {
                Account = aliasRule.HasProperty("key") ? aliasRule["key"].Value : aliasRule.Parameter,
                Container = aliasRule.HasProperty("container") ? aliasRule["container"].Value : "",
                SubPath = aliasRule.HasProperty("root") ? aliasRule["root"].Value.Replace('/', '\\').Replace("\\\\", "\\").Trim('\\') : "",
            };
            Path.Validate();
            Secret = aliasRule.HasProperty("secret") ? aliasRule["secret"].Value : psCredential != null ? psCredential.Password.ToString() : null;
        }

        private static PSDriveInfo GetDriveInfo(Rule aliasRule, ProviderInfo providerInfo, PSCredential psCredential) {
            var name = aliasRule.Parameter;
            var account = aliasRule.HasProperty("key") ? aliasRule["key"].Value : name;
            var container = aliasRule.HasProperty("container") ? aliasRule["container"].Value : "";

            if (string.IsNullOrEmpty(container)) {
                return new PSDriveInfo(name, providerInfo, @"{0}:\{1}\".format(ProviderScheme, account), ProviderDescription, psCredential);
            }

            var root = aliasRule.HasProperty("root") ? aliasRule["root"].Value.Replace('/', '\\').Replace("\\\\", "\\").Trim('\\') : "";

            if (string.IsNullOrEmpty(root)) {
                return new PSDriveInfo(name, providerInfo, @"{0}:\{1}\{2}\".format(ProviderScheme, account, container), ProviderDescription, psCredential);
            }

            return new PSDriveInfo(name, providerInfo, @"{0}:\{1}\{2}\{3}\".format(ProviderScheme, account, container, root), ProviderDescription, psCredential);
        }

        public AzureDriveInfo(PSDriveInfo driveInfo)
            : base(driveInfo) {
            Init(driveInfo.Provider, driveInfo.Root, driveInfo.Credential);
        }

        public AzureDriveInfo(string name, ProviderInfo provider, string root, string description, PSCredential credential)
            : base(name, provider, root, description, credential) {
            Init(provider, root, credential);
        }

        private void Init(ProviderInfo provider, string root, PSCredential credential) {
            var parsedPath = Path.ParseWithContainer(root);

            if (string.IsNullOrEmpty(parsedPath.Account) || string.IsNullOrEmpty(parsedPath.Scheme)) {
                Path = parsedPath;
                return; // this is the root azure namespace.
            }

            var pi = provider as AzureProviderInfo;
            if (pi == null) {
                throw new CoAppException("Invalid ProviderInfo");
            }

            if (parsedPath.Scheme == ProviderScheme) {
                // it's being passed a full url to a blob storage
                Path = parsedPath;

                if (credential == null || credential.Password == null) {
                    // look for another mount off the same account and container for the credential
                    foreach (var d in pi.Drives.Select(each => each as AzureDriveInfo).Where(d => d.Account == Account && d.ContainerName == ContainerName)) {
                        Secret = d.Secret;
                        return;
                    }
                    // now look for another mount off just the same account for the credential
                    foreach (var d in pi.Drives.Select(each => each as AzureDriveInfo).Where(d => d.Account == Account)) {
                        Secret = d.Secret;
                        return;
                    }
                    throw new CoAppException("Missing credential information for {0} mount '{1}'".format(ProviderScheme, root));
                }
                Secret = credential.Password.ToString();
                return;
            }

            // otherwise, it's an sub-folder off of another mount.

            foreach (var d in pi.Drives.Select(each => each as AzureDriveInfo).Where(d => d.Name == parsedPath.Scheme)) {
                Path = new Path {
                    Account = d.Account,
                    Container = string.IsNullOrEmpty(d.ContainerName) ? parsedPath.Account : d.ContainerName,
                    SubPath = string.IsNullOrEmpty(d.RootPath) ? parsedPath.SubPath : d.RootPath + '\\' + parsedPath.SubPath
                };
                Path.Validate();
                Secret = d.Secret;
                return;
            }
        }
    }
}