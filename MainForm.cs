using System.Diagnostics;
using AddonManager.Forms;

namespace AddonManager
{
    public partial class MainForm : Form
    {
        private Form? activeForm;
        public Button ConsoleButton { get; private set; }

        public MainForm()
        {
            InitializeComponent();
            this.Text = Program.title;
            logoLabel.Text = Program.title;
            versionLabel.Text = Program.version;
            Logger.Log($"{Program.title} {Program.version} loaded!");

            //Start on directory screen
            OpenChildForm(new DirectoryForm(), null);
            ConsoleButton = consoleButton;
            if (SettingsForm.hideConsoleTab)
            {
                consoleButton.Visible = false;
            }
        }
        // Opens a child form within a parent form, replacing any currently open child form
        private void OpenChildForm(Form childForm, object? buttonSender)
        {
            if (activeForm != null)
            {
                activeForm.Close();
                activeForm.Dispose(); // Ensure resources are released
            }
            activeForm = childForm;
            childForm.TopLevel = false;
            childForm.FormBorderStyle = FormBorderStyle.None;
            childForm.Dock = DockStyle.Fill;
            this.workspacePanel.Controls.Add(childForm);
            this.workspacePanel.Tag = childForm;
            childForm.BringToFront();
            childForm.Show();
            headerLabel.Text = childForm.Text;
            //Logger.Log(childForm.Text + " was clicked!");
        }
        // Link to the project's page when clicking the logo
        private void logoPictureBox_Click(object sender, EventArgs e) 
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = "https://github.com/DragonTech26/BDSAddonManager", UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Logger.Log($"Could not open link: {ex.Message}", "ERROR");
                MessageBox.Show("Could not open the project's GitHub page.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void directoryButton_Click(object sender, EventArgs e)
        {
            OpenChildForm(new DirectoryForm(), sender);
        }
        private void rpButton_Click(object sender, EventArgs e)
        {
            OpenChildForm(new ResourcePackForm(), sender);
        }
        private void bpButton_Click(object sender, EventArgs e)
        {
            OpenChildForm(new BehaviorPackForm(), sender);
        }
        private void consoleButton_Click(object sender, EventArgs e)
        {
            OpenChildForm(new ConsoleForm(), sender);
        }
        private void infoButton_Click(object sender, EventArgs e)
        {
            OpenChildForm(new AboutForm(), sender);
        }
        private void settingsButton_Click(object sender, EventArgs e)
        {
            OpenChildForm(new SettingsForm(this), sender);
        }
        // If a world is selected, prompt the user for confirmation to save pack changes
        private void saveButton_Click(object sender, EventArgs e)
        {
            // Use IsNullOrEmpty for robust checking.
            // Note: The static access to DirectoryForm.worldLocation creates tight coupling.
            // A better design would involve a shared state/service object.
            if (string.IsNullOrEmpty(DirectoryForm.worldLocation))
            {
                MessageBox.Show("No world has been loaded. Please select directories and validate first.", "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                Logger.Log("Save failed: No world selected or validated.", "ERROR");
                return;
            }

            string worldNameToDisplay = string.IsNullOrEmpty(DirectoryForm.worldName) ? "the selected world" : DirectoryForm.worldName;
            DialogResult result = MessageBox.Show($"Save pack changes to '{worldNameToDisplay}'?", "Save Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            
            if (result == DialogResult.Yes)
            {
                Cursor.Current = Cursors.WaitCursor;
                try
                {
                    JsonParser.SaveToJson();
                    MessageBox.Show("World pack configuration saved successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    Logger.Log("Active world packs have successfully saved to disk!");
                }
                catch (Exception ex)
                {
                    Logger.Log($"Failed to save pack data: {ex.Message}", "ERROR");
                    MessageBox.Show($"An error occurred while saving: {ex.Message}", "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    Cursor.Current = Cursors.Default;
                }
            }
            else
            {
                Logger.Log("Save operation was cancelled by the user.");
            }
        }
    }
}