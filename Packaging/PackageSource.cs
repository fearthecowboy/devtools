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

namespace CoApp.Packaging {
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Developer.Toolkit.Scripting.Languages.PropertySheet;
    using Client;
    using Toolkit.Collections;
    using Toolkit.Exceptions;
    using Toolkit.Extensions;
    using Toolkit.Tasks;

    public class PackageSource : PropertySheet {
        // collection of propertysheets

        // all the different sets of rules 
        public IEnumerable<Rule> AllRules {
            get {
                return Rules.Reverse();
            }
        }

        public IEnumerable<Rule> DefineRules {
            get {
                return AllRules.GetRulesById("define").GetRulesByName("*").ToArray();
            }
        }

        public IEnumerable<Rule> ApplicationRules { get { return AllRules.GetRulesByName("application"); } }
        public IEnumerable<Rule> AssemblyRules { get { return AllRules.GetRulesByName("assembly"); } }
        public IEnumerable<Rule> AssembliesRules { get { return AllRules.GetRulesByName("assemblies"); } }
        public IEnumerable<Rule> DeveloperLibraryRules { get { return AllRules.GetRulesByName("developer-library"); } }
        public IEnumerable<Rule> SourceCodeRules { get { return  AllRules.GetRulesByName("source-code"); } }
        public IEnumerable<Rule> ServiceRules { get { return AllRules.GetRulesByName("service"); } }
        public IEnumerable<Rule> WebApplicationRules { get { return AllRules.GetRulesByName("web-application"); } }
        public IEnumerable<Rule> FauxApplicationRules { get { return AllRules.GetRulesByName("faux-pax"); } }
        public IEnumerable<Rule> DriverRules { get { return AllRules.GetRulesByName("driver"); } }

        public IEnumerable<Rule> AllRoles { get {
            return ApplicationRules.Union(AssemblyRules).Union(AssembliesRules).Union(DeveloperLibraryRules).Union(SourceCodeRules).Union(ServiceRules).Union(WebApplicationRules).
                Union(DriverRules).Union(FauxApplicationRules);
        }}

        public IEnumerable<Rule> PackageRules { get {
            return AllRules.GetRulesByName("package");
        }}

        public IEnumerable<Rule> MetadataRules{ get {
            return AllRules.GetRulesByName("metadata");
        }}
        public IEnumerable<Rule> RequiresRules {
            get {
                return AllRules.GetRulesByName("requires");
            }
        }
        public IEnumerable<Rule> ProvidesRules {
            get {
                return AllRules.GetRulesByName("provides");
            }
        }
        public IEnumerable<Rule> CompatabilityPolicyRules {
            get {
                return AllRules.GetRulesByName("compatability-policy");
            }
        }
        public IEnumerable<Rule> ManifestRules {
            get {
                return AllRules.GetRulesByName("manifest");
            }
        }
        public IEnumerable<Rule> PackageCompositionRules {
            get {
                return AllRules.GetRulesByName("package-composition");
            }
        }
        public IEnumerable<Rule> IdentityRules {
            get {
                return AllRules.GetRulesByName("identity");
            }
        }
        public IEnumerable<Rule> SigningRules {
            get {
                return AllRules.GetRulesByName("signing");
            }
        }
        public IEnumerable<Rule> FileRules {
            get {
                return AllRules.GetRulesByName("files");
            }
        }

        public string SourceFile { get; set; }

        public IDictionary<string, string> MacroValues = new XDictionary<string, string>();

        public PackageSource(string sourceFile, IDictionary<string,string> macroValues ) {
            // load macro values 
            foreach (var k in macroValues.Keys) {
                MacroValues.Add(k, macroValues[k]);
            }

            // ------ Load Information to create Package 
            SourceFile = sourceFile;

            // load up all the specified property sheets
            LoadPropertySheets(SourceFile);

            // Determine the roles that are going into the MSI, and ensure we know the basic information for the package (ver, arch, etc)
            CollectRoleRules();
        }

