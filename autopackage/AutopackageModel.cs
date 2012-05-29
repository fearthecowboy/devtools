﻿//-----------------------------------------------------------------------
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
namespace CoApp.Autopackage {
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.IO;
    using System.Linq;
    using System.ServiceModel.Syndication;
    using System.Threading.Tasks;
    using System.Xml;
    using System.Xml.Serialization;
    using Developer.Toolkit.Publishing;
    using Packaging.Client;
    using Packaging.Common;
    using Packaging.Common.Model;
    using Packaging.Common.Model.Atom;
    using Properties;
    using Toolkit.Collections;
    using Toolkit.Crypto;
    using Toolkit.Extensions;
    using Toolkit.Logging;
    using Toolkit.Tasks;
    using Toolkit.Win32;

    [XmlRoot(ElementName = "Package", Namespace = "http://coapp.org/atom-package-feed-1.0")]
    public class AutopackageModel : PackageModel {
        [XmlIgnore]
        private PackageSource Source;
       
        [XmlIgnore]
        internal IEnumerable<FileEntry> DestinationDirectoryFiles;

        // Assemblies Roles
        [XmlIgnore]
        internal List<PackageAssembly> Assemblies;

        [XmlIgnore]
        internal List<Package> DependentPackages = new List<Package>();
        
        // package templates 
        [XmlIgnore]
        private string _managedPublisherConfiguration;

        [XmlIgnore]
        private string _nativePublisherConfiguration;

        [XmlIgnore]
        private string _nativeAssemblyManifest;

        [XmlIgnore]
        internal string WixTemplate;

        [XmlIgnore]
        private AtomFeed AtomFeed;

        [XmlIgnore]
        private TaskList _tasks = new TaskList();

        private BindingRedirect _bindingRedirect;
        internal BindingRedirect BindingRedirect {
            get {
                if( _bindingRedirect == null ) {
                    if (BindingPolicy != null && BindingPolicy.Maximum > 0) {
                        _bindingRedirect = new BindingRedirect {
                            Low = BindingPolicy.Minimum,
                            High = BindingPolicy.Maximum,
                            Target = Version,
                        };
                    }
                }
                return _bindingRedirect;
            }
        }

        private IEnumerable<TwoPartVersion> _versionRedirects = Enumerable.Empty<TwoPartVersion>();

        internal Image IconImage;
        internal XDictionary<string, string> ChildIcons; 

        internal AutopackageModel() {
            CompositionData = new Composition();
            DestinationDirectoryFiles = Enumerable.Empty<FileEntry>();
            Roles = new XList<Role>();
            Assemblies = new List<PackageAssembly>();
        }

        internal AutopackageModel(PackageSource source, AtomFeed feed) : this() {
            Source = source;
            foreach( var sheet in Source.PropertySheets ) {
                sheet.GetMacroValue += GetMacroValue;
            }
            AtomFeed = feed;
        }
        internal string GetMacroValue(string macroKey) {
            if (macroKey.StartsWith("Package.")) {
                var result = this.SimpleEval(macroKey.Substring(8));
                if (result == null || string.Empty == result.ToString()) {
                    return null;
                }

                return result.ToString();
            }
            return null;
        }

        internal void ProcessCertificateInformation() {
            Vendor = Source.Certificate.CommonName;
        }

        internal void ProcessPackageTemplates() {
            // load template data from script stack
            _nativePublisherConfiguration = Source.AllRules.GetProperty("templates", "native-publisher-configuration").Value as string;
            _managedPublisherConfiguration = Source.AllRules.GetProperty("templates", "managed-publisher-configuration").Value as string;
            _nativeAssemblyManifest = Source.AllRules.GetProperty("templates", "native-assembly-manifest").Value as string;

            WixTemplate = Resources.WixTemplate;
        }

        internal void ProcessFileLists() {
            // -----------------------------------------------------------------------------------------------------------------------------------
            // Run through the file lists and gather in all the files that we're going to include in the package.
            foreach (var fileSet in Source.FileRules.Select(each => each.Parameter).Distinct()) {
                FileList.GetFileList(fileSet, Source.FileRules);
            }
        }
         
        internal void ProcessApplicationRole() {
            // application rule supports the following properties:
            // include -- may include files or filesets; can not set 'destination' here, must set that in previously defined filesets.
            foreach (var appRule in Source.ApplicationRules) {
                Roles.Add(new Role() { Name = appRule.Parameter ?? string.Empty, PackageRole = PackageRole.Application });
                var files = FileList.ProcessIncludes(null, appRule, "application", "include", Source.FileRules, Environment.CurrentDirectory);
                var name = appRule.Parameter;

                if (!string.IsNullOrEmpty(name)) {
                    files = files.Select(
                        each => new FileEntry ( each.SourcePath, Path.Combine(name.MakeSafeFileName(), each.DestinationPath))).ToArray();
                }
                DestinationDirectoryFiles = DestinationDirectoryFiles.Union(files);
            }
        }

