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


namespace CoApp.Ptk {
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using Developer.Toolkit.Exceptions;
    using Developer.Toolkit.Scripting.Languages.PropertySheet;
    using Packaging.Client;
    using Toolkit.Collections;
    using Toolkit.Exceptions;
    using Toolkit.Extensions;
    using Toolkit.Linq;
    using Toolkit.Utility;
    using Toolkit.Win32;

    internal class pTkMain {
        /// <summary>
        /// Help message for the user
        /// </summary>
        private const string help =
            @"
Usage:
-------

pTK [options] action [buildconfiguration...]
    
    Options:
    --------
    --help                      this help
    --nologo                    don't display the logo
    --load-config=<file>        loads configuration from <file>
    --verbose                   prints verbose messages

    --rescan-tools              rescan for tool paths
    --show-tools                prints the path of the tools 

    --load=<file>               loads the build ptk buildinfo
                                defaults to .\COPKG\.buildinfo 

    --mingw-install=<path>      specifies the location of the mingw install
    --msys-install=<path>       specifies the location of the msys install

    --<var>=<value>             sets an environment/macro variable for the 
                                whole ptk run.

    --skip-built                checks targets before running build and skips
                                the build-command if the targets all exist.

    Actions:
        build                   builds the product

        clean                   removes all files that are not part of the 
                                project source

        status                  shows any files present that should not be

        verify                  ensures that the product source matches the 
                                built and cleaned

        list                    lists availible builds from buildinfo

    [buildconfiguration]        optional; indicates the builds from the 
                                buildinfo file to act on. Defaults to all

";

//        trace                   performs a build using CoApp Trace to gather 
//                                build data 


        private PackageManager _easy = new PackageManager();


        /// <summary>
        /// Wrapper for the Windows command line
        /// </summary>
        private ProcessUtility _cmdexe;
        /// <summary>
        /// Wrapper for git (source control)
        /// </summary>
        private ProcessUtility _gitexe;
        /// <summary>
        /// Wrapper for mercurial (source control)
        /// </summary>
        private ProcessUtility _hgexe;
        /// <summary>
        /// Wrapper for pTk (That's us!)
        /// </summary>
        private ProcessUtility _ptk;
        /// <summary>
        /// Wrapper for Trace. (Trace tells us what the build process does)
        /// </summary>
        private ProcessUtility _traceexe;
        
        private string _gitcmd;
        // private string _setenvcmd;
        // private string _vcvars;
        /// <summary>
        /// Command line to git.cmd
        /// </summary>
        // sdk batch file locations
        private string _setenvcmd71;
        private string _setenvcmd7;
        private string _setenvcmd6;
        /* private string _setenvcmdFeb2003; */

        private string _wdksetenvcmd7600;

        // compiler batch file locations
        private string _vcvarsallbat10;
        private string _vcvarsallbat9;
        private string _vcvarsallbat8;
        private string _vcvarsallbat7;
        private string _vcvarsallbat71;
        private string _vcvars32bat;


        private bool _useGit;
        private bool _useHg;
        /// <summary>
        /// Does the user want us to print more?
        /// </summary>
        private bool _verbose;
        private bool _skipBuilt;
        private XDictionary<string, string> _originalEnvironment = GetEnvironment();
        /// <summary>
        /// Tell the user which tools we are using?
        /// </summary>
        private bool _showTools;
        /// <summary>
        /// A list of temporary files for bookkeeping
        /// </summary>
        private readonly List<string> _tmpFiles= new List<string>();
        private string _searchPaths = "";
        internal static PropertySheet _propertySheet;
        internal Rule[] DefineRules;

        /// <summary>
        /// Entry Point
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private static int Main(string[] args) {
            return new pTkMain().main(args);
        }

        /// <summary>
        /// Get the environment variables (key/value pairs)
        /// </summary>
        /// <remarks>
        /// Path variable may differ in output from actual path on some systems
        /// Run «reg query "hklm\system\currentcontrolset\control\Session manager\Environment" /v path» to verify
        /// Character limit for path on Vista is 1024 http://support.microsoft.com/kb/924032
        /// </remarks>
        /// <returns>A dictionary of path variables as strings</returns>
        private static XDictionary<string, string> GetEnvironment() {
            var env = Environment.GetEnvironmentVariables();
            return env.Keys.Cast<object>().ToXDictionary(key => key.ToString(), key => env[key].ToString());
        }

        /// <summary>
        /// Resets application Environment 
        /// </summary>
        private void ResetEnvironment() {
            foreach( var key in Environment.GetEnvironmentVariables().Keys ) {
                Environment.SetEnvironmentVariable(key.ToString(),string.Empty);    
            }
            foreach (var key in _originalEnvironment.Keys) {
                Environment.SetEnvironmentVariable(key, _originalEnvironment[key]);    
            }
        }

