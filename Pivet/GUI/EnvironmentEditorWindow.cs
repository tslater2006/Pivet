using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terminal.Gui;

namespace Pivet.GUI
{
    internal class EnvironmentEditorWindow : Window
    {
        private ListView environmentList;
        private Config current_config;
        private EnvironmentConfig selectedEnvironment;
        private TextField environmentName;
        private Label connectionType;
        private TextField tnsName;
        private TextField tnsAdminPath;
        private TextField schema;
        private TextField userID;
        private TextField password;

        private bool dirtyFlag = false;
        private Button saveButton;

        internal void MarkDirty(NStack.ustring newValue)
        {
            saveButton.Visible = true;
            saveButton.SetNeedsDisplay();
            dirtyFlag = true;
        }

        internal void SaveEnvironmentData()
        {
            selectedEnvironment.Name = environmentName.Text.ToString();
            selectedEnvironment.Connection.TNS = tnsName.Text.ToString();
            selectedEnvironment.Connection.TNS_ADMIN = tnsAdminPath.Text.ToString();
            selectedEnvironment.Connection.Schema = schema.Text.ToString();
            selectedEnvironment.Connection.BootstrapParameters.User = userID.Text.ToString();
            selectedEnvironment.Connection.BootstrapParameters.EncryptedPassword = password.Text.ToString();
            
            // Validate the environment before saving
            var validationResult = ConfigValidator.ValidateEnvironment(selectedEnvironment);
            if (!validationResult.IsValid)
            {
                var errorMessages = string.Join("\n", validationResult.Errors.Select(e => e.Message));
                MessageBox.ErrorQuery("Validation Errors", $"Cannot save environment:\n{errorMessages}", "OK");
                return;
            }
            
            if (validationResult.HasWarnings)
            {
                var warningMessages = string.Join("\n", validationResult.Warnings.Select(w => w.Message));
                var result = MessageBox.Query("Validation Warnings", $"Environment has warnings:\n{warningMessages}\n\nSave anyway?", "Yes", "No");
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
                var result = MessageBox.Query("Save Changes?", "Would you like to save changes to the current environment?", "Yes", "No");
                if (result == 0)
                {
                    SaveEnvironmentData();
                }
            }
        }

        internal void SelectEnvironment(int index)
        {
            PromptForSaveIfNeeded();
            
            bool createdDefaultEnvironment = false;

            if (current_config.Environments.Count == 0)
            {
                /* need to make an initial Environment */
                current_config.Environments.Add(new EnvironmentConfig() { Name = "<unnamed>" });
                createdDefaultEnvironment = true;
            }

            selectedEnvironment = current_config.Environments[index];

            if (environmentName == null)
            {
                /* UI hasn't been built yet */
                return;
            }

            environmentName.Text = selectedEnvironment.Name;
            tnsName.Text = selectedEnvironment.Connection.TNS;
            tnsAdminPath.Text = selectedEnvironment.Connection.TNS_ADMIN;
            schema.Text = selectedEnvironment.Connection.Schema;
            userID.Text = selectedEnvironment.Connection.BootstrapParameters.User;
            password.Text = selectedEnvironment.Connection.BootstrapParameters.EncryptedPassword;
            dirtyFlag = createdDefaultEnvironment;
        }

