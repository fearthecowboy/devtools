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

namespace CoApp.Scripting.Powershell {
    using System;
    using System.Collections.Generic;
    using System.Dynamic;
    using System.Linq;
    using System.Management.Automation;
    using System.Management.Automation.Runspaces;
    using System.Threading.Tasks;
    using Developer.Toolkit.Collections;
    using Toolkit.Collections;

    public class DynamicPowershell : DynamicObject , IDisposable {
        private RunspacePool _runspacePool;
        private EnumerableForMutatingCollection<PSObject, object> _lastResult;
        
        private IDictionary<string, PSObject> _commands;
        private IDictionary<string, PSObject> _aliases;
        private PowerShell _powershell;

        public DynamicPowershell() {
            Reset();
            RefreshCommandList();
        }

        private string GetPropertyValue( PSObject obj, string propName ) {
            var property = obj.Properties.FirstOrDefault(prop => prop.Name ==propName);
            return property != null ? property.Value.ToString() : null;
        }

        public void Reset() {
            if (_runspacePool != null) {
                _runspacePool.Dispose();
            }
            _runspacePool = RunspaceFactory.CreateRunspacePool();
            _runspacePool.Open();

            _powershell = PowerShell.Create();
            _powershell.RunspacePool = _runspacePool;
        }

        public void WaitForResult() {
            _lastResult.Wait();
            _lastResult = null;
        }

        private void RefreshCommandList() {
            _powershell.Commands.Clear();

            var items = _powershell.AddCommand("get-command").Invoke();
            _commands = new XDictionary<string, PSObject>();

            foreach( var item in items ) {
                var name = GetPropertyValue(item, "Name").ToLower().Replace("-","");
                if( !string.IsNullOrEmpty(name)) {
                    _commands.Add(name, item);
                }
            }

            _powershell.Commands.Clear();
            var aliases = _powershell.AddCommand("get-alias").Invoke();
            _aliases = new XDictionary<string, PSObject >();

            foreach (var alias in aliases) {
                var name = GetPropertyValue(alias, "Name").ToLower().Replace("-", "");
                if (!string.IsNullOrEmpty(name)) {
                    _aliases.Add(name, alias);
                }
            }
        }

        public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result) {
            if (_lastResult != null) {
                WaitForResult();
            }

            var name = binder.Name.ToLower();
            if( !_commands.ContainsKey(name) &&  !_aliases.ContainsKey(name) ) {
                RefreshCommandList();
            }
            var cmd = _commands.ContainsKey(name) ? _commands[name] : null;
            var alias = _aliases.ContainsKey(name) ? _aliases[name] : null;
            var item = cmd ?? alias;
            if( item == null ) {
                result = null;
                return false;
            }
          

            var output = new PSDataCollection<PSObject>();
            _lastResult = new EnumerableForMutatingCollection<PSObject,object>(output, each => each.ImmediateBaseObject);
            output.DataAdded += (sender, eventArgs) => _lastResult.ElementAdded();
            
            Task.Factory.StartNew(() => {
                var actual = GetPropertyValue(item, "Name");
                var namedCount = binder.CallInfo.ArgumentNames.Count();
                var unnamedCount = args.Length - namedCount;

                _powershell.Commands.Clear();
                _powershell.AddCommand(actual);
                for (var i = 0; i < unnamedCount; i++) {
                    _powershell.AddArgument(args[i]);
                }
                for (var i = 0; i < namedCount; i++) {
                    _powershell.AddParameter(binder.CallInfo.ArgumentNames[i], args[i + unnamedCount]);
                }

                var input = new PSDataCollection<object>();
                input.Complete();

                output.DataAdded += (sender, eventArgs) => _lastResult.ElementAdded();

                var asyncResult = _powershell.BeginInvoke(input, output);

                _powershell.EndInvoke(asyncResult);
                _lastResult.Completed();
            });
            result = _lastResult;
            return true;
        }

        public void Dispose() {
            _runspacePool.Dispose();
        }
    }
}