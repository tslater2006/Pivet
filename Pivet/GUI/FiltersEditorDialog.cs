using System;
using System.Collections.Generic;
using System.Linq;
using Terminal.Gui;

namespace Pivet.GUI
{
    /// <summary>
    /// Modal dialog for editing basic filter configuration (Projects and Prefixes)
    /// </summary>
    internal class FiltersEditorDialog : Dialog
    {
        private readonly FilterConfig filters;
        private ListView projectsList;
        private ListView prefixesList;
        private TextField newProjectField;
        private TextField newPrefixField;
        private Button addProjectButton;
        private Button removeProjectButton;
        private Button addPrefixButton;
        private Button removePrefixButton;
        private Button okButton;
        private Button cancelButton;

        internal bool WasModified { get; private set; } = false;

        internal FiltersEditorDialog(FilterConfig filterConfig) : base("Edit Filters")
        {
            // Work with the actual filter config - changes are applied immediately
            // but we track if anything was modified
            filters = filterConfig ?? new FilterConfig();
            
            Width = 90;
            Height = 30;
            
            BuildUI();
            RefreshLists();
        }

        private void BuildUI()
        {
            // Projects section
            var projectsFrame = new FrameView("Projects")
            {
                X = 1,
                Y = 1,
                Width = Dim.Percent(50) - 1,
                Height = Dim.Fill(4)
            };

            var projectsLabel = new Label("Filter by project names:")
            {
                X = 1,
                Y = 1,
                AutoSize = true
            };
            projectsFrame.Add(projectsLabel);

            projectsList = new ListView()
            {
                X = 1,
                Y = Pos.Bottom(projectsLabel) + 1,
                Width = Dim.Fill(1),
                Height = Dim.Fill(5)
            };
            projectsFrame.Add(projectsList);

            newProjectField = new TextField()
            {
                X = 1,
                Y = Pos.Bottom(projectsList) + 1,
                Width = Dim.Fill(20)
            };
            projectsFrame.Add(newProjectField);

            addProjectButton = new Button("Add")
            {
                X = Pos.Right(newProjectField) + 1,
                Y = Pos.Top(newProjectField),
                Width = 8
            };
            addProjectButton.Clicked += AddProject_Clicked;
            projectsFrame.Add(addProjectButton);

            removeProjectButton = new Button("Remove")
            {
                X = 1,
                Y = Pos.Bottom(newProjectField) + 1,
                Width = 10
            };
            removeProjectButton.Clicked += RemoveProject_Clicked;
            projectsFrame.Add(removeProjectButton);

            Add(projectsFrame);

            // Prefixes section
            var prefixesFrame = new FrameView("Prefixes")
            {
                X = Pos.Right(projectsFrame) + 1,
                Y = 1,
                Width = Dim.Fill(1),
                Height = Dim.Fill(4)
            };

            var prefixesLabel = new Label("Filter by name prefixes:")
            {
                X = 1,
                Y = 1,
                AutoSize = true
            };
            prefixesFrame.Add(prefixesLabel);

            prefixesList = new ListView()
            {
                X = 1,
                Y = Pos.Bottom(prefixesLabel) + 1,
                Width = Dim.Fill(1),
                Height = Dim.Fill(5)
            };
            prefixesFrame.Add(prefixesList);

            newPrefixField = new TextField()
            {
                X = 1,
                Y = Pos.Bottom(prefixesList) + 1,
                Width = Dim.Fill(20)
            };
            prefixesFrame.Add(newPrefixField);

            addPrefixButton = new Button("Add")
            {
                X = Pos.Right(newPrefixField) + 1,
                Y = Pos.Top(newPrefixField),
                Width = 8
            };
            addPrefixButton.Clicked += AddPrefix_Clicked;
            prefixesFrame.Add(addPrefixButton);

            removePrefixButton = new Button("Remove")
            {
                X = 1,
                Y = Pos.Bottom(newPrefixField) + 1,
                Width = 10
            };
            removePrefixButton.Clicked += RemovePrefix_Clicked;
            prefixesFrame.Add(removePrefixButton);

            Add(prefixesFrame);

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
            var helpLabel = new Label("Projects and Prefixes are used to filter which objects are processed. Leave empty to process all.")
            {
                X = 1,
                Y = Pos.AnchorEnd(4),
                Width = Dim.Fill(2),
                ColorScheme = Colors.Dialog
            };
            Add(helpLabel);

            // Enter key handlers for text fields
            newProjectField.KeyUp += (e) => {
                if (e.KeyEvent.Key == Key.Enter)
                    AddProject_Clicked();
            };

            newPrefixField.KeyUp += (e) => {
                if (e.KeyEvent.Key == Key.Enter)
                    AddPrefix_Clicked();
            };
        }