        private void SetVCCompiler(string compilerName, string compilerBatchFile, string arch) {
            if (_verbose) {
                using (new ConsoleColors(ConsoleColor.White, ConsoleColor.Black)) {

                    Console.Write("Setting VC Compiler: ");
                }
                using (new ConsoleColors(ConsoleColor.Green, ConsoleColor.Black)) {
                    Console.Write(compilerName);
                }
                using (new ConsoleColors(ConsoleColor.Yellow, ConsoleColor.Black)) {
                    Console.WriteLine(" for [{0}]", arch);
                }
            }

            if (string.IsNullOrEmpty(compilerBatchFile))
                throw new CoAppException("Cannot locate Visual C++ vcvars batch file command. Please install {0} (and use --rescan-tools). ".format(compilerName));

            // _cmdexe.Exec(@"/c ""{0}"" /{1} & set ", compilerBatchFile, arch == "x86" ? "x86" : "x64");
            // thanks Raggles!
            var archToSet = string.Empty;
            if (arch == "x86")
                archToSet = "x86";
            else if (Environment.Is64BitOperatingSystem && arch == "x64")
                archToSet = "amd64";
            else if (!Environment.Is64BitOperatingSystem && arch == "x64")
                archToSet = "x86_amd64";

            _cmdexe.Exec(@"/c ""{0}"" {1} & set ", compilerBatchFile, archToSet);

            foreach (var x in _cmdexe.StandardOut.Split("\r\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries)) {
                var p = x.IndexOf("=");
                if( p > 0 ) {
                    Environment.SetEnvironmentVariable(x.Substring(0, p), x.Substring(p + 1));
                }
            }
        }



        private void SetSDK(string sdkName, string sdkBatchFile, string arch) {
            if (_verbose) {
                using (new ConsoleColors(ConsoleColor.White, ConsoleColor.Black)) {
                    Console.Write("Setting SDK: ");
                }
                using (new ConsoleColors(ConsoleColor.Green, ConsoleColor.Black)) {
                    Console.Write(sdkName);
                }
                using (new ConsoleColors(ConsoleColor.Yellow, ConsoleColor.Black)) {
                    Console.WriteLine(" for [{0}]", arch);
                }
            }
            var targetCpu = Environment.GetEnvironmentVariable("TARGET_CPU");

            if (string.IsNullOrEmpty(targetCpu) || (targetCpu == "x64" && arch == "x86") || (targetCpu == "x86" && arch != "x86")) {

                if (string.IsNullOrEmpty(sdkBatchFile))
                    throw new CoAppException("Cannot locate SDK SetEnv command for SDK ({0}). Please install the Windows SDK {0}".format(sdkName));

                // Console.WriteLine(@"/c ""{0}"" /{1} & set ", _setenvcmd, arch == "x86" ? "x86" : "x64");

                _cmdexe.Exec(@"/c ""{0}"" /{1} & set ", sdkBatchFile, arch == "x86" ? "x86" : "x64");

                foreach (var x in _cmdexe.StandardOut.Split("\r\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries)) {
                    var p = x.IndexOf("=");
                    if( p > 0 ) {
                        Environment.SetEnvironmentVariable(x.Substring(0, p), x.Substring(p + 1));
                    }
                }
            }
        }

        /// <summary>
        /// Set up environment and paths to use mingw
        /// </summary>
        /// <param name="arch">A string indicating the target platform</param>
        private void SetMingwCompiler( string arch) {
            if (_verbose) {

                using (new ConsoleColors(ConsoleColor.White, ConsoleColor.Black)) {
                    Console.Write("Setting Compiler: ");
                }
                using (new ConsoleColors(ConsoleColor.Green, ConsoleColor.Black)) {
                    Console.Write("mingw");
                }
                using (new ConsoleColors(ConsoleColor.Yellow, ConsoleColor.Black)) {
                    Console.WriteLine(" for [{0}]", arch);
                }
            }

            var mingwProgramFinder = new ProgramFinder("", Directory.GetDirectories(@"c:\\", "M*").Aggregate(_searchPaths+@"%ProgramFiles(x86)%;%ProgramFiles%;%ProgramW6432%", (current, dir) => dir + ";" + current));

            var gcc = mingwProgramFinder.ScanForFile("mingw32-gcc.exe");
            var msysmnt = mingwProgramFinder.ScanForFile("msysmnt.exe");

            if( string.IsNullOrEmpty(gcc)) {
                throw new ConsoleException("Unable to locate MinGW install location. Use --mingw-install=<path>\r\n   (it will remember after that.)");
            }

            if (string.IsNullOrEmpty(msysmnt)) {
                throw new ConsoleException("Unable to locate MSYS install location. Use --msys-install=<path>\r\n   (it will remember after that.)");
            }

            var msysBin = Path.GetDirectoryName(msysmnt);
            var msysPath = Path.GetDirectoryName(msysBin);

            var msysLocalBin = Path.Combine(msysPath, "local", "bin");
            var mingwBin = Path.GetDirectoryName(gcc);
            var mingwPath = Path.GetDirectoryName(mingwBin);
            var username = Environment.GetEnvironmentVariable("USERNAME") ?? "";

            var newPath = ".;" + mingwBin + ";" + msysBin + ";" + msysLocalBin + ";" + Environment.GetEnvironmentVariable("PATH");
            Environment.SetEnvironmentVariable("PATH", newPath);

            var tmpPath = Environment.GetEnvironmentVariable("TMP") ??
                Environment.GetEnvironmentVariable("TEMP") ??
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp").Replace("\\", "/");

            Environment.SetEnvironmentVariable("TMP", tmpPath);
            Environment.SetEnvironmentVariable("TEMP", tmpPath);

            Environment.SetEnvironmentVariable("WD", msysBin);
            Environment.SetEnvironmentVariable("TERM", "cygwin");

            var homedir = Environment.GetEnvironmentVariable("HOME");
            if( string.IsNullOrEmpty(homedir) ) {
                homedir = Path.Combine(Path.Combine(msysPath, "home"), username);
                if (!Directory.Exists(homedir)) {
                    homedir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                }
                Environment.SetEnvironmentVariable("HOME", homedir.Replace("\\", "/"));
            }
            
            Environment.SetEnvironmentVariable("HISTFILE", Path.Combine(homedir, ".bashhistory").Replace("\\", "/"));

            Environment.SetEnvironmentVariable("LOGNAME", username);
            Environment.SetEnvironmentVariable("MAKE_MODE", "unix");
            Environment.SetEnvironmentVariable("MSYSCON", "sh.exe");
            Environment.SetEnvironmentVariable("MSYSTEM", "MINGW32");
          
        }

        /// <summary>
        /// Change the designated compiler
        /// </summary>
        private void SwitchCompiler(string compiler, string platform) {
           
            switch( compiler ) {
                case "vc10":
                    SetVCCompiler("Visual Studio 2010", _vcvarsallbat10, platform);
                    break;

                case "vc9":
                    SetVCCompiler("Visual Studio 2008", _vcvarsallbat9, platform);
                    break;

                case "vc8":
                    SetVCCompiler("Visual Studio 2005", _vcvarsallbat8, platform);
                    break;

                case "vc7.1":
                    SetVCCompiler("Visual Studio 2003", _vcvarsallbat71, platform);
                    break;

                case "vc7":
                    SetVCCompiler("Visual Studio 2002", _vcvarsallbat7, platform);
                    break;

                case "vc6":
                    SetVCCompiler("Visual Studio 98 (vc6)", _vcvars32bat, platform);
                    break;

                case "sdk7.1":
                    SetSDK("Windows Sdk 7.1", _setenvcmd71, platform);
                    break;

                case "sdk7":
                    SetSDK("Windows Sdk 7", _setenvcmd7, platform);
                    break;

                case "sdk6":
                    SetSDK("Windows Sdk 6", _setenvcmd6, platform);
                    break;

                case "mingw":
                    SetMingwCompiler(platform);
                    break;

                default :
                    throw new ConsoleException("Unknown Compiler Selection: {0}", compiler);
            }
        }

        private void SetWDK( Rule build , string platform ) {
            // pick up any wdk settings from the build/wdk property
            var frechk = "fre";
            var target = "XP";

            var wdkProperty = build.HasProperty("wdk") ? build["wdk"] : null;

            if( wdkProperty != null ) {
                frechk = wdkProperty["frechk"] == null ? "fre" : wdkProperty["frechk"].Value;
                target = wdkProperty["target"] == null ? "xp" : wdkProperty["target"].Value;
            }
            // start with the directory for the wdk 
            var ddkLocation = Path.GetDirectoryName(_wdksetenvcmd7600.GetFullPath());
            if( ddkLocation.EndsWith("bin")) {
                ddkLocation = Path.GetDirectoryName(ddkLocation);
            }

            if( ddkLocation.IndexOf(' ') > -1 ) {
                ddkLocation = "\"{0}\"".format(ddkLocation);
            }

            Environment.SetEnvironmentVariable("current_wdk_location", ddkLocation);
            
            var cmdline = ddkLocation;

            // set the free/checked flag
            switch( frechk.ToLower() ) {
                case "check":
                case "checked":
                case "chk":
                    cmdline += " chk";
                    Environment.SetEnvironmentVariable("current_wdk_freechk", "chk" );
                    break;

                default:
                    cmdline += " fre";
                    Environment.SetEnvironmentVariable("current_wdk_freechk",  "fre");
                    break;
            }

            // platform choice
            cmdline += " "+platform.NormalizePlatform();

            // target OS
            switch( target.ToLower() ) {
                case "wlh":
                case "lh":
                case "vista":
                case "server2008":
                case "2008":
                case "windows6":
                case "win6":
                case "6":
                    cmdline += " WLH";
                    Environment.SetEnvironmentVariable("current_wdk_target", "WLH" );
                    break;

                case "win7":
                case "7":
                case "win6.1":
                case "6.1":
                case "2008r2":
                case "r2":
                case "server2008r2":
                    cmdline += " WIN7";
                    Environment.SetEnvironmentVariable("current_wdk_target", "WIN7");
                    break;

                case "wnet":
                case "2003":
                case "server2003":
                case "5.2":
                case "win5.2":
                    cmdline += " WNET";
                    Environment.SetEnvironmentVariable("current_wdk_target", "WNET");
                    break;
                
                case "HAL":
                    cmdline += " HAL";
                    Environment.SetEnvironmentVariable("current_wdk_target", "HAL");
                    break;

                default:
                    cmdline += " WXP";
                    Environment.SetEnvironmentVariable("current_wdk_target", "WXP");
                    break;
            }

            Console.WriteLine(@"WDK COMMAND LINE: ""{0}"" {1} & set ", _wdksetenvcmd7600, cmdline );

            _cmdexe.Exec(@"/c ""{0}"" {1} & set ", _wdksetenvcmd7600, cmdline );

            foreach (var x in _cmdexe.StandardOut.Split("\r\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries)) {
                var p = x.IndexOf("=");
                if (p > 0) {
                    Environment.SetEnvironmentVariable(x.Substring(0, p), x.Substring(p + 1));
                }
            }
        }

        private void SwitchSdk( string sdk, string platform ) {

            switch (sdk) {
                case "sdk7.1":
                    SetSDK("Windows Sdk 7.1", _setenvcmd71, platform);
                     break;

                case "sdk7":
                    SetSDK("Windows Sdk 7", _setenvcmd7, platform);
                    break;

                case "sdk6":
                    SetSDK("Windows Sdk 6", _setenvcmd6, platform);
                    break;

                case "feb2003":
                    SetSDK("Platform SDK Feb 2003", _setenvcmd6, platform);
                    break;

                case "none":
                    break;

                default:
                    throw new ConsoleException("Unknown Compiler Selection: {0}", sdk);

            }
        }

        private string[] sessionFeeds;

        /// <summary>
        /// This is the main procedure
        /// </summary>
        /// <param name="args">Command line parameters</param>
        /// <returns>Error codes (0 for success, non-zero on Error)</returns>
        private int main(IEnumerable<string> args) {
            var options = args.Where(each => each.StartsWith("--")).Switches();
            var parameters = args.Parameters().ToArray();

            var buildinfo = string.Empty;

            Console.CancelKeyPress += (x, y) => {
                Console.WriteLine("Stopping ptk.");
                if (_cmdexe != null)
                    _cmdexe.Kill();
                if (_gitexe != null)
                    _gitexe.Kill();
                if (_hgexe != null)
                    _hgexe.Kill();
                if (_ptk != null)
                    _ptk.Kill();
//                if (_traceexe != null) {
  //                  _traceexe.Kill();
    //            }
            };
            

            #region Parse Options

            // set up options which were defined by the user
            foreach (string arg in options.Keys) {
                IEnumerable<string> argumentParameters = options[arg];

                switch (arg) {
                    case "nologo":
                        // disable logo (will print "\n" anyway)
                        this.Assembly().SetLogo("");
                        break;

                    case "verbose":
                        _verbose = true;
                        break;

                    case "skip-built":
                        _skipBuilt = true;
                        _originalEnvironment.AddOrSet("skip-built", "true");
                        break;

                    case "load":
                        // user specified a custom PropertySheet
                        buildinfo = argumentParameters.LastOrDefault().GetFullPath();
                        if (!File.Exists(buildinfo)) {
                            return Fail("Unable to find buildinfo file [{0}]. \r\n\r\n    Use --help for command line help.", buildinfo);
                        }

                        break;

                    case "mingw-install":
                    case "msys-install":
                        _searchPaths += argumentParameters.LastOrDefault().GetFullPath() + ";";
                        break;

                    case "rescan-tools":
                        ProgramFinder.IgnoreCache = true;
                        break;

                    case "show-tools":
                        _showTools = true;
                        break;

                    case "help":
                        return Help();

                    default:
                        var p = argumentParameters.LastOrDefault();
                        if (string.IsNullOrEmpty(p)) {
                            if( _originalEnvironment.ContainsKey(arg) ) {
                                _originalEnvironment.Remove(arg);
                            }
                        } else{
                            _originalEnvironment.AddOrSet(arg, p);
                        }
                        break;
                }
            }

            // _originalEnvironment.Add("COAPP", CoApp.Toolkit.Engine.PackageManagerSettings.CoAppRootDirectory);
            _originalEnvironment.AddOrSet("COAPP", "C:/programdata/");

            while (string.IsNullOrEmpty(buildinfo) || !File.Exists(buildinfo)) {
                // if the user didn't pass in the file, walk up the tree to find the first directory that has a COPKG\.buildinfo file 
                buildinfo = (from a in @".\COPKG\".DirectoryEnumerateFilesSmarter("*.buildinfo", SearchOption.TopDirectoryOnly)
                                     orderby a.Length ascending
                                     select a.GetFullPath()).FirstOrDefault() ?? @".\COPKG\.buildinfo".GetFullPath();

                // try the parent directory.
                var p = Path.GetDirectoryName(Environment.CurrentDirectory);
                if (string.IsNullOrEmpty(p)) {
                    return Fail("Unable to find buildinfo file [COPKG\\.buildinfo]--Even walking up the current tree.\r\n\r\n    Use --help for command line help.");
                }
                Environment.CurrentDirectory = p;
            }

            // make sure that we're in the parent directory of the .buildinfo file.
            Environment.CurrentDirectory = Path.GetDirectoryName(Path.GetDirectoryName(buildinfo));

            sessionFeeds = new[] {Environment.CurrentDirectory, Path.GetDirectoryName(buildinfo)};



            // tell the user what we are
            Logo();


            
            #endregion

            // connect to coapp service
            
            // set up several tools we need
            _cmdexe = new ProcessUtility("cmd.exe");
            //var f = new ProgramFinder("").ScanForFile("trace.exe");

            //if(!string.IsNullOrEmpty(f)) {
                //_traceexe = new ProcessUtility(new ProgramFinder("").ScanForFile("trace.exe"));
            //}

            _ptk = new ProcessUtility(Assembly.GetEntryAssembly().Location);
            // if this package is tracked by git, we can use git
            _useGit = Directory.Exists(".git".GetFullPath());            // if this package is tracked by mercurial, we can use mercurial
            _useHg = _useGit ? false : Directory.Exists(".hg".GetFullPath());

            // source control is mandatory! create a repository for this package
            if (!(_useGit || _useHg) && _verbose) {
                Console.WriteLine("WARNING: Source should be checked out using git or hg-git.");
            }

            // find git in the file system
            // - we prefer the CMD script over git.exe
            // git.exe may be located at "C:\Program Files\Git\bin"
            // git.cmd may be located at "C:\Program Files\Git\cmd"
            if (_useGit) {
                if (_verbose) {
                    Console.WriteLine("Using git for verification");
                }
                // attemt to find git.cmd
                _gitcmd = ProgramFinder.ProgramFilesAndDotNet.ScanForFile("git.cmd");
                _gitexe = null;
                if (string.IsNullOrEmpty(_gitcmd)) {
                    var f = ProgramFinder.ProgramFilesAndDotNet.ScanForFile("git.exe");
                    if (string.IsNullOrEmpty(f)) {
                        return Fail("Can not find git.cmd or git.exe (required to perform verification.)");
                    }
                    _gitexe = new ProcessUtility(ProgramFinder.ProgramFilesAndDotNet.ScanForFile("git.exe"));
                }
            }

            if (_useHg) {
                var f = ProgramFinder.ProgramFilesAndDotNet.ScanForFile("hg.exe");
                if (string.IsNullOrEmpty(f)) {
                    return Fail("Can not find hg.exe (required to perform verification.)");
                }
                _hgexe = new ProcessUtility(f);
            }

            // find sdk batch files.

            /* FIXME: URGENT.
             * C:\Program Files (x86)\Microsoft Visual Studio 8\VC\vcvarsall.bat
                C:\Program Files (x86)\Microsoft Visual Studio 8\VC\bin\vcvars32.bat
                C:\Program Files (x86)\Microsoft Visual Studio 8\VC\bin\amd64\vcvarsamd64.bat
                C:\Program Files (x86)\Microsoft Visual Studio 8\VC\bin\x86_amd64\vcvarsx86_amd64.bat
             * VC7.0: C:\Program Files\Microsoft Visual Studio .NET\Vc7\bin\vcvars32.bat
             * */
#if NOT_READY

            var s71 = 
                (from 
                    file in ProgramFinder.ProgramFilesAndDotNetAndSdk["setenv.cmd" , "7.1"]
                where 
                    file.FolderList.ContainsAllAsWildcards("*sdk*", "v7.1") && 
                    !file.FolderList.ContainsAnyAsWildcards("Windows Azure SDK", "winddk") 
                select 
                    file).Remember();


            var setEnvCmd71 = ProgramFinder.ProgramFilesAndDotNetAndSdk["setenv.cmd", "7.1"];
#endif 


            _setenvcmd71 = ProgramFinder.ProgramFilesAndDotNetAndSdk.ScanForFile("setenv.cmd", excludeFilters: new[] { @"**\Windows Azure SDK\**" , "**winddk**" }, includeFilters: new [] {"**sdk**", "**v7.1**"}, rememberMissingFile:true, tagWithCosmeticVersion:"7.1");
            _setenvcmd7 = ProgramFinder.ProgramFilesAndDotNetAndSdk.ScanForFile("setenv.cmd", excludeFilters: new[] { @"**\Windows Azure SDK\**", "**7.1**", "**winddk**" }, includeFilters: new[] { "**sdk**", "**v7**" }, rememberMissingFile: true, tagWithCosmeticVersion: "7.0");
            _setenvcmd6 = ProgramFinder.ProgramFilesAndDotNetAndSdk.ScanForFile("setenv.cmd", excludeFilters: new[] { @"**\Windows Azure SDK\**", "**winddk**" }, includeFilters: new[] { "**sdk**", "**6**" }, rememberMissingFile: true, tagWithCosmeticVersion: "6");

            _wdksetenvcmd7600 = ProgramFinder.ProgramFilesAndDotNetAndSdk.ScanForFile("setenv.bat", excludeFilters: new[] { @"**\Windows Azure SDK\**"}, includeFilters: new[] { "**winddk**"  }, rememberMissingFile: true, tagWithCosmeticVersion: "7600.16385.1");
            
            /// SDK Setenv (sdk2003): c:\program files (x86)\Microsoft SDK\SetEnv.bat

            _vcvarsallbat10 = ProgramFinder.ProgramFilesAndDotNetAndSdk.ScanForFile("vcvarsall.bat", includeFilters: new[] { "**vc**", "**10.0**" }, rememberMissingFile: true, tagWithCosmeticVersion: "10.0");
            _vcvarsallbat9 = ProgramFinder.ProgramFilesAndDotNetAndSdk.ScanForFile("vcvarsall.bat", includeFilters: new[] { "**vc**", "**9.0**" }, rememberMissingFile: true, tagWithCosmeticVersion: "9.0");
            _vcvarsallbat8 = ProgramFinder.ProgramFilesAndDotNetAndSdk.ScanForFile("vcvarsall.bat", includeFilters: new[] { "**vc**", "**8**" }, rememberMissingFile: true, tagWithCosmeticVersion: "8");
            _vcvarsallbat7 = ProgramFinder.ProgramFilesAndDotNetAndSdk.ScanForFile("vcvarsall.bat", includeFilters: new[] { "**vc**", "**7.0**" }, rememberMissingFile: true, tagWithCosmeticVersion: "7.0");
            _vcvarsallbat71 = ProgramFinder.ProgramFilesAndDotNetAndSdk.ScanForFile("vcvarsall.bat", includeFilters: new[] { "**vc**", "**7.1**" }, rememberMissingFile: true, tagWithCosmeticVersion: "7.1");
            _vcvars32bat = ProgramFinder.ProgramFilesAndDotNetAndSdk.ScanForFile("vcvars32.bat", includeFilters: new[] { "**vc98**" }, rememberMissingFile: true, tagWithCosmeticVersion: "6");

            // _originalEnvironment.AddOrSet("COAPP", "C:/programdata/");
            var sdks = new List<string>();
            if (_setenvcmd71 != null) {
                sdks.Add("sdk7.1");
            }
            if (_setenvcmd7 != null) {
                sdks.Add("sdk7");
            }
            if (_setenvcmd6 != null) {
                sdks.Add("sdk6");
            }

            _originalEnvironment.AddOrSet("installed_sdks", sdks.Aggregate((s, s1) => s + ", " + s1));

            var compilers = new List<string>();

            if( _vcvarsallbat10 != null ) {
                compilers.Add("vc10");
            }
            if( _vcvarsallbat9 != null ) {
                compilers.Add("vc9");
            }
            if( _vcvarsallbat8 != null ) {
                compilers.Add("vc8");
            }
            if( _vcvarsallbat71 != null ) {
                compilers.Add("vc7.1");
            }
            if( _vcvarsallbat7 != null ) {
                compilers.Add("vc7");
            }
            if( _vcvars32bat != null ) {
                compilers.Add("vc6");
            }

            _originalEnvironment.AddOrSet("installed_compilers", sdks.Aggregate((s, s1) => s + ", " + s1));

            if (_showTools) {
                if (_useGit) {
                    Console.WriteLine("Git: {0}", _gitcmd ??  (_gitexe != null ? _gitexe.Executable : "Not-Found"));
                }
                if (_useHg) {
                    Console.WriteLine("hg: {0}", _hgexe.Executable);
                }
                Console.WriteLine("SDK Setenv (7.1): {0}", _setenvcmd71 ?? "Not-Found");
                Console.WriteLine("SDK Setenv (7.0): {0}", _setenvcmd7 ?? "Not-Found");
                Console.WriteLine("SDK Setenv (6): {0}", _setenvcmd6 ?? "Not-Found");

                Console.WriteLine("VC vcvarsall (10.0): {0}", _vcvarsallbat10 ?? "Not-Found");
                Console.WriteLine("VC vcvarsall (9.0): {0}", _vcvarsallbat9 ?? "Not-Found");
                Console.WriteLine("VC vcvarsall (8.0): {0}", _vcvarsallbat8 ?? "Not-Found");
                Console.WriteLine("VC vcvarsall (7.0): {0}", _vcvarsallbat7 ?? "Not-Found");
                Console.WriteLine("VC vcvarsall (7.1): {0}", _vcvarsallbat71 ?? "Not-Found");
                Console.WriteLine("VC vcvars32 (6): {0}", _vcvars32bat ?? "Not-Found");

                Console.WriteLine("ptk: {0}", _ptk.Executable);
                //Console.WriteLine("trace: {0}", _traceexe.Executable);
            }

            // tell the user we can't work without instructions
            if (!parameters.Any()) {
                return Fail("Missing action . \r\n\r\n    Use --help for command line help.");
            }

            // load property sheet (that is the .buildinfo file by default)
            _propertySheet = null;
            try {
                // load and parse. propertySheet will contain everything else we need for later
                _propertySheet = PropertySheet.Load(buildinfo);

                DefineRules = _propertySheet.Rules.Where(each => each.Id == "define" && each.Name == "*").ToArray();

                _propertySheet.GetMacroValue += (valueName) => {
                    if (valueName == "DEFAULTLAMBDAVALUE") {
                        return ".";
                    }

                    string defaultValue = null;
                    if( valueName.Contains("??")) {
                        var parts = valueName.Split(new[] {'?'}, StringSplitOptions.RemoveEmptyEntries);
                        defaultValue = parts.Length > 1 ? parts[1].Trim() : string.Empty;
                        valueName = parts[0];
                    }

                     var property = (from rule in DefineRules where rule.HasProperty(valueName) select rule[valueName]).FirstOrDefault();

                     if (property != null && property.HasValues && property.Values.Count() > 1) {
                         // it's a collection of values. 
                         // let's take the collection and return it as a comma seperated string.
                         return property.Values.Aggregate((s, s1) => s + ", " + s1);
                     }

                    return (from rule in DefineRules where rule.HasProperty(valueName) select rule[valueName].Value).FirstOrDefault() ?? Environment.GetEnvironmentVariable(valueName) ?? defaultValue;
                };

                _propertySheet.GetCollection += (collectionName) => {
                    var property = (from rule in DefineRules where rule.HasProperty(collectionName) select rule[collectionName]).FirstOrDefault();
                    if( property != null && property.HasValues && property.Values.Count() > 1 ) {
                        return property.Values;
                    }
                    var collection = _propertySheet.GetMacroValue(collectionName);
                    return collection != null ? collection.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(each => each.Trim()) : Enumerable.Empty<object>();
                };
            }
            catch (EndUserParseException pspe) {
                using (new ConsoleColors(ConsoleColor.Yellow, ConsoleColor.Black)) {
                    Console.Write(pspe.Message);
                    Console.WriteLine("--found '{0}'", pspe.Token.Data);
                }

                return Fail("Error parsing .buildinfo file");
            }
            
            var builds = from rule in _propertySheet.Rules where rule.Name != "*" && (rule.HasProperty("default") && rule["default"].Value.IsTrue()) select rule;
            var command = string.Empty;

           
            switch(parameters.FirstOrDefault().ToLower()) {
                case "build ":
                case "clean":
                case "verify":
                case "status":
                case "trace":
                case "list":
                    command = parameters.FirstOrDefault().ToLower();
                    parameters = parameters.Skip(1).ToArray();
                    break;

                default:
                    command = "build";
                    break;
            }
            if (parameters.Any()) {
                var allbuilds = from rule in _propertySheet.Rules where rule.Name != "*" select rule;
                builds = parameters.Aggregate(Enumerable.Empty<Rule>(), (current, p) => current.Union(from build in allbuilds where build.Name.NewIsWildcardMatch(p) select build));
            }

            // are there even builds present?
            if(!builds.Any() ) {
                return Fail("No valid build configurations selected.");
            }

            // do the user's bidding
            try {
                switch (command) {
                    case "build":
                        ResetEnvironment();
                        Build(builds);

                        break;
                    case "clean":
                        ResetEnvironment();
                        Clean(builds);
                        using (new ConsoleColors(ConsoleColor.White, ConsoleColor.Black)) {
                            Console.WriteLine("Project Cleaned.");
                        }
                        break;
                    case "verify":
                        ResetEnvironment();
                        // Clean(builds); // clean up other builds in the list first.
                        Verify(builds);
                        using (new ConsoleColors(ConsoleColor.White, ConsoleColor.Black)) {
                            Console.WriteLine("Project Verified.");
                        }
                        break;
                    case "status":
                        ResetEnvironment();
                        Status(builds);
                        using (new ConsoleColors(ConsoleColor.White, ConsoleColor.Black)) {
                            Console.WriteLine("Project is in clean state.");
                        }
                        break;
                    //case "trace":
                      //  Trace(builds);
                        //break;
                    case "list":
                        Console.WriteLine("Buildinfo from [{0}]\r\n", buildinfo);
                        (from build in builds
                            let compiler = build["compiler"] 
                            let sdk = build["sdk"]
                            let platform = build["platform"]
                            let targets = build["targets"]
                            select new {
                                Configuration = build.Name,
                                Compiler = compiler != null ? compiler.Value : "sdk7.1",
                                Sdk = sdk != null ? sdk.Value : "sdk7.1",
                                Platform = platform != null ? platform.Value.NormalizePlatform() : "x86",
                                Number_of_Outputs = targets != null ? targets.Values.Count() : 0
                            }).ToTable().ConsoleOut();
                        break;
                    default:
                        return Fail("'{0}' is not a valid command. \r\n\r\n    Use --help for assistance.", command);
                }
            }
            catch (ConsoleException e) {
                // these exceptions are expected
                return Fail("   {0}", e.Message);
            }
            catch (Exception e) {
                // it's probably okay to crash within proper commands (because something else crashed)
                Console.WriteLine(e.StackTrace);
                return Fail("   {0}", e.Message);
            }

            return 0;
        }

        /// <summary>
        /// Traces the changes made by a specific script
        /// </summary>
        /// <param name="script">Script to trace</param>
        /// <param name="traceFile">An output file</param>
        private void TraceExec( string script, string traceFile ) {
            // multiline scripts need to be executed with a temporary script, 
            // everything else runs directly from the cmd prompt
            if (script.Contains("\r") || script.Contains("\n") ) {
                script =
@"@echo off
@setlocal 
{1}:
@cd ""{0}""
{2}

REM ===================================================================
REM STANDARD ERROR HANDLING BLOCK
REM ===================================================================
REM Everything went ok!
:success
exit /b 0
        
REM ===================================================================
REM Something not ok :(
:failed
echo ERROR: Failure in script. aborting.
exit /b 1
REM ===================================================================
".format(Environment.CurrentDirectory, Environment.CurrentDirectory[0], script);
                var scriptpath = WriteTempScript(script);
                _traceexe.ExecNoRedirections(@"--nologo ""--output-file={1}"" cmd.exe /c ""{0}""", scriptpath, traceFile);
            }
            else {
                _traceexe.ExecNoRedirections(@"--nologo ""--output-file={1}"" cmd.exe /c ""{0}""", script, traceFile);
            }
        }

        /// <summary>
        /// Create a temporary .cmd file
        /// </summary>
        /// <param name="text">The script to be written into the .cmd file</param>
        /// <returns>Full path to the temporary script</returns>
        private string WriteTempScript(string text) {
            var tmpFilename = "ptk_script".GenerateTemporaryFilename();
            _tmpFiles.Add(tmpFilename);
            // append proper file extension
            tmpFilename += ".cmd";
            _tmpFiles.Add(tmpFilename);
            File.WriteAllText(tmpFilename, text);

            return tmpFilename;
        }

        /// <summary>
        /// Runs a command line script
        /// </summary>
        /// <param name="script">A command line script</param>
        private void Exec(string script) {
            
            // multiline scripts need prepration,
            // everything else can be run straight from cmd
            if (script.Contains("\r") || script.Contains("\n") ) {
                // set up environment for the script
                script =
@"@echo off
@setlocal 
{1}:
@cd ""{0}""
{2}

REM ===================================================================
REM STANDARD ERROR HANDLING BLOCK
REM ===================================================================
REM Everything went ok!
:success
exit /b 0
        
REM ===================================================================
REM Something not ok :(
:failed
echo ERROR: Failure in script. aborting.
exit /b 1
REM ===================================================================
".format(Environment.CurrentDirectory, Environment.CurrentDirectory[0],script);
                // tell the user what we are about to run
                //Console.WriteLine(script);
                // create temporary file
                var scriptpath = WriteTempScript(script);
                // run it
                _cmdexe.ExecNoRedirections(@"/c ""{0}""", scriptpath);
            }
            else {
                // run script
                _cmdexe.ExecNoRedirections(@"/c ""{0}""", script);
            }
            // handle error conditions
            if( _cmdexe.ExitCode != 0 ) {
                throw new ConsoleException("Command Exited with value {0}", _cmdexe.ExitCode);
            }
        }

       
        private void SetCompilerSdkAndPlatform( Rule build ) {
            ResetEnvironment();

            var compilerProperty = build["compiler"];
            var sdkProperty = build["sdk"];

            var compiler = compilerProperty != null ? compilerProperty.Value : "sdk7.1";
            var sdk = sdkProperty != null ? sdkProperty.Value : "sdk7.1";

            var platformProperty = build["platform"];
            var platform = platformProperty != null ? platformProperty.Value.NormalizePlatform() : "x86";


            if (compiler.Equals("wdk", StringComparison.InvariantCultureIgnoreCase) || compiler.Equals("ddk", StringComparison.InvariantCultureIgnoreCase) || sdk.Equals("wdk", StringComparison.InvariantCultureIgnoreCase) || sdk.Equals("ddk", StringComparison.InvariantCultureIgnoreCase)) {
                // using the WDK trumps other settings.
                if (_verbose) {
                    using (new ConsoleColors(ConsoleColor.White, ConsoleColor.Black)) {
                        Console.Write("Setting SDK: ");
                    }
                    using (new ConsoleColors(ConsoleColor.Green, ConsoleColor.Black)) {
                        Console.Write("WDK");
                    }
                    using (new ConsoleColors(ConsoleColor.Yellow, ConsoleColor.Black)) {
                        Console.WriteLine(" for [{0}]", platform);
                    }
                }
                compiler = "wdk";
                sdk = "wdk";
                SetWDK( build, platform );
            }
            else {
                if (!compiler.Contains("sdk")) {
                    SwitchSdk(sdk, platform);
                }
                SwitchCompiler(compiler, platform);
            }

            Environment.SetEnvironmentVariable("current_compiler", compiler);
            Environment.SetEnvironmentVariable("current_sdk", sdk);
            Environment.SetEnvironmentVariable("current_platform", platform);
        }


        private void Clean(IEnumerable<Rule> builds) {
            foreach( var build in builds ) {
                try {
                    // set environment variables:
                    var savedVariables = _originalEnvironment;
                    _originalEnvironment = new XDictionary<string, string>(savedVariables);

                    var sets = build["set"];
                    if (sets != null) {
                        foreach (var label in sets.Labels) {
                            _originalEnvironment.AddOrSet(label, sets[label].Value);
                            Environment.SetEnvironmentVariable(label, sets[label].Value); 
                        }
                    }

                    // build dependencies first
                    CleanDependencies(build);

                    SetCompilerSdkAndPlatform(build);

                    var cmd = build["clean-command"];
                    try {
                        if (cmd != null && !string.IsNullOrEmpty(cmd.Value)) {
                            Exec(cmd.Value);
                        }
                    }
                    catch  {
                        //ignoring any failures from clean command.
                    }
                    File.Delete(Path.Combine(Environment.CurrentDirectory, "trace[{0}].xml".format(build.Name)));

                    _originalEnvironment = savedVariables;
                }
                catch (Exception e) {
                    Console.WriteLine("Uh, throw? {0} : {1}", e.Message, e.StackTrace);
                    //ignoring any failures from clean command.
                }

            }
        }

        /// <summary>
        /// Builds all dependencies listed in a given build rule
        /// </summary>
        /// <param name="buildRule">A build rule to which the dependencies should be built</param>
        private void CleanDependencies(Rule buildRule) {
            var uses = buildRule["uses"];
            if (uses != null) {
                foreach (var configuration in uses.Labels) {
                    var locations = buildRule["uses"][configuration];

                    if (string.IsNullOrEmpty(configuration)) {
                        // this could be a use in the local file.
                        var builds = from rule in _propertySheet.Rules where locations.Contains(rule.Name) select rule;

                        if (builds.Any()) {
                            Clean(builds);
                            continue;
                        }
                    }

                    foreach (var folder in locations.Select(path => path.GetFullPath())) {
                        // if it wasn't an internal build, then switch to the folder and run the build specified.
                        if (!Directory.Exists(folder)) {
                            throw new ConsoleException("Dependency project [{0}] does not exist.", folder);
                        }

                        var depBuildinfo = Path.Combine(folder, @"copkg\.buildinfo");
                        if (!File.Exists(depBuildinfo)) {
                            throw new ConsoleException("Dependency project is missing buildinfo [{0}]", depBuildinfo);
                        }

                        using (var popd = new PushDirectory(folder)) {
                            // build dependency project
                            _ptk.ExecNoRedirections("--nologo clean {0}", configuration);
                            if (_ptk.ExitCode != 0) {
                                throw new ConsoleException("Dependency project failed to build [{0}] config={1}", depBuildinfo, string.IsNullOrEmpty(configuration) ? "all" : configuration);
                            }
                        }
                    }
                }
            }            
            
            /*
            // save current directory

            var uses = build["uses"];
            if (uses != null) {
                foreach (var useLabel in uses.Labels) {
                    var use = build["uses"][useLabel];

                    var config = string.Empty;
                    var folder = string.Empty;

                    // set folder and configuration as needed
                    config = useLabel;
                    folder = use.Value;


                    if (string.IsNullOrEmpty(config)) {
                        // this could be a use in the local file.
                        
                        var builds = from rule in _propertySheet.Rules where use.Contains(rule.Name) select rule;
                        
                        if (builds.Any()) {
                            Clean(builds);
                            continue;
                        }
                    }

                    // if it wasn't an internal build, then switch to the folder and run the build specified.

                    folder = folder.GetFullPath();
                    if (!Directory.Exists(folder)) {
                        throw new ConsoleException("Dependency project [{0}] does not exist.", folder);
                    }

                    var depBuildinfo = Path.Combine(folder, @"copkg\.buildinfo");
                    if (!File.Exists(depBuildinfo)) {
                        throw new ConsoleException("Dependency project is missing buildinfo [{0}]", depBuildinfo);
                    }

                    // switch project directory
                    using (var popd = new PushDirectory(folder)) {
                        // build dependency project
                        _ptk.ExecNoRedirections("--nologo clean {0}", config);
                        if (_ptk.ExitCode != 0)
                            throw new ConsoleException(
                                "Dependency project failed to clean [{0}] config={1}", depBuildinfo, string.IsNullOrEmpty(config) ? "all" : config);
                        // reset directory to where we came from
                    }
                }
            }*/
        }

        private IEnumerable<Rule> LocalChildBuilds( Rule buildRule ) {
            var uses = buildRule["uses"];
            return uses != null ? (from useLabel in uses.Labels let use = uses[useLabel] where string.IsNullOrEmpty(useLabel) select (from rule in _propertySheet.Rules where use.Contains(rule.Name) select rule)).Aggregate(Enumerable.Empty<Rule>(), (current, builds) => current.Union(builds)) : Enumerable.Empty<Rule>();
        }

        /*
       private IDictionary<string,string> ExternalChildBuilds( Rule build ) {
           var result = new XDictionary<string, string>();
           
           var uses = build["uses"];
           if (uses != null) {
               foreach (var useLabel in uses.Labels) {
                   var use = build["uses"][useLabel];

                   var config = useLabel;
                   var folder = use.Value;

                   if (string.IsNullOrEmpty(config)) {
                       if ((from rule in _propertySheet.Rules where use.Contains(rule.Name) select rule).Any()) {
                           continue;
                       }
                   }

                   // if it wasn't an internal build, then switch to the folder and run the build specified.

                   folder = folder.GetFullPath();
                   if (!Directory.Exists(folder)) {
                       throw new ConsoleException("Dependency project [{0}] does not exist.", folder);
                   }

                   var depBuildinfo = Path.Combine(folder, @"copkg\.buildinfo");
                   if (!File.Exists(depBuildinfo)) {
                       throw new ConsoleException("Dependency project is missing buildinfo [{0}]", depBuildinfo);
                   }
                   result.Add(folder, config);
               }
           }
           return result;
       }*/

        /// <summary>
        /// Builds all dependencies listed in a given build rule
        /// </summary>
        /// <param name="buildRule">A build rule to which the dependencies should be built</param>
        private void BuildDependencies(Rule buildRule) {
            // save current directory
            var uses = buildRule["uses"];
            if (uses != null) {
                foreach (var configuration in uses.Labels) {
                    var locations = buildRule["uses"][configuration];

                    if (string.IsNullOrEmpty(configuration)) {
                        // this could be a use in the local file.
                        var builds = from rule in _propertySheet.Rules where locations.Contains(rule.Name) select rule;

                        if (builds.Any()) {
                            Build(builds);
                            continue;
                        }
                    }

                    /*
                     * uses x86="..\zlib";
                     * uses x86="..\libpng";
                     * 
                     * or
                     * 
                     * uses : configuration {
                     *      "location",
                     *      "location",
                     *      "location",
                     *      "location"
                     * }
                     * 
                     * uses : configuration: location;
                     * 
                     */

                    foreach (var folder in locations.Select(path => path.GetFullPath())) {
                        // if it wasn't an internal build, then switch to the folder and run the build specified.
                        if (!Directory.Exists(folder)) {
                            throw new ConsoleException("Dependency project [{0}] does not exist.", folder);
                        }

                        var depBuildinfo = Path.Combine(folder, @"copkg\.buildinfo");
                        if (!File.Exists(depBuildinfo)) {
                            throw new ConsoleException("Dependency project is missing buildinfo [{0}]", depBuildinfo);
                        }

                        using (var popd = new PushDirectory(folder)) {
                            // build dependency project
                            _ptk.ExecNoRedirections("--nologo build {0}", configuration);
                            if (_ptk.ExitCode != 0) {
                                throw new ConsoleException("Dependency project failed to build [{0}] config={1}", depBuildinfo, string.IsNullOrEmpty(configuration) ? "all" : configuration);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Runs build rules
        /// </summary>
        /// <param name="builds">A list of build rules to build</param>
        private void Build(IEnumerable<Rule> builds) {
            foreach (var build in builds) {
                // set environment variables:
                var savedVariables = _originalEnvironment;
                _originalEnvironment = new XDictionary<string, string>(savedVariables);

                var sets = build["set"];
                if (sets != null) {
                    foreach( var label in sets.Labels ) {
                        _originalEnvironment.AddOrSet(label, sets[label].Value);
                        Environment.SetEnvironmentVariable(label, sets[label].Value); 
                    }
                }

                // build dependencies first
                BuildDependencies(build);

                // install required packages...
                var requires = build["requires"];
                if( requires != null ) {
                    foreach( var pkg in requires.Values ) {
                        Console.WriteLine("Looking for {0}", pkg);
                        var installedPkgs = _easy.QueryPackages(pkg, Package.Properties.Installed.Is(true), null, null).Result;
                        if( !installedPkgs.Any()) {
                            // there isn't a matching installed package, we'd better install one.
                            // refresh the feeds, as a package dependency might have recently been built...
                            foreach( var feed in sessionFeeds) {
                                _easy.AddSessionFeed(feed);
                            }

                            var pkgToInstall = _easy.QueryPackages(pkg, Package.Properties.Installed.Is(false), null, null).Result;
                            bool failed = false;
                            _easy.Install(pkgToInstall.First().CanonicalName, autoUpgrade: true).Wait();

                            if( failed ) {
                                throw new ConsoleException("Unable to install dependent package.");
                            }
                        }
                    }
                }

                SetCompilerSdkAndPlatform(build);

                var generate = build["generate"];
                if(generate !=null &&  generate.HasValues ) {
                    var files = build["generate"].Labels;
                    if (!files.IsNullOrEmpty()) {
                        foreach (var file in files) {
                            File.WriteAllText(file,generate[file].Value);
                        }
                    }
                }

                if( !(_skipBuilt && CheckTargets(build, false)) )  {
                    // read the build command from PropertySheet
                    var cmd = build["build-command"];

                    // tell the user which build rule we are processing right now
                    if (_verbose || (cmd != null && !string.IsNullOrEmpty(cmd.Value))) {
                        using (new ConsoleColors(ConsoleColor.White, ConsoleColor.Black)) {
                            Console.WriteLine("Building Configuration [{0}]", build.Name);
                        }
                    }

                    // run this build command
                    if (cmd != null && !string.IsNullOrEmpty(cmd.Value)) {
                        Exec(cmd.Value);
                    }
                }

                // check to see that the right things got built
                CheckTargets(build);

                _originalEnvironment = savedVariables;
            }
        }

        private bool CheckTargets(Rule build, bool haltOnFail = true ) {
            // we need the environment set correctly here.
            var savedVariables = _originalEnvironment;
            _originalEnvironment = new XDictionary<string, string>(savedVariables);
            
            var sets = build["set"];
            if (sets != null) {
                foreach (var label in sets.Labels) {
                    _originalEnvironment.AddOrSet(label, sets[label].Value);
                    Environment.SetEnvironmentVariable(label, sets[label].Value); 
                }
            }

            ResetEnvironment();

            // var kids = LocalChildBuilds(build);
            var failed = LocalChildBuilds(build).Aggregate(false, (current, childBuild) => current || (!CheckTargets(childBuild, haltOnFail)));

            // if there are no targets, then you can't skip them if they aren't there.
            if (build["targets"] == null && !haltOnFail ) {
                return false;
            }

            if (build["targets"] != null) {
                foreach (var targ in build["targets"].Values.Where(targ => !File.Exists(targ))) {
                    failed = true;
                    if (haltOnFail) {
                        throw new ConsoleException("Target [{0}] was not found.", targ);
                    }
                }

                using (new ConsoleColors(ConsoleColor.Gray, ConsoleColor.Black)) {
                    Console.WriteLine("Targets Verified.");
                }
            }
            
            _originalEnvironment = savedVariables;
            return !failed;
        }

        /// <summary>
        /// Checks if the process chain clean/build/clean leaves excess or unaccounted files
        /// </summary>
        /// <remarks>
        /// Runs Clean, Build (and checks targets), Clean and Status (to check for excess files)
        /// </remarks>
        /// <param name="builds">A list of build rules to verify</param>
        private void Verify(IEnumerable<Rule> builds) {
            foreach (var build in builds) {
                Clean(build.SingleItemAsEnumerable());
                Build(build.SingleItemAsEnumerable());

                CheckTargets(build);
/*
                // check (local) children's targets first
                var kids = LocalChildBuilds(build);
                foreach (var childBuild in kids) {
                    if (childBuild["targets"] != null) {
                        foreach (var targ in childBuild["targets"].Values.Where(targ => !File.Exists(targ))) {
                            throw new ConsoleException("Target [{0}] was not found.", targ);
                        }
                    }
                }

                if (build["targets"] != null) {
                    foreach (var targ in build["targets"].Values.Where(targ => !File.Exists(targ))) {
                        throw new ConsoleException("Target [{0}] was not found.", targ);
                    }
                }

                using (new ConsoleColors(ConsoleColor.Gray, ConsoleColor.Black)) {
                    Console.WriteLine("Targets Verified.");
                }
*/
                Clean(build.SingleItemAsEnumerable());
                Status(build.SingleItemAsEnumerable());

            }
        }

        /// <summary>
        /// Checks if excess files are present in the project directory
        /// </summary>
        /// <remarks>Throws ConsoleException if excess files are found</remarks>
        /// <param name="builds">A list of build rules to check</param>
        private void Status(IEnumerable<Rule> builds) {
            foreach (var build in builds) {
                IEnumerable<string> results = new string[] { };
                
                // this returns all new files created by the build process
                if (_useGit) {
                    results = Git("status -s");
                }
                else if (_useHg) {
                    results = Hg("status");
                }

                // Zero results means clean directory
                if (results.Count() > 0) {
                    Fail("Project directory is not clean:");
                    using (new ConsoleColors(ConsoleColor.White, ConsoleColor.Black)) {
                        // list offending files
                        foreach (var result in results) {
                            Console.WriteLine("   {0}", result);
                        }
                    }
                    throw new ConsoleException("Failed.");
                }
            }
        }

        /// <summary>
        /// Trace a build process
        /// </summary>
        /// <param name="builds">The build rules to trace</param>
        private void Trace(IEnumerable<Rule> builds) {
            foreach (var build in builds) {
                // prepare dependencies. these are not part of the trace
                BuildDependencies(build);

                SetCompilerSdkAndPlatform(build);

                // does this build rule contain a build command?
                var cmd = build["build-command"];
                if (cmd == null)
                    throw new ConsoleException("missing build command in build {0}", build.Name);

                // run trace
                TraceExec(cmd.Value, Path.Combine(Environment.CurrentDirectory, "trace[{0}].xml".format(build.Name)));
            }
        }

        /// <summary>
        /// Run a git command
        /// </summary>
        /// <param name="cmdLine">A command to run with git</param>
        /// <returns>Any line from git's output except for those containing "copkg"</returns>
        /// <example>
        /// Git ("status -s")
        /// </example>
        private IEnumerable<string> Git(string cmdLine) {
            if( !string.IsNullOrEmpty(_gitcmd) ) {
                _cmdexe.Exec(@"/c ""{0}"" {1}", _gitcmd, cmdLine);
                return from line in _cmdexe.StandardOut.Split("\r\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries) where !line.ToLower().Contains("copkg") select line;
            } else {
                _gitexe.Exec(cmdLine);
                return from line in _gitexe.StandardOut.Split("\r\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries) where !line.ToLower().Contains("copkg") select line;  
            }
        }

        /// <summary>
        /// Run an Hg command
        /// </summary>
        /// <param name="cmdLine">A command to run with hg</param>
        /// <returns>Any line from git's output except for those containing "copkg"</returns>
        private IEnumerable<string> Hg(string cmdLine) {
            _hgexe.Exec(cmdLine);
            return from line in _hgexe.StandardOut.Split("\r\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries) where !line.ToLower().Contains("copkg") select line;
        }

        #region fail/help/logo

        /// <summary>
        /// Print an error to the console
        /// </summary>
        /// <param name="text">An error message</param>
        /// <param name="par">A format string</param>
        /// <returns>Always returns 1</returns>
        /// <seealso cref="String.Format(string, object[])"/>
        /// <remarks>
        /// Format according to http://msdn.microsoft.com/en-us/library/b1csw23d.aspx
        /// </remarks>
        public static int Fail(string text, params object[] par) {
            Logo();
            using (new ConsoleColors(ConsoleColor.Red, ConsoleColor.Black)) {
                Console.WriteLine("Error:{0}", text.format(par));
            }
            return 1;
        }

        /// <summary>
        /// Print usage notes (help) and logo
        /// </summary>
        /// <returns>Always returns 0</returns>
        private static int Help() {
            Logo();
            using (new ConsoleColors(ConsoleColor.White, ConsoleColor.Black)) {
                Console.WriteLine(help);
            }
            return 0;
        }

        /// <summary>
        /// Print program logo, information an copyright notice once.
        /// </summary>
        /// <remarks>
        /// Recurring calls to the function will not print "\n" (blank line) instead.
        /// </remarks>
        private static void Logo() {
            using (new ConsoleColors(ConsoleColor.Cyan, ConsoleColor.Black)) {
                Console.WriteLine(Assembly.GetEntryAssembly().Logo());
            }
            Assembly.GetEntryAssembly().SetLogo("");
        }

        #endregion
    }

    public static class DictionaryExtension {
        /*
        public static TValue AddOrSet<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, TValue value) where TValue : class {
            if( dictionary.ContainsKey(key) ) {
                dictionary[key] = value;
            } else {
                dictionary.Add(key, value);
            }
            return value;
        }
        */
        public static string NormalizePlatform( this string platform ) {
            if (string.IsNullOrEmpty(platform)) {
                return "x86";
            }

            switch( platform.ToLower()) {
                case "x64":
                case "amd64":
                case "64":
                    return "x64";
            }

            return "x86";
        }
    }
}