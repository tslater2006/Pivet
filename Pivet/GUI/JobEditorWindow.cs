using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terminal.Gui;

namespace Pivet.GUI
{
    internal class JobEditorWindow : Window
    {
        private ListView jobList;
        private Config current_config;
        private JobConfig selectedJob;

        private bool dirtyFlag = false;
        private TextField jobName;
        private TextField outputFolder;
        private ComboBox envChoice;
        private ComboBox profileChoice;
        private Button saveButton;
        private TextField userID;
        private TextField remoteURL;
        private ComboBox commitStyle;

        internal void MarkDirty(NStack.ustring newValue)
        {
            saveButton.Visible = true;
            saveButton.SetNeedsDisplay();
            dirtyFlag = true;
        }

        internal void SaveJobData()
        {
            /* selectedEnvironment.Name = environmentName.Text.ToString();
            selectedEnvironment.Connection.TNS = tnsName.Text.ToString();
            selectedEnvironment.Connection.TNS_ADMIN = tnsAdminPath.Text.ToString();
            selectedEnvironment.Connection.Schema = schema.Text.ToString();
            selectedEnvironment.Connection.BootstrapParameters.User = userID.Text.ToString();
            selectedEnvironment.Connection.BootstrapParameters.EncryptedPassword = password.Text.ToString();*/
            saveButton.Visible = false;
            saveButton.SetNeedsDisplay();
            dirtyFlag = false;
        }

        internal void PromptForSaveIfNeeded()
        {
            if (dirtyFlag)
            {
                var result = MessageBox.Query("Save Changes?", "Would you like to save changes to the current job?", "Yes", "No");
                if (result == 0)
                {
                    SaveJobData();
                }
            }
        }

        internal void SelectJob(int index)
        {
            PromptForSaveIfNeeded();

            bool createdDefaultJob = false;

            if (current_config.Jobs.Count == 0)
            {
                /* need to make an initial Environment */
                current_config.Jobs.Add(new JobConfig() { Name = "<unnamed>" });
                createdDefaultJob = true;
            }

            selectedJob = current_config.Jobs[index];


            /*environmentName.Text = selectedEnvironment.Name;
            tnsName.Text = selectedEnvironment.Connection.TNS;
            tnsAdminPath.Text = selectedEnvironment.Connection.TNS_ADMIN;
            schema.Text = selectedEnvironment.Connection.Schema;
            userID.Text = selectedEnvironment.Connection.BootstrapParameters.User;
            password.Text = selectedEnvironment.Connection.BootstrapParameters.EncryptedPassword;*/

            dirtyFlag = createdDefaultJob;
        }

