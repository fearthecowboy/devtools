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

namespace CoApp.Provider.Utility {
    using System;
    using System.Linq;
    using System.Text.RegularExpressions;
    using CoApp.Toolkit.Collections;
    using CoApp.Toolkit.Extensions;

    public class Path {
        private static Regex UriRx = new Regex(@"^([a-zA-Z]+):([\\|/]*)(\w*.*)");
        private const char Slash = '\\';

        private static readonly char[] SingleSlashes = new[] {
            Slash
        };

        private static readonly char[] Colon = new[] {
            ':'
        };

        private static readonly char[] Slashes = new[] {
            '\\', '/'
        };

        public string Account;
        public string Container;
        public string[] Parts;
        public string SubPath;
        public string ParentPath;
        public string Name;
        public bool StartsWithSlash;
        public bool EndsWithSlash;
        public uint Port;
        public string Scheme;
        public string OriginalPath;

        public string FilePath {
            get {
                if (Scheme != "file") {
                    return "";
                }
                if (IsUnc) {
                    return @"\\{0}\{1}\{2}".format(Host, Share, SubPath);
                }
                return @"{0}\{1}".format(Drive, SubPath);
            }
        }

        public string Host {
            get {
                return Account;
            }
            set {
                var parts = value.Split(Colon, StringSplitOptions.RemoveEmptyEntries);
                Account = parts.Length > 0 ? parts[0] : string.Empty;
                if (parts.Length > 1) {
                    uint.TryParse(parts[1], out Port);
                }
            }
        }

        public string Share {
            get {
                return Container;
            }
            set {
                Container = value;
            }
        }

        public bool HasDrive {
            get {
                return Account.Length == 2 && Account[1] == ':';
            }
        }

        public bool IsUnc {get; set;}

        public string Drive {
            get {
                return Account;
            }
            set {
                Account = value;
            }
        }

        private static XDictionary<string, Path> _parsedLocationCache = new XDictionary<string, Path>();

        public static Path ParseWithContainer(Uri url) {
            return ParseWithContainer(url.AbsoluteUri);
        }

        public static Path ParseWithContainer(string path) {
            if (_parsedLocationCache.ContainsKey(path)) {
                return _parsedLocationCache[path];
            }

            var pathToParse = (path ?? string.Empty).UrlDecode();

            var match = UriRx.Match(pathToParse);
            if (match.Success) {
                pathToParse = match.Groups[3].Value;
            }

            var segments = pathToParse.Split(Slashes, StringSplitOptions.RemoveEmptyEntries);
            return _parsedLocationCache.AddOrSet(path, new Path {
                Account = segments.Length > 0 ? segments[0] : string.Empty,
                Container = segments.Length > 1 ? segments[1] : string.Empty,
                Parts = segments.Length > 2 ? segments.Skip(2).ToArray() : new string[0],
                SubPath = segments.Length > 2 ? segments.Skip(2).Aggregate((current, each) => current + Slash + each) : string.Empty,
                ParentPath = segments.Length > 3 ? segments.Skip(2).Take(segments.Length - 3).Aggregate((current, each) => current + Slash + each) : string.Empty,
                Name = segments.Length > 2 ? segments.Last() : string.Empty,
                StartsWithSlash = pathToParse.IndexOfAny(Slashes) == 0,
                EndsWithSlash = pathToParse.LastIndexOfAny(Slashes) == pathToParse.Length,
                Scheme = match.Success ? match.Groups[1].Value.ToLower() : string.Empty,
                OriginalPath = path,
            });
        }

        public static Path ParseUrl(Uri url) {
            return ParseUrl(url.AbsoluteUri);
        }

