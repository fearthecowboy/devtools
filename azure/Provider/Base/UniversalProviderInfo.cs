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

namespace CoApp.UniversalFileAccess.Base {
    using System.Collections.Generic;
    using System.Linq;
    using System.Management.Automation;
    using Developer.Toolkit.Scripting.Languages.PropertySheet;
    using Toolkit.Extensions;

    public abstract class UniversalProviderInfo : ProviderInfo, ILocationResolver {
        protected readonly PropertySheet PropertySheet;
        protected abstract string Prefix {get;}

        protected UniversalProviderInfo(ProviderInfo providerInfo) : base(providerInfo) {
            PropertySheet = PropertySheet.Parse(@"@import ""pstab.properties"";", "default");
        }

        public IEnumerable<Rule> Aliases {
            get {
                return PropertySheet.Rules.Where(each => each.Name == Prefix).Where(alias => !alias.HasProperty("disabled") || !alias["disabled"].Value.IsTrue());
            }
        }

        public abstract ILocation GetLocation(string path);
    }
}