        internal void ProcessFauxRoles() {
            foreach (var fauxPaxRule in Source.FauxApplicationRules) {
                Roles.Add(new Role() { Name = fauxPaxRule.Parameter ?? string.Empty, PackageRole = PackageRole.Faux });
                Console.WriteLine("Processing Faux-Pax Role: {0}", fauxPaxRule.Parameter);
                var files = FileList.ProcessIncludes(null, fauxPaxRule, "faux-pax", "include", Source.FileRules, Environment.CurrentDirectory);
                var name = fauxPaxRule.Parameter;
                var fauxPax = CompositionData.FauxApplications ?? (CompositionData.FauxApplications = new List<FauxApplication>());
                var downloadProperty = fauxPaxRule["download"];
                var downloads = new XDictionary<string, Uri>();

                foreach( var l in downloadProperty.Labels ) {
                    if( downloadProperty.HasValues ) {
                        var values = downloadProperty[l];
                        foreach( var uri in values ) {
                            try {
                                var targetUrl = new Uri(uri);
                                string filename = l.MakeSafeFileName();
                                if( string.IsNullOrEmpty(filename) ) {
                                    var p = targetUrl.AbsolutePath;
                                    filename = p.Substring(p.IndexOf("/") + 1).MakeSafeFileName();
                                }
                                downloads[filename] = targetUrl;
                            } catch {
                                Event<Error>.Raise(MessageCode.InvalidUri, fauxPaxRule.SourceLocation, "Uri '{0}' is not valid",uri );
                            } 
                        }
                    }
                }

                fauxPax.Add( new FauxApplication {
                    Name = fauxPaxRule.Parameter,
                    Downloads =downloads,

                    InstallCommand = fauxPaxRule["install"]["command"].Value,
                    InstallParameters = fauxPaxRule["install"]["parameters"].Value,

                    RemoveCommand = fauxPaxRule["remove"]["command"].Value,
                    RemoveParameters = fauxPaxRule["remove"]["parameters"].Value,
                });

                if (!string.IsNullOrEmpty(name)) {
                    files = files.Select(
                        each => new FileEntry(each.SourcePath, Path.Combine(name.MakeSafeFileName(), each.DestinationPath))).ToArray();
                }
                DestinationDirectoryFiles = DestinationDirectoryFiles.Union(files);
            }
        } 

        public const string IncludeDir = "include";
        public const string DocDir = "docs";
        public const string LibDir = "lib";
        public const string RefAsmDir = "ReferenceAssemblies";

        internal void ProcessDeveloperLibraryRoles() {
            foreach (var devLibRule in Source.DeveloperLibraryRules) {
                var roleName = devLibRule.Parameter ?? string.Empty;
                Roles.Add(new Role() { Name = roleName, PackageRole = PackageRole.DeveloperLibrary });
                Console.WriteLine("Processing Developer Library Role: {0}",devLibRule.Parameter );

                // create the developer library object in the composition data
                var devLibraries = CompositionData.DeveloperLibraries ?? (CompositionData.DeveloperLibraries = new List<DeveloperLibrary>());

                var headerFiles = FileList.ProcessIncludes(null, devLibRule, "developer-library", "headers", Source.FileRules, Environment.CurrentDirectory).Select( each => new FileEntry(each.SourcePath, IncludeDir+"\\"+each.DestinationPath) ).ToArray();
                var docFiles = FileList.ProcessIncludes(null, devLibRule, "developer-library", "docs", Source.FileRules, Environment.CurrentDirectory).Select(each => new FileEntry(each.SourcePath, DocDir+"\\" + each.DestinationPath)).ToArray();
                var libFiles = FileList.ProcessIncludes(null, devLibRule, "developer-library", "libraries", Source.FileRules, Environment.CurrentDirectory).Select(each => new FileEntry(each.SourcePath, LibDir+"\\" + each.DestinationPath)).ToArray();
                var assemblyFiles = FileList.ProcessIncludes(null, devLibRule, "developer-library", "reference-assemblies", Source.FileRules, Environment.CurrentDirectory).Select(each => new FileEntry(each.SourcePath, RefAsmDir + "\\" + each.DestinationPath)).ToArray();

                devLibraries.Add(new DeveloperLibrary { 
                    Name = roleName,
                    ReferenceAssemblyFiles = assemblyFiles.Any() ? assemblyFiles.Select(each => each.DestinationPath).ToList() : null,
                    LibraryFiles = libFiles.Any() ?  libFiles.Select(each => each.DestinationPath).ToList() : null,

                    HeaderFolders = headerFiles.Any() ? IncludeDir.SingleItemAsEnumerable().ToList() : null,
                    DocumentFolders = docFiles.Any() ? DocDir.SingleItemAsEnumerable().ToList() : null,
                });

                DestinationDirectoryFiles = DestinationDirectoryFiles.Union(headerFiles).Union(docFiles).Union(libFiles).Union(assemblyFiles);
            }
        }

        internal void ProcessServiceRoles() {
            foreach (var serviceRule in Source.ServiceRules) {
                var roleName = serviceRule.Parameter ?? string.Empty;
                Roles.Add(new Role() { Name = roleName, PackageRole = PackageRole.Service });
                // create the developer library object in the composition data
                var services = CompositionData.Services ?? (CompositionData.Services = new List<Service>());
                services.Add(new Service {
                    Name = roleName
                    // DocumentFolders = 
                });
            }
        }

