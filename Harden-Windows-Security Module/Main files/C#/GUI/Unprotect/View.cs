﻿using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using System.Xml;
using System.Windows.Media.Imaging;
using System.Linq;
using System.Windows.Forms;
using System.Collections.Concurrent;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.ComponentModel;
using System.Threading;
using System.Windows.Automation;
using System.Windows.Controls.Ribbon;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms.Integration;
using System.Windows.Ink;
using System.Windows.Media.Animation;
using System.Windows.Media.Media3D;
using System.Windows.Media.TextFormatting;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Shell;
using System.Threading.Tasks;
using System.Text;
using System.Reflection.PortableExecutable;

#nullable enable

namespace HardenWindowsSecurity
{
    public partial class GUIMain
    {

        // Partial class definition for handling navigation and view models
        public partial class NavigationVM : ViewModelBase
        {

            // Method to handle the Unprotect view, including loading
            private void Unprotect(object obj)
            {

                // Check if the view is already cached
                if (_viewCache.TryGetValue("UnprotectView", out var cachedView))
                {
                    CurrentView = cachedView;
                    return;
                }

                // Defining the path to the XAML XML file
                if (HardenWindowsSecurity.GlobalVars.path == null)
                {
                    throw new System.ArgumentNullException("GlobalVars.path cannot be null.");
                }

                // Construct the file path for the Unprotect view XAML
                string xamlPath = System.IO.Path.Combine(HardenWindowsSecurity.GlobalVars.path, "Resources", "XAML", "Unprotect.xaml");

                // Read the XAML content from the file
                string xamlContent = File.ReadAllText(xamlPath);

                // Parse the XAML content to create a UserControl
                HardenWindowsSecurity.GUIUnprotect.View = (System.Windows.Controls.UserControl)XamlReader.Parse(xamlContent);

                #region Finding The Elements

                // Find the Parent Grid
                HardenWindowsSecurity.GUIUnprotect.ParentGrid = (System.Windows.Controls.Grid)HardenWindowsSecurity.GUIUnprotect.View.FindName("ParentGrid");

                // Finding the Execute Button Grid
                System.Windows.Controls.Grid? ExecuteButtonGrid = GUIUnprotect.ParentGrid.FindName("ExecuteButtonGrid") as System.Windows.Controls.Grid;

                if (ExecuteButtonGrid == null)
                {
                    throw new Exception("ExecuteButtonGrid is null in the ASRRules View");
                }

                // Finding the Execute Button
                System.Windows.Controls.Primitives.ToggleButton? ExecuteButton = ExecuteButtonGrid.FindName("ExecuteButton") as System.Windows.Controls.Primitives.ToggleButton;

                if (ExecuteButton == null)
                {
                    throw new Exception("Couldn't find the ExecuteButton in ASRRules view");
                }

                // Apply the template to make sure it's available
                ExecuteButton.ApplyTemplate();

                // Access the image within the Execute Button's template
                System.Windows.Controls.Image? RefreshIconImage = ExecuteButton.Template.FindName("RefreshIconImage", ExecuteButton) as System.Windows.Controls.Image;

                if (RefreshIconImage == null)
                {
                    throw new Exception("RefreshIconImage could not be found in the ASRRules view");
                }

                // Update the image source for the Refresh button
                RefreshIconImage.Source =
                    new System.Windows.Media.Imaging.BitmapImage(
                        new System.Uri(System.IO.Path.Combine(HardenWindowsSecurity.GlobalVars.path!, "Resources", "Media", "ExecuteButton.png"))
                    );

                #endregion

                System.Windows.Controls.ComboBox? UnprotectCategoriesComboBox = GUIUnprotect.ParentGrid.FindName("UnprotectCategories") as System.Windows.Controls.ComboBox;

                System.Windows.Controls.ComboBox? WDACPoliciesComboBox = GUIUnprotect.ParentGrid.FindName("WDACPolicies") as System.Windows.Controls.ComboBox;


                // Remove Windows protections only if user has Admin privileges
                if (!HardenWindowsSecurity.UserPrivCheck.IsAdmin())
                {
                    // Disable the execute button
                    ExecuteButton.IsEnabled = false;
                    HardenWindowsSecurity.Logger.LogMessage("You need Administrator privileges to remove protections from the system.");
                }
                // If there is no Admin rights, this dynamic enablement/disablement isn't necessary as it will override the disablement that happens above.
                else
                {
                    // Register the ExecuteButton as an element that will be enabled/disabled based on current activity
                    HardenWindowsSecurity.ActivityTracker.RegisterUIElement(ExecuteButton);
                }

                // Set up the Click event handler for the ExecuteButton button
                ExecuteButton.Click += async (sender, e) =>
                {
                    // Only continue if there is no activity other places
                    if (HardenWindowsSecurity.ActivityTracker.IsActive == false)
                    {
                        // mark as activity started
                        HardenWindowsSecurity.ActivityTracker.IsActive = true;

                        // Disable the ExecuteButton button while processing
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            ExecuteButton.IsEnabled = false;

                            if (WDACPoliciesComboBox == null)
                            {
                                throw new Exception("WDACPoliciesComboBox is null");
                            }

                            if (UnprotectCategoriesComboBox == null)
                            {
                                throw new Exception("UnprotectCategoriesComboBox is null");
                            }

                            // Store the values of the combo boxes in View variables since they need to be acquired through the Application dispatcher since they belong to the UI thread
                            GUIUnprotect.UnprotectCategoriesComboBoxSelection = (byte)UnprotectCategoriesComboBox.SelectedIndex;
                            GUIUnprotect.WDACPoliciesComboBoxSelection = (byte)WDACPoliciesComboBox.SelectedIndex;

                        });

                        // Run the Unprotect commands asynchronously in a different thread
                        await System.Threading.Tasks.Task.Run(() =>
                        {
                            // if LGPO doesn't already exist in the working directory, then download it
                            if (!System.IO.Path.Exists(GlobalVars.LGPOExe))
                            {
                                Logger.LogMessage("LGPO.exe doesn't exist, downloading it.");
                                AsyncDownloader.PrepDownloadedFiles(GlobalVars.LGPOExe, null, null, true);
                            }
                            else
                            {
                                Logger.LogMessage("LGPO.exe already exists, skipping downloading it.");
                            }



                            switch (GUIUnprotect.UnprotectCategoriesComboBoxSelection)
                            {
                                // Only Remove The Process Mitigations
                                case 0:
                                    {
                                        HardenWindowsSecurity.UnprotectWindowsSecurity.RemoveExploitMitigations();
                                        break;
                                    }
                                // Only Remove The WDAC Policies
                                case 1:
                                    {
                                        // Downloads Defense Measures
                                        if (GUIUnprotect.WDACPoliciesComboBoxSelection == 0)
                                        {
                                            HardenWindowsSecurity.UnprotectWindowsSecurity.RemoveWDACPolicies(true, false);
                                        }
                                        // Dangerous Script Hosts Blocking
                                        else if (GUIUnprotect.WDACPoliciesComboBoxSelection == 1)
                                        {
                                            HardenWindowsSecurity.UnprotectWindowsSecurity.RemoveWDACPolicies(false, true);
                                        }
                                        // All WDAC Policies
                                        else
                                        {
                                            HardenWindowsSecurity.UnprotectWindowsSecurity.RemoveWDACPolicies(true, true);
                                        }

                                        break;
                                    }
                                // Only Remove The Country IP Blocking Firewall Rules
                                case 2:
                                    {
                                        HardenWindowsSecurity.UnprotectWindowsSecurity.RemoveCountryIPBlockingFirewallRules();
                                        break;
                                    }
                                // Remove All Protections
                                case 3:
                                    {
                                        HardenWindowsSecurity.UnprotectWindowsSecurity.RemoveWDACPolicies(true, true);
                                        HardenWindowsSecurity.UnprotectWindowsSecurity.Unprotect();
                                        HardenWindowsSecurity.UnprotectWindowsSecurity.RemoveExploitMitigations();

                                        break;
                                    }
                            }

                        });

                        // Update the UI Elements at the end of the run
                        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            ExecuteButton.IsEnabled = true; // Enable the ExecuteButton button
                            ExecuteButton.IsChecked = false; // Uncheck the ExecuteButton button to start the reverse animation

                        });

                        // mark as activity completed
                        HardenWindowsSecurity.ActivityTracker.IsActive = false;

                    }
                };

                // Cache the view before setting it as the CurrentView
                _viewCache["UnprotectView"] = HardenWindowsSecurity.GUIUnprotect.View;

                // Set the CurrentView to the Protect view
                CurrentView = HardenWindowsSecurity.GUIUnprotect.View;
            }
        }
    }
}
