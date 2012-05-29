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


namespace CoApp.RepositoryService {
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using System.Xml;
    using Packaging.Client;
    using Packaging.Common;
    using Packaging.Common.Model.Atom;
    using Toolkit.Collections;
    using Toolkit.Extensions;
    using Toolkit.Tasks;
    using System.ServiceModel.Syndication;
    using Toolkit.Win32;

    public class UploadedFileHandler : RequestHandler {
        private readonly Tweeter _tweeter;
        private readonly string _localfeedLocation;
        private readonly string _packageStorageFolder;
        private readonly Uri _packagePrefixUrl;
        private readonly Uri _canonicalFeedUrl;
        private readonly CloudFileSystem _cloudFileSystem;
        private readonly string _feedName;

        private static readonly IDictionary<string, UploadedFileHandler> FeedHandlers = new XDictionary<string, UploadedFileHandler>();

        public UploadedFileHandler(string feedName, string localfeedLocation, string canonicalFeedUrl, string packageStoragePath, string packagePrefixUrl, string twitterHandle, CloudFileSystem cloudFileSystem) {
            
            _feedName = feedName;
            _localfeedLocation = localfeedLocation.GetFullPath();
            if( !_localfeedLocation.EndsWith(".xml")) {
                _localfeedLocation +=  ".xml";
            }
            _canonicalFeedUrl = new Uri(canonicalFeedUrl);
            _packageStorageFolder = packageStoragePath;
            _packagePrefixUrl = new Uri(packagePrefixUrl);
            _cloudFileSystem = cloudFileSystem;

            if (!string.IsNullOrEmpty(twitterHandle)) {
                _tweeter = new Tweeter(twitterHandle);
            }

            if( _cloudFileSystem == null ) {
                _packageStorageFolder = _packageStorageFolder.GetFullPath();
                if(!Directory.Exists(_packageStorageFolder)) {
                    Directory.CreateDirectory(_packageStorageFolder);
                }
            }

            CurrentTask.Events += new DownloadProgress((remoteLocation, location, progress) => "Downloading {0}".format(remoteLocation.UrlDecode()).PrintProgressBar(progress));
            CurrentTask.Events += new DownloadCompleted((remoteLocation, locallocation) => Console.WriteLine());

            FeedHandlers.Add(feedName, this);
        }

        public override Task Get(HttpListenerResponse response, string relativePath, Toolkit.Pipes.UrlEncodedMessage message) {
            switch (message.Command) {
                case "add":
                    if (!string.IsNullOrEmpty(message["location"])) {
                        try {
                            var uri = new Uri(message["location"]);
                            if (Peek(uri)) {
                                var filename = "UploadedFile.bin".GenerateTemporaryFilename();
                                var rf = new RemoteFile(uri, filename);
                                rf.Get();
                                if (File.Exists(filename)) {
                                    return HandleFile(filename).ContinueWith(antecedent => {
                                        if (antecedent.IsFaulted) {
                                            var e = antecedent.Exception.InnerException;
                                            Listener.HandleException(e);
                                            response.StatusCode = 500;
                                            response.Close();
                                        } else {
                                            response.StatusCode = antecedent.Result;
                                            response.Close();
                                        }
                                    });
                                }
                            }
                        } catch {
                        }
                    }
                    break;

                case "validate":
                    return Validate().ContinueWith(antecedent => {
                        if (antecedent.IsFaulted) {
                            var e = antecedent.Exception.InnerException;
                            Listener.HandleException(e);
                            response.StatusCode = 500;
                            response.Close();
                        }
                        else {
                            response.StatusCode = antecedent.Result;
                            response.Close();
                        }
                    });
                    
            }

            response.StatusCode = 500;
            response.Close();
            return "".AsResultTask();
        }
            
        private Task<int> Validate() {
            return Task.Factory.StartNew(() => {
                var feed = new AtomFeed();
                //load the feed from the _canonicalFeedUrl if we can
                try {
                    var originalFeed = LoadFeed();
                    foreach (AtomItem i in originalFeed.Items.Where(each => each is AtomItem)) {
                        // drop dead urls
                        i.Model.Feeds = i.Model.Feeds.Distinct().Where(Peek).ToXList();
                        i.Model.Locations = i.Model.Locations.Distinct().Where(Peek).ToXList();
                        foreach (var l in i.Links.ToArray().Where(each => !Peek(each.Uri))) {
                            i.Links.Remove(l);
                        }
                        if (i.Model.Locations.Any()) {
                            feed.Add(i);
                        }
                    }
                    SaveFeed(feed);
                } catch {
                    return 500;
                }
                return 200;
            });
        }