        private void AddProject_Clicked()
        {
            var projectName = newProjectField.Text.ToString().Trim();
            if (string.IsNullOrWhiteSpace(projectName))
            {
                MessageBox.ErrorQuery("Add Project", "Please enter a project name.", "OK");
                return;
            }

            if (filters.Projects.Contains(projectName))
            {
                MessageBox.ErrorQuery("Add Project", $"Project '{projectName}' is already in the list.", "OK");
                return;
            }

            filters.Projects.Add(projectName);
            newProjectField.Text = "";
            RefreshLists();
            WasModified = true;
        }

        private void RemoveProject_Clicked()
        {
            if (projectsList.SelectedItem < 0 || projectsList.SelectedItem >= filters.Projects.Count)
            {
                MessageBox.ErrorQuery("Remove Project", "Please select a project to remove.", "OK");
                return;
            }

            var projectName = filters.Projects[projectsList.SelectedItem];
            var result = MessageBox.Query("Remove Project", 
                $"Are you sure you want to remove project '{projectName}'?", "Yes", "No");
                
            if (result == 0)
            {
                filters.Projects.RemoveAt(projectsList.SelectedItem);
                RefreshLists();
                WasModified = true;
            }
        }

        private void AddPrefix_Clicked()
        {
            var prefixName = newPrefixField.Text.ToString().Trim();
            if (string.IsNullOrWhiteSpace(prefixName))
            {
                MessageBox.ErrorQuery("Add Prefix", "Please enter a prefix.", "OK");
                return;
            }

            if (filters.Prefixes.Contains(prefixName))
            {
                MessageBox.ErrorQuery("Add Prefix", $"Prefix '{prefixName}' is already in the list.", "OK");
                return;
            }

            filters.Prefixes.Add(prefixName);
            newPrefixField.Text = "";
            RefreshLists();
            WasModified = true;
        }

        private void RemovePrefix_Clicked()
        {
            if (prefixesList.SelectedItem < 0 || prefixesList.SelectedItem >= filters.Prefixes.Count)
            {
                MessageBox.ErrorQuery("Remove Prefix", "Please select a prefix to remove.", "OK");
                return;
            }

            var prefixName = filters.Prefixes[prefixesList.SelectedItem];
            var result = MessageBox.Query("Remove Prefix", 
                $"Are you sure you want to remove prefix '{prefixName}'?", "Yes", "No");
                
            if (result == 0)
            {
                filters.Prefixes.RemoveAt(prefixesList.SelectedItem);
                RefreshLists();
                WasModified = true;
            }
        }

        private void RefreshLists()
        {
            // Refresh projects list
            var projectsWrapper = new ListWrapper(filters.Projects);
            projectsList.Source = projectsWrapper;
            removeProjectButton.Enabled = filters.Projects.Count > 0;

            // Refresh prefixes list  
            var prefixesWrapper = new ListWrapper(filters.Prefixes);
            prefixesList.Source = prefixesWrapper;
            removePrefixButton.Enabled = filters.Prefixes.Count > 0;
        }
    }
}