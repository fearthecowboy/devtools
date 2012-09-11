﻿/* Copyright (c) 2006, J.P. Trosclair
 * All rights reserved.
 *
 * Redistribution and use in source and binary forms, with or without modification, are permitted 
 * provided that the following conditions are met:
 *
 *  * Redistributions of source code must retain the above copyright notice, this list of conditions and 
 *		the following disclaimer.
 *  * Redistributions in binary form must reproduce the above copyright notice, this list of conditions 
 *		and the following disclaimer in the documentation and/or other materials provided with the 
 *		distribution.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED 
 * WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A 
 * PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR 
 * ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT 
 * LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS 
 * INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, 
 * OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF 
 * ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 * 
 * Based on FTPFactory.cs code, pretty much a complete re-write with FTPFactory.cs
 * as a reference.
 * 
 ***********************
 * Authors of this code:
 ***********************
 * J.P. Trosclair    (jptrosclair@judelawfirm.com)
 * Filipe Madureira  (filipe_madureira@hotmail.com) 
 * Carlo M. Andreoli (cmandreoli@numericaprogetti.it)
 * Sloan Holliday    (sloan@ipass.net)
 * Garrett Serack    (gserack@gmail.com)
 * 
 *********************** 
 * FTPFactory.cs was written by Jaimon Mathew (jaimonmathew@rediffmail.com)
 * and modified by Dan Rolander (Dan.Rolander@marriott.com).
 *	http://www.csharphelp.com/archives/archive9.html
 ***********************
 * 
 * ** DO NOT ** contact the authors of FTPFactory.cs about problems with this code. It
 * is not their responsibility. Only contact people listed as authors of THIS CODE.
 * 
 *  Any bug fixes or additions to the code will be properly credited to the author.
 * 
 *  BUGS: There probably are plenty. If you fix one, please email me with info
 *   about the bug and the fix, code is welcome.
 * 
 * All calls to the ftplib functions should be:
 * 
 * try 
 * { 
 *		// ftplib function call
 * } 
 * catch(Exception ex) 
 * {
 *		// error handeler
 * }
 * 
 * If you add to the code please make use of OpenDataSocket(), CloseDataSocket(), and
 * ReadResponse() appropriately. See the comments above each for info about using them.
 * 
 * The Fail() function terminates the entire connection. Only call it on critical errors.
 * Non critical errors should NOT close the connection.
 * All errors should throw an exception of type Exception with the response string from
 * the server as the message.
 * 
 * See the simple ftp client for examples on using this class
 */