        /*
        public override Task Post(HttpListenerResponse response, string relativePath, Toolkit.Pipes.UrlEncodedMessage message) {
            var fileData = message["file"].ToString();
            if( string.IsNullOrEmpty(fileData) ) {
                response.StatusCode = 500;
                response.Close();
                return "".AsResultTask();
            }
            
            var data = fileData.UrlDecode();
        }
        */

        private static readonly PackageManager _packageManager = new PackageManager();

        public override Task Put(HttpListenerResponse response, string relativePath, byte[] data) {
            if( data.Length < 1 ) {
                response.StatusCode = 500;
                response.Close();
                return "".AsResultTask();
            }

            var filename = "UploadedFile.bin".GenerateTemporaryFilename();
            File.WriteAllBytes(filename, data);

           return  HandleFile(filename).ContinueWith(antecedent => {
                if( antecedent.IsFaulted ) {
                    var e = antecedent.Exception.InnerException;
                    Listener.HandleException(e);
                    response.StatusCode = 500;
                    response.Close();
                } else {
                    response.StatusCode = antecedent.Result;
                    response.Close();
                }
            });
        }

        private void InsertIntoFeed(CanonicalName pkgCanonicalName, FourPartVersion pkgVersion,  Uri location , AtomItem item = null) {

            lock (typeof(UploadedFileHandler)) {
                // update the feed
                var feed = new AtomFeed();

                //load the feed from the _canonicalFeedUrl if we can
                try {
                    var originalFeed = LoadFeed();

                    foreach (AtomItem i in originalFeed.Items.Where(each => each is AtomItem)) {
                        if (_feedName == "current") {
                            // if an older version of this package is in the current feed, 
                            if (i.Model.CanonicalName.DiffersOnlyByVersion(pkgCanonicalName) && i.Model.CanonicalName.Version < pkgVersion) {
                                // push it to the archive feed.
                                FeedHandlers["archive"].InsertIntoFeed(i.Model.CanonicalName, i.Model.Version, i.Model.Locations[0]);
                                // and skip it
                                continue;
                            }
                        }
                        feed.Add(i);
                    }
                } catch {

                }
                item = item ?? _packageManager.GetAtomItem(pkgCanonicalName).Result;

                if (item != null) {
                    // first, make sure that the feeds contains the intended feed location.
                    
                    item.Model.Feeds = item.Model.Feeds  ??new XList<Uri>();
                    if (!item.Model.Feeds.Contains(_canonicalFeedUrl)) {
                        item.Model.Feeds.Insert(0, _canonicalFeedUrl);
                    }
                    
                    item.Model.Locations = item.Model.Locations  ?? new XList<Uri>();
                    if (!item.Model.Locations.Contains(location)) {
                        item.Model.Locations.Insert(0, location);
                    }

                    // drop dead urls
                    item.Model.Feeds = item.Model.Feeds.Distinct().Where(Peek).ToXList();
                    item.Model.Locations = item.Model.Locations.Distinct().Where(Peek).ToXList();
                    foreach( var l in item.Links.ToArray().Where( each => !Peek(each.Uri) ) ) {
                        item.Links.Remove(l);
                    }

                    if (item.Model.Locations.Any()) {
                        // if we've got at least one valid location, add the item to the feed.
                        feed.Add(item);
                    }
                }
                SaveFeed( feed); 
            }
        }

        private AtomFeed LoadFeed() {
            try {
                _cloudFileSystem.ReadBlob(_packageStorageFolder, Path.GetFileName(_localfeedLocation).ToLower(), _localfeedLocation,
                    (progress) => "Getting package feed from blob store {0} => {1}".format(_localfeedLocation, _packageStorageFolder).PrintProgressBar(progress));
                if (!string.IsNullOrEmpty(_localfeedLocation) && File.Exists(_localfeedLocation)) {
                    var originalFeed = AtomFeed.LoadFile(_localfeedLocation);
                    if (originalFeed != null) {
                        return originalFeed;
                    }
                }
            } catch {
                
            }
            return new AtomFeed();
        }

        private void SaveFeed( AtomFeed feed ) {
            feed.Save(_localfeedLocation);
            if (_cloudFileSystem != null) {
                _cloudFileSystem.WriteBlob(_packageStorageFolder, Path.GetFileName(_localfeedLocation).ToLower(), _localfeedLocation, false, (progress) => "{0} => {1}".format(_localfeedLocation, _packageStorageFolder).PrintProgressBar(progress));
                Console.WriteLine();

                _cloudFileSystem.WriteBlob(_packageStorageFolder, Path.GetFileName(_localfeedLocation).ToLower() + ".gz", _localfeedLocation, true, (progress) => "{0} => {1}".format(_localfeedLocation + ".gz", _packageStorageFolder).PrintProgressBar(progress));
                Console.WriteLine();
            }
        }

