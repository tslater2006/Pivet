using System;
using System.Collections.Generic;
using System.Linq;
using Terminal.Gui;

namespace Pivet.GUI
{
    /// <summary>
    /// Modal dialog for editing the data providers list of a profile
    /// </summary>
    internal class DataProvidersEditorDialog : Dialog
    {
        private readonly List<string> dataProviders;
        private ListView dataProvidersList;
        private ComboBox processorDropdown;
        private Label processorInfoLabel;
        private Button addButton;
        private Button removeButton;
        private Button okButton;
        private Button cancelButton;

        internal bool WasModified { get; private set; } = false;

        internal DataProvidersEditorDialog(List<string> providers) : base("Edit Data Providers")
        {
            // Create a working copy to avoid modifying original until OK is pressed
            dataProviders = new List<string>(providers ?? new List<string>());
            
            Width = 80;
            Height = 25;
            
            BuildUI();
            RefreshProvidersList();
        }

        private void BuildUI()
        {
            // Current providers list
            var providersLabel = new Label("Current Data Providers:")
            {
                X = 1,
                Y = 1,
                AutoSize = true
            };
            Add(providersLabel);

            dataProvidersList = new ListView()
            {
                X = 1,
                Y = Pos.Bottom(providersLabel) + 1,
                Width = Dim.Fill(2),
                Height = 8
            };
            Add(dataProvidersList);

            // Add new provider section
            var addLabel = new Label("Add Data Provider:")
            {
                X = 1,
                Y = Pos.Bottom(dataProvidersList) + 1,
                AutoSize = true
            };
            Add(addLabel);

            var processorLabel = new Label("Available Processors:")
            {
                X = 1,
                Y = Pos.Bottom(addLabel) + 1,
                AutoSize = true
            };
            Add(processorLabel);

            processorDropdown = new ComboBox()
            {
                X = 1,
                Y = Pos.Bottom(processorLabel) + 1,
                Width = 40,
                Height = 6,
                ColorScheme = Colors.Dialog
            };

            // Populate processor dropdown
            var availableProcessors = DataProcessorService.GetAvailableProcessors();
            processorDropdown.SetSource(availableProcessors);
            processorDropdown.SelectedItemChanged += ProcessorDropdown_SelectedItemChanged;
            Add(processorDropdown);

            // Processor info display
            processorInfoLabel = new Label("Select a processor to see details")
            {
                X = Pos.Right(processorDropdown) + 2,
                Y = Pos.Top(processorDropdown),
                Width = Dim.Fill(2),
                Height = 3,
                ColorScheme = Colors.Dialog
            };
            Add(processorInfoLabel);

            // Add/Remove buttons
            addButton = new Button("Add")
            {
                X = 1,
                Y = Pos.Bottom(processorDropdown) + 1,
                Width = 10
            };
            addButton.Clicked += AddButton_Clicked;
            Add(addButton);

            removeButton = new Button("Remove")
            {
                X = Pos.Right(addButton) + 2,
                Y = Pos.Top(addButton),
                Width = 10
            };
            removeButton.Clicked += RemoveButton_Clicked;
            Add(removeButton);

            // OK/Cancel buttons
            okButton = new Button("OK")
            {
                X = Pos.AnchorEnd(20),
                Y = Pos.AnchorEnd(2),
                Width = 8,
                IsDefault = true
            };
            okButton.Clicked += OkButton_Clicked;
            Add(okButton);

            cancelButton = new Button("Cancel")
            {
                X = Pos.AnchorEnd(10),
                Y = Pos.AnchorEnd(2),
                Width = 8
            };
            cancelButton.Clicked += () => Application.RequestStop();
            Add(cancelButton);

            // Help text
            var helpLabel = new Label("Use dropdown to select processors or type custom names. Press Tab to navigate.")
            {
                X = 1,
                Y = Pos.AnchorEnd(4),
                Width = Dim.Fill(2),
                ColorScheme = Colors.Dialog
            };
            Add(helpLabel);
        }

        private void ProcessorDropdown_SelectedItemChanged(ListViewItemEventArgs obj)
        {
            if (obj.Item >= 0)
            {
                var availableProcessors = DataProcessorService.GetAvailableProcessors();
                if (obj.Item < availableProcessors.Count)
                {
                    var processorName = availableProcessors[obj.Item];
                    var processorInfo = DataProcessorService.GetProcessorInfo(processorName);
                    
                    var infoText = processorInfo.IsAvailable 
                        ? $"Name: {processorInfo.Name}\nType: {processorInfo.ItemName}\nStatus: Available"
                        : $"Name: {processorInfo.Name}\nStatus: Not Available";
                        
                    processorInfoLabel.Text = infoText;
                }
            }
            else
            {
                processorInfoLabel.Text = "Select a processor to see details";
            }
        }

        private void AddButton_Clicked()
        {
            string processorName = null;

            // Get selected processor from dropdown
            if (processorDropdown.SelectedItem >= 0)
            {
                var availableProcessors = DataProcessorService.GetAvailableProcessors();
                if (processorDropdown.SelectedItem < availableProcessors.Count)
                {
                    processorName = availableProcessors[processorDropdown.SelectedItem];
                }
            }

            if (string.IsNullOrWhiteSpace(processorName))
            {
                MessageBox.ErrorQuery("Add Processor", "Please select a processor from the dropdown.", "OK");
                return;
            }

            if (dataProviders.Contains(processorName))
            {
                MessageBox.ErrorQuery("Add Processor", $"Processor '{processorName}' is already in the list.", "OK");
                return;
            }

            // Add processor and refresh
            dataProviders.Add(processorName);
            RefreshProvidersList();
            processorDropdown.SelectedItem = -1;
            WasModified = true;
        }

        private void RemoveButton_Clicked()
        {
            if (dataProvidersList.SelectedItem < 0 || dataProvidersList.SelectedItem >= dataProviders.Count)
            {
                MessageBox.ErrorQuery("Remove Processor", "Please select a processor to remove.", "OK");
                return;
            }

            var processorName = dataProviders[dataProvidersList.SelectedItem];
            var result = MessageBox.Query("Remove Processor", 
                $"Are you sure you want to remove '{processorName}'?", "Yes", "No");
                
            if (result == 0)
            {
                dataProviders.RemoveAt(dataProvidersList.SelectedItem);
                RefreshProvidersList();
                WasModified = true;
            }
        }

        private void OkButton_Clicked()
        {
            // Validate that we have at least one data provider
            if (dataProviders.Count == 0)
            {
                var result = MessageBox.Query("No Data Providers", 
                    "No data providers are configured. This profile will not process any data. Continue?", 
                    "Yes", "No");
                if (result != 0) return;
            }

            Application.RequestStop();
        }

        private void RefreshProvidersList()
        {
            var wrapper = new ListWrapper(dataProviders);
            dataProvidersList.Source = wrapper;
            
            // Update remove button state
            removeButton.Enabled = dataProviders.Count > 0;
        }

        /// <summary>
        /// Gets the modified list of data providers
        /// </summary>
        internal List<string> GetDataProviders()
        {
            return new List<string>(dataProviders);
        }
    }
}