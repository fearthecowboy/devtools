﻿//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     ResourceLib Original Code from http://resourcelib.codeplex.com
//     Original Copyright (c) 2008-2009 Vestris Inc.
//     Changes Copyright (c) 2011 Garrett Serack . All rights reserved.
// </copyright>
// <license>
// MIT License
// You may freely use and distribute this software under the terms of the following license agreement.
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated 
// documentation files (the "Software"), to deal in the Software without restriction, including without limitation 
// the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and 
// to permit persons to whom the Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of 
// the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO 
// THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE 
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, 
// TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE
// </license>
//-----------------------------------------------------------------------

namespace CoApp.Developer.Toolkit.ResourceLib {
    using System;
    using System.IO;
    using CoApp.Toolkit.Win32;

    /// <summary>
    ///   A font, RT_FONT resource.
    /// </summary>
    public class FontResource : GenericResource {
        /// <summary>
        ///   A new font resource.
        /// </summary>
        public FontResource() : base(IntPtr.Zero, IntPtr.Zero, new ResourceId(ResourceTypes.RT_FONT), null, ResourceUtil.NEUTRALLANGID, 0) {
        }

        /// <summary>
        ///   An existing font resource.
        /// </summary>
        /// <param name = "hModule">Module handle.</param>
        /// <param name = "hResource">Resource ID.</param>
        /// <param name = "type">Resource type.</param>
        /// <param name = "name">Resource name.</param>
        /// <param name = "language">Language ID.</param>
        /// <param name = "size">Resource size.</param>
        public FontResource(IntPtr hModule, IntPtr hResource, ResourceId type, ResourceId name, UInt16 language, int size)
            : base(hModule, hResource, type, name, language, size) {
        }

        /// <summary>
        ///   Read the font resource.
        /// </summary>
        /// <param name = "hModule">Handle to a module.</param>
        /// <param name = "lpRes">Pointer to the beginning of the font structure.</param>
        /// <returns>Address of the end of the font structure.</returns>
        internal override IntPtr Read(IntPtr hModule, IntPtr lpRes) {
            return base.Read(hModule, lpRes);
        }

        /// <summary>
        ///   Write the font resource to a binary writer.
        /// </summary>
        /// <param name = "w">Binary writer.</param>
        internal override void Write(BinaryWriter w) {
            base.Write(w);
        }
    }
}