        private static bool Peek(Uri url) {
            HttpWebResponse response = null;
            try {
                // create the request
                var request = WebRequest.Create(url) as HttpWebRequest;
                // instruct the server to return headers only
                request.Method = "HEAD";
                // make the connection
                response = request.GetResponse() as HttpWebResponse;
                return true;
            }
            catch {
                return false;  
            }
            finally {
                // make sure the response gets closed
                //  this avoids leaking connections
                if (response != null) {
                    response.Close();
                }
            }
        }

        private Task<int> HandleFile( string filename) {
            // verify that the file is actually a valid package
          return  _packageManager.QueryPackages(filename, null, null, null).ContinueWith(
                antecedent => {
                    if (antecedent.IsFaulted) {
                        Console.WriteLine("Fault occurred after upload: {0}", filename);
                        filename.TryHardToDelete();
                        return 400;
                   }

                    if (antecedent.IsCanceled) {
                        Console.WriteLine("Request was cancelled");
                        filename.TryHardToDelete();
                        return 400;
                    }

                    var pkg = antecedent.Result.FirstOrDefault();
                    if (pkg == null) {
                        Console.WriteLine("File uploaded is not recognized as a package: {0}", filename);
                        filename.TryHardToDelete();
                        return 400;
                    }
                    _packageManager.GetPackageDetails(pkg.CanonicalName).Wait();
                    
                    var targetFilename = "{0}{1}-{2}-{3}.msi".format(pkg.CanonicalName.Name, pkg.CanonicalName.Flavor, pkg.CanonicalName.Version, pkg.CanonicalName.Architecture.InCanonicalFormat).ToLower();
                    var location = new Uri(_packagePrefixUrl, targetFilename);

                    // upload to it's final resting place.
                    CopyFileToDestination(filename, targetFilename, pkg);

                    // add it to the appropriate feed 
                    InsertIntoFeed(pkg.CanonicalName, pkg.Version, location);

                    // Advertise the package on twitter
                    TweetPackage(location, pkg);
                    return 200;
                }, TaskContinuationOptions.AttachedToParent);
        }

        private void TweetPackage(Uri location, Package pkg) {
            if (_tweeter != null) {
                // pkg.Name
                Bitly.Shorten(location.AbsoluteUri).ContinueWith(
                    (x) => {
                        var name = "[{0}-{1}-{2}]".format(pkg.Name, pkg.Version, pkg.Architecture);

                        var summary = pkg.PackageDetails.SummaryDescription;
                        var l1 = 138 - (name.Length + x.Result.Length);
                        if (summary.Length > l1) {
                            summary = summary.Substring(0, l1 - 1) + "\u2026";
                        }
                        var text = "{0} {1} {2}".format(name, summary, x.Result);
                        Console.WriteLine("Tweet: {0}", text);
                        _tweeter.Tweet(text);
                    });
            }
        }

        private void CopyFileToDestination(string filename, string targetFilename, Package pkg) {
            //copy the package to the destination
            if (_cloudFileSystem != null) {
                // copy file to azure storage
                _cloudFileSystem.WriteBlob(_packageStorageFolder, targetFilename, filename, false, progress => "{0} => {1}".format(pkg.CanonicalName, _packageStorageFolder).PrintProgressBar(progress));

                if (pkg.CanonicalName.Matches(CanonicalName.CoAppItself)) {
                    // update the default toolkit too
                    _cloudFileSystem.WriteBlob(_packageStorageFolder, "coapp.msi", filename, false, progress => "{0} => {1}".format(_localfeedLocation, _packageStorageFolder).PrintProgressBar(progress));
                    Console.WriteLine();
                }

                if (pkg.CanonicalName.Matches(CanonicalName.CoAppDevtools)) {
                    // update the default toolkit too
                    _cloudFileSystem.WriteBlob(_packageStorageFolder, "coapp.devtools.msi", filename, false, progress => "{0} => {1}".format(_localfeedLocation, _packageStorageFolder).PrintProgressBar(progress));
                    Console.WriteLine();
                }

                // remove the local file, since we don't need it anymore.
                filename.TryHardToDelete();

                Console.WriteLine();
            } else {
                var targetLocation = Path.Combine(_packageStorageFolder, targetFilename);
                if (File.Exists(targetLocation)) {
                    targetLocation.TryHardToDelete();
                }

                File.Copy(filename, targetLocation);
            }
        }

      
    }
}
