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


namespace CoApp.Developer.Toolkit.Scripting.Languages.GSharp {
    using System.Collections.Generic;
    using Utility;

    /// <summary>
    ///   Transforms g# code into legal c# code
    /// </summary>
    public class GSharpProcessor {
        /// <summary>
        ///   A list of tokens
        /// </summary>
        protected List<Token> tokens;

        /// <summary>
        ///   the text to process
        /// </summary>
        protected string scriptText;

        /// <summary>
        ///   Constructor to create script processor
        /// </summary>
        /// <param name = "scriptText">g# source code to execute</param>
        public GSharpProcessor(string scriptText) {
            this.scriptText = scriptText;
        }

        /// <summary>
        ///   Performs the text processing.
        /// </summary>
        /// <returns>The c# code</returns>
        private string ProcessText() {
            tokens = GSharpTokenizer.Tokenize(scriptText);

            foreach(Token t in tokens) {
            }

            return null;
        }

        /// <summary>
        ///   Public static accessor to process script text
        /// </summary>
        /// <param name = "scriptText">Script text to process</param>
        /// <returns>c# source code</returns>
        public static string Process(string scriptText) {
            return new GSharpProcessor(scriptText).ProcessText();
        }
    }
}