        internal void ProcessDriverRoles() {
            foreach (var driverRule in Source.DriverRules) {
                var roleName = driverRule.Parameter ?? string.Empty;
                Roles.Add(new Role() { Name = roleName, PackageRole = PackageRole.Driver });
                // create the developer library object in the composition data
                var services = CompositionData.Drivers ?? (CompositionData.Drivers = new List<Driver>());
                services.Add(new Driver() {
                    Name = roleName
                    // DocumentFolders = 
                });
            }
        }

        
        internal void ProcessWebApplicationRoles() {
            foreach (var webAppRule in Source.WebApplicationRules) {
                var roleName = webAppRule.Parameter ?? string.Empty;
                Roles.Add(new Role() { Name = roleName?? string.Empty, PackageRole = PackageRole.WebApplication });
                // create the developer library object in the composition data
                var services = CompositionData.WebApplications ?? (CompositionData.WebApplications = new List<WebApplication>());
                services.Add(new WebApplication() {
                    Name = roleName
                    // DocumentFolders = 
                });
            }
        }

        internal void ProcessSourceCodeRoles() {
            foreach (var sourceCodeRule in Source.SourceCodeRules) {
                var roleName = sourceCodeRule.Parameter ?? string.Empty;
                Roles.Add(new Role() { Name = roleName?? string.Empty, PackageRole = PackageRole.SourceCode });
                // create the developer library object in the composition data
                var services = CompositionData.SourceCodes ?? (CompositionData.SourceCodes = new List<SourceCode>());
                services.Add(new SourceCode() {
                    Name = roleName
                    // DocumentFolders = 
                });
            }
        }

        internal void ProcessAssemblyRules() {
            foreach (var asmRule in Source.AssembliesRules) {
                // create an assembly for each one of the files.
                var asmFiles = FileList.ProcessIncludes(null, asmRule, "assemblies", "include", Source.FileRules, Environment.CurrentDirectory);
                Assemblies.AddRange(
                    asmFiles.Select(file => new PackageAssembly(Path.GetFileNameWithoutExtension(file.SourcePath), asmRule, file)));
            }

            foreach (var asm in Source.AssemblyRules) {
                var fileList = FileList.ProcessIncludes(null, asm, "assembly", "include", Source.FileRules, Environment.CurrentDirectory);
                if( string.IsNullOrEmpty(asm.Parameter) ) {
                    Event<Error>.Raise(
                       MessageCode.AssemblyHasNoName, asm.SourceLocation, "Assembly definition requires name.");
                }
                Assemblies.Add(new PackageAssembly(asm.Parameter, asm, fileList));
            }

            // now, check to see that our assemblies are unique.
            var assemblyNames = Assemblies.Select(each => each.Name).ToArray();
           
            foreach( var asmName in assemblyNames) {
                Roles.Add(new Role() { Name = asmName ?? string.Empty, PackageRole = PackageRole.Assembly });
            }

            // unique name/culture?
            foreach( var asm in Assemblies ) {
                var conflicts = Assemblies.Where(each => asm != each && each.Name == asm.Name && each.Culture == asm.Culture);
                foreach (var c in conflicts) {
                    Event<Error>.Raise(
                        MessageCode.DuplicateAssemblyDefined, c.Rule.SourceLocation, "Assembly with name/culture '{0}'/'{1}' defined more than once.", c.Name, c.Culture);
                }
            }
/*
            if (assemblyNames.Count() != assemblyNames.Distinct().Count()) {
                // there is a duplicate there somewhere. run thru the list and rat em out.
                foreach (var name in assemblyNames) {
                    var asms = Assemblies.Where(each => each.Name == name );
                    if (asms.Count() > 1) {
                        foreach (var a in asms) {
                            Event<Error>.Raise(
                                MessageCode.DuplicateAssemblyDefined, a.Rule.SourceLocation, "Assembly with name '{0}' defined more than once.", name);
                        }
                    }
                }
                // fail fast, this is pointless.
                return;
            }
            */


            // check to see that all the assemblies are the same archetecture.
            var arches = Assemblies.Select(each => each.Architecture).Distinct().ToArray();
            if (arches.Length > 1) {
                foreach (var asm in Assemblies) {
                    Event<Error>.Raise(
                        MessageCode.MultipleAssemblyArchitectures, asm.Rule.SourceLocation,
                        "All Assemblies must have the same architecture. '{0}' architecure => {1}.", asm.Name, asm.Architecture);
                }
                // fail fast, this is pointless.
                return;
            }

            

            foreach (var assembly in Assemblies) {
                assembly.PublicKeyToken = Source.Certificate.PublicKeyToken;
            }
        }

