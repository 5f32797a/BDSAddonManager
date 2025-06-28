using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AddonManager.Forms
{
    public class AppSettings
    {
        public string? WorldLocation { get; set; }
        public string? RpLocation { get; set; }
        public string? BpLocation { get; set; }
    }

    public partial class DirectoryForm : Form
    {
        private readonly JsonParser parser = new JsonParser();

        private bool worldFlag = false;
        private bool packFlag = false;

        // NOTE: Using static fields creates tight coupling between forms and makes state management difficult.
        // A better approach would be to use a singleton instance for application state/settings
        // or dependency injection to share data between forms.
        public static bool canEdit = true;
        public static string worldLocation = string.Empty;
        public static string rpLocation = string.Empty;
        public static string bpLocation = string.Empty;
        public static string worldName = string.Empty;

        private static readonly string settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BDSAddonManager");
        private static readonly string settingsFile = Path.Combine(settingsPath, "settings.json");

        public DirectoryForm()
        {
            InitializeComponent();
            LoadSettings();
            this.Text = "SELECT FILE DIRECTORIES";
            worldDirectoryTextBox.Text = worldLocation;
            rpDirectoryTextBox.Text = rpLocation;
            bpDirectoryTextBox.Text = bpLocation;
            warningLabel.Text = "⚠️ Make sure the world is not currently running!";
            if (!canEdit)
            {
                DisableInput();
                warningLabel.Text = "Press the restart button to select another world.";
                GetWorldName(); // Show world name on subsequent loads
            }
        }

        private void worldFilePicker_Click(object sender, EventArgs e)
        {
            using (var folderBrowserDialog = new FolderBrowserDialog())
            {
                folderBrowserDialog.Description = "Select the world folder (e.g., 'Bedrock level')";
                if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
                {
                    string selectedPath = folderBrowserDialog.SelectedPath;
                    worldDirectoryTextBox.Text = selectedPath;
                    AutoDetectPackFolders(selectedPath);
                }
            }
            Logger.Log("World directory file explorer used.");
        }
        
        private void AutoDetectPackFolders(string worldPath)
        {
            try
            {
                string? parentDir = Path.GetDirectoryName(worldPath);
                if (parentDir != null && Path.GetFileName(parentDir).Equals("worlds", StringComparison.OrdinalIgnoreCase))
                {
                    string? serverRoot = Path.GetDirectoryName(parentDir);
                    if (serverRoot != null)
                    {
                        string rpPath = Path.Combine(serverRoot, "resource_packs");
                        string bpPath = Path.Combine(serverRoot, "behavior_packs");

                        if (Directory.Exists(rpPath) && Directory.Exists(bpPath))
                        {
                            rpDirectoryTextBox.Text = rpPath;
                            bpDirectoryTextBox.Text = bpPath;
                            Logger.Log("Auto-detected resource and behavior pack paths.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error during path auto-detection: {ex.Message}", "ERROR");
            }
        }

        private void resourcePackPicker_Click(object sender, EventArgs e)
        {
            OpenFolderPicker(rpDirectoryTextBox, "Select the 'resource_packs' folder");
            Logger.Log("RP directory file explorer used.");
        }

        private void behaviorPackPicker_Click(object sender, EventArgs e)
        {
            OpenFolderPicker(bpDirectoryTextBox, "Select the 'behavior_packs' folder");
            Logger.Log("BP directory file explorer used.");
        }

        private async void validatePathsButton_Click(object sender, EventArgs e)
        {
            if (!CheckFilePaths())
            {
                return; // Validation failed
            }

            Cursor.Current = Cursors.WaitCursor;
            validatePathsButton.Enabled = false;

            try
            {
                // Parse the existing world configuration
                parser.ParseWorldJson();

                // Clear previous lists before parsing
                ResultLists.rpList.Clear();
                ResultLists.bpList.Clear();
                
                // Run parsing tasks in parallel for performance
                Task task1 = Task.Run(() => parser.ParsePackFolder(path: rpLocation, list: ResultLists.rpList));
                Task task2 = Task.Run(() => parser.ParsePackFolder(path: bpLocation, list: ResultLists.bpList));
                await Task.WhenAll(task1, task2);

                // Finalize (sort, clean, categorize) the lists after all packs are parsed
                parser.FinalizePackLists();

                GetWorldName();
                SaveSettings();
                canEdit = false;
                DisableInput();
                warningLabel.Text = "World loaded. Press the restart button to select another world.";
                Logger.Log("World data has been successfully loaded!");
            }
            catch (Exception ex)
            {
                Logger.Log($"An error occurred during validation: {ex.Message}", "ERROR");
                MessageBox.Show($"An error occurred while loading world data: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                validatePathsButton.Enabled = true; // Re-enable button on failure
            }
            finally
            {
                Cursor.Current = Cursors.Default;
            }
        }

        private void GetWorldName()
        {
            try
            {
                string levelNamePath = Path.Combine(worldLocation, "levelname.txt");
                if (File.Exists(levelNamePath))
                {
                    worldName = File.ReadAllText(levelNamePath).Trim();
                    worldNameLabel.Text = "Loaded World: " + worldName;
                    Logger.Log("World name found: " + worldName);
                }
                else
                {
                    worldName = "Unknown World";
                    worldNameLabel.Text = "Loaded World: Unknown";
                    Logger.Log("levelname.txt not found. Using default name.", "WARN");
                }
            }
            catch (Exception ex)
            {
                worldName = "Error Reading Name";
                worldNameLabel.Text = "Loaded World: Error";
                Logger.Log($"Could not read world name from levelname.txt. Error: {ex.Message}", "ERROR");
            }
            worldNameLabel.Show();
        }

        private void DisableInput()
        {
            worldDirectoryTextBox.ReadOnly = true;
            rpDirectoryTextBox.ReadOnly = true;
            bpDirectoryTextBox.ReadOnly = true;
            worldFilePicker.Enabled = false;
            resourcePackPicker.Enabled = false;
            behaviorPackPicker.Enabled = false;
            validatePathsButton.Enabled = false;
            Logger.Log("Directory form input has been disabled.");
        }

        private void OpenFolderPicker(TextBox pathTextBox, string description)
        {
            using (var folderBrowserDialog = new FolderBrowserDialog())
            {
                folderBrowserDialog.Description = description;
                if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
                {
                    pathTextBox.Text = folderBrowserDialog.SelectedPath;
                }
            }
        }
        
        private bool CheckFilePaths()
        {
            worldLocation = worldDirectoryTextBox.Text.Trim();
            rpLocation = rpDirectoryTextBox.Text.Trim();
            bpLocation = bpDirectoryTextBox.Text.Trim();

            if (string.IsNullOrEmpty(worldLocation) || string.IsNullOrEmpty(rpLocation) || string.IsNullOrEmpty(bpLocation))
            {
                MessageBox.Show("Please select a valid path for all three directory locations.", "Missing Paths", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                Logger.Log("One or more directory paths are missing.", "WARN");
                return false;
            }

            if (!Directory.Exists(worldLocation) || !File.Exists(Path.Combine(worldLocation, "level.dat")))
            {
                MessageBox.Show("'level.dat' not found in the selected world directory. Please select a valid world folder.", "Invalid World Path", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Logger.Log("level.dat not found in the specified world directory.", "ERROR");
                return false;
            }

            if (!Directory.Exists(rpLocation) || !Path.GetFileName(rpLocation).Equals("resource_packs", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("The selected resource pack path does not appear to be a 'resource_packs' folder.", "Invalid Resource Pack Path", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                Logger.Log("Invalid resource_packs directory selected.", "WARN");
            }
            
            if (!Directory.Exists(bpLocation) || !Path.GetFileName(bpLocation).Equals("behavior_packs", StringComparison.OrdinalIgnoreCase))
            {
                 MessageBox.Show("The selected behavior pack path does not appear to be a 'behavior_packs' folder.", "Invalid Behavior Pack Path", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                 Logger.Log("Invalid behavior_packs directory selected.", "WARN");
            }
            
            return true;
        }

        private void resetButton_Click(object sender, EventArgs e)
        {
            DialogResult result = MessageBox.Show("Restart the application? All unsaved changes will be lost.", "Restart Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            Logger.Log("Restart button clicked.");
            if (result == DialogResult.Yes)
            {
                // Use Application.Restart() for a cleaner restart process.
                Application.Restart();
                Environment.Exit(0); // Ensure the current process terminates.
            }
        }

        private void LoadSettings()
        {
            if (!File.Exists(settingsFile)) return;
            try
            {
                string jsonString = File.ReadAllText(settingsFile);
                var settings = JsonSerializer.Deserialize<AppSettings>(jsonString);
                if (settings != null)
                {
                    worldLocation = settings.WorldLocation ?? string.Empty;
                    rpLocation = settings.RpLocation ?? string.Empty;
                    bpLocation = settings.BpLocation ?? string.Empty;
                    Logger.Log("Settings loaded successfully.");
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to load settings from '{settingsFile}'. Error: {ex.Message}", "ERROR");
            }
        }

        private void SaveSettings()
        {
            try
            {
                Directory.CreateDirectory(settingsPath);

                var settings = new AppSettings
                {
                    WorldLocation = worldLocation,
                    RpLocation = rpLocation,
                    BpLocation = bpLocation
                };

                var options = new JsonSerializerOptions { WriteIndented = true };
                string jsonString = JsonSerializer.Serialize(settings, options);
                File.WriteAllText(settingsFile, jsonString);
                Logger.Log("Settings saved successfully.");
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to save settings to '{settingsFile}'. Error: {ex.Message}", "ERROR");
            }
        }
    }
}