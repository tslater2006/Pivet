using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terminal.Gui;

namespace Pivet.GUI
{
    /// <summary>
    /// Modal dialog for editing Raw Data configuration entries
    /// </summary>
    internal class RawDataEditorDialog : Dialog
    {
        private readonly FilterConfig filters;
        private ListView rawDataList;
        private Button addButton;
        private Button editButton;
        private Button removeButton;
        private Button okButton;
        private Button cancelButton;

        internal bool WasModified { get; private set; } = false;

        internal RawDataEditorDialog(FilterConfig filterConfig) : base("Edit Raw Data Configuration")
        {
            filters = filterConfig ?? new FilterConfig();
            if (filters.RawData == null)
                filters.RawData = new List<RawDataEntry>();
            
            Width = 100;
            Height = 25;
            
            BuildUI();
            RefreshRawDataList();
        }

        private void BuildUI()
        {
            var listLabel = new Label("Raw Data Entries:")
            {
                X = 1,
                Y = 1,
                AutoSize = true
            };
            Add(listLabel);

            rawDataList = new ListView()
            {
                X = 1,
                Y = Pos.Bottom(listLabel) + 1,
                Width = Dim.Fill(2),
                Height = Dim.Fill(7)
            };
            Add(rawDataList);

            // Buttons
            addButton = new Button("Add")
            {
                X = 1,
                Y = Pos.Bottom(rawDataList) + 1,
                Width = 10
            };
            addButton.Clicked += AddButton_Clicked;
            Add(addButton);

            editButton = new Button("Edit")
            {
                X = Pos.Right(addButton) + 2,
                Y = Pos.Top(addButton),
                Width = 10
            };
            editButton.Clicked += EditButton_Clicked;
            Add(editButton);

            removeButton = new Button("Remove")
            {
                X = Pos.Right(editButton) + 2,
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
            okButton.Clicked += () => Application.RequestStop();
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
            var helpLabel = new Label("Raw Data entries configure specific database records to extract. Double-click to edit.")
            {
                X = 1,
                Y = Pos.AnchorEnd(4),
                Width = Dim.Fill(2),
                ColorScheme = Colors.Dialog
            };
            Add(helpLabel);

            // Double-click to edit
            rawDataList.OpenSelectedItem += (e) => EditButton_Clicked();
        }

        private void AddButton_Clicked()
        {
            var newEntry = new RawDataEntry() { Record = "<new record>" };
            var editor = new RawDataEntryEditorDialog(newEntry);
            Application.Run(editor);

            if (editor.WasModified)
            {
                filters.RawData.Add(newEntry);
                RefreshRawDataList();
                WasModified = true;
            }
        }

        private void EditButton_Clicked()
        {
            if (rawDataList.SelectedItem < 0 || rawDataList.SelectedItem >= filters.RawData.Count)
            {
                MessageBox.ErrorQuery("Edit Entry", "Please select an entry to edit.", "OK");
                return;
            }

            var entry = filters.RawData[rawDataList.SelectedItem];
            var editor = new RawDataEntryEditorDialog(entry);
            Application.Run(editor);

            if (editor.WasModified)
            {
                RefreshRawDataList();
                WasModified = true;
            }
        }

        private void RemoveButton_Clicked()
        {
            if (rawDataList.SelectedItem < 0 || rawDataList.SelectedItem >= filters.RawData.Count)
            {
                MessageBox.ErrorQuery("Remove Entry", "Please select an entry to remove.", "OK");
                return;
            }

            var entry = filters.RawData[rawDataList.SelectedItem];
            var result = MessageBox.Query("Remove Entry", 
                $"Are you sure you want to remove raw data entry '{entry.Record}'?", "Yes", "No");
                
            if (result == 0)
            {
                filters.RawData.RemoveAt(rawDataList.SelectedItem);
                RefreshRawDataList();
                WasModified = true;
            }
        }

        private void RefreshRawDataList()
        {
            var wrapper = new ListWrapper(filters.RawData);
            rawDataList.Source = wrapper;
            
            editButton.Enabled = filters.RawData.Count > 0;
            removeButton.Enabled = filters.RawData.Count > 0;
        }
    }

