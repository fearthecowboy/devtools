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
    using CSharp;
    using Utility;

    /// <summary>
    ///   Tokenizer for the g# language
    /// </summary>
    public class GSharpTokenizer : CSharpTokenizer {
        /// <summary>
        ///   Protected Constructor for tokenizing g# code. Public access via the static Tokenize methods.
        /// </summary>
        /// <param name = "text">the array of characters to tokenize.</param>
        protected GSharpTokenizer(char[] text)
            : base(text) {
        }

        /// <summary>
        ///   Parses multi-line #macros
        /// </summary>
        protected virtual void ParseMultiLineMacro() {
        }

        /// <summary>
        ///   Parses the #! style shell execute macros
        /// </summary>
        protected virtual void ParseShellExecute() {
        }

        /// <summary>
        ///   Parses the #macro style declarations
        /// </summary>
        protected override void ParsePound() {
            if(NextCharacter == '{') {
                // gSharp style multi-line-macro
                ParseMultiLineMacro();
                return;
            }

            if(NextCharacter == '!') {
                // gSharp style shell execution
                ParseShellExecute();
                return;
            }

            base.ParsePound();
        }

        /// <summary>
        ///   Tokenizes the source code and returns a list of tokens
        /// </summary>
        /// <param name = "text">The g# source code to tokenize (as a string)</param>
        /// <returns>A List of tokens</returns>
        public static new List<Token> Tokenize(string text) {
            return new GSharpTokenizer(text.ToCharArray()).Tokens;
        }

        /// <summary>
        ///   Tokenizes the source code and returns a list of tokens
        /// </summary>
        /// <param name = "text">The g# source code to tokenize (as an array of characters)</param>
        /// <returns>A List of tokens</returns>
        public static new List<Token> Tokenize(char[] text) {
            var tokenizer = new GSharpTokenizer(text);
            tokenizer.Tokenize();
            return tokenizer.Tokens;
        }
    }
}