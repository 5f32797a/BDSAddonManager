using AddonManager.Forms;
using System.Diagnostics;

namespace AddonManager
{
    public class PackHandler
    {
        // Marked as nullable to satisfy compiler warnings
        private ListView? inactiveListView;
        private ListView? activeListView;
        private List<ManifestInfo>? inactiveList;
        private List<ManifestInfo>? activeList;

        private readonly ResultLists resultLists = new ResultLists();

        private string? inactiveListName;
        private string? activeListName;

        public void FormDeclaration(ListView iLV, ListView aLV, string inactiveListName, string activeListName)
        {
            inactiveListView = iLV;
            activeListView = aLV;

            // Create and assign ImageList once to prevent memory leaks and re-creation.
            if (inactiveListView.SmallImageList == null)
            {
                inactiveListView.SmallImageList = new ImageList { ImageSize = new Size(32, 32) };
            }
            if (activeListView.SmallImageList == null)
            {
                activeListView.SmallImageList = new ImageList { ImageSize = new Size(32, 32) };
            }

            inactiveList = resultLists.GetList(inactiveListName);
            activeList = resultLists.GetList(activeListName);

            this.inactiveListName = inactiveListName;
            this.activeListName = activeListName;
        }

        // Determines if a pack is excluded based on its name prefix
        public bool IsExcludedPack(string packName)
        {
            // Using a readonly array for performance and clarity
            string[] excludedPrefixes = { "resourcePack.education", "resourcePack.vanilla", "behaviorPack.education", "behaviorPack.vanilla", "experimental" };

            foreach (var prefix in excludedPrefixes)
            {
                if (packName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    Logger.Log($"Pack '{packName}' was hidden. Toggle 'Hide default packs' in Settings to view.");
                    return true;
                }
            }
            return false;
        }

        private void PopulateList(List<ManifestInfo>? packList, ListView? listView, string? listName)
        {
            if (packList == null || listView == null || listView.SmallImageList == null) return;

            listView.BeginUpdate(); // Suspend layout for performance
            listView.Items.Clear(); // Clear existing items to ensure a fresh list

            foreach (var pack in packList)
            {
                if (pack.name == null) continue; // Skip packs with no name

                // Check if the pack should be hidden
                if (SettingsForm.hideDefaultPacks && IsExcludedPack(pack.name))
                {
                    continue;
                }

                // Check for pack type mismatch and update description if necessary
                if (pack.type == "resources" && listName != "inactiveRpList" && listName != "activeRpList")
                {
                    pack.description = "⚠️ This is a resource pack!";
                }
                if ((pack.type == "data" || pack.type == "script") && listName != "inactiveBpList" && listName != "activeBpList")
                {
                    pack.description = "⚠️ This is a behavior pack!";
                }

                int imageIndex = -1;
                if (pack.pack_icon != null)
                {
                    imageIndex = listView.SmallImageList.Images.Add(pack.pack_icon, Color.Transparent);
                }

                var item = new ListViewItem(pack.name)
                {
                    ImageIndex = imageIndex,
                    SubItems = { pack.description ?? string.Empty, string.Join(".", pack.version ?? Array.Empty<int>()) },
                    Tag = pack
                };
                listView.Items.Add(item);
                // Logger.Log($"Pack: {pack.name} was added to {listView.Name}");
            }
            listView.EndUpdate(); // Resume layout
        }
        
        public void InactiveListPopulate()
        {
            PopulateList(inactiveList, inactiveListView, inactiveListName);
        }

        public void ActiveListPopulate()
        {
            PopulateList(activeList, activeListView, activeListName);
        }

        // Moves selected items from one ListView to another and updates the corresponding lists
        public void MoveSelectedItems(ListView source, ListView destination, List<ManifestInfo> sourceList, List<ManifestInfo> destinationList)
        {
            if (source.SelectedItems.Count == 0 || destination.SmallImageList == null) return;
            
            destination.BeginUpdate();
            source.BeginUpdate();

            var itemsToMove = source.SelectedItems.Cast<ListViewItem>().ToList();
            foreach (ListViewItem item in itemsToMove)
            {
                if (item.Tag is ManifestInfo pack)
                {
                    sourceList.Remove(pack);
                    destinationList.Add(pack);
                    
                    source.Items.Remove(item);
                    destination.Items.Add(item); // Re-add the same ListViewItem to preserve its properties

                    Logger.Log($"Pack: '{item.Text}' was moved to {destination.Name}");
                }
            }

            source.EndUpdate();
            destination.EndUpdate();
        }

        // Moves a selected item up or down within a ListView and updates the corresponding list
        public void MoveItemUpOrDown(ListView listView, List<ManifestInfo> list, int direction)
        {
            if (listView.SelectedItems.Count == 1) // Only move single items
            {
                var selectedItem = listView.SelectedItems[0];
                int index = selectedItem.Index;
                int newIndex = index + direction;

                if (newIndex >= 0 && newIndex < listView.Items.Count)
                {
                    // Update the underlying data list first
                    list.Move(index, newIndex);
                    
                    // Then update the UI
                    listView.Items.RemoveAt(index);
                    listView.Items.Insert(newIndex, selectedItem);
                    selectedItem.Selected = true; // Keep the item selected
                    listView.Focus();
                    
                    Logger.Log($"Pack: '{selectedItem.Text}' was moved {(direction < 0 ? "up" : "down")} in {listView.Name}");
                }
            }
        }
        
