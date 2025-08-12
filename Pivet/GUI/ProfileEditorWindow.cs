using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terminal.Gui;

namespace Pivet.GUI
{
    internal class ProfileEditorWindow : Window
    {
        private ListView profileList;
        private Config current_config;
        private ProfileConfig selectedProfile;

        private bool dirtyFlag = false;
        private TextField profileName;
        private Label dataProvidersCountLabel;
        private Button editDataProvidersButton;
        private Label filtersCountLabel;
        private Button editFiltersButton;
        private Button editRawDataButton;
        
        // Filter fields - will be implemented in Phase 4
        // private ListView projectsList;
        // private ListView prefixesList; 
        // private ListView includeOpridsList;
        // private ListView excludeOpridsList;
        // private ListView messageCatalogsList;
        // private ListView rawDataList;
        
        private Button saveButton;

        internal void MarkDirty(NStack.ustring newValue)
        {
            saveButton.Visible = true;
            saveButton.SetNeedsDisplay();
            dirtyFlag = true;
        }

        internal void SaveProfileData()
        {
            if (selectedProfile == null) return;
            
            selectedProfile.Name = profileName.Text.ToString();
            
            // Data providers are handled separately through Add/Remove buttons
            
            // Validate the profile before saving
            var validationResult = ConfigValidator.ValidateProfile(selectedProfile);
            if (!validationResult.IsValid)
            {
                var errorMessages = string.Join("\n", validationResult.Errors.Select(e => e.Message));
                MessageBox.ErrorQuery("Validation Errors", $"Cannot save profile:\n{errorMessages}", "OK");
                return;
            }
            
            if (validationResult.HasWarnings)
            {
                var warningMessages = string.Join("\n", validationResult.Warnings.Select(w => w.Message));
                var result = MessageBox.Query("Validation Warnings", $"Profile has warnings:\n{warningMessages}\n\nSave anyway?", "Yes", "No");
                if (result != 0) return;
            }
            
            saveButton.Visible = false;
            saveButton.SetNeedsDisplay();
            dirtyFlag = false;
        }

        internal void PromptForSaveIfNeeded()
        {
            if (dirtyFlag)
            {
                var result = MessageBox.Query("Save Changes?", "Would you like to save changes to the current profile?", "Yes", "No");
                if (result == 0)
                {
                    SaveProfileData();
                }
            }
        }

        internal void SelectProfile(int index)
        {
            PromptForSaveIfNeeded();

            bool createdDefaultProfile = false;

            if (current_config.Profiles.Count == 0)
            {
                current_config.Profiles.Add(new ProfileConfig() { Name = "<unnamed>" });
                createdDefaultProfile = true;
            }

            selectedProfile = current_config.Profiles[index];

            if (profileName == null)
            {
                /* UI hasn't been built yet */
                return;
            }

            // Populate profile fields
            profileName.Text = selectedProfile.Name;
            
            // Refresh summary displays
            RefreshSummaryLabels();
            
            dirtyFlag = createdDefaultProfile;
        }

