﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EnvDTE;
using System.IO;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Web.Configuration;
using System.Diagnostics;

namespace Microsoft.VSPowerToys.ResourceRefactor.Common
{
    public class AspxHardCodedString : CSharpRazorHardCodedString
    {
        public AspxHardCodedString() : base() { }

        public AspxHardCodedString(ProjectItem parent, int start, int end) : base(parent,start,end) { }

        protected override string StringRegExp {
            get { return String.Format("{0}|{1}", Strings.RegexInnerXML, base.StringRegExp); }
        }

        Regex commentsregex = new Regex("INEVERMATCHANYTHING\\+\\|\\*\\.\\\\", RegexOptions.Compiled);

        protected override System.Text.RegularExpressions.Regex CommentRegularExpression {
            get { return commentsregex; }
        }

        /// <summary>
        /// Cached value of the string
        /// </summary>
        private string value;
        private bool needsLocalizeControl = false;

        public override string Value {
            get {
                if (this.value == null) {
                    this.value = this.BeginEditPoint.GetText(this.TextLength);
                    if (this.value.StartsWith("\"")) {
                        // String in quotes.
                        this.value = value.Substring(1, value.Length - 2);
                        value = Regex.Unescape(value);
                    } else {
                        // String in html tags.
                        this.value = value.Trim();
                        this.needsLocalizeControl = true;
                    }
                }
                return this.value;

            }
        }

        public override string GetShortestReference(string reference, Collection<NamespaceImport> namespaces) {
            string refstr = base.GetShortestReference(reference, namespaces);

            if (this.needsLocalizeControl) {
                refstr = string.Format("<asp:Localize runat=\"server\" Text=\"<%$ Resources:Glossary, {0} %>\" />", refstr);
            } else {
                refstr = string.Format("<%$ Resources:Glossary, {0} %>", refstr);
            }
            return refstr;
        }


        public override BaseHardCodedString CreateInstance(EnvDTE.ProjectItem parent, int start, int end) {
            return new AspxHardCodedString(parent, start, end);
        }

        public override System.Collections.ObjectModel.Collection<NamespaceImport> GetImportedNamespaces() {
            var namespaces = GetImportedNamespaces();
            GetWebConfigNamespaces(namespaces);
            //GetNamespacesFromFile(namespaces);

            return namespaces;   
        }

        //copied from CSharpRazorHardCodedString
        //TODO - move this somewhere common. 

        private void GetNamespacesFromFile(Collection<NamespaceImport> namespaces) {
            TextDocument doc = (TextDocument)this.Parent.Document.Object("TextDocument");
            string contents = ExtensibilityMethods.GetDocumentText(doc);
            System.Text.RegularExpressions.Regex regExp = new Regex("@using[ \\t]+((.)*)");
            Match m = regExp.Match(contents);

            if (m.Groups.Count > 0) {
                if (m.Groups[1].Value.Contains("=")) {
                    var parts = m.Groups[1].Value.Split(new[] { '=' }, 2);
                    namespaces.Add(new NamespaceImport(m.Groups[1].Value, parts[0].Trim(), parts[1].Trim()));
                } else {
                    string ns = m.Groups[1].Value.Trim();
                    namespaces.Add(new NamespaceImport(ns, ns));
                }
            }
        }

        private void GetWebConfigNamespaces(Collection<NamespaceImport> namespaces) {
            string currentPath = this.Parent.Document.Path; // "D:\\CarolesFiles\\Documents\\Visual Studio 2010\\Projects\\OdeToFood\\OdeToFood\\Views\\Home\\";
            string projectPath = this.Parent.ContainingProject.FullName;  //"D:\\CarolesFiles\\Documents\\Visual Studio 2010\\Projects\\OdeToFood\\OdeToFood\\OdeToFood.csproj";
            projectPath = Path.GetDirectoryName(projectPath);

            var configFileMap = new WebConfigurationFileMap();
            var virtualDirectories = configFileMap.VirtualDirectories;
            string directoryVirtualPath = null;

            while (!currentPath.Equals(projectPath, StringComparison.OrdinalIgnoreCase)) {
                currentPath = Path.GetDirectoryName(currentPath);  // Gets the path of the current path's parent
                string relativePath = currentPath.Substring(projectPath.Length);

                bool isAppRoot = currentPath.Equals(projectPath, StringComparison.OrdinalIgnoreCase);
                string virtualPath = relativePath.Replace('\\', '/');
                if (virtualPath.Length == 0) {
                    virtualPath = "/";
                }

                directoryVirtualPath = directoryVirtualPath ?? virtualPath;

                virtualDirectories.Add(virtualPath, new VirtualDirectoryMapping(currentPath, isAppRoot: isAppRoot));
            }

            var config = WebConfigurationManager.OpenMappedWebConfiguration(configFileMap, directoryVirtualPath);

            // We use dynamic here because we could be dealing both with a 1.0 or a 2.0 RazorPagesSection, which
            // are not type compatible.
            dynamic section = config.GetSection("system.web/pages/namespaces");
            if (section != null) {
                foreach (NamespaceInfo n in section.Namespaces) {
                    Debug.WriteLine(n.Namespace);
                    namespaces.Add(new NamespaceImport(n.Namespace, n.Namespace));
                }

            }
        }

    }
}