        internal void ProcessDependencyInformation() {
            // -----------------------------------------------------------------------------------------------------------------------------------
            // Step 3 : Gather the dependency information for the package

            // explictly defined
            DependentPackages = new List<Package>();

            if( !Name.Equals("coapp", StringComparison.CurrentCultureIgnoreCase) ) {
                // don't auto-add the coapp.toolkit dependency for the toolkit itself.
                var toolkitPackage = AutopackageMain.PackageManager.QueryPackages(CanonicalName.CoAppItself, null, null, null).Result.OrderByDescending(each => each.Version).FirstOrDefault();
                
                if( toolkitPackage != null ) {
                    AutopackageMain.PackageManager.GetPackageDetails(toolkitPackage.CanonicalName).Wait();
                    //Console.WriteLine("Implict Package Dependency: {0}", toolkitPackage.CanonicalName);
                    DependentPackages.Add(toolkitPackage);    
                }
            }

            AutopackageMain.PackageManager.SetAllFeedsStale().Wait();

            foreach (var pkgName in Source.RequiresRules.SelectMany(each => each["package"].Values)) {
                // for now, lets just see if we can do a package match, and grab just that packages
                // in the future, we should figure out how to make better decisions for this.
                try {
                    var packages = AutopackageMain.PackageManager.QueryPackages(pkgName, null, null, null).Result.OrderByDescending(each => each.Version).ToArray();

                    if( packages.IsNullOrEmpty()) {
                        Event<Error>.Raise( MessageCode.FailedToFindRequiredPackage, null, "Failed to find package '{0}'.", pkgName);
                    }

                    if( packages.Select(each => each.Name).Distinct().Count() > 1 ) {
                        Event<Error>.Raise(MessageCode.MultiplePackagesMatched, null, "Multiple Packages Matched package reference: '{0}'.", pkgName);
                    }

                    // makes sure it takes the latest one that matches. Hey, if you wanted an earlier one, you'd say explicitly :p
                    var pkg = packages.OrderByDescending(each => each.Version).FirstOrDefault();

                    // Console.WriteLine("Package Dependency: {0}", pkg.CanonicalName);

                    AutopackageMain.PackageManager.GetPackageDetails(pkg.CanonicalName).Wait();

                    DependentPackages.Add(pkg);
                    
                } catch (Exception e) {
                    Event<Error>.Raise(
                        MessageCode.FailedToFindRequiredPackage, null, "Failed to find package '{0}'. [{1}]", pkgName, e.Message);
                }
            }

            foreach (var pkg in DependentPackages) {
                if (Dependencies == null ) {
                    Dependencies = new XDictionary<CanonicalName, XList<Uri>>();
                }
                Dependencies.Add(pkg.CanonicalName, pkg.Feeds.Where( each => each.IsWebUri()).ToXList());
            }

            // implicitly defined (check all binaries, to see what they depend on)
            // maybe in RC.
        }

        private void DigitallySign(string filename) {
            _tasks.Add(Binary.Load(filename , BinaryLoadOptions.All).ContinueWith(antecedent => {
                if (antecedent.IsFaulted) {
                    Event<Error>.Raise(MessageCode.SigningFailed, null, "Failed to load binary '{0}'", filename);
                    return;
                }
                DigitallySign(antecedent.Result);
            }, TaskContinuationOptions.AttachedToParent));
        }

        private void DigitallySign( Binary binary ) {
            if (binary.IsManaged) {
                binary.StrongNameKeyCertificate = Source.Certificate;
            }
            binary.SigningCertificate = Source.Certificate;
        }

        internal void ProcessDigitalSigning() {
            // -----------------------------------------------------------------------------------------------------------------------------------
            // Step 4 : Ensure digital signatures and strong names are all good to go
            foreach (var signRule in Source.SigningRules) {
                var reSign = signRule.HasProperty("replace-signature") && signRule["replace-signature"].Value.IsTrue();

                var filesToSign = FileList.ProcessIncludes(null, signRule, "signing", "include",Source.FileRules, Environment.CurrentDirectory);
                foreach (var f in filesToSign) {
                    var file = f;

                    if (reSign || !Verifier.HasValidSignature(file.SourcePath)) {
                        try {
                            _tasks.Add(Binary.Load(file.SourcePath).ContinueWith(antecedent => {
                                if( antecedent.IsFaulted ) {
                                    Event<Error>.Raise(MessageCode.SigningFailed, null, "Failed to load binary '{0}'", file.SourcePath);
                                    return;
                                }

                                var binary = antecedent.Result;
                                DigitallySign(binary);

                                if (signRule.HasProperty("attributes")) {
                                    var attribs = signRule["attributes"];
                                    foreach (var l in attribs.Labels) {
                                        switch (l.ToLower()) {
                                            case "company":
                                                binary.CompanyName = attribs[l].Value.ToString();
                                                break;
                                            case "description":
                                                binary.FileDescription = attribs[l].Value.ToString();
                                                break;
                                            case "product-name":
                                                binary.ProductName = attribs[l].Value.ToString();
                                                break;
                                            case "product-version":
                                                binary.ProductVersion = attribs[l].Value.ToString();
                                                break;
                                            case "file-version":
                                                binary.FileVersion = attribs[l].Value.ToString();
                                                break;
                                            case "copyright":
                                                binary.LegalCopyright = attribs[l].Value.ToString();
                                                break;
                                            case "trademark":
                                                binary.LegalTrademarks = attribs[l].Value.ToString();
                                                break;
                                            case "title":
                                                binary.AssemblyTitle = attribs[l].Value.ToString();
                                                break;
                                            case "comments":
                                                binary.Comments = attribs[l].Value.ToString();
                                                break;
                                        }
                                    }
                                }

                            }, TaskContinuationOptions.AttachedToParent));

                        } catch (Exception e) {
                            Logger.Error(e);
                            Event<Error>.Raise(MessageCode.SigningFailed, null, "Digital Signing of binary '{0}' failed.", file.SourcePath);
                        }
                    }
                }
            }

            // verify that all files that should be signed are actually signed.
            // TODO : make sure stuff is actually signed.
        }