        public void SavePackageFile( string destinationFilename ) {
            // we're only going to save the first packag file, the rest should just be 'support' files
            Save(destinationFilename);
        }

        public string PostprocessValue(string value) {
            if (!string.IsNullOrEmpty(value) && value.Contains("[]")) {
                return value.Replace("[]", "");
            }
            return value;
        }

        public string GetMacroValueImpl(string valuename) {
            if (valuename == "DEFAULTLAMBDAVALUE") {
                return "${packagedir}\\${each.Path}";
            }

            string defaultValue = null;

            if (valuename.Contains("??")) {
                var prts = valuename.Split(new[] {'?'}, StringSplitOptions.RemoveEmptyEntries);
                defaultValue = prts.Length > 1 ? prts[1].Trim() : string.Empty;
                valuename = prts[0];
            }

            var parts = valuename.Split('.');
            if (parts.Length > 0) {
                if (parts.Length == 3) {
                    var result = AllRules.GetRulesByName(parts[0]).GetRulesByParameter(parts[1]).GetPropertyValue(parts[2]);
                    if (result != null) {
                        return result;
                    }
                }

                if (parts.Length == 2) {
                    var result = AllRules.GetRulesByName(parts[0]).GetPropertyValue(parts[1]);
                    if (result != null) {
                        return result;
                    }
                }

                // still not found?
                if (parts[0].Equals("package", StringComparison.InvariantCultureIgnoreCase)) {
                    var result = this.SimpleEval(valuename.Substring(8));
                    if (result != null && !string.IsNullOrEmpty(result.ToString())) {
                        return result.ToString();
                    }
                }
            }

            return DefineRules.GetPropertyValue(valuename) ?? (MacroValues.ContainsKey(valuename.ToLower()) ? MacroValues[valuename.ToLower()] : Environment.GetEnvironmentVariable(valuename)) ?? defaultValue;
        }

        public IEnumerable<object> GetFileCollection(string collectionname) {
            // we use this to pick up file collections.
            var fileRule = FileRules.FirstOrDefault(each => each.Parameter == collectionname);

            if (fileRule == null) {
                var collection = GetMacroValue(collectionname);
                if (collection != null) {
                    return collection.Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries).Select(each => each.Trim());
                }

                Event<Error>.Raise(MessageCode.UnknownFileList, null, "Reference to unknown file list '{0}'", collectionname);
            } else {
                var list = FileList.GetFileList(collectionname, FileRules);
                return list.FileEntries.Select(each => new {
                    Path = each.DestinationPath,
                    Name = Path.GetFileName(each.DestinationPath),
                    Extension = Path.GetExtension(each.DestinationPath),
                    NameWithoutExtension = Path.GetFileNameWithoutExtension(each.DestinationPath),
                });
            }

            return Enumerable.Empty<object>();
        }

        public void LoadPropertySheets(string autopackageSourceFile) {
            if (!File.Exists(autopackageSourceFile.GetFullPath())) {
                throw new ConsoleException("Can not find autopackage file '{0}'", autopackageSourceFile.GetFullPath());
            }

            PropertySheetParser.Parse(File.ReadAllText(autopackageSourceFile), autopackageSourceFile, this);
            
            GetCollection += GetFileCollection;
            GetMacroValue += GetMacroValueImpl;
            PostprocessProperty += PostprocessValue;

            // this is the collection of rules for all the #define category. (macros)
            

        }

        public void CollectRoleRules() {
            // -----------------------------------------------------------------------------------------------------------------------------------
            // Determine the roles that are going into the MSI, and ensure we know the basic information for the package (ver, arch, etc)
            // check for any roles...
            if (!AllRoles.Any()) {
                Event<Error>.Raise(
                    MessageCode.ZeroPackageRolesDefined, null,
                    "No package roles are defined. Must have at least one of {{ application, assembly, service, web-application, developer-library, source-code, driver }} rules defined.");
            }
        }
    }
}