//#define FTP_DEBUG   

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace CoApp.Toolkit.Network {
    using Exceptions;

    public class FTP {
        #region Public Variables

        /// <summary>
        /// IP address or hostname to connect to
        /// </summary>
        public string Server;
        /// <summary>
        /// Username to login as
        /// </summary>
        public string User;
        /// <summary>
        /// Password for account
        /// </summary>
        public string Password;
        /// <summary>
        /// Port number the FTP server is listening on
        /// </summary>
        public int Port;
        /// <summary>
        /// The timeout (miliseconds) for waiting on data to arrive
        /// </summary>
        public int Timeout;

        #endregion

        #region Private Variables

        private string messages; // server messages
        private string responseStr; // server response if the user wants it.
        private long bytesTotal; // upload/download info if the user wants it.
        private long fileSize; // gets set when an upload or download takes place
        private Socket mainSock;
        private IPEndPoint mainIpEndPoint;
        private Socket listeningSock;
        private Socket dataSock;
        private IPEndPoint dataIpEndPoint;
        private Stream file;
        private int response;
        private string bucket;

        #endregion

        #region Constructors
        /// <summary>
        /// Constructor
        /// </summary>
        public FTP() {
            Port = 21;
            PassiveMode = true;		
            bucket = string.Empty;
            Timeout = 10000;	// 10 seconds
            messages = string.Empty;
        }
       
        #endregion

        /// <summary>
        /// Connection status to the server
        /// </summary>
        public bool IsConnected {
            get {
                if (mainSock != null)
                    return mainSock.Connected;
                return false;
            }
        }
        /// <summary>
        /// Returns true if the message buffer has data in it
        /// </summary>
        public bool MessagesAvailable {
            get {
                if (messages.Length > 0)
                    return true;
                return false;
            }
        }
        /// <summary>
        /// Server messages if any, buffer is cleared after you access this property
        /// </summary>
        public string Messages {
            get {
                string tmp = messages;
                messages = "";
                return tmp;
            }
        }
        /// <summary>
        /// The response string from the last issued command
        /// </summary>
        public string ResponseString {
            get {
                return responseStr;
            }
        }
        /// <summary>
        /// The total number of bytes sent/recieved in a transfer
        /// </summary>
        public long BytesTotal		// #######################################
        {
            get {
                return bytesTotal;
            }
        }
        /// <summary>
        /// The size of the file being downloaded/uploaded (Can possibly be 0 if no size is available)
        /// </summary>
        public long FileSize		// #######################################
        {
            get {
                return fileSize;
            }
        }

        /// <summary>
        /// True:  Passive mode [default]
        /// False: Active Mode
        /// </summary>
        public bool PassiveMode { get; set; }
        
        private void Fail() {
            Disconnect();
            throw new CoAppException(responseStr);
        }


        private void SetBinaryMode(bool mode) {
            SendCommand(mode ? "TYPE I" : "TYPE A");

            ReadResponse();
            if (response != 200)
                Fail();
        }


        private void SendCommand(string command) {
            Byte[] cmd = Encoding.ASCII.GetBytes((command + "\r\n").ToCharArray());

#if (FTP_DEBUG)
            if (command.Length > 3 && command.Substring(0, 4) == "PASS")
                Console.WriteLine("\rPASS xxx");
            else
                Console.WriteLine("\r" + command);
#endif

            mainSock.Send(cmd, cmd.Length, 0);
        }


        private void FillBucket() {
            Byte[] bytes = new Byte[512];
            long bytesgot;
            int msecs_passed = 0;		// #######################################

            while (mainSock.Available < 1) {
                System.Threading.Thread.Sleep(50);
                msecs_passed += 50;
                // this code is just a fail safe option 
                // so the code doesn't hang if there is 
                // no data comming.
                if (msecs_passed > Timeout) {
                    Disconnect();
                    throw new CoAppException("Timed out waiting on server to respond.");
                }
            }

            while (mainSock.Available > 0) {
                bytesgot = mainSock.Receive(bytes, 512, 0);
                bucket += Encoding.ASCII.GetString(bytes, 0, (int)bytesgot);
                // this may not be needed, gives any more data that hasn't arrived
                // just yet a small chance to get there.
                System.Threading.Thread.Sleep(50);
            }
        }


        private string GetLineFromBucket() {
            int i;
            string buf = "";

            if ((i = bucket.IndexOf('\n')) < 0) {
                while (i < 0) {
                    FillBucket();
                    i = bucket.IndexOf('\n');
                }
            }

            buf = bucket.Substring(0, i);
            bucket = bucket.Substring(i + 1);

            return buf;
        }


        // Any time a command is sent, use ReadResponse() to get the response
        // from the server. The variable responseStr holds the entire string and
        // the variable response holds the response number.
        private void ReadResponse() {
            string buf;
            messages = "";

            while (true) {
                //buf = GetLineFromBucket();
                buf = GetLineFromBucket();

#if (FTP_DEBUG)
                Console.WriteLine(buf);
#endif
                // the server will respond with "000-Foo bar" on multi line responses
                // "000 Foo bar" would be the last line it sent for that response.
                // Better example:
                // "000-This is a multiline response"
                // "000-Foo bar"
                // "000 This is the end of the response"
                if (Regex.Match(buf, "^[0-9]+ ").Success) {
                    responseStr = buf;
                    response = int.Parse(buf.Substring(0, 3));
                    break;
                }
                else
                    messages += Regex.Replace(buf, "^[0-9]+-", "") + "\n";
            }
        }


        // if you add code that needs a data socket, i.e. a PASV or PORT command required,
        // call this function to do the dirty work. It sends the PASV or PORT command,
        // parses out the port and ip info and opens the appropriate data socket
        // for you. The socket variable is private Socket data_socket. Once you
        // are done with it, be sure to call CloseDataSocket()
        private void OpenDataSocket() {
            if (PassiveMode)		// #######################################
            {
                string[] pasv;
                string server;
                int port;

                Connect();
                SendCommand("PASV");
                ReadResponse();
                if (response != 227)
                    Fail();

                try {
                    int i1, i2;

                    i1 = responseStr.IndexOf('(') + 1;
                    i2 = responseStr.IndexOf(')') - i1;
                    pasv = responseStr.Substring(i1, i2).Split(',');
                }
                catch (Exception) {
                    Disconnect();
                    throw new CoAppException("Malformed PASV response: " + responseStr);
                }

                if (pasv.Length < 6) {
                    Disconnect();
                    throw new CoAppException("Malformed PASV response: " + responseStr);
                }

                server = String.Format("{0}.{1}.{2}.{3}", pasv[0], pasv[1], pasv[2], pasv[3]);
                port = (int.Parse(pasv[4]) << 8) + int.Parse(pasv[5]);

                try {
#if (FTP_DEBUG)
                    Console.WriteLine("Data socket: {0}:{1}", server, port);
#endif
                    CloseDataSocket();

#if (FTP_DEBUG)
                    Console.WriteLine("Creating socket...");
#endif
                    dataSock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

#if (FTP_DEBUG)
                    Console.WriteLine("Resolving host");
#endif
                    #pragma warning disable 0618
                    dataIpEndPoint = new IPEndPoint(Dns.GetHostByName(server).AddressList[0], port);


#if (FTP_DEBUG)
                    Console.WriteLine("Connecting..");
#endif
                    dataSock.Connect(dataIpEndPoint);

#if (FTP_DEBUG)
                    Console.WriteLine("Connected.");
#endif
                }
                catch (Exception ex) {
                    throw new CoAppException("Failed to connect for data transfer: " + ex.Message);
                }
            }
            else		// #######################################
            {
                Connect();

                try {
#if (FTP_DEBUG)
                    Console.WriteLine("Data socket (active mode)");
#endif
                    CloseDataSocket();

#if (FTP_DEBUG)
                    Console.WriteLine("Creating listening socket...");
#endif
                    listeningSock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

#if (FTP_DEBUG)
                    Console.WriteLine("Binding it to local address/port");
#endif
                    // for the PORT command we need to send our IP address; let's extract it
                    // from the LocalEndPoint of the main socket, that's already connected
                    string sLocAddr = mainSock.LocalEndPoint.ToString();
                    int ix = sLocAddr.IndexOf(':');
                    if (ix < 0) {
                        throw new CoAppException("Failed to parse the local address: " + sLocAddr);
                    }
                    string sIPAddr = sLocAddr.Substring(0, ix);
                    // let the system automatically assign a port number (setting port = 0)
                    System.Net.IPEndPoint localEP = new IPEndPoint(IPAddress.Parse(sIPAddr), 0);

                    listeningSock.Bind(localEP);
                    sLocAddr = listeningSock.LocalEndPoint.ToString();
                    ix = sLocAddr.IndexOf(':');
                    if (ix < 0) {
                        throw new CoAppException("Failed to parse the local address: " + sLocAddr);
                    }
                    int nPort = int.Parse(sLocAddr.Substring(ix + 1));
#if (FTP_DEBUG)
                    Console.WriteLine("Listening on {0}:{1}", sIPAddr, nPort);
#endif
                    // start to listen for a connection request from the host (note that
                    // Listen is not blocking) and send the PORT command
                    listeningSock.Listen(1);
                    string sPortCmd = string.Format("PORT {0},{1},{2}",
                                                    sIPAddr.Replace('.', ','),
                                                    nPort / 256, nPort % 256);
                    SendCommand(sPortCmd);
                    ReadResponse();
                    if (response != 200)
                        Fail();
                }
                catch (Exception ex) {
                    throw new CoAppException("Failed to connect for data transfer: " + ex.Message);
                }
            }
        }


        private void ConnectDataSocket()		// #######################################
        {
            if (dataSock != null)		// already connected (always so if passive mode)
                return;

            try {
#if (FTP_DEBUG)
                Console.WriteLine("Accepting the data connection.");
#endif
                dataSock = listeningSock.Accept();	// Accept is blocking
                listeningSock.Close();
                listeningSock = null;

                if (dataSock == null) {
                    throw new CoAppException("Winsock error: " +
                        Convert.ToString(System.Runtime.InteropServices.Marshal.GetLastWin32Error()));
                }
#if (FTP_DEBUG)
                Console.WriteLine("Connected.");
#endif
            }
            catch (Exception ex) {
                throw new CoAppException("Failed to connect for data transfer: " + ex.Message);
            }
        }


        private void CloseDataSocket() {
#if (FTP_DEBUG)
            Console.WriteLine("Attempting to close data channel socket...");
#endif
            if (dataSock != null) {
                if (dataSock.Connected) {
#if (FTP_DEBUG)
                        Console.WriteLine("Closing data channel socket!");
#endif
                    dataSock.Close();
#if (FTP_DEBUG)
                        Console.WriteLine("Data channel socket closed!");
#endif
                }
                dataSock = null;
            }

            dataIpEndPoint = null;
        }
        /// <summary>
        /// Closes all connections to the ftp server
        /// </summary>
        public void Disconnect() {
            CloseDataSocket();

            if (mainSock != null) {
                if (mainSock.Connected) {
                    SendCommand("QUIT");
                    mainSock.Close();
                }
                mainSock = null;
            }

            if (file != null)
                file.Close();

            mainIpEndPoint = null;
            file = null;
        }
        
        /// <summary>
        /// Connect to an ftp server
        /// </summary>
        public void Connect() {
            if (Server == null)
                throw new CoAppException("No server has been set.");
            if (User == null)
                throw new CoAppException("No username has been set.");

            if (mainSock != null)
                if (mainSock.Connected)
                    return;

            mainSock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            mainIpEndPoint = new IPEndPoint(Dns.GetHostByName(Server).AddressList[0], Port);

            try {
                mainSock.Connect(mainIpEndPoint);
            }
            catch (Exception ex) {
                throw new CoAppException(ex.Message);
            }

            ReadResponse();
            if (response != 220)
                Fail();

            SendCommand("USER " + User);
            ReadResponse();

            switch (response) {
                case 331:
                    if (Password == null) {
                        Disconnect();
                        throw new CoAppException("No password has been set.");
                    }
                    SendCommand("PASS " + Password);
                    ReadResponse();
                    if (response != 230)
                        Fail();
                    break;
                case 230:
                    break;
            }

            return;
        }
        /// <summary>
        /// Retrieves a list of files from the ftp server
        /// </summary>
        /// <returns>An ArrayList of files</returns>
        public IEnumerable<string> List() {
            Byte[] bytes = new Byte[512];
            string fileList = "";
            long bytesgot = 0;
            int msecsPassed = 0;
            var list = new List<string>();

            Connect();
            OpenDataSocket();
            SendCommand("LIST");
            ReadResponse();

            //FILIPE MADUREIRA.
            //Added response 125
            switch (response) {
                case 125:
                case 150:
                    break;
                default:
                    CloseDataSocket();
                    throw new CoAppException(responseStr);
            }
            ConnectDataSocket();		// #######################################

            while (dataSock.Available < 1) {
                System.Threading.Thread.Sleep(50);
                msecsPassed += 50;
                // this code is just a fail safe option 
                // so the code doesn't hang if there is 
                // no data comming.
                if (msecsPassed > (Timeout / 10)) {
                    //CloseDataSocket();
                    //throw new CoAppException("Timed out waiting on server to respond.");

                    //FILIPE MADUREIRA.
                    //If there are no files to list it gives timeout.
                    //So I wait less time and if no data is received, means that there are no files
                    break;//Maybe there are no files
                }
            }

            while (dataSock.Available > 0) {
                bytesgot = dataSock.Receive(bytes, bytes.Length, 0);
                fileList += Encoding.ASCII.GetString(bytes, 0, (int)bytesgot);
                System.Threading.Thread.Sleep(50); // *shrug*, sometimes there is data comming but it isn't there yet.
            }

            CloseDataSocket();

            ReadResponse();
            if (response != 226)
                throw new CoAppException(responseStr);

            foreach (string f in fileList.Split('\n')) {
                if (f.Length > 0 && !Regex.Match(f, "^total").Success)
                    list.Add(f.Substring(0, f.Length - 1));
            }

            return list;
        }
        /// <summary>
        /// Gets a file list only
        /// </summary>
        /// <returns>ArrayList of files only</returns>
        public IEnumerable<string> ListFiles() {
            var list = new List<string>();

            foreach (string f in List()) {
                //FILIPE MADUREIRA
                //In Windows servers it is identified by <DIR>
                if ((f.Length > 0)) {
                    if ((f[0] != 'd') && (f.ToUpper().IndexOf("<DIR>") < 0))
                        list.Add(f);
                }
            }

            return list;
        }
        /// <summary>
        /// Gets a directory list only
        /// </summary>
        /// <returns>ArrayList of directories only</returns>
        public IEnumerable<string> ListDirectories() {
            var list = new List<string>();

            foreach (string f in List()) {
                //FILIPE MADUREIRA
                //In Windows servers it is identified by <DIR>
                if (f.Length > 0) {
                    if ((f[0] == 'd') || (f.ToUpper().IndexOf("<DIR>") >= 0))
                        list.Add(f);
                }
            }

            return list;
        }
        /// <summary>
        /// Returns the 'Raw' DateInformation in ftp format. (YYYYMMDDhhmmss). Use GetFileDate to return a DateTime object as a better option.
        /// </summary>
        /// <param name="fileName">Remote FileName to Query</param>
        /// <returns>Returns the 'Raw' DateInformation in ftp format</returns>
        public string GetFileDateRaw(string fileName) {
            Connect();

            SendCommand("MDTM " + fileName);
            ReadResponse();
            if (response != 213) {
#if (FTP_DEBUG)
                Console.Write("\r" + responseStr);
#endif
                throw new CoAppException(responseStr);
            }

            return (this.responseStr.Substring(4));
        }
        /// <summary>
        /// GetFileDate will query the ftp server for the date of the remote file.
        /// </summary>
        /// <param name="fileName">Remote FileName to Query</param>
        /// <returns>DateTime of the Input FileName</returns>
        public DateTime GetFileDate(string fileName) {
            return ConvertFTPDateToDateTime(GetFileDateRaw(fileName));
        }

        private static DateTime ConvertFTPDateToDateTime(string input) {
            if (input.Length < 14)
                throw new ArgumentException("Input Value for ConvertFTPDateToDateTime method was too short.");

            //YYYYMMDDhhmmss": 
            int year = Convert.ToInt16(input.Substring(0, 4));
            int month = Convert.ToInt16(input.Substring(4, 2));
            int day = Convert.ToInt16(input.Substring(6, 2));
            int hour = Convert.ToInt16(input.Substring(8, 2));
            int min = Convert.ToInt16(input.Substring(10, 2));
            int sec = Convert.ToInt16(input.Substring(12, 2));

            return new DateTime(year, month, day, hour, min, sec);
        }
        /// <summary>
        /// Get the working directory on the ftp server
        /// </summary>
        /// <returns>The working directory</returns>
        public string GetWorkingDirectory() {
            //PWD - print working directory
            Connect();
            SendCommand("PWD");
            ReadResponse();

            if (response != 257)
                throw new CoAppException(responseStr);

            string pwd;
            try {
                pwd = responseStr.Substring(responseStr.IndexOf("\"", 0) + 1);//5);
                pwd = pwd.Substring(0, pwd.LastIndexOf("\""));
                pwd = pwd.Replace("\"\"", "\""); // directories with quotes in the name come out as "" from the server
            }
            catch (Exception ex) {
                throw new CoAppException("Uhandled PWD response: " + ex.Message);
            }

            return pwd;
        }

        public void ChangeDirMakeIfNeccesary(string path) {
            try {
                ChangeDir(path);
            }
            catch {
                MakeDir(path);
                ChangeDir(path);
            }
        }

        /// <summary>
        /// Change to another directory on the ftp server
        /// </summary>
        /// <param name="path">Directory to change to</param>
        public void ChangeDir(string path) {
            Connect();
            SendCommand("CWD " + path);
            ReadResponse();
            if (response != 250) {
#if (FTP_DEBUG)
                Console.Write("\r" + responseStr);
#endif
                throw new CoAppException(responseStr);
            }
        }
        /// <summary>
        /// Create a directory on the ftp server
        /// </summary>
        /// <param name="dir">Directory to create</param>
        public void MakeDir(string dir) {
            Connect();
            SendCommand("MKD " + dir);
            ReadResponse();

            switch (response) {
                case 257:
                case 250:
                    break;
                default:
#if (FTP_DEBUG)
                    Console.Write("\r" + responseStr);
#endif
                    throw new CoAppException(responseStr);
            }
        }
        /// <summary>
        /// Remove a directory from the ftp server
        /// </summary>
        /// <param name="dir">Name of directory to remove</param>
        public void RemoveDir(string dir) {
            Connect();
            SendCommand("RMD " + dir);
            ReadResponse();
            if (response != 250) {
#if (FTP_DEBUG)
                Console.Write("\r" + responseStr);
#endif
                throw new CoAppException(responseStr);
            }
        }
        /// <summary>
        /// Remove a file from the ftp server
        /// </summary>
        /// <param name="filename">Name of the file to delete</param>
        public void RemoveFile(string filename) {
            Connect();
            SendCommand("DELE " + filename);
            ReadResponse();
            if (response != 250) {
#if (FTP_DEBUG)
                Console.Write("\r" + responseStr);
#endif
                throw new CoAppException(responseStr);
            }
        }
        /// <summary>
        /// Rename a file on the ftp server
        /// </summary>
        /// <param name="oldfilename">Old file name</param>
        /// <param name="newfilename">New file name</param>
        public void RenameFile(string oldfilename, string newfilename)		// #######################################
        {
            Connect();
            SendCommand("RNFR " + oldfilename);
            ReadResponse();
            if (response != 350) {
#if (FTP_DEBUG)
                Console.Write("\r" + responseStr);
#endif
                throw new CoAppException(responseStr);
            }
            else {
                SendCommand("RNTO " + newfilename);
                ReadResponse();
                if (response != 250) {
#if (FTP_DEBUG)
                    Console.Write("\r" + responseStr);
#endif
                    throw new CoAppException(responseStr);
                }
            }
        }
        /// <summary>
        /// Get the size of a file (Provided the ftp server supports it)
        /// </summary>
        /// <param name="filename">Name of file</param>
        /// <returns>The size of the file specified by filename</returns>
        public long GetFileSize(string filename) {
            Connect();
            SendCommand("SIZE " + filename);
            ReadResponse();
            if (response != 213) {
#if (FTP_DEBUG)
                Console.Write("\r" + responseStr);
#endif
                throw new CoAppException(responseStr);
            }

            return Int64.Parse(responseStr.Substring(4));
        }

        /// <summary>
        /// Open an upload with no resume if it already exists
        /// </summary>
        /// <param name="filename">File to upload</param>
        public void OpenUpload(string filename) {
            OpenUpload(filename, filename, false);
        }
        /// <summary>
        /// Open an upload with no resume if it already exists
        /// </summary>
        /// <param name="filename">Local file to upload (Can include path to file)</param>
        /// <param name="remotefilename">Filename to store file as on ftp server</param>
        public void OpenUpload(string filename, string remotefilename) {
            OpenUpload(filename, remotefilename, false);
        }
        /// <summary>
        /// Open an upload with resume support
        /// </summary>
        /// <param name="filename">Local file to upload (Can include path to file)</param>
        /// <param name="resume">Attempt resume if exists</param>
        public void OpenUpload(string filename, bool resume) {
            OpenUpload(filename, filename, resume);
        }

        /// <summary>
        /// Open an upload with resume support
        /// </summary>
        /// <param name="filename">Local file to upload (Can include path to file)</param>
        /// <param name="remote_filename">Filename to store file as on ftp server</param>
        /// <param name="resume">Attempt resume if exists</param>
        public void OpenUpload(string filename, string remote_filename, bool resume) {
            try {
                file = new FileStream(filename, FileMode.Open);
            }
            catch (Exception ex) {
                file = null;
                throw new CoAppException(ex.Message);
            }
            fileSize = file.Length;
            UploadImpl(remote_filename, resume);
        }

        /// <summary>
        /// Open an upload with resume support
        /// </summary>
        /// <param name="filename">Local file to upload (Can include path to file)</param>
        /// <param name="remote_filename">Filename to store file as on ftp server</param>
        /// <param name="resume">Attempt resume if exists</param>
        public void OpenUpload(Stream fileToUpload, long length, string remote_filename, bool resume) {
            file = fileToUpload;
            fileSize = length;
            UploadImpl(remote_filename, resume);
        }


        /// <summary>
        /// Open an upload with resume support
        /// </summary>
        /// <param name="filename">Local file to upload (Can include path to file)</param>
        /// <param name="remote_filename">Filename to store file as on ftp server</param>
        /// <param name="resume">Attempt resume if exists</param>
        private void UploadImpl(string remote_filename, bool resume) {
            bytesTotal = 0;

            Connect();
            SetBinaryMode(true);
            OpenDataSocket();

            if (resume) {
                long size = GetFileSize(remote_filename);
                SendCommand("REST " + size);
                ReadResponse();
                if (response == 350)
                    file.Seek(size, SeekOrigin.Begin);
            }

            SendCommand("STOR " + remote_filename);
            ReadResponse();

            switch (response) {
                case 125:
                case 150:
                    break;
                default:
                    file.Close();
                    file = null;
                    throw new CoAppException(responseStr);
            }
            ConnectDataSocket();		// #######################################	

            return;
        }
        /// <summary>
        /// Download a file with no resume
        /// </summary>
        /// <param name="filename">Remote file name</param>
        public void OpenDownload(string filename) {
            OpenDownload(filename, filename, false);
        }
        /// <summary>
        /// Download a file with optional resume
        /// </summary>
        /// <param name="filename">Remote file name</param>
        /// <param name="resume">Attempt resume if file exists</param>
        public void OpenDownload(string filename, bool resume) {
            OpenDownload(filename, filename, resume);
        }
        /// <summary>
        /// Download a file with no attempt to resume
        /// </summary>
        /// <param name="filename">Remote filename</param>
        /// <param name="localfilename">Local filename (Can include path to file)</param>
        public void OpenDownload(string filename, string localfilename) {
            OpenDownload(filename, localfilename, false);
        }
        /// <summary>
        /// Open a file for download
        /// </summary>
        /// <param name="remote_filename">The name of the file on the FTP server</param>
        /// <param name="local_filename">The name of the file to save as (Can include path to file)</param>
        /// <param name="resume">Attempt resume if file exists</param>
        public void OpenDownload(string remote_filename, string local_filename, bool resume) {
            Connect();
            SetBinaryMode(true);

            bytesTotal = 0;

            try {
                fileSize = GetFileSize(remote_filename);
            }
            catch {
                fileSize = 0;
            }

            if (resume && File.Exists(local_filename)) {
                try {
                    file = new FileStream(local_filename, FileMode.Open);
                }
                catch (Exception ex) {
                    file = null;
                    throw new CoAppException(ex.Message);
                }

                SendCommand("REST " + file.Length);
                ReadResponse();
                if (response != 350)
                    throw new CoAppException(responseStr);
                file.Seek(file.Length, SeekOrigin.Begin);
                bytesTotal = file.Length;
            }
            else {
                try {
                    file = new FileStream(local_filename, FileMode.Create);
                }
                catch (Exception ex) {
                    file = null;
                    throw new CoAppException(ex.Message);
                }
            }

            OpenDataSocket();
            SendCommand("RETR " + remote_filename);
            ReadResponse();

            switch (response) {
                case 125:
                case 150:
                    break;
                default:
                    file.Close();
                    file = null;
                    throw new CoAppException(responseStr);
            }
            ConnectDataSocket();		// #######################################	

            return;
        }

        /// <summary>
        /// THIS IS A BLOCKING CALL
        /// </summary>
        /// <returns></returns>
        public void UploadAndComplete(Stream fileToUpload, long length, string remote_filename, bool resume) {
            OpenUpload(fileToUpload, length, remote_filename, resume);
            DoUploadUntilComplete();
        }

        /// <summary>
        /// THIS IS A BLOCKING CALL
        /// </summary>
        /// <returns></returns>
        public long DoUploadUntilComplete() {
            long sz=0;
            long bytes;
            do {
                bytes = DoUpload();
                sz += bytes;
            } while (bytes > 0);
            return sz;
        }

        /// <summary>
        /// Upload the file, to be used in a loop until file is completely uploaded
        /// </summary>
        /// <returns>Bytes sent</returns>
        public long DoUpload() {
            Byte[] bytes = new Byte[512];
            long bytes_got;

            try {
                bytes_got = file.Read(bytes, 0, bytes.Length);
                bytesTotal += bytes_got;
                dataSock.Send(bytes, (int)bytes_got, 0);

                if (bytes_got <= 0) {
                    // the upload is complete or an error occured
                    file.Close();
                    file = null;

                    CloseDataSocket();
                    ReadResponse();
                    switch (response) {
                        case 226:
                        case 250:
                            break;
                        default:
                            throw new CoAppException(responseStr);
                    }

                    SetBinaryMode(false);
                }
            }
            catch (Exception ex) {
                file.Close();
                file = null;
                CloseDataSocket();
                ReadResponse();
                SetBinaryMode(false);
                throw ex;
            }

            return bytes_got;
        }
        /// <summary>
        /// Download a file, to be used in a loop until the file is completely downloaded
        /// </summary>
        /// <returns>Number of bytes recieved</returns>
        public long DoDownload() {
            Byte[] bytes = new Byte[512];
            long bytes_got;

            try {
                bytes_got = dataSock.Receive(bytes, bytes.Length, 0);

                if (bytes_got <= 0) {
                    // the download is done or an error occured
                    CloseDataSocket();
                    file.Close();
                    file = null;

                    ReadResponse();
                    switch (response) {
                        case 226:
                        case 250:
                            break;
                        default:
                            throw new CoAppException(responseStr);
                    }

                    SetBinaryMode(false);

                    return bytes_got;
                }

                file.Write(bytes, 0, (int)bytes_got);
                bytesTotal += bytes_got;
            }
            catch (Exception ex) {
                CloseDataSocket();
                file.Close();
                file = null;
                ReadResponse();
                SetBinaryMode(false);
                throw ex;
            }

            return bytes_got;
        }
    }
}