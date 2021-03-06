/// Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using Extensibility;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.CommandBars;
using System.Collections;
using System.Windows.Forms;
using System.Text.RegularExpressions;
using System.Diagnostics.CodeAnalysis;
using System.Resources;
using System.Globalization;
using System.Reflection;
using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.VSPowerToys.ResourceRefactor {
    /// <summary>The object for implementing an Add-in.</summary>
    /// <seealso class='IDTExtensibility2' />
    [System.Runtime.InteropServices.ComVisible(true)]
    public class Connect : IDTExtensibility2, IDTCommandTarget {

        #region Private Variables

        private DTE2 applicationObject;

        private AddIn addInInstance;

        /// <summary>
        /// List of available string refactor options.
        /// </summary>
        private Dictionary<string, Common.IStringRefactorOption> availableRefactorOptions = new Dictionary<string, Microsoft.VSPowerToys.ResourceRefactor.Common.IStringRefactorOption>();

        #endregion

        /// <summary>Implements the OnConnection method of the IDTExtensibility2 interface. Receives notification that the Add-in is being loaded.</summary>
        /// <param term='application'>Root object of the host application.</param>
        /// <param term='connectMode'>Describes how the Add-in is being loaded.</param>
        /// <param term='addInInst'>Object representing this Add-in.</param>
        /// <seealso class='IDTExtensibility2' />
        public void OnConnection(object Application, ext_ConnectMode ConnectMode, object AddInInst, ref Array custom) {
            if (Application == null) {
                throw new ArgumentNullException("Application");
            }
            if (AddInInst == null) {
                throw new ArgumentNullException("AddInInst");
            }
            applicationObject = (DTE2)Application;
            addInInstance = (AddIn)AddInInst;

            // Prepare the list of all available refactoring options
            this.ReadStringRefactorActions();

            if (ConnectMode == ext_ConnectMode.ext_cm_UISetup) {
                this.SetupRefactorActions();
            }

        }

        /// <summary>Implements the OnDisconnection method of the IDTExtensibility2 interface. Receives notification that the Add-in is being unloaded.</summary>
        /// <param term='disconnectMode'>Describes how the Add-in is being unloaded.</param>
        /// <param term='custom'>Array of parameters that are host application specific.</param>
        /// <seealso class='IDTExtensibility2' />
        public void OnDisconnection(ext_DisconnectMode RemoveMode, ref Array custom) {
        }

        /// <summary>Implements the OnAddInsUpdate method of the IDTExtensibility2 interface. Receives notification when the collection of Add-ins has changed.</summary>
        /// <param term='custom'>Array of parameters that are host application specific.</param>
        /// <seealso class='IDTExtensibility2' />		
        public void OnAddInsUpdate(ref Array custom) {
        }

        /// <summary>Implements the OnStartupComplete method of the IDTExtensibility2 interface. Receives notification that the host application has completed loading.</summary>
        /// <param term='custom'>Array of parameters that are host application specific.</param>
        /// <seealso class='IDTExtensibility2' />
        public void OnStartupComplete(ref Array custom) {
        }

        /// <summary>Implements the OnBeginShutdown method of the IDTExtensibility2 interface. Receives notification that the host application is being unloaded.</summary>
        /// <param term='custom'>Array of parameters that are host application specific.</param>
        /// <seealso class='IDTExtensibility2' />
        public void OnBeginShutdown(ref Array custom) {
        }

        #region IDTCommandTarget Members

        /// <summary>
        /// Handles the command execution. First method checks if user has selected a part of string literal in the code, if so 
        /// method invokes RefactorStringDialog.
        /// </summary>
        /// <param name="CmdName"></param>
        /// <param name="ExecuteOption"></param>
        /// <param name="VariantIn"></param>
        /// <param name="VariantOut"></param>
        /// <param name="Handled"></param>
        /// <remarks>Globalization message is suppressed because add-in is only designed for En-US at this point.</remarks>
        [SuppressMessage("Microsoft.Globalization", "CA1300:SpecifyMessageBoxOptions")]
        public void Exec(string CmdName, vsCommandExecOption ExecuteOption, ref object VariantIn, ref object VariantOut, ref bool Handled) {
            if (CmdName == null) {
                throw new ArgumentNullException("CmdName");
            }
            if (this.availableRefactorOptions.ContainsKey(CmdName)) {
                TextSelection selection = (TextSelection)(applicationObject.ActiveDocument.Selection);
                if (applicationObject.ActiveDocument.ProjectItem.Object != null) {
                    Common.BaseHardCodedString stringInstance = null;
                    /// Create the hard coded string instance
                    switch (applicationObject.ActiveDocument.Language) {
                        case "CSharp":
                            stringInstance = new Common.CSharpHardCodedString();
                            break;
                        case "Basic":
                            stringInstance = new Common.VBHardCodedString();
                            break;
                        case "XAML":
                            stringInstance = new Common.XamlHardCodedString();
                            break;
                        case "HTML":
                            if (applicationObject.ActiveDocument.Name.Contains(".cshtml")) {
                                stringInstance = new Common.CSharpRazorHardCodedString();
                            }
                            break;
                        default:
                            MessageBox.Show(
                                Strings.UnsupportedFile + " (" + applicationObject.ActiveDocument.Language + ")",
                                Strings.WarningTitle,
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                            return;
                    }
                    Common.MatchResult scanResult = stringInstance.CheckForHardCodedString(
                       selection.Parent,
                       selection.AnchorPoint.AbsoluteCharOffset - 1,
                       selection.BottomPoint.AbsoluteCharOffset - 1);

                    if (!scanResult.Result && selection.AnchorPoint.AbsoluteCharOffset < selection.BottomPoint.AbsoluteCharOffset) {
                        scanResult.StartIndex = selection.AnchorPoint.AbsoluteCharOffset - 1;
                        scanResult.EndIndex = selection.BottomPoint.AbsoluteCharOffset - 1;
                        scanResult.Result = true;
                    }
                    if (scanResult.Result) {
                        stringInstance = stringInstance.CreateInstance(applicationObject.ActiveDocument.ProjectItem, scanResult.StartIndex, scanResult.EndIndex);
                        if (stringInstance != null && stringInstance.Parent != null) {
                            Common.IStringRefactorOption option = this.availableRefactorOptions[CmdName];
                            if (option.QuerySupportForString(stringInstance)) {
                                option.PerformAction(stringInstance);
                            } else {
                                MessageBox.Show(
                               Strings.UnsupportedFile,
                               Strings.WarningTitle,
                               MessageBoxButtons.OK,
                               MessageBoxIcon.Error);
                            }
                        }
                    } else {
                        MessageBox.Show(
                                Strings.NotStringLiteral,
                                Strings.WarningTitle,
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                    }
                }
            }
        }

        public void QueryStatus(string CmdName, vsCommandStatusTextWanted NeededText, ref vsCommandStatus StatusOption, ref object CommandText) {
            if (CmdName == null) {
                throw new ArgumentNullException("CmdName");
            }
            if (NeededText == vsCommandStatusTextWanted.vsCommandStatusTextWantedNone) {
                if (this.availableRefactorOptions.ContainsKey(CmdName)) {
                    this.availableRefactorOptions[CmdName].QueryStatus(CmdName, NeededText, ref StatusOption, ref CommandText, applicationObject);
                }
            }
        }

        #endregion

        /// <summary>
        /// Finds the localized menu name from English menu name
        /// </summary>
        /// <param name="name">Name of the menu</param>
        /// <returns>Localized name</returns>
        /// <remarks>We have to catch all exceptions since GetString method can throw a very extensive
        /// list of exceptions</remarks>
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        private string FindLocalizedMenuName(string name) {
            string menuName;
            ResourceManager resourceManager = new ResourceManager("Microsoft.VSPowerToys.ResourceRefactor.CommandBar", Assembly.GetExecutingAssembly());
            CultureInfo cultureInfo = new System.Globalization.CultureInfo(this.applicationObject.LocaleID);
            string resourceName = String.Concat(cultureInfo.TwoLetterISOLanguageName, name);
            try {
                menuName = resourceManager.GetString(resourceName);
            } catch {
                //We tried to find a localized version of the word, but one was not found.
                //Default to the en-US word, which may work for the current culture.
                menuName = name;
            }
            return menuName;
        }

        /// <summary>
        /// Inspects the current assembly for implementation of <see cref="Common.IStringRefactorAction"/>n and
        /// adds them to the menu entries
        /// </summary>
        private void ReadStringRefactorActions() {
            try {

                Type[] availableTypes = Assembly.GetExecutingAssembly().GetTypes();
                foreach (Type type in availableTypes) {
                    if (!type.IsAbstract && (typeof(Common.IStringRefactorOption)).IsAssignableFrom(type)) {
                        try {
                            Common.IStringRefactorOption option = System.Activator.CreateInstance(type) as Common.IStringRefactorOption;
                            if (option == null) continue;
                            if (this.availableRefactorOptions.ContainsKey(addInInstance.ProgID + "." + option.CommandName)) continue;
                            this.availableRefactorOptions.Add(addInInstance.ProgID + "." + option.CommandName, option);
                        } catch (TargetInvocationException e) {
                            System.Diagnostics.Trace.TraceError(e.ToString());
                        } catch (ArgumentException e) {
                            System.Diagnostics.Trace.TraceError(e.ToString());
                        } catch (MissingMethodException e) {
                            System.Diagnostics.Trace.TraceError(e.ToString());
                        }
                    }
                }
            } catch (Exception e) {
                System.Diagnostics.Trace.TraceError(e.ToString());
                throw;
            }
        }

        private void SetupRefactorActions() {
            object[] contextGUIDS = new object[] { };
            Commands2 commands = (Commands2)applicationObject.Commands;
            Command refactorCommand = null;
            CommandBar refactorContextMenu;
            CommandBar refactorToolbarMenu;
            try {
                CommandBar menuBarCommandBar = ((CommandBars)applicationObject.CommandBars)["MenuBar"];
                refactorToolbarMenu = ((CommandBarPopup)(menuBarCommandBar.Controls[FindLocalizedMenuName("Refactor")])).CommandBar;
                refactorContextMenu = ((CommandBars)(applicationObject.CommandBars))["Refactor"];

                //Editor Context Menus | XAML Editor
                CommandBar xamlEditorContextMenu = ((CommandBars)(applicationObject.CommandBars))["XAML Editor"];

                //Editor Context Menus | HTML Editor
                CommandBar htmlEditorContextMenu = ((CommandBars)(applicationObject.CommandBars))["ASPX Context"];

                foreach (Common.IStringRefactorOption option in this.availableRefactorOptions.Values) {
                    while (true) {
                        try {
                            CommandBarControl ctl = (CommandBarControl)(refactorContextMenu.Controls[option.MenuText]);
                            ctl.Delete(false);
                        } catch (ArgumentException) {
                            break;
                        }
                    }

                    while (true) {
                        try {
                            CommandBarControl ctl = (CommandBarControl)(refactorToolbarMenu.Controls[option.MenuText]);
                            ctl.Delete(false);
                        } catch (ArgumentException) {
                            break;
                        }
                    }

                    while (true) {
                        try {
                            CommandBarControl ctl = (CommandBarControl)(xamlEditorContextMenu.Controls[option.MenuText]);
                            ctl.Delete(false);
                        } catch (ArgumentException) {
                            break;
                        }

                    }

                    while (true) {
                        try {
                            CommandBarControl ctl = (CommandBarControl)(htmlEditorContextMenu.Controls[option.MenuText]);
                            ctl.Delete(false);
                        } catch (ArgumentException) {
                            break;
                        }
                    }
                    // Create refactor command if necessary
                    try {
                        refactorCommand = commands.AddNamedCommand2(addInInstance,
                            option.CommandName,
                            option.MenuText,
                            option.Description,
                            true, 69, ref contextGUIDS, (int)(vsCommandStatus.vsCommandStatusEnabled | vsCommandStatus.vsCommandStatusSupported), (int)vsCommandStyle.vsCommandStyleText, vsCommandControlType.vsCommandControlTypeButton);
                        if (!String.IsNullOrEmpty(option.Hotkey)) {
                            refactorCommand.Bindings = new object[] { option.Hotkey };
                        }
                    } catch (ArgumentException) {
                        refactorCommand = commands.Item(addInInstance.ProgID + "." + option.CommandName, -1);
                    }
                    refactorCommand.AddControl(refactorContextMenu, refactorContextMenu.Controls.Count + 1);
                    refactorCommand.AddControl(refactorToolbarMenu, refactorToolbarMenu.Controls.Count + 1);
                    refactorCommand.AddControl(xamlEditorContextMenu, xamlEditorContextMenu.Controls.Count + 1);
                    refactorCommand.AddControl(htmlEditorContextMenu, htmlEditorContextMenu.Controls.Count + 1);

                }
            } catch (Exception e) {
                System.Diagnostics.Trace.TraceError(e.ToString());
                throw;
            }
        }


    }
}