        internal void SaveModifiedBinaries() {
            // Console.WriteLine("(info) ... waiting for binary file modifications to complete ...");
            _tasks.WaitAll();
            // Console.WriteLine("(info) ... modifications complete, now saving binary files ...");
            var saving = Binary.Files.Where(each => each.Modified).Select(each => each.Save());
            try {
                Task.WaitAll(saving.ToArray());
                // Console.WriteLine("(info) ... done saving binary files ...");
            } catch( Exception e) {
                
                if( e.GetType() == typeof(AggregateException)) {
                    e = (e as AggregateException).Flatten().InnerExceptions[0];
                }
                Logger.Error(e);
                Event<Error>.Raise(MessageCode.SigningFailed, null, "Saving binary failed. (see inner exception ) {0}--{1}",e.Message, e.StackTrace );
            }

            // now, do a post signing check to see that all assemblies are actually signed.
            foreach( var assembly in Assemblies.Where( each =>!each.FilesAreSigned ) ) {
                Event<Error>.Raise(MessageCode.AssembliesMustBeSigned, null, "Assembly '{0}' has one or more binaries that are not digitally signed",assembly.Name);
            }

        }

        internal void ProcessBasicPackageInformation() {

            // -----------------------------------------------------------------------------------------------------------------------------------
            // New Step: Validate the basic information of this package
            string pkgName = Source.PackageRules.GetProperty("name").Value;
            Architecture pkgArchitecture = Architecture.Auto; 
            FourPartVersion pkgVersion = Source.PackageRules.GetPropertyValue("version");
            FlavorString pkgFlavor = Source.PackageRules.GetPropertyValue("flavor");
            string pkgPublicKeyToken = Source.Certificate.PublicKeyToken;

            if (string.IsNullOrEmpty(pkgName)) {
                Event<Error>.Raise(
                    MessageCode.MissingPackageName, Source.PackageRules.Last().SourceLocation, "Missing property 'name' in 'package' rule.");
            }

            if (pkgVersion == 0) {
                // try to figure out package version from binaries.
                // check assemblies first
                foreach (var assembly in Assemblies) {
                    pkgVersion = assembly.Version;
                    if (pkgVersion == 0) {
                        Event<Error>.Raise(
                            MessageCode.AssemblyHasNoVersion, assembly.Rule.SourceLocation, "Assembly '{0}/{1}' doesn't have a version.", assembly.Name, assembly.Culture ?? "");
                    } else {
                        Event<Warning>.Raise(
                            MessageCode.AssumingVersionFromAssembly, Assemblies.First().Rule.SourceLocation,
                            "Package Version not specified, assuming version '{0}' from first assembly", pkgVersion.ToString());

                        if (pkgArchitecture == Architecture.Auto || pkgArchitecture == Architecture.Unknown) {
                            // while we're here, let's grab this as the architecture.
                            pkgArchitecture = assembly.Architecture;
                        }

                        break;
                    }
                }
                if (pkgVersion == 0) {
                    // check application next 
                    foreach (var file in DestinationDirectoryFiles) {
                        var binary = Binary.Load(file.SourcePath).Result;

                        if (binary.IsPEFile) {
                            pkgVersion = binary.FileVersion;

                            Event<Warning>.Raise(
                                MessageCode.AssumingVersionFromApplicationFile, null,
                                "Package Version not specified, assuming version '{0}' from application file '{1}'", pkgVersion.ToString(),
                                file.SourcePath);

                            if (pkgArchitecture == Architecture.Auto || pkgArchitecture == Architecture.Unknown) {
                                // while we're here, let's grab this as the architecture.
                                if (binary.IsAnyCpu) {
                                    pkgArchitecture = Architecture.Any;
                                } else if (binary.Is64Bit) {
                                    pkgArchitecture = Architecture.x64;
                                } else {
                                    pkgArchitecture = Architecture.x86;
                                }
                            }

                            break;
                        }
                    }
                }

                if (pkgVersion == 0) {
                    Event<Error>.Raise(MessageCode.UnableToDeterminePackageVersion, null, "Unable to determine package version.");
                    return; // fast fail.
                }
                
            }

            // set any assemblies without version numbers to package version
            foreach (var assembly in Assemblies.Where(each => each.Version == 0L)) {
                assembly.Version = pkgVersion;
            }

            // make sure that all the assemblies have the same version as the package
            foreach (var assembly in Assemblies.Where(each => each.Version != pkgVersion)) {
                Event<Error>.Raise(MessageCode.AssemblyVersionDoesNotMatch, null, "Assembly '{0}' has different version ({1}) that this package ({2}) .", assembly.Name, assembly.Version, Version);
            }

            // check to see that all the assemblies are the same version.
            var versions = Assemblies.Select(each => each.Version).Distinct().ToArray();
            if (versions.Length > 1) {
                foreach (var asm in Assemblies) {
                    Event<Error>.Raise(
                        MessageCode.MultipleAssemblyVersions, asm.Rule.SourceLocation, "All Assemblies must have the same version. '{0}' Version => {1}.",
                        asm.Name, asm.Version);
                }
                // fail fast, this is pointless.
                return;
            }
            
            var arch = Source.PackageRules.GetPropertyValue("arch") as string;
            arch = arch ?? Source.PackageRules.GetPropertyValue("architecture") as string;
            if ((pkgArchitecture == Architecture.Auto || pkgArchitecture == Architecture.Unknown) && arch != null) {
                pkgArchitecture = arch;
            }

            // is it still not set?
            if (pkgArchitecture == Architecture.Auto || pkgArchitecture == Architecture.Unknown) {
                // figure it out from what's going in the package.
                Event<Error>.Raise(MessageCode.UnableToDeterminePackageArchitecture, null, "Unable to determine package architecture.");
            }

           

            var locations = Source.PackageRules.GetPropertyValues("location").Union(Source.PackageRules.GetPropertyValues("locations"));
            if( !locations.IsNullOrEmpty()) {
                Locations = new XList<Uri>();
                Locations.AddRange(locations.Select(location => location.ToUri()).Where(uri => uri != null));
            }

            var feeds = Source.PackageRules.GetPropertyValues("feed").Union(Source.PackageRules.GetPropertyValues("feeds"));
            if (!feeds.IsNullOrEmpty()) {
                Feeds = new XList<Uri>();
                Feeds.AddRange(feeds.Select(feed => feed.ToUri()).Where(uri => uri != null));
            }

            var publisher = Source.PackageRules.GetPropertyValue("publisher");

            if( !string.IsNullOrEmpty(publisher)) {
                var identityRules = Source.IdentityRules.GetRulesByParameter(publisher);
                if (!identityRules.IsNullOrEmpty()) {
                    PackageDetails.Publisher = new Identity {
                        Name = identityRules.GetPropertyValue("name"),
                        Email = identityRules.GetPropertyValue("email"),
                        Location = identityRules.GetPropertyValue("website").ToUri()
                    };
                }
            }
            CanonicalName = string.Format("coapp:{0}{1}-{2}-{3}-{4}", pkgName, pkgFlavor, pkgVersion, pkgArchitecture, pkgPublicKeyToken);
        }