    /// <summary>
    /// Dialog for editing a single RawDataEntry
    /// </summary>
    internal class RawDataEntryEditorDialog : Dialog
    {
        private readonly RawDataEntry entry;
        private TextField recordField;
        private TextField filterFieldField;
        private TextField namePatternField;
        private TextField folderField;
        private CheckBox includeRelatedCheckBox;
        private TextView extraCriteriaField;
        private ListView blacklistList;
        private TextField newBlacklistField;

        internal bool WasModified { get; private set; } = false;

        internal RawDataEntryEditorDialog(RawDataEntry rawDataEntry) : base("Edit Raw Data Entry")
        {
            entry = rawDataEntry ?? throw new ArgumentNullException(nameof(rawDataEntry));
            
            Width = 90;
            Height = 30;
            
            BuildUI();
            PopulateFields();
        }

        private void BuildUI()
        {
            // Basic fields
            var recordLabel = new Label("Record Name:")
            {
                X = 1,
                Y = 1,
                AutoSize = true
            };
            Add(recordLabel);

            recordField = new TextField()
            {
                X = Pos.Right(recordLabel) + 1,
                Y = 1,
                Width = 30
            };
            Add(recordField);

            var filterFieldLabel = new Label("Filter Field:")
            {
                X = 1,
                Y = Pos.Bottom(recordField) + 1,
                AutoSize = true
            };
            Add(filterFieldLabel);

            filterFieldField = new TextField()
            {
                X = Pos.Right(filterFieldLabel) + 1,
                Y = Pos.Top(filterFieldLabel),
                Width = 30
            };
            Add(filterFieldField);

            var namePatternLabel = new Label("Name Pattern:")
            {
                X = 1,
                Y = Pos.Bottom(filterFieldField) + 1,
                AutoSize = true
            };
            Add(namePatternLabel);

            namePatternField = new TextField()
            {
                X = Pos.Right(namePatternLabel) + 1,
                Y = Pos.Top(namePatternLabel),
                Width = 30
            };
            Add(namePatternField);

            var folderLabel = new Label("Folder:")
            {
                X = 1,
                Y = Pos.Bottom(namePatternField) + 1,
                AutoSize = true
            };
            Add(folderLabel);

            folderField = new TextField()
            {
                X = Pos.Right(folderLabel) + 1,
                Y = Pos.Top(folderLabel),
                Width = 30
            };
            Add(folderField);

            var folderButton = new Button("...")
            {
                X = Pos.Right(folderField) + 1,
                Y = Pos.Top(folderField),
                Width = 5
            };
            folderButton.Clicked += FolderButton_Clicked;
            Add(folderButton);

            // Include Related checkbox
            includeRelatedCheckBox = new CheckBox("Include Related Records")
            {
                X = 1,
                Y = Pos.Bottom(folderField) + 1,
                AutoSize = true
            };
            Add(includeRelatedCheckBox);

            // Extra Criteria text area
            var extraCriteriaLabel = new Label("Extra Criteria (SQL WHERE clause):")
            {
                X = 1,
                Y = Pos.Bottom(includeRelatedCheckBox) + 1,
                AutoSize = true
            };
            Add(extraCriteriaLabel);

            extraCriteriaField = new TextView()
            {
                X = 1,
                Y = Pos.Bottom(extraCriteriaLabel) + 1,
                Width = Dim.Fill(2),
                Height = 4
            };
            Add(extraCriteriaField);

            // Related Blacklist
            var blacklistFrame = new FrameView("Related Blacklist")
            {
                X = 1,
                Y = Pos.Bottom(extraCriteriaField) + 1,
                Width = Dim.Fill(2),
                Height = 8
            };

            blacklistList = new ListView()
            {
                X = 1,
                Y = 1,
                Width = Dim.Fill(20),
                Height = Dim.Fill(3)
            };
            blacklistFrame.Add(blacklistList);

            newBlacklistField = new TextField()
            {
                X = 1,
                Y = Pos.Bottom(blacklistList) + 1,
                Width = Dim.Fill(30)
            };
            blacklistFrame.Add(newBlacklistField);

            var addBlacklistButton = new Button("Add")
            {
                X = Pos.Right(newBlacklistField) + 1,
                Y = Pos.Top(newBlacklistField),
                Width = 8
            };
            addBlacklistButton.Clicked += AddBlacklist_Clicked;
            blacklistFrame.Add(addBlacklistButton);

            var removeBlacklistButton = new Button("Remove")
            {
                X = Pos.Right(addBlacklistButton) + 1,
                Y = Pos.Top(addBlacklistButton),
                Width = 10
            };
            removeBlacklistButton.Clicked += RemoveBlacklist_Clicked;
            blacklistFrame.Add(removeBlacklistButton);

            Add(blacklistFrame);

            // OK/Cancel buttons
            var okButton = new Button("OK")
            {
                X = Pos.AnchorEnd(20),
                Y = Pos.AnchorEnd(2),
                Width = 8,
                IsDefault = true
            };
            okButton.Clicked += OkButton_Clicked;
            Add(okButton);

            var cancelButton = new Button("Cancel")
            {
                X = Pos.AnchorEnd(10),
                Y = Pos.AnchorEnd(2),
                Width = 8
            };
            cancelButton.Clicked += () => Application.RequestStop();
            Add(cancelButton);

            // Track changes
            recordField.TextChanged += (e) => WasModified = true;
            filterFieldField.TextChanged += (e) => WasModified = true;
            namePatternField.TextChanged += (e) => WasModified = true;
            folderField.TextChanged += (e) => WasModified = true;
            includeRelatedCheckBox.Toggled += (e) => WasModified = true;
            extraCriteriaField.TextChanged += () => WasModified = true;
        }