        internal ProfileEditorWindow(Config config) : base("Profiles")
        {
            current_config = config;
            
            // Left pane - Profile list
            var listFrame = new FrameView("Profiles")
            {
                X = 1,
                Y = 1,
                Height = Dim.Fill(2),
                Width = Dim.Percent(25)
            };

            profileList = new ListView()
            {
                X = 0,
                Y = 0,
                Height = Dim.Fill(),
                Width = Dim.Fill()
            };
            
            var wrapper = new ListWrapper(current_config.Profiles);
            profileList.Source = wrapper;
            profileList.SelectedItemChanged += ProfileList_SelectedItemChanged;

            listFrame.Add(profileList);
            Add(listFrame);

            var createProfileButton = new Button("Create Profile")
            {
                X = 1,
                Y = Pos.Bottom(listFrame),
                Height = 1,
                Width = Dim.Percent(25),
                TextAlignment = TextAlignment.Centered
            };

            createProfileButton.Clicked += CreateProfileButton_Clicked;
            Add(createProfileButton);

            var statusBar = new Label("ESC: Close | Ctrl+S: Save | Ctrl+N: New Profile | Tab: Navigate fields")
            {
                X = 1,
                Y = Pos.Bottom(createProfileButton),
                Height = 1,
                Width = Dim.Fill(),
                ColorScheme = Colors.Dialog
            };
            Add(statusBar);

            // Right pane - Profile details
            var profileDetailsFrame = new FrameView("Profile Details")
            {
                X = Pos.Right(listFrame),
                Y = 1,
                Width = Dim.Fill(2),
                Height = Dim.Fill(2)
            };

            var label = new Label("Profile Name: ")
            {
                X = 1,
                Y = 1,
                Height = 1,
                Width = 15
            };

            profileName = new TextField()
            {
                X = Pos.Right(label),
                Y = 1,
                Width = 40
            };
            
            profileDetailsFrame.Add(label, profileName);

            // Data Providers section
            var dataProvidersLabel = new Label("Data Providers:")
            {
                X = 1,
                Y = Pos.Bottom(profileName) + 2,
                AutoSize = true
            };
            profileDetailsFrame.Add(dataProvidersLabel);

            dataProvidersCountLabel = new Label("(0 configured)")
            {
                X = Pos.Right(dataProvidersLabel) + 1,
                Y = Pos.Top(dataProvidersLabel),
                Width = 20,
                ColorScheme = Colors.Dialog
            };
            profileDetailsFrame.Add(dataProvidersCountLabel);

            editDataProvidersButton = new Button("Edit...")
            {
                X = Pos.Right(dataProvidersCountLabel) + 2,
                Y = Pos.Top(dataProvidersLabel),
                Width = 10
            };
            editDataProvidersButton.Clicked += EditDataProviders_Clicked;
            profileDetailsFrame.Add(editDataProvidersButton);

            // Filters section
            var filtersLabel = new Label("Filters:")
            {
                X = 1,
                Y = Pos.Bottom(dataProvidersLabel) + 2,
                AutoSize = true
            };
            profileDetailsFrame.Add(filtersLabel);

            filtersCountLabel = new Label("(Projects: 0, Prefixes: 0)")
            {
                X = Pos.Right(filtersLabel) + 1,
                Y = Pos.Top(filtersLabel),
                Width = 30,
                ColorScheme = Colors.Dialog
            };
            profileDetailsFrame.Add(filtersCountLabel);

            editFiltersButton = new Button("Edit...")
            {
                X = Pos.Right(filtersCountLabel) + 2,
                Y = Pos.Top(filtersLabel),
                Width = 10
            };
            editFiltersButton.Clicked += EditFilters_Clicked;
            profileDetailsFrame.Add(editFiltersButton);

            // Raw Data section (conditional)
            var rawDataLabel = new Label("Raw Data:")
            {
                X = 1,
                Y = Pos.Bottom(filtersLabel) + 2,
                AutoSize = true
            };
            profileDetailsFrame.Add(rawDataLabel);

            editRawDataButton = new Button("Edit...")
            {
                X = Pos.Right(rawDataLabel) + 2,
                Y = Pos.Top(rawDataLabel),
                Width = 10
            };
            editRawDataButton.Clicked += EditRawData_Clicked;
            profileDetailsFrame.Add(editRawDataButton);

            // Save button
            saveButton = new Button("Save Changes")
            {
                X = 1,
                Y = Pos.Bottom(rawDataLabel) + 3,
                AutoSize = true,
                Visible = false
            };

            saveButton.Clicked += () =>
            {
                SaveProfileData();
                profileName.SetFocus();
                saveButton.SetNeedsDisplay();
            };

            profileDetailsFrame.Add(saveButton);
            Add(profileDetailsFrame);

            // Event handlers
            profileName.TextChanged += MarkDirty;
            
            // Initialize first profile if any exist
            if (current_config.Profiles.Count > 0)
            {
                profileList.SelectedItem = 0;
                SelectProfile(0);
            }

            // Keyboard shortcuts
            KeyUp += (e) => {
                switch (e.KeyEvent.Key)
                {
                    case Key.Esc:
                        RequestStop();
                        break;
                    case Key.CtrlMask | Key.S:
                        if (saveButton.Visible)
                            SaveProfileData();
                        break;
                    case Key.CtrlMask | Key.N:
                        CreateProfileButton_Clicked();
                        break;
                }
            };

            Closing += ProfileEditorWindow_Closing;
        }

