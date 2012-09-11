namespace CoApp.Toolkit.Text.Sgml {
    using System;
    using System.Collections;
    using System.Globalization;

    /// <summary>
    ///   Defines a group of elements nested within another element.
    /// </summary>
    public class Group {
        private Group m_parent;
        private ArrayList Members;
        private GroupType m_groupType;
        private Occurrence m_occurrence;
        private bool Mixed;

        /// <summary>
        ///   The <see cref = "Occurrence" /> of this group.
        /// </summary>
        public Occurrence Occurrence {
            get { return m_occurrence; }
        }

        /// <summary>
        ///   Checks whether the group contains only text.
        /// </summary>
        /// <value>true if the group is of mixed content and has no members, otherwise false.</value>
        public bool TextOnly {
            get { return this.Mixed && Members.Count == 0; }
        }

        /// <summary>
        ///   The parent group of this group.
        /// </summary>
        public Group Parent {
            get { return m_parent; }
        }

        /// <summary>
        ///   Initialises a new Content Model Group.
        /// </summary>
        /// <param name = "parent">The parent model group.</param>
        public Group(Group parent) {
            m_parent = parent;
            Members = new ArrayList();
            m_groupType = GroupType.None;
            m_occurrence = Occurrence.Required;
        }

        /// <summary>
        ///   Adds a new child model group to the end of the group's members.
        /// </summary>
        /// <param name = "g">The model group to add.</param>
        public void AddGroup(Group g) {
            Members.Add(g);
        }

        /// <summary>
        ///   Adds a new symbol to the group's members.
        /// </summary>
        /// <param name = "sym">The symbol to add.</param>
        public void AddSymbol(string sym) {
            if(string.Equals(sym, "#PCDATA", StringComparison.OrdinalIgnoreCase)) {
                Mixed = true;
            }
            else {
                Members.Add(sym);
            }
        }

        /// <summary>
        ///   Adds a connector onto the member list.
        /// </summary>
        /// <param name = "c">The connector character to add.</param>
        /// <exception cref = "SgmlParseException">
        ///   If the content is not mixed and has no members yet, or if the group type has been set and the
        ///   connector does not match the group type.
        /// </exception>
        public void AddConnector(char c) {
            if(!Mixed && Members.Count == 0) {
                throw new SgmlParseException(string.Format(CultureInfo.CurrentUICulture, "Missing token before connector '{0}'.", c));
            }

            var gt = GroupType.None;
            switch(c) {
                case ',':
                    gt = GroupType.Sequence;
                    break;
                case '|':
                    gt = GroupType.Or;
                    break;
                case '&':
                    gt = GroupType.And;
                    break;
            }

            if(this.m_groupType != GroupType.None && this.m_groupType != gt) {
                throw new SgmlParseException(string.Format(CultureInfo.CurrentUICulture, "Connector '{0}' is inconsistent with {1} group.", c, m_groupType));
            }

            m_groupType = gt;
        }

        /// <summary>
        ///   Adds an occurrence character for this group, setting it's <see cref = "Occurrence" /> value.
        /// </summary>
        /// <param name = "c">The occurrence character.</param>
        public void AddOccurrence(char c) {
            var o = Occurrence.Required;
            switch(c) {
                case '?':
                    o = Occurrence.Optional;
                    break;
                case '+':
                    o = Occurrence.OneOrMore;
                    break;
                case '*':
                    o = Occurrence.ZeroOrMore;
                    break;
            }

            m_occurrence = o;
        }

        /// <summary>
        ///   Checks whether an element using this group can contain a specified element.
        /// </summary>
        /// <param name = "name">The name of the element to look for.</param>
        /// <param name = "dtd">The DTD to use during the checking.</param>
        /// <returns>true if an element using this group can contain the element, otherwise false.</returns>
        /// <remarks>
        ///   Rough approximation - this is really assuming an "Or" group
        /// </remarks>
        public bool CanContain(string name, SgmlDtd dtd) {
            if(dtd == null) {
                throw new ArgumentNullException("dtd");
            }

            // Do a simple search of members.
            foreach(object obj in Members) {
                if(obj is string) {
                    if(string.Equals((string) obj, name, StringComparison.OrdinalIgnoreCase)) {
                        return true;
                    }
                }
            }
            // didn't find it, so do a more expensive search over child elements
            // that have optional start tags and over child groups.
            foreach(object obj in Members) {
                var s = obj as string;
                if(s != null) {
                    var e = dtd.FindElement(s);
                    if(e != null) {
                        if(e.StartTagOptional) {
                            // tricky case, the start tag is optional so element may be
                            // allowed inside this guy!
                            if(e.CanContain(name, dtd)) {
                                return true;
                            }
                        }
                    }
                }
                else {
                    var m = (Group) obj;
                    if(m.CanContain(name, dtd)) {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}