        internal void UpdateApplicationManifests() {
            foreach( var manifestRule in Source.ManifestRules) {
                var filesToTouch = FileList.ProcessIncludes(null, manifestRule, "manifest", "include", Source.FileRules, Environment.CurrentDirectory);
                var depAsms = Enumerable.Empty<string>();

                if(manifestRule.HasProperty("assemblies")) {
                    depAsms = manifestRule["assemblies"].Values;
                } 

                if(manifestRule.HasProperty("assembly")) {
                    depAsms = depAsms.Union(manifestRule["assembly"].Values);
                } 

                foreach( var depAsm in depAsms) {
                    var pkt = string.Empty;
                    var ver = string.Empty;
                    var arch = Architecture;

                    var name = depAsm;
                    // find the assembly. it's either in this package, or in one of our dependencies.
                    var assembly = Assemblies.FirstOrDefault(each => each.Name == name);
                    if( assembly != null ) {
                        pkt = PublicKeyToken;
                        ver = Version.ToString();
                    } else {
                        // the assembly isn't in this package. 
                        // it must be in one of the dependent packages.
                        var depPackage =
                            DependentPackages.FirstOrDefault(each => each.Roles.Any(role => role.Name == name && role.PackageRole == PackageRole.Assembly));
                        if (depPackage == null) {
                            Event<Error>.Raise(
                                MessageCode.ManifestReferenceNotFound, manifestRule.SourceLocation,
                                "Assembly Reference for {0} not found in this package, or any dependencies", name);
                            continue;
                        }
                        pkt = depPackage.PublicKeyToken;
                        ver = depPackage.Version;
                    }

                    foreach( var file in filesToTouch) {
                        Console.WriteLine("Adding dependency to manifest [{0}] => {1} {2} {3} {4}", file.SourcePath, name, ver, arch, pkt);
                        _tasks.Add(Binary.Load(file.SourcePath).ContinueWith(antecedent => {
                            var binary = antecedent.Result;
                            binary.Manifest.Value.AddDependency(name, ver, arch, pkt);
                        }, TaskContinuationOptions.AttachedToParent));
                    }
                }
            }
        }

