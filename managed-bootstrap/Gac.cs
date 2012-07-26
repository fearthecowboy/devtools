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

namespace CoApp.Bootstrapper {

    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("e707dcde-d1cd-11d2-bab9-00c04f8eceae")]
    internal interface IAssemblyCache {
        [PreserveSig]
        int UninstallAssembly(
            int flags,
            [MarshalAs(UnmanagedType.LPWStr)] String assemblyName,
            InstallReference refData,
            out AssemblyCacheUninstallDisposition disposition);

        [PreserveSig]
        int QueryAssemblyInfo(
            int flags,
            [MarshalAs(UnmanagedType.LPWStr)] String assemblyName,
            ref AssemblyInfo assemblyInfo);

        [PreserveSig]
        int Reserved(
            int flags,
            IntPtr pvReserved,
            out Object ppAsmItem,
            [MarshalAs(UnmanagedType.LPWStr)] String assemblyName);

        [PreserveSig]
        int Reserved(out Object ppAsmScavenger);

        [PreserveSig]
        int InstallAssembly(
            int flags,
            [MarshalAs(UnmanagedType.LPWStr)] String assemblyFilePath,
            InstallReference refData);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("CD193BC0-B4BC-11d2-9833-00C04FC31D2E")]
    internal interface IAssemblyName {
        [PreserveSig]
        int SetProperty(
            int PropertyId,
            IntPtr pvProperty,
            int cbProperty);

        [PreserveSig]
        int GetProperty(
            int PropertyId,
            IntPtr pvProperty,
            ref int pcbProperty);

        [PreserveSig]
        int Finalize();

        [PreserveSig]
        int GetDisplayName(
            StringBuilder pDisplayName,
            ref int pccDisplayName,
            int displayFlags);

        [PreserveSig]
        int Reserved(ref Guid guid,
            Object obj1,
            Object obj2,
            String string1,
            Int64 llFlags,
            IntPtr pvReserved,
            int cbReserved,
            out IntPtr ppv);

        [PreserveSig]
        int GetName(
            ref int pccBuffer,
            StringBuilder pwzName);

        [PreserveSig]
        int GetVersion(
            out int versionHi,
            out int versionLow);

        [PreserveSig]
        int IsEqual(
            IAssemblyName pAsmName,
            int cmpFlags);

        [PreserveSig]
        int Clone(out IAssemblyName pAsmName);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("21b8916c-f28e-11d2-a473-00c04f8ef448")]
    internal interface IAssemblyEnum {
        [PreserveSig]
        int GetNextAssembly(
            IntPtr pvReserved,
            out IAssemblyName ppName,
            int flags);

        [PreserveSig]
        int Reset();

        [PreserveSig]
        int Clone(out IAssemblyEnum ppEnum);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("582dac66-e678-449f-aba6-6faaec8a9394")]
    internal interface IInstallReferenceItem {
        // A pointer to a FUSION_INSTALL_REFERENCE structure. 
        // The memory is allocated by the GetReference method and is freed when 
        // IInstallReferenceItem is released. Callers must not hold a reference to this 
        // buffer after the IInstallReferenceItem object is released. 
        // This uses the InstallReferenceOutput object to avoid allocation 
        // issues with the interop layer. 
        // This cannot be marshaled directly - must use IntPtr 
        [PreserveSig]
        int GetReference(
            out IntPtr pRefData,
            int flags,
            IntPtr pvReserced);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("56b1a988-7c0c-4aa2-8639-c3eb5a90226f")]
    internal interface IInstallReferenceEnum {
        [PreserveSig]
        int GetNextInstallReferenceItem(
            out IInstallReferenceItem ppRefItem,
            int flags,
            IntPtr pvReserced);
    }

    public enum AssemblyCommitFlags {
        Default = 1,
        Force = 2
    }

    public enum AssemblyCacheUninstallDisposition {
        Unknown = 0,
        Uninstalled = 1,
        StillInUse = 2,
        AlreadyUninstalled = 3,
        DeletePending = 4,
        HasInstallReference = 5,
        ReferenceNotFound = 6
    }

    [Flags]
    internal enum AssemblyCacheFlags {
        GAC = 2,
    }

    internal enum CreateAssemblyNameObjectFlags {
        CANOF_DEFAULT = 0,
        CANOF_PARSE_DISPLAY_NAME = 1,
    }

    public enum GACVersion {
        Net20,
        Net40
    }

    [Flags]
    internal enum AssemblyNameDisplayFlags {
        VERSION = 0x01,
        CULTURE = 0x02,
        PUBLIC_KEY_TOKEN = 0x04,
        PROCESSORARCHITECTURE = 0x20,
        RETARGETABLE = 0x80,
        // This enum will change in the future to include
        // more attributes.
        ALL = VERSION
            | CULTURE
                | PUBLIC_KEY_TOKEN
                    | PROCESSORARCHITECTURE
                        | RETARGETABLE
    }

    [StructLayout(LayoutKind.Sequential)]
    public class InstallReference {
        public InstallReference(Guid guid, String id, String data) {
            cbSize = (2*IntPtr.Size + 16 + (id.Length + data.Length)*2);
            flags = 0;
            // quiet compiler warning 
            if (flags == 0) {
            }
            guidScheme = guid;
            identifier = id;
            description = data;
        }

        public Guid GuidScheme {
            get {
                return guidScheme;
            }
        }

        public String Identifier {
            get {
                return identifier;
            }
        }

        public String Description {
            get {
                return description;
            }
        }

        private int cbSize;
        private int flags;
        private Guid guidScheme;

        [MarshalAs(UnmanagedType.LPWStr)]
        private String identifier;

        [MarshalAs(UnmanagedType.LPWStr)]
        private String description;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct AssemblyInfo {
        public int cbAssemblyInfo; // size of this structure for future expansion
        public int assemblyFlags;
        public long assemblySizeInKB;

        [MarshalAs(UnmanagedType.LPWStr)]
        public String currentAssemblyPath;

        public int cchBuf; // size of path buf.
    }

    [ComVisible(false)]
    public class InstallReferenceGuid {
        public static bool IsValidGuidScheme(Guid guid) {
            return (guid.Equals(UninstallSubkeyGuid) ||
                guid.Equals(FilePathGuid) ||
                    guid.Equals(OpaqueGuid) ||
                        guid.Equals(Guid.Empty));
        }

        public static readonly Guid UninstallSubkeyGuid = new Guid("8cedc215-ac4b-488b-93c0-a50a49cb2fb8");
        public static readonly Guid FilePathGuid = new Guid("b02f9d65-fb77-4f7a-afa5-b391309f11c9");
        public static readonly Guid OpaqueGuid = new Guid("2ec93463-b0c3-45e1-8364-327e96aea856");
        // these GUID cannot be used for installing into GAC.
        public static readonly Guid MsiGuid = new Guid("25df0fc1-7f97-4070-add7-4b13bbfd7cb8");
        public static readonly Guid OsInstallGuid = new Guid("d16d444c-56d8-11d5-882d-0080c847b195");
    }

    [ComVisible(false)]
    public static class AssemblyCache {
        public static void InstallAssembly(String assemblyPath, InstallReference reference, AssemblyCommitFlags flags, GACVersion gacVersion = GACVersion.Net40) {
            if (reference != null) {
                if (!InstallReferenceGuid.IsValidGuidScheme(reference.GuidScheme)) {
                    throw new ArgumentException("Invalid reference guid.", "guid");
                }
            }

            IAssemblyCache ac = null;

            int hr = 0;

            switch (gacVersion) {
                case GACVersion.Net40:
                    hr = Utils.CreateAssemblyCache40(out ac, 0);
                    break;
                case GACVersion.Net20:
                    hr = Utils.CreateAssemblyCache20(out ac, 0);
                    break;
            }
            // hr = Utils.CreateAssemblyCache(out ac, 0);
            if (hr >= 0) {
                hr = ac.InstallAssembly((int)flags, assemblyPath, reference);
            }

            if (hr < 0) {
                Marshal.ThrowExceptionForHR(hr);
            }
        }

        // assemblyName has to be fully specified name. 
        // A.k.a, for v1.0/v1.1 assemblies, it should be "name, Version=xx, Culture=xx, PublicKeyToken=xx".
        // For v2.0 assemblies, it should be "name, Version=xx, Culture=xx, PublicKeyToken=xx, ProcessorArchitecture=xx".
        // If assemblyName is not fully specified, a random matching assembly will be uninstalled. 
        public static void UninstallAssembly(String assemblyName, InstallReference reference, out AssemblyCacheUninstallDisposition disp, GACVersion gacVersion = GACVersion.Net40) {
            var dispResult = AssemblyCacheUninstallDisposition.Uninstalled;
            if (reference != null) {
                if (!InstallReferenceGuid.IsValidGuidScheme(reference.GuidScheme)) {
                    throw new ArgumentException("Invalid reference guid.", "guid");
                }
            }

            IAssemblyCache ac = null;
            int hr = 0;

            switch (gacVersion) {
                case GACVersion.Net40:
                    hr = Utils.CreateAssemblyCache40(out ac, 0);
                    break;
                case GACVersion.Net20:
                    hr = Utils.CreateAssemblyCache20(out ac, 0);
                    break;
            }

            if (hr >= 0) {
                hr = ac.UninstallAssembly(0, assemblyName, reference, out dispResult);
            }

            if (hr < 0) {
                Marshal.ThrowExceptionForHR(hr);
            }

            disp = dispResult;
        }

        // See comments in UninstallAssembly
        public static String QueryAssemblyInfo(String assemblyName, GACVersion gacVersion = GACVersion.Net40) {
            if (assemblyName == null) {
                throw new ArgumentException("Invalid name", "assemblyName");
            }

            var aInfo = new AssemblyInfo();

            aInfo.cchBuf = 1024;
            // Get a string with the desired length
            aInfo.currentAssemblyPath = new String('\0', aInfo.cchBuf);

            IAssemblyCache ac = null;
            int hr = 0;
            switch (gacVersion) {
                case GACVersion.Net40:
                    hr = Utils.CreateAssemblyCache40(out ac, 0);
                    break;
                case GACVersion.Net20:
                    hr = Utils.CreateAssemblyCache20(out ac, 0);
                    break;
            }

            if (hr >= 0) {
                hr = ac.QueryAssemblyInfo(0, assemblyName, ref aInfo);
            }
            if (hr < 0) {
                Marshal.ThrowExceptionForHR(hr);
            }

            return aInfo.currentAssemblyPath;
        }
    }

    [ComVisible(false)]
    public class AssemblyCacheEnum {
        private static UInt64 Parse(string[] parts, int index) {
            int i = 0;
            return parts.Length < index ? 0 : (UInt64)(Int32.TryParse(parts[index], out i) ? i : 0);
        }

        internal static UInt64 VersionStringToUInt64(string version) {
            var vers = (version ?? "0").Split('.');
            return (((Parse(vers, 0)) << 48) + ((Parse(vers, 1)) << 32) + ((Parse(vers, 2)) << 16) + Parse(vers, 3));
        }

        public static IEnumerable<UInt64> GetAssemblyVersions(string assemblyName) {
            return GetAssemblyStrongNames(assemblyName).Select(assembly => (from p in assembly.Split(", ".ToCharArray(), StringSplitOptions.RemoveEmptyEntries)
                select p.Split('=')
                into kvp
                where kvp[0].Equals("Version", StringComparison.InvariantCultureIgnoreCase)
                select VersionStringToUInt64(kvp[1])).FirstOrDefault());
        }

        public static IEnumerable<string> GetAssemblyStrongNames(string assemblyName) {
            var ace = new AssemblyCacheEnum(assemblyName);
            for (string assembly = ace.GetNextAssembly(); assembly != null; assembly = ace.GetNextAssembly()) {
                yield return assembly;
            }
        }

        // null means enumerate all the assemblies
        public AssemblyCacheEnum(String assemblyName, GACVersion gacVersion = GACVersion.Net40) {
            IAssemblyName fusionName = null;
            int hr = 0;

            if (assemblyName != null) {
                switch (gacVersion) {
                    case GACVersion.Net40:
                        hr = Utils.CreateAssemblyNameObject40(
                            out fusionName,
                            assemblyName,
                            CreateAssemblyNameObjectFlags.CANOF_PARSE_DISPLAY_NAME,
                            IntPtr.Zero);
                        break;
                    case GACVersion.Net20:
                        hr = Utils.CreateAssemblyNameObject20(
                            out fusionName,
                            assemblyName,
                            CreateAssemblyNameObjectFlags.CANOF_PARSE_DISPLAY_NAME,
                            IntPtr.Zero);
                        break;
                }
            }

            if (hr >= 0) {
                switch (gacVersion) {
                    case GACVersion.Net40:
                        hr = Utils.CreateAssemblyEnum40(
                            out m_AssemblyEnum,
                            IntPtr.Zero,
                            fusionName,
                            AssemblyCacheFlags.GAC,
                            IntPtr.Zero);
                        break;
                    case GACVersion.Net20:
                        hr = Utils.CreateAssemblyEnum20(
                            out m_AssemblyEnum,
                            IntPtr.Zero,
                            fusionName,
                            AssemblyCacheFlags.GAC,
                            IntPtr.Zero);
                        break;
                }
            }

            if (hr < 0) {
                Marshal.ThrowExceptionForHR(hr);
            }
        }

        public String GetNextAssembly() {
            int hr = 0;
            IAssemblyName fusionName = null;

            if (done) {
                return null;
            }

            // Now get next IAssemblyName from m_AssemblyEnum
            hr = m_AssemblyEnum.GetNextAssembly((IntPtr)0, out fusionName, 0);

            if (hr < 0) {
                Marshal.ThrowExceptionForHR(hr);
            }

            if (fusionName != null) {
                return GetFullName(fusionName);
            } else {
                done = true;
                return null;
            }
        }

        private String GetFullName(IAssemblyName fusionAsmName) {
            var sDisplayName = new StringBuilder(1024);
            int iLen = 1024;

            int hr = fusionAsmName.GetDisplayName(sDisplayName, ref iLen, (int)AssemblyNameDisplayFlags.ALL);
            if (hr < 0) {
                Marshal.ThrowExceptionForHR(hr);
            }

            return sDisplayName.ToString();
        }

        private IAssemblyEnum m_AssemblyEnum;
        private bool done;
    }

    // class AssemblyCacheEnum

    public class AssemblyCacheInstallReferenceEnum {
        public AssemblyCacheInstallReferenceEnum(String assemblyName, GACVersion gacVersion = GACVersion.Net40) {
            IAssemblyName fusionName = null;
            int hr = 0;
            switch (gacVersion) {
                case GACVersion.Net40:
                    hr = Utils.CreateAssemblyNameObject40(
                        out fusionName,
                        assemblyName,
                        CreateAssemblyNameObjectFlags.CANOF_PARSE_DISPLAY_NAME,
                        IntPtr.Zero);

                    break;
                case GACVersion.Net20:
                    hr = Utils.CreateAssemblyNameObject20(
                        out fusionName,
                        assemblyName,
                        CreateAssemblyNameObjectFlags.CANOF_PARSE_DISPLAY_NAME,
                        IntPtr.Zero);

                    break;
            }

            if (hr >= 0) {
                switch (gacVersion) {
                    case GACVersion.Net40:
                        hr = Utils.CreateInstallReferenceEnum40(out refEnum, fusionName, 0, IntPtr.Zero);
                        break;
                    case GACVersion.Net20:
                        hr = Utils.CreateInstallReferenceEnum20(out refEnum, fusionName, 0, IntPtr.Zero);
                        break;
                }
            }

            if (hr < 0) {
                Marshal.ThrowExceptionForHR(hr);
            }
        }

        public InstallReference GetNextReference() {
            IInstallReferenceItem item = null;
            int hr = refEnum.GetNextInstallReferenceItem(out item, 0, IntPtr.Zero);
            if ((uint)hr == 0x80070103) {
                // ERROR_NO_MORE_ITEMS
                return null;
            }

            if (hr < 0) {
                Marshal.ThrowExceptionForHR(hr);
            }

            IntPtr refData;
            var instRef = new InstallReference(Guid.Empty, String.Empty, String.Empty);

            hr = item.GetReference(out refData, 0, IntPtr.Zero);
            if (hr < 0) {
                Marshal.ThrowExceptionForHR(hr);
            }

            Marshal.PtrToStructure(refData, instRef);
            return instRef;
        }

        private IInstallReferenceEnum refEnum;
    }

    internal class Utils {
        [DllImport("kernel32")]
        private static extern IntPtr LoadLibrary(string dllname);

        [DllImport("kernel32")]
        private static extern void FreeLibrary(IntPtr handle);

        [DllImport("kernel32", CharSet = CharSet.Ansi)]
        private static extern IntPtr GetProcAddress(IntPtr dllPointer, string functionName);

        private sealed class UnmanagedLibrary {
            private IntPtr handle;

            internal UnmanagedLibrary(IntPtr handle) {
                this.handle = handle;
            }

            ~UnmanagedLibrary() {
                lock (this) {
                    if (handle != IntPtr.Zero) {
                        FreeLibrary(handle);
                        handle = IntPtr.Zero;
                    }
                }
            }

            internal IntPtr GetProcAddr(string functionName) {
                return GetProcAddress(handle, functionName);
            }
        }

        internal delegate int CreateAssemblyEnumDelegate(
            out IAssemblyEnum ppEnum,
            IntPtr pUnkReserved,
            IAssemblyName pName,
            AssemblyCacheFlags flags,
            IntPtr pvReserved);

        internal delegate int CreateAssemblyNameObjectDelegate(
            out IAssemblyName ppAssemblyNameObj,
            String szAssemblyName,
            CreateAssemblyNameObjectFlags flags,
            IntPtr pvReserved);

        internal delegate int CreateAssemblyCacheDelegate(
            out IAssemblyCache ppAsmCache,
            int reserved);

        internal delegate int CreateInstallReferenceEnumDelegate(
            out IInstallReferenceEnum ppRefEnum,
            IAssemblyName pName,
            int dwFlags,
            IntPtr pvReserved);

        internal static CreateAssemblyEnumDelegate CreateAssemblyEnum20;
        internal static CreateAssemblyNameObjectDelegate CreateAssemblyNameObject20;
        internal static CreateAssemblyCacheDelegate CreateAssemblyCache20;
        internal static CreateInstallReferenceEnumDelegate CreateInstallReferenceEnum20;

        internal static CreateAssemblyEnumDelegate CreateAssemblyEnum40;
        internal static CreateAssemblyNameObjectDelegate CreateAssemblyNameObject40;
        internal static CreateAssemblyCacheDelegate CreateAssemblyCache40;
        internal static CreateInstallReferenceEnumDelegate CreateInstallReferenceEnum40;

        private static UnmanagedLibrary Net20Fusion;
        private static UnmanagedLibrary Net40Fusion;

        static Utils() {
            var sysroot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            var f40 = Path.Combine(sysroot, "Microsoft.NET", Environment.Is64BitProcess ? "Framework64" : "Framework", "v4.0.30319", "fusion.dll");
            var f20 = Path.Combine(sysroot, "Microsoft.NET", Environment.Is64BitProcess ? "Framework64" : "Framework", "v2.0.50727", "fusion.dll");

            if (File.Exists(f40)) {
                var f40dll = LoadLibrary(f40);
                if (f40dll != IntPtr.Zero) {
                    Net40Fusion = new UnmanagedLibrary(f40dll);
                    CreateAssemblyEnum40 = (CreateAssemblyEnumDelegate)Marshal.GetDelegateForFunctionPointer(Net40Fusion.GetProcAddr("CreateAssemblyEnum"), typeof (CreateAssemblyEnumDelegate));
                    CreateAssemblyNameObject40 = (CreateAssemblyNameObjectDelegate)Marshal.GetDelegateForFunctionPointer(Net40Fusion.GetProcAddr("CreateAssemblyNameObject"), typeof (CreateAssemblyNameObjectDelegate));
                    CreateAssemblyCache40 = (CreateAssemblyCacheDelegate)Marshal.GetDelegateForFunctionPointer(Net40Fusion.GetProcAddr("CreateAssemblyCache"), typeof (CreateAssemblyCacheDelegate));
                    CreateInstallReferenceEnum40 = (CreateInstallReferenceEnumDelegate)Marshal.GetDelegateForFunctionPointer(Net40Fusion.GetProcAddr("CreateInstallReferenceEnum"), typeof (CreateInstallReferenceEnumDelegate));
                }
            }

            if (File.Exists(f20)) {
                var f20dll = LoadLibrary(f20);
                if (f20dll != IntPtr.Zero) {
                    Net20Fusion = new UnmanagedLibrary(f20dll);
                    CreateAssemblyEnum20 = (CreateAssemblyEnumDelegate)Marshal.GetDelegateForFunctionPointer(Net20Fusion.GetProcAddr("CreateAssemblyEnum"), typeof (CreateAssemblyEnumDelegate));
                    CreateAssemblyNameObject20 = (CreateAssemblyNameObjectDelegate)Marshal.GetDelegateForFunctionPointer(Net20Fusion.GetProcAddr("CreateAssemblyNameObject"), typeof (CreateAssemblyNameObjectDelegate));
                    CreateAssemblyCache20 = (CreateAssemblyCacheDelegate)Marshal.GetDelegateForFunctionPointer(Net20Fusion.GetProcAddr("CreateAssemblyCache"), typeof (CreateAssemblyCacheDelegate));
                    CreateInstallReferenceEnum20 = (CreateInstallReferenceEnumDelegate)Marshal.GetDelegateForFunctionPointer(Net20Fusion.GetProcAddr("CreateInstallReferenceEnum"), typeof (CreateInstallReferenceEnumDelegate));
                }
            }
        }
    }
}