        private void PopulateFields()
        {
            recordField.Text = entry.Record ?? "";
            filterFieldField.Text = entry.FilterField ?? "";
            namePatternField.Text = entry.NamePattern ?? "";
            folderField.Text = entry.Folder ?? "";
            includeRelatedCheckBox.Checked = entry.IncludeRelated;
            extraCriteriaField.Text = entry.ExtraCriteria ?? "";

            if (entry.RelatedBlacklist == null)
                entry.RelatedBlacklist = new List<string>();

            RefreshBlacklistList();
        }

        private void FolderButton_Clicked()
        {
            var openDialog = new OpenDialog("Select Folder", "Choose output folder", null, OpenDialog.OpenMode.Directory);
            Application.Run(openDialog);
            
            if (!string.IsNullOrEmpty(openDialog.DirectoryPath?.ToString()))
            {
                folderField.Text = openDialog.DirectoryPath.ToString();
                WasModified = true;
            }
        }

        private void AddBlacklist_Clicked()
        {
            var blacklistItem = newBlacklistField.Text.ToString().Trim();
            if (string.IsNullOrWhiteSpace(blacklistItem))
                return;

            if (!entry.RelatedBlacklist.Contains(blacklistItem))
            {
                entry.RelatedBlacklist.Add(blacklistItem);
                newBlacklistField.Text = "";
                RefreshBlacklistList();
                WasModified = true;
            }
        }

        private void RemoveBlacklist_Clicked()
        {
            if (blacklistList.SelectedItem >= 0 && blacklistList.SelectedItem < entry.RelatedBlacklist.Count)
            {
                entry.RelatedBlacklist.RemoveAt(blacklistList.SelectedItem);
                RefreshBlacklistList();
                WasModified = true;
            }
        }

        private void RefreshBlacklistList()
        {
            var wrapper = new ListWrapper(entry.RelatedBlacklist);
            blacklistList.Source = wrapper;
        }

        private void OkButton_Clicked()
        {
            // Validate required fields
            if (string.IsNullOrWhiteSpace(recordField.Text.ToString()))
            {
                MessageBox.ErrorQuery("Validation Error", "Record name is required.", "OK");
                return;
            }

            // Save all fields back to entry
            entry.Record = recordField.Text.ToString();
            entry.FilterField = filterFieldField.Text.ToString();
            entry.NamePattern = namePatternField.Text.ToString();
            entry.Folder = folderField.Text.ToString();
            entry.IncludeRelated = includeRelatedCheckBox.Checked;
            entry.ExtraCriteria = extraCriteriaField.Text.ToString();

            Application.RequestStop();
        }
    }
}