        private void CreateManagedAssemblyPolicies() {
            // create a policy assembly for each one of the policies required for each of the managed assemblies.
            var managedAssemblies = Assemblies.Where(each => each.IsManaged).ToArray();

            foreach (var managedAssembly in managedAssemblies) {
                foreach (var oldVersion in _versionRedirects) {
                    // create the policy file 
                    var policyXml = _managedPublisherConfiguration.FormatWithMacros(
                        Source.GetMacroValue, new {
                            Assembly = managedAssembly,
                            OldAssembly = new {
                                MajorMinorVersion = oldVersion.ToString(),
                                VersionRange = BindingRedirect.VersionRange
                            },
                        }, null, Source.PostprocessValue);

                    var policyConfigFile = "policy.{0}.{1}.dll.config".format(oldVersion.ToString(), managedAssembly.Name).GetFileInTempFolder();
                    var policyFile = "policy.{0}.{1}.dll".format(oldVersion.ToString(), managedAssembly.Name).GetFileInTempFolder();

                    // write out the policy config file
                    File.WriteAllText(policyConfigFile, policyXml);

                    var rc = Tools.AssemblyLinker.Exec("/link:{0} /out:{1} /v:{2}", policyConfigFile, policyFile, Version.ToString());
                    if (rc != 0) {
                        Event<Error>.Raise(
                            MessageCode.AssemblyLinkerError, null, "Unable to make policy assembly\r\n{0}",
                            Tools.AssemblyLinker.StandardError + Tools.AssemblyLinker.StandardOut);
                    }
                    // Logger.Message("About to sign {0}", policyFile);
                    DigitallySign(policyFile);
                    //Logger.Message("Should have signed {0}: Is Signed: {1}", policyFile, Verifier.HasValidSignature(policyFile));

                    // and now we can create assembly entries for these.
                    Assemblies.Add(new PackageAssembly(Path.GetFileName(policyFile), null, new[] {policyFile, policyConfigFile}));
                }
            }

        }

        private void CreateNativeAssemblyPolicies() {
            var nativeAssemblies = Assemblies.Where(each => each.IsNative).ToArray();
            foreach (var nativeAssembly in nativeAssemblies) {
                foreach (var oldVersion in _versionRedirects) {
                    var policyAssemblyName = "policy.{0}.{1}".format(oldVersion.ToString(), nativeAssembly.Name);

                    var policyManifest = new NativeManifest(null) {
                        AssemblyName = policyAssemblyName,
                        AssemblyArchitecture = Architecture,
                        AssemblyPublicKeyToken = PublicKeyToken,
                        AssemblyType = AssemblyType.win32policy,
                        AssemblyVersion = Version
                    };
                    policyManifest.AddDependency(nativeAssembly.Name, 0L, Architecture, PublicKeyToken, "*", AssemblyType.win32, BindingRedirect);

                    Assemblies.Add(new PackageAssembly(policyAssemblyName, null, policyManifest));
                }
            }
        }

        internal void CreateAssemblyPolicies() {
            // -----------------------------------------------------------------------------------------------------------------------------------
            // Step 5 : Build Assembly Manifests, catalog files and policy files
            var policyRule = Source.CompatabilityPolicyRules.FirstOrDefault();
            if (policyRule == null) {
                return;
            }

            // figure out what major/minor versions we need are overriding.
            var minimum = (FourPartVersion)policyRule["minimum"].Value;
            var maximum = (FourPartVersion)policyRule["maximum"].Value;

            if (minimum != 0 && maximum == 0) {
                maximum = Version - 1;
            }
            
            _versionRedirects = policyRule["versions"].Values.Select(each => (TwoPartVersion)each);

            if (_versionRedirects.IsNullOrEmpty()) {
                // didn't specify versions explicitly.
                // we can check for overriding versions.
                // TODO: SOON
            }

            if (maximum > 0L) {
                BindingPolicy = new BindingPolicy {
                    Minimum = minimum, 
                    Maximum = maximum
                };

                CreateNativeAssemblyPolicies();
                CreateManagedAssemblyPolicies();
            }
        }

