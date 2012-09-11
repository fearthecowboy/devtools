﻿//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2010 Garrett Serack. All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

namespace CoApp.Toolkit.DynamicXml {
    using System.Dynamic;
    using System.Xml.Linq;

    /// <summary>
    ///   some summary
    /// </summary>
    public class DynamicAttributes : DynamicObject {
        /// <summary>
        ///   The node this object is fronting for.
        /// </summary>
        private readonly XElement node;

        /// <summary>
        ///   Creates an Attribute handler for the given XML Node.
        /// </summary>
        /// <param name = "node">the XML node</param>
        public DynamicAttributes(XElement node) {
            this.node = node;
        }

        public bool Has(string attributeName ) {
            return node.Attribute(attributeName) != null;
        }

        /// <summary>
        ///   Returns the Attribute value
        /// </summary>
        /// <param name = "binder">the Attribute Name</param>
        /// <param name = "result">the return value (attribute value)</param>
        /// <returns>true if successful</returns>
        public override bool TryGetMember(GetMemberBinder binder, out object result) {
            var attr = node.Attribute(binder.Name);
            if(attr != null) {
                result = attr.Value;
                return true;
            }

            result = null;
            return false;
        }

        /// <summary>
        ///   Sets the attribute value
        /// </summary>
        /// <param name = "binder">Attribute name</param>
        /// <param name = "value">Value to set</param>
        /// <returns>True</returns>
        public override bool TrySetMember(SetMemberBinder binder, object value) {
            node.SetAttributeValue(binder.Name, value);
            return true;
        }
    }
}