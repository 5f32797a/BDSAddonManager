﻿
namespace AddonManager.Forms
{
    public partial class AboutForm : Form
    {
        // Using a static readonly string is more efficient as it's created only once
        // instead of per-instance.
        private static readonly string AboutText =
@"This tool is designed to simplify the management of addons for self-hosted Minecraft: Bedrock Edition servers.

⚠️ Please note: Modifying an existing world may lead to unexpected results. It's recommended to create a backup before proceeding.

Instructions:
1. In the Directory screen, locate the root world folder that contains 'level.dat', as well as the root resource and behavior pack folders.
2. Click the 'Validate' button to verify the world paths and initiate the loading process.
3. From the sidebar, select the desired pack type and use the mouse to select packs. Once selected, use the buttons to arrange them. Packs listed higher in the list will take precedence.
4. After making the changes, press the 'Save' button to apply them to the world.

Adding New Packs:
Simply drag and drop a .mcpack/.mcaddon/.zip file into the 'inactive' side of the pack selector. The new pack will then be available for selection. Alternatively, you can right click and import a pack with the file picker.

Attribution:
This application includes a variety of icons and media elements. For a detailed list, please refer to the source code.

Disclaimer:
This is not an official Minecraft product and is neither endorsed nor affiliated with Mojang or Microsoft.";

        public AboutForm()
        {
            InitializeComponent();
            this.Text = "ABOUT";
            // Set the label text from the static readonly field.
            aboutLabel.Text = AboutText;
        }
    }
}