        internal void ProcessCosmeticMetadata() {
            DisplayName = Source.PackageRules.GetPropertyValue("display-name");
            if( string.IsNullOrEmpty(DisplayName )) {
                DisplayName = Name;
            }

            // pull child icons from packages and insert as binaries locally.
            PackageDetails.Description = Source.MetadataRules.GetPropertyValue("description").LiteralOrFileText();
            PackageDetails.SummaryDescription = Source.MetadataRules.GetPropertyValue("summary");
            
            var iconFilename = Source.MetadataRules.GetPropertyValue("icon");

            if (File.Exists(iconFilename)) {
                try {
                    IconImage = Image.FromFile(iconFilename);
                }
                catch (Exception e) {
                    Event<Warning>.Raise(MessageCode.BadIconReference, Source.MetadataRules.GetProperty("icon").SourceLocation,
                        "Unable to use specified image for icon {0}", e.Message);
                }
            }
            else {
                Event<Warning>.Raise(MessageCode.NoIcon, null , "Image for icon not specified", iconFilename);
            }

            
            // ChildIcons = DependentPackages.ToXDictionary(each => each.CanonicalName.ToString(), each => each.Icon);

            var licenses = Source.MetadataRules.GetPropertyValues("licenses");
            if( licenses.Any()) {
                PackageDetails.Licenses = new XList<License>();
            }
            foreach( var l in licenses) {

                LicenseId lid;
                if( LicenseId.TryParse(l, true, out lid)) {
                    PackageDetails.Licenses.Add(new License { 
                        LicenseId = lid, 
                        Location = lid.GetUrl(), 
                        Name = lid.GetDescription(),
                       // Text = lid.GetText(),
                    });
                }

                // todo : let the user specify the license data in a license rule.
            }

            PackageDetails.AuthorVersion = Source.MetadataRules.GetPropertyValue("author-version");
            PackageDetails.BugTracker = Source.MetadataRules.GetPropertyValue("bug-tracker");
            var pubDate = Source.MetadataRules.GetPropertyValue("publish-date");
            PackageDetails.PublishDate = DateTime.Now;

            if( !string.IsNullOrEmpty(pubDate) && pubDate != "auto") {
                DateTime dt;
                if( DateTime.TryParse(pubDate,out dt) ) {
                    PackageDetails.PublishDate = dt;
                } else {
                    Event<Warning>.Raise(MessageCode.BadDate, Source.MetadataRules.GetProperty("publish-date").SourceLocation,
                       "Can't parse publish date {0}, assuming now", pubDate);
                }
            }
            
            PackageDetails.IsNsfw = Source.MetadataRules.GetPropertyValue("nsfw").IsTrue();
            PackageDetails.Stability = (sbyte)(Source.MetadataRules.GetPropertyValue("stability").ToInt32());

            PackageDetails.Tags = Source.MetadataRules.GetPropertyValues("tags").ToXList();
            

            var contributors = Source.MetadataRules.GetPropertyValues("contributors");

            if (!contributors.IsNullOrEmpty()) {
                PackageDetails.Contributors = new XList<Identity>();

                foreach( var contributor in contributors ) {
                    var identityRules = Source.IdentityRules.GetRulesByParameter(contributor);
                    if (!identityRules.IsNullOrEmpty()) {
                        PackageDetails.Contributors.Add(  new Identity {
                            Name = identityRules.GetPropertyValue("name"),
                            Email = identityRules.GetPropertyValue("email"),
                            Location = identityRules.GetPropertyValue("website").ToUri()
                        });
                    }
                }
            }


        }

        internal void ProcessCompositionRules() {
            CompositionData.CompositionRules = new List<CompositionRule>();

            // PackageCompositionRules = AllRules.GetRulesByName("package-composition");
            var compositionRuleCategories = Source.PackageCompositionRules.Select(each => each.Parameter).Distinct();
            
            foreach( var category in compositionRuleCategories) {
                var categoryRules = Source.PackageCompositionRules.GetRulesByParameter(category);
                foreach(var rule in categoryRules) {
                    foreach( var propertyName in rule.PropertyNames) {
                        CompositionAction type;

                        switch( propertyName ) {
                            case "symlink":
                            case "symlinks":
                            case "symlink-file":
                            case "symlink-files":
                            case "file-symlink":
                                type = CompositionAction.SymlinkFile;
                                break;

                            case "registry":
                            case "registry-keys":
                                type = CompositionAction.Registry;
                                break;

                            case "symlink-directory":
                            case "symlink-directories":
                            case "symlink-folder":
                            case "symlink-folders":
                            case "folder-symlink":
                            case "directory-symlink":
                                type = CompositionAction.SymlinkFolder;
                                break;

                            case "environment-variable":
                            case "environment-variables":
                                type = CompositionAction.EnvironmentVariable;
                                break;

                            case "shortcut":
                            case "shelllink":
                            case "shell-link":
                            case "shellink":

                            case "shortcuts":
                            case "shellinks":
                            case "shelllinks":
                            case "shell-links":
                                type = CompositionAction.Shortcut;
                                break;

                            case "file-copy":
                            case "copy-file":
                            case "copy-files":
                            case "copy":
                                type = CompositionAction.FileCopy;
                                break;

                            case "file-rewrite":
                            case "rewrite-file":
                            case "rewrite-files":
                            case "rewrite":
                                type = CompositionAction.FileRewrite;
                                break;


                            default:
                                Event<Error>.Raise(MessageCode.UnknownCompositionRuleType, rule.SourceLocation, "Unknown composition rule '{0}'",
                                    propertyName);
                                continue;
                        }

                        var propertyValue = rule[propertyName];

                        if (!propertyValue.Labels.IsNullOrEmpty()) {
                            foreach (var label in propertyValue.Labels) {
                                CompositionData.CompositionRules.Add(new CompositionRule {
                                    Action = type,
                                    Category = category,
                                    Destination = label,                // aka "Key"
                                    Source = propertyValue[label].Value // aka "Value"
                                });
                            }
                        }
                    }
                }
            }
        }
    }
}