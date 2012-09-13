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
//------------------------------------------------------------  -----------

namespace CoApp.Scripting.Commands {
    using System.Collections.Generic;
    using System.Linq;
    using System.Management.Automation;
    using System.Management.Automation.Runspaces;
    using Developer.Toolkit.Scripting.Languages.PropertySheet;
    using Service;
    using Toolkit.Extensions;

    [Cmdlet(VerbsCommon.New, "RestService")]
    public class NewRestService :Cmdlet {
        [Parameter]
        public SwitchParameter Auto { get; set; }

        [Parameter]
        public string Config { get; set; }

        [Parameter]
        public string Name {get; set;}

        [Parameter]
        public string[] ListenOn {get; set;}

        [Parameter()]
        public string[] Command { get; set; }

        private IEnumerable<string> ActivePowershellModules {
            get {
                return Runspace.DefaultRunspace.CreateNestedPipeline("get-module", false).Invoke().Select(each => each.ImmediateBaseObject as PSModuleInfo).Where(each => each != null).Select(each => each.Path);
            }
        }

        protected override void ProcessRecord() {
            if( Auto ) {
                Config = "restservice.properties";
            }

            if( !string.IsNullOrEmpty(Config) ) {
                var propertySheet = PropertySheet.Parse(@"@import @""{0}"";".format(Config), "default");
                foreach( var srv in propertySheet.Rules.Where( rule => rule.Name == "rest-service" ) ) {
                    var cmds = srv["command"].Values.Union(srv["commands"].Values);
                    var listenOn = srv["listen-on"].Values;
                    ConfigRestService(srv.Parameter.ToLower(), cmds, listenOn);
                }
            } else {
                ConfigRestService(Name.ToLower(), Command, ListenOn);
            }
        }

        private void ConfigRestService(string name, IEnumerable<string> cmds, IEnumerable<string> listenOn) {
            if(string.IsNullOrEmpty(name)) {
                name = "default";
            }

            var instance = RestAppHost.Instances.ContainsKey(name) ? RestAppHost.Instances[name] : (RestAppHost.Instances[name] = new RestAppHost(ActivePowershellModules));

            // if the instance is already started, we really need to make a new one.
            if(instance.Started) {
                instance.Stop();
                using(var oldInstance = instance) {
                    instance = RestAppHost.Instances[name] = new RestAppHost(oldInstance, ActivePowershellModules);
                }
            }

            // add commands
            if(cmds != null) {
                foreach(var cmd in cmds) {
                    instance.Add(cmd);
                }
            }

            // add listen urls
            if(listenOn != null) {
                foreach(var url in listenOn) {
                    instance.AddListener(url);
                }
            }

            WriteObject("Created REST Service '{0}'".format(name));
        }
    }
}