        public static Path ParseUrl(string path) {
            if (_parsedLocationCache.ContainsKey(path)) {
                return _parsedLocationCache[path];
            }

            var pathToParse = (path ?? string.Empty).UrlDecode();

            var match = UriRx.Match(pathToParse);
            if (match.Success) {
                pathToParse = match.Groups[3].Value;
            }

            var segments = pathToParse.Split(Slashes, StringSplitOptions.RemoveEmptyEntries);
            return _parsedLocationCache.AddOrSet(path, new Path {
                Host = segments.Length > 0 ? segments[0] : string.Empty,
                Container = string.Empty,
                Parts = segments.Length > 1 ? segments.Skip(1).ToArray() : new string[0],
                SubPath = segments.Length > 1 ? segments.Skip(1).Aggregate((current, each) => current + Slash + each) : string.Empty,
                ParentPath = segments.Length > 2 ? segments.Skip(1).Take(segments.Length - 2).Aggregate((current, each) => current + Slash + each) : string.Empty,
                Name = segments.Length > 1 ? segments.Last() : string.Empty,
                StartsWithSlash = pathToParse.IndexOfAny(Slashes) == 0,
                EndsWithSlash = pathToParse.LastIndexOfAny(Slashes) == pathToParse.Length,
                Scheme = match.Success ? match.Groups[1].Value.ToLower() : string.Empty,
                OriginalPath = path,
            });
        }

        public static Path ParsePath(string path) {
            if (_parsedLocationCache.ContainsKey(path)) {
                return _parsedLocationCache[path];
            }
            var uri = new Uri((path ?? string.Empty).UrlDecode());

            var pathToParse = uri.AbsoluteUri;

            var match = UriRx.Match(pathToParse);
            if (match.Success) {
                pathToParse = match.Groups[3].Value;
            }

            var segments = pathToParse.Split(Slashes, StringSplitOptions.RemoveEmptyEntries);

            return uri.IsUnc
                ? _parsedLocationCache.AddOrSet(path, new Path {
                    Host = segments.Length > 0 ? segments[0] : string.Empty,
                    Share = segments.Length > 1 ? segments[1] : string.Empty,
                    Parts = segments.Length > 2 ? segments.Skip(2).ToArray() : new string[0],
                    SubPath = segments.Length > 2 ? segments.Skip(2).Aggregate((current, each) => current + Slash + each) : string.Empty,
                    ParentPath = segments.Length > 3 ? segments.Skip(2).Take(segments.Length - 2).Aggregate((current, each) => current + Slash + each) : string.Empty,
                    Name = segments.Length > 2 ? segments.Last() : string.Empty,
                    StartsWithSlash = pathToParse.IndexOfAny(Slashes) == 0,
                    EndsWithSlash = pathToParse.LastIndexOfAny(Slashes) == pathToParse.Length,
                    Scheme = match.Success ? match.Groups[1].Value.ToLower() : string.Empty,
                    IsUnc = true,
                })
                : _parsedLocationCache.AddOrSet(path, new Path {
                    Drive = segments.Length > 0 ? segments[0] : string.Empty,
                    Share = string.Empty,
                    Parts = segments.Length > 1 ? segments.Skip(1).ToArray() : new string[0],
                    SubPath = segments.Length > 1 ? segments.Skip(1).Aggregate((current, each) => current + Slash + each) : string.Empty,
                    ParentPath = segments.Length > 2 ? segments.Skip(1).Take(segments.Length - 2).Aggregate((current, each) => current + Slash + each) : string.Empty,
                    Name = segments.Length > 1 ? segments.Last() : string.Empty,
                    StartsWithSlash = pathToParse.IndexOfAny(Slashes) == 0,
                    EndsWithSlash = pathToParse.LastIndexOfAny(Slashes) == pathToParse.Length,
                    Scheme = match.Success ? match.Groups[1].Value.ToLower() : string.Empty,
                    IsUnc = false,
                    OriginalPath = uri.AbsoluteUri,
                });
        }

        public void Validate() {
            if (Parts == null) {
                // not set from original creation
                SubPath = SubPath ?? string.Empty;
                Parts = SubPath.Split(Slashes, StringSplitOptions.RemoveEmptyEntries);
                ParentPath = Parts.Length > 1 ? Parts.Take(Parts.Length - 1).Aggregate((current, each) => current + Slash + each) : string.Empty;
                Name = Parts.Length > 0 ? Parts.Last() : string.Empty;
            }
        }

        public bool IsSubpath(Path childPath) {
            if (Parts.Length >= childPath.Parts.Length) {
                return false;
            }

            return !Parts.Where((t, i) => t != childPath.Parts[i]).Any();
        }
    }
}