        internal JobEditorWindow(Config config) : base("Jobs")
        {
            current_config = config;
            var listFrame = new FrameView()
            {
                X = 1,
                Y = 1,
                Height = Dim.Fill(2),
                Width = Dim.Percent(25)
            };

            jobList = new ListView()
            {
                X = 0,
                Y = 0,
                Height = Dim.Fill(),
                Width = Dim.Fill()
            };
            var wrapper = new ListWrapper(current_config.Jobs);
            jobList.Source = wrapper;

            jobList.SelectedItemChanged += JobList_SelectedItemChanged;

            listFrame.Add(jobList);
            Add(listFrame);

            var createJobButton = new Button("Create Job")
            {
                X = 1,
                Y = Pos.Bottom(listFrame),
                Height = 1,
                Width = Dim.Percent(25),
                TextAlignment = TextAlignment.Centered
            };

            createJobButton.Clicked += CreateJobButton_Clicked;

            Add(createJobButton);

            var escapeMessage = new Label("Press [ESC] to close.")
            {
                X = Pos.Right(createJobButton) - 2,
                Y = Pos.Top(createJobButton),
                Height = 1,
                Width = Dim.Percent(75),
                TextAlignment = TextAlignment.Right
            };
            Add(escapeMessage);

            var _scrollBar = new ScrollBarView(jobList, true);

            _scrollBar.ChangedPosition += () =>
            {
                jobList.TopItem = _scrollBar.Position;
                if (jobList.TopItem != _scrollBar.Position)
                {
                    _scrollBar.Position = jobList.TopItem;
                }
                jobList.SetNeedsDisplay();
            };

            _scrollBar.OtherScrollBarView.ChangedPosition += () =>
            {
                jobList.LeftItem = _scrollBar.OtherScrollBarView.Position;
                if (jobList.LeftItem != _scrollBar.OtherScrollBarView.Position)
                {
                    _scrollBar.OtherScrollBarView.Position = jobList.LeftItem;
                }
                jobList.SetNeedsDisplay();
            };

            jobList.DrawContent += (e) =>
            {
                _scrollBar.Size = jobList.Source.Count - 1;
                _scrollBar.Position = jobList.TopItem;
                _scrollBar.OtherScrollBarView.Size = jobList.Maxlength - 1;
                _scrollBar.OtherScrollBarView.Position = jobList.LeftItem;
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

            var jobDetailsFrame = new FrameView("Details")
            {
                X = Pos.Right(listFrame),
                Y = 1,
                Width = Dim.Fill(2),
                Height = Dim.Fill(2)
            };

            Add(jobDetailsFrame);

            var label = new Label("Job Name: ")
            {
                X = 1,
                Y = 1,
                Height = 1,
                Width = 16
            };


            jobName = new TextField()
            {
                X = Pos.Right(label),
                Y = 1,
                Width = 40
            };
            jobDetailsFrame.Add(label);
            jobDetailsFrame.Add(jobName);

            label = new Label("Output Folder: ")
            {
                X = 1,
                Y = Pos.Bottom(jobName) + 1,
                Height = 1,
                Width = 16
            };

            outputFolder = new TextField()
            {
                Height = 1,
                Width = 40,
                X = Pos.Right(label),
                Y = Pos.Top(label)
            };

            var button = new Button("...")
            {
                X = Pos.Right(outputFolder) + 1,
                Y = Pos.Top(outputFolder),
                Height = 1,
                Width = 7,

            };

            jobDetailsFrame.Add(label, outputFolder, button);
            button.Clicked += outputFolderButton_Clicked; ;

            label = new Label("Environment: ")
            {
                X = 1,
                Y = Pos.Bottom(outputFolder) + 1,
                Height = 1,
                Width = 16
            };

            envChoice = new ComboBox("")
            {
                Height = 7,
                Width = 20,
                X = Pos.Right(label),
                Y = Pos.Top(label),
                ColorScheme = Colors.Dialog
            };
            
            envChoice.SetSource(current_config.Environments);

            jobDetailsFrame.Add(label, envChoice);

            label = new Label("Profile: ")
            {
                X = 1,
                Y = Pos.Top(envChoice) + 2,
                Height = 1,
                Width = 16
            };

            profileChoice = new ComboBox()
            {
                Height = 7,
                Width = 20,
                X = Pos.Right(label),
                Y = Pos.Top(label),
                ColorScheme = Colors.Dialog
            };
            profileChoice.SetSource(current_config.Profiles);

            jobDetailsFrame.Add(label, profileChoice);

            /* repository details */
            var repoFrame = new FrameView("Remote Repository")
            {
                Height = Dim.Fill() - 3,
                Width = Dim.Fill(),
                X = 1,
                Y = Pos.Top(profileChoice) + 2
            };

            jobDetailsFrame.Add(repoFrame);

            label = new Label("Commit Style: ")
            {
                X = 1,
                Y = 1,
                AutoSize = true
            };

            commitStyle = new ComboBox("")
            {
                X = Pos.Right(label),
                Y = Pos.Top(label),
                Height = 4,
                Width = 25,
                ColorScheme = Colors.Dialog
            };
            commitStyle.SetSource(new string[] { "Single Commit","PeopleCode Separate","Top Level Folders"});
            repoFrame.Add(label, commitStyle);

            label = new Label("Git User: ")
            {
                X = 1,
                Y = Pos.Top(commitStyle) + 2,
                AutoSize = true
            };

            userID = new TextField()
            {
                X = Pos.Right(label),
                Y = Pos.Top(label),
                Height = 1,
                Width = 20
            };

            repoFrame.Add(label, userID);

            label = new Label("Git Password: ")
            {
                X = Pos.Right(userID) + 1,
                Y = Pos.Top(userID),
                AutoSize = true
            };

            remoteURL = new TextField()
            {
                X = Pos.Right(label),
                Y = Pos.Top(label),
                Height = 1,
                Width = 20
            };

            repoFrame.Add(label, remoteURL);

            button = new Button("Encrypt")
            {
                X = Pos.Right(remoteURL) + 1,
                Y = Pos.Top(remoteURL),
                Height = 1,
                Width = 11
            };

            button.Clicked += encryptButtonClick;

            repoFrame.Add(button);


            label = new Label("Remote URL: ")
            {
                X = 1,
                Y = Pos.Bottom(userID) + 1,
                AutoSize = true
            };

            remoteURL = new TextField()
            {
                X = Pos.Right(label),
                Y = Pos.Top(label),
                Height = 1,
                Width = Dim.Fill(1)
            };

            repoFrame.Add(label, remoteURL);

            saveButton = new Button("Save Changes")
            {
                X = 1,
                Y = Pos.Bottom(repoFrame) + 1,
                AutoSize = true,
                Visible = false
            };

            saveButton.Clicked += () =>
            {
                SaveJobData();
                jobName.SetFocus();
                saveButton.SetNeedsDisplay();
            };

            jobDetailsFrame.Add(saveButton);


            if (!dirtyFlag)
            {
                SelectJob(0);
            }

            /* close window */
            KeyUp += (e) => {
                if (e.KeyEvent.Key == Key.Esc)
                    RequestStop();
            };

            jobName.TextChanged += MarkDirty;
            outputFolder.TextChanged += MarkDirty;
            envChoice.SelectedItemChanged += MarkDirty_List;
            profileChoice.SelectedItemChanged += MarkDirty_List;
            userID.TextChanged += MarkDirty;
            remoteURL.TextChanged += MarkDirty;
            commitStyle.SelectedItemChanged += MarkDirty_List;
            Closing += JobEditorWindow_Closing;
        }

        private void MarkDirty_List(ListViewItemEventArgs obj)
        {
            MarkDirty("");
        }

        private void outputFolderButton_Clicked()
        {
            var od = new OpenDialog("Output Folder", "Select output folder", null, OpenDialog.OpenMode.Directory);
            Application.Run(od);
            if (od.DirectoryPath.Length > 0)
            {
                outputFolder.Text = od.DirectoryPath.ToString() + Path.DirectorySeparatorChar + od.FilePath.ToString();
            }
        }

        private void JobEditorWindow_Closing(ToplevelClosingEventArgs obj)
        {
            PromptForSaveIfNeeded();
        }

        private void JobList_SelectedItemChanged(ListViewItemEventArgs obj)
        {
            SelectJob(obj.Item);
        }

        private void encryptButtonClick()
        {
            var encrypt = new EncryptDialog();
            Application.Run(encrypt);
            remoteURL.Text = encrypt.Result;
        }


        private void CreateJobButton_Clicked()
        {
            PromptForSaveIfNeeded();
            var new_env = new EnvironmentConfig() { Name = "<unnamed>" };
            current_config.Environments.Add(new_env);
            jobList.SetFocus();
        }
    }
}