        // ... (The rest of the methods: DragEnterHandler, DragDropHandler, HandleMouseClick, OpenFolder, DeletePack, ImportPack are mostly fine)
        // A small improvement for ImportPack:
        public void ImportPack(string location)
        {
            FileImport import = new FileImport();
            // Use 'using' statement to ensure the dialog is disposed correctly.
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Addon files (*.mcpack;*.mcaddon;*.zip)|*.mcpack;*.mcaddon;*.zip|All files (*.*)|*.*";
                openFileDialog.Title = "Import Pack";

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string filePath = openFileDialog.FileName;
                    import.ProcessPack(filePath, location);

                    // Refresh the list after import
                    inactiveList = resultLists.GetList(inactiveListName!);
                    InactiveListPopulate();
                }
            }
        }
        // ... (Other methods remain largely the same, but below are the original methods for completeness)
        public void DragEnterHandler(DragEventArgs e)
        {
            //Check if the dragged data is a valid file
            if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.All(file =>
                    {
                        var ext = Path.GetExtension(file);
                        return ext.Equals(".zip", StringComparison.OrdinalIgnoreCase) ||
                               ext.Equals(".mcpack", StringComparison.OrdinalIgnoreCase) ||
                               ext.Equals(".mcaddon", StringComparison.OrdinalIgnoreCase);
                    }))
                {
                    e.Effect = DragDropEffects.Copy;
                }
                else { e.Effect = DragDropEffects.None; }
            }
            else { e.Effect = DragDropEffects.None; }
        }

        public void DragDropHandler(DragEventArgs e, string location)
        {
            if (e.Data == null) return;
            FileImport import = new FileImport();
            foreach (var filePath in (string[])e.Data.GetData(DataFormats.FileDrop))
            {
                var extension = Path.GetExtension(filePath);
                if (File.Exists(filePath) && (extension.Equals(".zip", StringComparison.OrdinalIgnoreCase) || extension.Equals(".mcpack", StringComparison.OrdinalIgnoreCase) || extension.Equals(".mcaddon", StringComparison.OrdinalIgnoreCase)))
                {
                    import.ProcessPack(filePath, location);
                }
            }
            inactiveList = resultLists.GetList(inactiveListName!);
            activeList = resultLists.GetList(activeListName!);
            InactiveListPopulate();
            Logger.Log("New pack(s) were successfully imported and added!");
        }

        public void HandleMouseClick(object sender, MouseEventArgs e, Action<ListViewItem> openFolderAction, Action<ListViewItem> deletePackAction, Action importFileAction)
        {
            if (!(sender is ListView listView)) return;
            
            if (e.Button == MouseButtons.Right)
            {
                using (var menu = new ContextMenuStrip())
                {
                    if (listView.SelectedItems.Count > 0)
                    {
                        menu.Items.Add("Open pack folder(s)", null, (s, args) =>
                        {
                            foreach (ListViewItem item in listView.SelectedItems)
                            {
                                openFolderAction(item);
                            }
                        });
                        menu.Items.Add($"Delete {listView.SelectedItems.Count} pack(s)", null, (s, args) =>
                        {
                            var result = MessageBox.Show($"Are you sure you want to permanently delete these {listView.SelectedItems.Count} pack(s)? This action cannot be undone.", "Confirm Deletion", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                            if (result == DialogResult.Yes)
                            {
                                foreach (ListViewItem item in listView.SelectedItems)
                                {
                                    deletePackAction(item);
                                }
                            }
                        });
                        menu.Items.Add(new ToolStripSeparator());
                    }

                    menu.Items.Add("Import new pack...", null, (s, args) => importFileAction());
                    menu.Show(Cursor.Position);
                }
            }
        }
        
        public void OpenFolder(ListViewItem item)
        {
            if (item.Tag is ManifestInfo pack && !string.IsNullOrEmpty(pack.pack_folder))
            {
                try
                {
                    Process.Start("explorer.exe", pack.pack_folder);
                    Logger.Log($"Opened folder for pack: {pack.name}");
                }
                catch (Exception ex)
                {
                    Logger.Log($"Failed to open folder for {pack.name}. Error: {ex.Message}", "ERROR");
                }
            }
        }
        
        public void DeletePack(ListViewItem item)
        {
            if (!(item.Tag is ManifestInfo pack) || string.IsNullOrEmpty(pack.pack_folder)) return;
            
            string folderPath = pack.pack_folder;

            if (Directory.Exists(folderPath))
            {
                try
                {
                    Directory.Delete(folderPath, true);
                    item.ListView?.Items.Remove(item);

                    inactiveList?.Remove(pack);
                    activeList?.Remove(pack);
                    
                    Logger.Log($"Pack '{pack.name}' was deleted from the disk!");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"An error occurred while deleting the folder: {ex.Message}", "Deletion Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Logger.Log($"Failed to delete pack '{pack.name}'. Error: {ex.Message}", "ERROR");
                }
            }
            else
            {
                MessageBox.Show("The directory for this pack does not exist. It may have been removed manually.", "Directory Not Found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Logger.Log($"Directory for pack '{pack.name}' not found. It was likely removed manually.", "WARN");
            }
        }
    }
}