        private void ProfileEditorWindow_Closing(ToplevelClosingEventArgs obj)
        {
            PromptForSaveIfNeeded();
        }

        private void ProfileList_SelectedItemChanged(ListViewItemEventArgs obj)
        {
            SelectProfile(obj.Item);
        }

        private void CreateProfileButton_Clicked()
        {
            PromptForSaveIfNeeded();
            var new_profile = new ProfileConfig() { Name = "<unnamed>" };
            current_config.Profiles.Add(new_profile);
            
            // Refresh the list and select the new profile
            var wrapper = new ListWrapper(current_config.Profiles);
            profileList.Source = wrapper;
            profileList.SelectedItem = current_config.Profiles.Count - 1;
            profileList.SetFocus();
        }

        private void EditDataProviders_Clicked()
        {
            if (selectedProfile == null) return;

            var dialog = new DataProvidersEditorDialog(selectedProfile.DataProviders);
            Application.Run(dialog);

            if (dialog.WasModified)
            {
                selectedProfile.DataProviders.Clear();
                selectedProfile.DataProviders.AddRange(dialog.GetDataProviders());
                RefreshSummaryLabels();
                UpdateRawDataButtonVisibility();
                MarkDirty("");
            }
        }

        private void EditFilters_Clicked()
        {
            if (selectedProfile == null) return;

            var dialog = new FiltersEditorDialog(selectedProfile.Filters);
            Application.Run(dialog);

            if (dialog.WasModified)
            {
                RefreshSummaryLabels();
                MarkDirty("");
            }
        }

        private void EditRawData_Clicked()
        {
            if (selectedProfile == null) return;

            var dialog = new RawDataEditorDialog(selectedProfile.Filters);
            Application.Run(dialog);

            if (dialog.WasModified)
            {
                RefreshSummaryLabels();
                MarkDirty("");
            }
        }

        private void RefreshSummaryLabels()
        {
            if (selectedProfile == null) return;

            // Update data providers count
            var providerCount = selectedProfile.DataProviders?.Count ?? 0;
            dataProvidersCountLabel.Text = $"({providerCount} configured)";

            // Update filters count
            var projectCount = selectedProfile.Filters?.Projects?.Count ?? 0;
            var prefixCount = selectedProfile.Filters?.Prefixes?.Count ?? 0;
            filtersCountLabel.Text = $"(Projects: {projectCount}, Prefixes: {prefixCount})";

            // Update raw data button visibility
            UpdateRawDataButtonVisibility();
        }

        private void UpdateRawDataButtonVisibility()
        {
            if (selectedProfile?.DataProviders != null)
            {
                var hasRawDataProcessor = selectedProfile.DataProviders.Contains("RawDataProcessor");
                editRawDataButton.Visible = hasRawDataProcessor;
                
                if (hasRawDataProcessor)
                {
                    var rawDataCount = selectedProfile.Filters?.RawData?.Count ?? 0;
                    var rawDataLabel = editRawDataButton.SuperView?.Subviews?.OfType<Label>()
                        ?.FirstOrDefault(l => l.Text.ToString().StartsWith("Raw Data"));
                    if (rawDataLabel != null)
                    {
                        rawDataLabel.Text = $"Raw Data: ({rawDataCount} configured)";
                    }
                }
            }
            else
            {
                editRawDataButton.Visible = false;
            }
        }
    }
}