        internal EnvironmentEditorWindow(Config config) : base("Environments")
        {
            current_config = config;
            var listFrame = new FrameView()
            {
                X = 1,
                Y = 1,
                Height = Dim.Fill(2),
                Width = Dim.Percent(25)
            };

            environmentList = new ListView()
            {
                X = 0,
                Y = 0,
                Height = Dim.Fill(),
                Width = Dim.Fill()
            };
            var wrapper = new ListWrapper(current_config.Environments);
            environmentList.Source = wrapper;

            environmentList.SelectedItemChanged += EnvironmentList_SelectedItemChanged;

            listFrame.Add(environmentList);
            Add(listFrame);

            var createEnvButton = new Button("Create Environment")
            {
                X = 1,
                Y = Pos.Bottom(listFrame),
                Height = 1,
                Width = Dim.Percent(25),
                TextAlignment = TextAlignment.Centered
            };

            createEnvButton.Clicked += CreateEnvButton_Clicked;

            Add(createEnvButton);

            var escapeMessage = new Label("Press [ESC] to close.")
            {
                X = Pos.Right(createEnvButton) - 2,
                Y = Pos.Top(createEnvButton),
                Height = 1,
                Width = Dim.Percent(75),
                TextAlignment = TextAlignment.Right
            };
            Add(escapeMessage);

            var _scrollBar = new ScrollBarView(environmentList, true);

            _scrollBar.ChangedPosition += () =>
            {
                environmentList.TopItem = _scrollBar.Position;
                if (environmentList.TopItem != _scrollBar.Position)
                {
                    _scrollBar.Position = environmentList.TopItem;
                }
                environmentList.SetNeedsDisplay();
            };

            _scrollBar.OtherScrollBarView.ChangedPosition += () =>
            {
                environmentList.LeftItem = _scrollBar.OtherScrollBarView.Position;
                if (environmentList.LeftItem != _scrollBar.OtherScrollBarView.Position)
                {
                    _scrollBar.OtherScrollBarView.Position = environmentList.LeftItem;
                }
                environmentList.SetNeedsDisplay();
            };

            environmentList.DrawContent += (e) =>
            {
                _scrollBar.Size = environmentList.Source.Count - 1;
                _scrollBar.Position = environmentList.TopItem;
                _scrollBar.OtherScrollBarView.Size = environmentList.Maxlength - 1;
                _scrollBar.OtherScrollBarView.Position = environmentList.LeftItem;
                _scrollBar.Refresh();
            };

            /* Setup Right pane which has the details for an environment */
            /* Name */
            /* Connection: */
            /*   Connection Provider
             *   TNS
             *   TNS_ADMIN
             *   Schema
             *   Bootstrap Params:
             *      User
             *      Password */

            var envDetailsFrame = new FrameView("Details")
            {
                X = Pos.Right(listFrame),
                Y = 1,
                Width = Dim.Fill(2),
                Height = Dim.Fill(2)
            };

            var label = new Label("Environment Name: ")
            {
                X = 1,
                Y = 1,
                Height = 1,
                Width = 20
            };

            envDetailsFrame.Add();
            environmentName = new TextField()
            {
                X = Pos.Right(label),
                Y = 1,
                Width = 40
            };
            envDetailsFrame.Add(label);
            envDetailsFrame.Add(environmentName);

            /* Connection provider */
            label = new Label("Connection Type: ")
            {
                X = 1,
                Y = Pos.Bottom(environmentName) + 1,
                Height = 1,
                Width = 20
            };

            connectionType = new Label("Bootstrap")
            {
                Height = 1,
                Width = 40,
                X = Pos.Right(label),
                Y = Pos.Top(label)
            };
            envDetailsFrame.Add(label);
            envDetailsFrame.Add(connectionType);


            /* TNS Name */
            label = new Label("TNS Name: ")
            {
                X = 1,
                Y = Pos.Bottom(connectionType) + 1,
                Height = 1,
                Width = 11
            };

            tnsName = new TextField()
            {
                Height = 1,
                Width = 20,
                X = Pos.Right(label),
                Y = Pos.Top(label)
            };
            envDetailsFrame.Add(label);
            envDetailsFrame.Add(tnsName);

            /* DB Schema */
            label = new Label("DB Schema: ")
            {
                X = Pos.Right(tnsName) + 2,
                Y = Pos.Bottom(connectionType) + 1,
                Height = 1,
                Width = 12
            };

            schema = new TextField()
            {
                Height = 1,
                Width = 20,
                X = Pos.Right(label),
                Y = Pos.Top(label)
            };
            envDetailsFrame.Add(label);
            envDetailsFrame.Add(schema);

            Add(envDetailsFrame);

            /* TNS Admin Path */
            label = new Label("TNS Path: ")
            {
                X = 1,
                Y = Pos.Bottom(tnsName) + 1,
                Height = 1,
                Width = 11
            };

            tnsAdminPath = new TextField()
            {
                Height = 1,
                Width = 40,
                X = Pos.Right(label),
                Y = Pos.Top(label)
            };

            var button = new Button("...")
            {
                X = Pos.Right(tnsAdminPath) + 1,
                Y = Pos.Top(tnsAdminPath),
                Height = 1,
                Width = 7,

            };

            button.Clicked += adminPathPromptClick;


            envDetailsFrame.Add(label);
            envDetailsFrame.Add(tnsAdminPath);
            envDetailsFrame.Add(button);

            label = new Label("DB User: ")
            {
                X = 1,
                Y = Pos.Bottom(tnsAdminPath) + 1,
                Height = 1,
                Width = 11
            };

            userID = new TextField()
            {
                X = Pos.Right(label),
                Y = Pos.Top(label),
                Height = 1,
                Width = 20
            };

            envDetailsFrame.Add(label, userID);

            label = new Label("DB Password: ")
            {
                X = Pos.Right(userID) + 1,
                Y = Pos.Top(userID),
                Height = 1,
                Width = 14
            };

            password = new TextField()
            {
                X = Pos.Right(label),
                Y = Pos.Top(label),
                Height = 1,
                Width = 20
            };

            envDetailsFrame.Add(label, password);

            button = new Button("Encrypt")
            {
                X = Pos.Right(password) + 1,
                Y = Pos.Top(password),
                Height = 1,
                Width = 11
            };

            button.Clicked += encryptButtonClick;

            envDetailsFrame.Add(button);


            saveButton = new Button("Save Changes")
            {
                X = 1,
                Y = Pos.Bottom(userID) + 2,
                AutoSize = true,
                Visible = false
            };

            saveButton.Clicked += () =>
            {
                SaveEnvironmentData();
                environmentName.SetFocus();
                saveButton.SetNeedsDisplay();
            };

            envDetailsFrame.Add(saveButton);

            environmentList.SelectedItem = 0;

            if (!dirtyFlag)
            {
                SelectEnvironment(0);
            }

            environmentName.TextChanged += MarkDirty;
            tnsName.TextChanged += MarkDirty;
            tnsAdminPath.TextChanged += MarkDirty;
            schema.TextChanged += MarkDirty;
            userID.TextChanged += MarkDirty;
            password.TextChanged += MarkDirty;


            /* keyboard shortcuts */
            KeyUp += (e) => {
                switch (e.KeyEvent.Key)
                {
                    case Key.Esc:
                        RequestStop();
                        break;
                    case Key.CtrlMask | Key.S:
                        if (saveButton.Visible)
                            SaveEnvironmentData();
                        break;
                    case Key.CtrlMask | Key.N:
                        CreateEnvButton_Clicked();
                        break;
                }
            };

            Closing += EnvironmentEditorWindow_Closing;
        }

        private void EnvironmentEditorWindow_Closing(ToplevelClosingEventArgs obj)
        {
            PromptForSaveIfNeeded();
        }

        private void EnvironmentList_SelectedItemChanged(ListViewItemEventArgs obj)
        {
            SelectEnvironment(obj.Item);
        }

        private void encryptButtonClick()
        {
            var encrypt = new EncryptDialog();
            Application.Run(encrypt);
            password.Text = encrypt.Result;
        }

        private void adminPathPromptClick()
        {
            var od = new OpenDialog("TNS Admin Folder", "Select TNS Admin folder",null,OpenDialog.OpenMode.Directory);
            Application.Run(od);
            if (od.DirectoryPath.Length > 0)
            {
                tnsAdminPath.Text = od.DirectoryPath.ToString() + Path.DirectorySeparatorChar + od.FilePath.ToString();
            }
        }

        private void CreateEnvButton_Clicked()
        {
            PromptForSaveIfNeeded();
            var new_env = new EnvironmentConfig() { Name = "<unnamed>" };
            current_config.Environments.Add(new_env);
            environmentList.SetFocus();
        }
    }
}
