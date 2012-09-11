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

namespace CoApp.Developer.Toolkit.Publishing {
    using CoApp.Toolkit.Win32;

    public class AssemblyReference {
        public string Name;
        public FourPartVersion Version;
        public Architecture Architecture;
        public string PublicKeyToken;
        public string Language;
        public AssemblyType AssemblyType;
        public BindingRedirect BindingRedirect;
    }
}