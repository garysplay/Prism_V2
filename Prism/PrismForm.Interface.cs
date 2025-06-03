using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using OpenTK;
using OpenTK.Graphics;
using Prism.Controls;
using Prism.Extensions;
using Prism.Render;
using RainbowForge;
using RainbowForge.Archive;
using RainbowForge.Components;
using RainbowForge.Core;
using RainbowForge.Core.Container;
using SkiaSharp.Views.Desktop;

namespace Prism
{
    public partial class PrismForm
    {
        private const string SETTINGS_FILENAME = "settings.json";

        private readonly ToolStripLabel _statusForgeInfo;

        private readonly ToolStripMenuItem _bOpenForge;
        private readonly ToolStripMenuItem _bGenerateFilelist;
        private readonly ToolStripMenuItem _bResetViewport;

        private readonly ToolStripMenuItem _bDumpAsBinHeader;
        private readonly ToolStripMenuItem _bDumpAsBinQuick;
        private readonly ToolStripMenuItem _bDumpAsBinAs;

        private readonly ToolStripMenuItem _bDumpAsDdsHeader;
        private readonly ToolStripMenuItem _bDumpAsDdsQuick;
        private readonly ToolStripMenuItem _bDumpAsDdsAs;

        private readonly ToolStripMenuItem _bDumpAsPngHeader;
        private readonly ToolStripMenuItem _bDumpAsPngQuick;
        private readonly ToolStripMenuItem _bDumpAsPngAs;

        private readonly ToolStripMenuItem _bDumpAsObjHeader;
        private readonly ToolStripMenuItem _bDumpAsObjQuick;
        private readonly ToolStripMenuItem _bDumpAsObjAs;

        private readonly ToolStripMenuItem _bViewAsHex; // New: Hex View menu item

        private readonly ToolStripMenuItem _bDumpAsSoundHeader;
        private readonly ToolStripMenuItem _bDumpAsWemQuick;
        private readonly ToolStripMenuItem _bDumpAsWemAs;

        private readonly TextBox _searchTextBox;
        private readonly TreeView _assetList; // Changed from ListView to TreeView
        private readonly FlowLayoutPanel _assetListHeaderPanel; // Changed from Label to FlowLayoutPanel for headers
        private readonly Label _filenameHeaderLabel; // New: Label for Filename header
        private readonly Label _typeHeaderLabel;     // New: Label for Type header
        private readonly Label _sizeHeaderLabel;     // New: Label for Size header
        private readonly Label _uidHeaderLabel;      // New: Label for UID header


        private readonly MinimalSplitContainer _splitContainer;
        private readonly GLControl _glControl;
        private readonly SKControl _imageControl;
        private readonly TreeView _infoControl; // Remains TreeView for info panel
        private readonly TextBox _errorInfoControl;
        private readonly RichTextBox _hexInfoControl; // New: RichTextBox for hex view

        private ToolStripMenuItem _bEditSettings;

        private PrismSettings _settings;

        public PrismForm()
        {
            SuspendLayout();

            // Load settings immediately to ensure _settings is initialized
            _settings = PrismSettings.Load(SETTINGS_FILENAME);

            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1000, 600); // Slightly larger default window size
            Text = "Prism";

            // Initialize the split container
            _splitContainer = new MinimalSplitContainer
            {
                Dock = DockStyle.Fill,
                SplitterDistance = 450, // Initial split distance
            };

            // Initialize controls
            _searchTextBox = new TextBox
            {
                Dock = DockStyle.Fill, // Dock to fill the TableLayoutPanel cell
                PlaceholderText = "Search groups (i.e. keyword, ?type, #uid, seperated by commas)",
                Font = new Font("Segoe UI", 9) // Consistent font
            };

            _filenameHeaderLabel = new Label
            {
                Text = "Filename",
                Font = new Font("Consolas", 9, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Width = 320, // Approximate pixel width for 40 chars in Consolas 9pt
                Tag = SortColumn.Filename, // Store sort column in Tag
                Cursor = Cursors.Hand // Indicate clickable
            };
            _typeHeaderLabel = new Label
            {
                Text = "Type",
                Font = new Font("Consolas", 9, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Width = 120, // Approximate pixel width for 15 chars
                Tag = SortColumn.Type,
                Cursor = Cursors.Hand
            };
            _sizeHeaderLabel = new Label
            {
                Text = "Size",
                Font = new Font("Consolas", 9, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Width = 80, // Approximate pixel width for 10 chars
                Tag = SortColumn.Size,
                Cursor = Cursors.Hand
            };
            _uidHeaderLabel = new Label
            {
                Text = "UID",
                Font = new Font("Consolas", 9, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Width = 144, // Approximate pixel width for 18 chars
                Tag = SortColumn.Uid,
                Cursor = Cursors.Hand
            };

            _assetListHeaderPanel = new FlowLayoutPanel // New: FlowLayoutPanel for headers
            {
                Dock = DockStyle.Fill, // Dock to fill the TableLayoutPanel cell
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false, // Ensure headers stay on one line
                AutoSize = false, // We will manually set width for alignment
                Padding = new Padding(18, 0, 0, 0), // Adjust padding to align with TreeView's default node indentation
            };
            _assetListHeaderPanel.Controls.Add(_filenameHeaderLabel);
            _assetListHeaderPanel.Controls.Add(_typeHeaderLabel);
            _assetListHeaderPanel.Controls.Add(_sizeHeaderLabel);
            _assetListHeaderPanel.Controls.Add(_uidHeaderLabel);

            _assetList = new TreeView // Initialized as TreeView
            {
                Dock = DockStyle.Fill, // Dock to fill the TableLayoutPanel cell
                ShowLines = true, // Show lines connecting nodes
                ShowPlusMinus = true, // Show expandable/collapsible buttons
                HideSelection = false, // Keep selection highlighted even when control loses focus
                Font = new Font("Consolas", 9), // Monospace font for CSV-like alignment
                ItemHeight = 20 // Ensure consistent row height for better table appearance
            };


            // Use TableLayoutPanel for precise vertical layout within Panel1
            var panel1Layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1,
                RowStyles =
                {
                    new RowStyle(SizeType.Absolute, 25), // Row for searchTextBox
                    new RowStyle(SizeType.Absolute, 25), // Row for _assetListHeaderPanel
                    new RowStyle(SizeType.Percent, 100)  // Row for _assetList
                }
            };
            panel1Layout.Controls.Add(_searchTextBox, 0, 0);
            panel1Layout.Controls.Add(_assetListHeaderPanel, 0, 1);
            panel1Layout.Controls.Add(_assetList, 0, 2);

            _splitContainer.Panel1.Controls.Add(panel1Layout); // Add the TableLayoutPanel to Panel1

            Controls.Add(_splitContainer); // Add the split container to the form

            Controls.Add(new MenuStrip
            {
                Dock = DockStyle.Top,
                Renderer = new FlatToolStripRenderer(), // Custom renderer for flatter menu
                Font = new Font("Segoe UI", 9), // Consistent font for menu strip

                Items =
                {
                    new ToolStripDropDownButton
                    {
                        Text = "&File",
                        DisplayStyle = ToolStripItemDisplayStyle.Text, // Ensure only text is shown
						DropDownItems =
                        {
                            (_bOpenForge = new ToolStripMenuItem("&Open Forge")
                            {
                                ShortcutKeys = Keys.Control | Keys.O,
                                Font = new Font("Segoe UI", 9)
                            }),
                            new ToolStripSeparator(),
                            (_bGenerateFilelist = new ToolStripMenuItem("&Generate Filelist")
                            {
                                ShortcutKeys = Keys.Control | Keys.I,
                                Font = new Font("Segoe UI", 9)
                            }),
                            new ToolStripSeparator(),
                            (_bEditSettings = new ToolStripMenuItem("&Settings")
                            {
                                ShortcutKeys = Keys.Control | Keys.Oemcomma,
                                ShortcutKeyDisplayString = "Ctrl+,",
                                Font = new Font("Segoe UI", 9)
                            }),
                        }
                    },
                    new ToolStripDropDownButton
                    {
                        Text = "&Export",
                        DisplayStyle = ToolStripItemDisplayStyle.Text,
                        DropDownItems =
                        {
                            (_bDumpAsBinHeader = new ToolStripMenuItem("&Binary File (*.bin)")
                            {
                                DropDownItems =
                                {
                                    (_bDumpAsBinQuick = new ToolStripMenuItem("&Quick Export") { Font = new Font("Segoe UI", 9) }),
                                    (_bDumpAsBinAs = new ToolStripMenuItem("&Export as...") { Font = new Font("Segoe UI", 9) }),
                                }
                            }),
                            new ToolStripSeparator(),
                            (_bDumpAsDdsHeader = new ToolStripMenuItem("&DirectDraw Surface (*.dds)")
                            {
                                DropDownItems =
                                {
                                    (_bDumpAsDdsQuick = new ToolStripMenuItem("&Quick Export") { Font = new Font("Segoe UI", 9) }),
                                    (_bDumpAsDdsAs = new ToolStripMenuItem("&Export as...") { Font = new Font("Segoe UI", 9) }),
                                }
                            }),
                            (_bDumpAsPngHeader = new ToolStripMenuItem("&PNG (*.png)")
                            {
                                DropDownItems =
                                {
                                    (_bDumpAsPngQuick = new ToolStripMenuItem("&Quick Export") { Font = new Font("Segoe UI", 9) }),
                                    (_bDumpAsPngAs = new ToolStripMenuItem("&Export as...") { Font = new Font("Segoe UI", 9) }),
                                }
                            }),
                            new ToolStripSeparator(),
                            (_bDumpAsObjHeader = new ToolStripMenuItem("&Wavefront OBJ (*.obj)")
                            {
                                DropDownItems =
                                {
                                    (_bDumpAsObjQuick = new ToolStripMenuItem("&.obj | Quick Export") { Font = new Font("Segoe UI", 9) }),
                                    (_bDumpAsObjAs = new ToolStripMenuItem("&.obj | Export as...") { Font = new Font("Segoe UI", 9) })
                                }
                            })
                        }
                    },
                    new ToolStripDropDownButton
                    {
                        Text = "&View",
                        DisplayStyle = ToolStripItemDisplayStyle.Text,
                        DropDownItems =
                        {
                            (_bResetViewport = new ToolStripMenuItem("&Reset 3D Viewport") { Font = new Font("Segoe UI", 9) }),
                            new ToolStripSeparator(), // Separator before new view options
                            (_bViewAsHex = new ToolStripMenuItem("View as &Hex") { Font = new Font("Segoe UI", 9) }) // New: Hex View menu item
                        }
                    }
                }
            });

            Controls.Add(new StatusStrip
            {
                Dock = DockStyle.Bottom,
                Font = new Font("Segoe UI", 9), // Consistent font for status strip
                Items =
                {
                    (_statusForgeInfo = new ToolStripLabel("Ready")
                    {
                        Font = new Font("Segoe UI", 9) // Consistent font for label
                    })
                }
            });

            _glControl = new GLControl(new GraphicsMode(new ColorFormat(8), 24, 8, 1))
            {
                Dock = DockStyle.Fill,
                VSync = true,
                BackColor = Color.Black // Ensure GLControl has a dark background
            };

            _imageControl = new SKControl
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black // Ensure SKControl has a dark background
            };

            _infoControl = new TreeView // Initialized as TreeView
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9) // Consistent font
            };

            _errorInfoControl = new TextBox
            {
                Multiline = true,
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.White, // Will be set by ApplyTheme
                ForeColor = Color.Red,   // Will be set by ApplyTheme
                BorderStyle = BorderStyle.FixedSingle, // Flat border
                Font = new Font("Segoe UI", 9) // Consistent font
            };

            _hexInfoControl = new RichTextBox // New: Initialize hex view control
            {
                Multiline = true,
                Dock = DockStyle.Fill,
                ReadOnly = true,
                WordWrap = false, // Essential for hex view to maintain column alignment
                ScrollBars = RichTextBoxScrollBars.Both, // Allow both horizontal and vertical scrolling
                Font = new Font("Consolas", 9), // Monospace font for precise alignment
                // Colors will be set by ApplyTheme
            };

            SetPreviewPanel(new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Text = "Open a Forge to get started.",
                Font = new Font("Segoe UI", 12, FontStyle.Bold), // Larger, bolder font for welcome message
                ForeColor = Color.Gray // Softer color for welcome message
            });

            ResumeLayout(true);

            // Initialize theme immediately after controls are set up
            InitializeTheme();

            SetupRenderer();
            SetupAssetList();

            UpdateAbility(null);

            // Re-assign all missing button click handlers
            _bOpenForge.Click += (sender, args) =>
            {
                using var ofd = new OpenFileDialog
                {
                    Filter = "Forge Files|*.forge",
                    Multiselect = true
                };

                if (ofd.ShowDialog() != DialogResult.OK)
                    return;

                OpenForge(ofd.FileNames);
            };

            _bGenerateFilelist.Click += (sender, args) =>
            {
                using var fbd = new FolderBrowserDialog
                {
                    Description = "Selected the folder containing the forges."
                };

                if (fbd.ShowDialog() != DialogResult.OK || string.IsNullOrWhiteSpace(fbd.SelectedPath))
                    return;

                string[] forgeFiles = Directory.GetFiles(fbd.SelectedPath, "*.forge");

                if (forgeFiles.Length < 1)
                    return;

                GenerateFileList(forgeFiles);
            };

            _bEditSettings.Click += (sender, args) =>
            {
                if (new SettingsForm(SETTINGS_FILENAME).ShowDialog(this) == DialogResult.OK)
                    _settings = PrismSettings.Load(SETTINGS_FILENAME);
                ApplyTheme(); // Re-apply theme in case settings affect UI elements
            };

            _bResetViewport.Click += (sender, args) => _renderer3d.ResetView();

            _bDumpAsBinQuick.Click += CreateDumpEventHandler(DumpSelectionAsBin);
            _bDumpAsBinAs.Click += CreateDumpEventHandler(DumpSelectionAsBin, true);
            _bDumpAsDdsQuick.Click += CreateDumpEventHandler(DumpSelectionAsDds);
            _bDumpAsDdsAs.Click += CreateDumpEventHandler(DumpSelectionAsDds, true);
            _bDumpAsPngQuick.Click += CreateDumpEventHandler(DumpSelectionAsPng);
            _bDumpAsPngAs.Click += CreateDumpEventHandler(DumpSelectionAsPng, true);
            _bDumpAsObjQuick.Click += CreateDumpEventHandler(DumpSelectionAsObj);
            _bDumpAsObjAs.Click += CreateDumpEventHandler(DumpSelectionAsObj, true);
            _bViewAsHex.Click += (sender, args) =>
            {
                if (_assetList.SelectedNode != null)
                {
                    var selectedEntry = _assetList.SelectedNode.Tag;
                    var stream = GetAssetStream(selectedEntry);
                    if (stream != null)
                    {
                        PreviewHex(stream);
                    }
                }
            };

            // Assign click handlers for new header labels
            _filenameHeaderLabel.Click += HeaderLabel_Click;
            _typeHeaderLabel.Click += HeaderLabel_Click;
            _sizeHeaderLabel.Click += HeaderLabel_Click;
            _uidHeaderLabel.Click += HeaderLabel_Click;
        }

        private EventHandler CreateDumpEventHandler(Action<string, object> action, bool saveAs = false)
        {
            return (_, _) =>
            {
                var outputPath = _settings.QuickExportLocation;

                if (saveAs)
                {
                    var folderBrowserDialog = new FolderBrowserDialog();
                    if (folderBrowserDialog.ShowDialog() != DialogResult.OK)
                        return;

                    outputPath = folderBrowserDialog.SelectedPath;
                }

                Directory.CreateDirectory(outputPath);

                // Only dump the currently selected node, as TreeView does not support multiple selections in the same way ListView does.
                if (_assetList.SelectedNode != null)
                {
                    action(outputPath, _assetList.SelectedNode.Tag); // Pass the stored object
                }
            };
        }
        private void SetPreviewPanel(Control control)
        {
            using (_splitContainer.Panel2.SuspendPainting())
            {
                _splitContainer.Panel2.Controls.Clear();
                _splitContainer.Panel2.Controls.Add(control);
            }
            ApplyTheme(); // Re-apply theme to ensure new control gets themed
        }

        private void SetupRenderer()
        {
            _renderer2d = new SurfaceRenderer(_imageControl);
            _imageControl.MouseMove += (sender, args) => _renderer2d.OnMouseMove(args.Location, (args.Button & MouseButtons.Left) != 0);
            _imageControl.MouseWheel += (sender, args) => _renderer2d.OnMouseWheel(args.Location, args.Delta);

            _imageControl.PaintSurface += (sender, args) => { _renderer2d.Render(args); };

            _renderer3d = new ModelRenderer(new GlControlContext(_glControl), () => _settings);
            _glControl.MouseDown += (sender, args) => _renderer3d.OnMouseDown(args.Location);
            _glControl.MouseMove += (sender, args) => _renderer3d.OnMouseMove(args.Location, (args.Button & MouseButtons.Left) != 0, (args.Button & MouseButtons.Right) != 0);
            _glControl.MouseWheel += (sender, args) => _renderer3d.OnMouseWheel(args.Delta);

            _glControl.Paint += (sender, args) =>
            {
                _glControl.MakeCurrent();
                _renderer3d.Render();
                _glControl.SwapBuffers();
            };
        }

        private void SetupAssetList()
        {
            // No columns for TreeView, content is in node text
            _assetList.Nodes.Clear(); // Ensure it's clear on setup

            // Re-enabled automatic preview update on selection change.
            _assetList.AfterSelect += OnAssetListOnSelectionChanged; // Use AfterSelect for TreeView

            _assetList.BeforeExpand += AssetList_BeforeExpand; // Handle dynamic loading for FlatArchives

            _searchTextBox.TextChanged += (sender, args) =>
            {
                var filterStr = _searchTextBox.Text;
                FilterAssetList(filterStr); // Call filtering method
            };

            // Context menu for TreeView
            _assetList.MouseUp += (sender, args) =>
            {
                if (args.Button == MouseButtons.Right)
                {
                    TreeNode clickedNode = _assetList.GetNodeAt(args.X, args.Y);
                    if (clickedNode != null)
                    {
                        _assetList.SelectedNode = clickedNode; // Select the right-clicked node

                        var selectedObject = clickedNode.Tag;
                        if (selectedObject == null) return;

                        var filenameText = "";
                        var uidText = "";
                        var magicText = "";

                        // For TreeView, we only right-click one node at a time, so no loop needed for multiple selections
                        var meta = GetAssetMetaData(selectedObject);
                        if (meta != null)
                        {
                            filenameText = meta.Filename;
                            uidText = $"0x{meta.Uid:X16}";
                            magicText = $"0x{meta.Magic:X8}";
                        }

                        var stream = GetAssetStream(selectedObject);
                        var type = stream != null ? MagicHelper.GetFiletype(stream.MetaData.Magic) : AssetType.Unknown;

                        // Determine colors based on the selected theme pack for the context menu
                        Color contextMenuBackColor, contextMenuForeColor, itemHighlightColor, itemNormalBackColor;
                        switch (_settings.ActiveThemePack)
                        {
                            case ThemePack.Dark:
                                contextMenuBackColor = ThemeColors.Dark.ControlBackColor;
                                contextMenuForeColor = ThemeColors.Dark.ForeColor;
                                itemHighlightColor = ThemeColors.Dark.HighlightColor;
                                itemNormalBackColor = ThemeColors.Dark.ButtonBackColor;
                                break;
                            case ThemePack.Blue:
                                contextMenuBackColor = ThemeColors.Blue.ControlBackColor;
                                contextMenuForeColor = ThemeColors.Blue.ForeColor;
                                itemHighlightColor = ThemeColors.Blue.HighlightColor;
                                itemNormalBackColor = ThemeColors.Blue.ButtonBackColor;
                                break;
                            case ThemePack.HatsuneMiku:
                                contextMenuBackColor = ThemeColors.HatsuneMiku.ControlBackColor;
                                contextMenuForeColor = ThemeColors.HatsuneMiku.ForeColor;
                                itemHighlightColor = ThemeColors.HatsuneMiku.HighlightColor;
                                itemNormalBackColor = ThemeColors.HatsuneMiku.ButtonBackColor;
                                break;
                            case ThemePack.Light:
                            default:
                                contextMenuBackColor = ThemeColors.Light.ControlBackColor;
                                contextMenuForeColor = ThemeColors.Light.ForeColor;
                                itemHighlightColor = ThemeColors.Light.HighlightColor;
                                itemNormalBackColor = ThemeColors.Light.ButtonBackColor;
                                break;
                        }

                        var contextMenu = new ContextMenuStrip
                        {
                            Location = Cursor.Position,
                            BackColor = contextMenuBackColor,
                            ForeColor = contextMenuForeColor,
                            RenderMode = ToolStripRenderMode.System // Use system rendering for a flatter look
                        };

                        // Copy options
                        var bCopyName = new ToolStripMenuItem("Copy Name") { BackColor = itemNormalBackColor, ForeColor = contextMenuForeColor };
                        var bCopyUid = new ToolStripMenuItem("Copy UID") { BackColor = itemNormalBackColor, ForeColor = contextMenuForeColor };
                        var bCopyFiletype = new ToolStripMenuItem("Copy Filetype") { BackColor = itemNormalBackColor, ForeColor = contextMenuForeColor };

                        // Add hover effects for context menu items
                        bCopyName.MouseEnter += (s, e) => ((ToolStripItem)s).BackColor = itemHighlightColor;
                        bCopyName.MouseLeave += (s, e) => ((ToolStripItem)s).BackColor = itemNormalBackColor;
                        bCopyUid.MouseEnter += (s, e) => ((ToolStripItem)s).BackColor = itemHighlightColor;
                        bCopyUid.MouseLeave += (s, e) => ((ToolStripItem)s).BackColor = itemNormalBackColor;
                        bCopyFiletype.MouseEnter += (s, e) => ((ToolStripItem)s).BackColor = itemHighlightColor;
                        bCopyFiletype.MouseLeave += (s, e) => ((ToolStripItem)s).BackColor = itemNormalBackColor;

                        bCopyName.Click += (o, eventArgs) => Clipboard.SetText(filenameText);
                        bCopyUid.Click += (o, eventArgs) => Clipboard.SetText(uidText);
                        bCopyFiletype.Click += (o, eventArgs) => Clipboard.SetText(magicText);

                        // Export options
                        var exportMenu = new ToolStripMenuItem("Export") { BackColor = itemNormalBackColor, ForeColor = contextMenuForeColor };
                        exportMenu.MouseEnter += (s, e) => ((ToolStripItem)s).BackColor = itemHighlightColor;
                        exportMenu.MouseLeave += (s, e) => ((ToolStripItem)s).BackColor = itemNormalBackColor;

                        var bExportBin = new ToolStripMenuItem("Binary File (*.bin)") { BackColor = itemNormalBackColor, ForeColor = contextMenuForeColor };
                        bExportBin.MouseEnter += (s, e) => ((ToolStripItem)s).BackColor = itemHighlightColor;
                        bExportBin.MouseLeave += (s, e) => ((ToolStripItem)s).BackColor = itemNormalBackColor;
                        bExportBin.Click += (o, eventArgs) => DumpSelectionAsBin(_settings.QuickExportLocation, selectedObject);

                        if (type == AssetType.Texture)
                        {
                            var bExportDds = new ToolStripMenuItem("DirectDraw Surface (*.dds)") { BackColor = itemNormalBackColor, ForeColor = contextMenuForeColor };
                            var bExportPng = new ToolStripMenuItem("PNG (*.png)") { BackColor = itemNormalBackColor, ForeColor = contextMenuForeColor };

                            bExportDds.MouseEnter += (s, e) => ((ToolStripItem)s).BackColor = itemHighlightColor;
                            bExportDds.MouseLeave += (s, e) => ((ToolStripItem)s).BackColor = itemNormalBackColor;
                            bExportPng.MouseEnter += (s, e) => ((ToolStripItem)s).BackColor = itemHighlightColor;
                            bExportPng.MouseLeave += (s, e) => ((ToolStripItem)s).BackColor = itemNormalBackColor;

                            bExportDds.Click += (o, eventArgs) => DumpSelectionAsDds(_settings.QuickExportLocation, selectedObject);
                            bExportPng.Click += (o, eventArgs) => DumpSelectionAsPng(_settings.QuickExportLocation, selectedObject);

                            exportMenu.DropDownItems.Add(bExportDds);
                            exportMenu.DropDownItems.Add(bExportPng);
                        }

                        if (type == AssetType.Mesh)
                        {
                            var bExportObj = new ToolStripMenuItem("Wavefront OBJ (*.obj)") { BackColor = itemNormalBackColor, ForeColor = contextMenuForeColor };
                            bExportObj.MouseEnter += (s, e) => ((ToolStripItem)s).BackColor = itemHighlightColor;
                            bExportObj.MouseLeave += (s, e) => ((ToolStripItem)s).BackColor = itemNormalBackColor;
                            bExportObj.Click += (o, eventArgs) => DumpSelectionAsObj(_settings.QuickExportLocation, selectedObject);
                            exportMenu.DropDownItems.Add(bExportObj);
                        }

                        exportMenu.DropDownItems.Add(bExportBin);

                        contextMenu.Items.AddRange(new ToolStripItem[]
                        {
                            bCopyName,
                            bCopyUid,
                            bCopyFiletype,
                            new ToolStripSeparator(),
                            exportMenu
                        });

                        contextMenu.Show(Cursor.Position);
                    }
                }
            };
        }

        /// <summary>
        /// Filters the asset list (TreeView) based on the provided search string.
        /// </summary>
        /// <param name="filter">The filter string.</param>
        private void FilterAssetList(string filter)
        {
            _assetList.BeginUpdate();
            _assetList.Nodes.Clear(); // Clear existing nodes

            // The header node is now handled by _assetListHeaderPanel, so we don't add it to the TreeView itself.

            if (string.IsNullOrWhiteSpace(filter))
            {
                // If filter is empty, repopulate with all original top-level entries
                SortAndPopulateAssetList(); // Call sorting after filtering
            }
            else
            {
                // Filter the original entries and add matching ones
                var filteredEntries = new List<Entry>();
                foreach (var entry in _allForgeEntries)
                {
                    if (DoesEntryMatchFilter(entry, filter))
                    {
                        filteredEntries.Add(entry);
                    }
                    else if (MagicHelper.GetFiletype(entry.MetaData.FileType) == AssetType.FlatArchive)
                    {
                        // For FlatArchives, check if any of their children match
                        var forge = _openedForges.FirstOrDefault(f => f.Entries.Any(fe => fe.Uid == entry.Uid));
                        if (forge != null)
                        {
                            var container = forge.GetContainer(entry.Uid);
                            if (container is ForgeAsset forgeAsset)
                            {
                                using var assetStream = forgeAsset.GetDataStream(forge);
                                var fa = FlatArchive.Read(assetStream, forge.Version);
                                if (fa.Entries.Any(innerEntry => DoesEntryMatchFilter(innerEntry, filter)))
                                {
                                    // If an inner entry matches, add the parent FlatArchive and expand it
                                    filteredEntries.Add(entry); // Add the parent entry
                                }
                            }
                        }
                    }
                }
                PopulateAssetList(ApplySorting(filteredEntries)); // Apply sorting to filtered list
            }
            _assetList.EndUpdate();
        }


        private static bool DoesEntryMatchFilter(object entry, string filter)
        {
            var searchGroups = filter.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var groupMatches = new List<bool>();

            var meta = GetAssetMetaData(entry);
            if (meta == null) return false; // Ensure meta is not null

            foreach (var t in searchGroups)
            {
                if (string.IsNullOrWhiteSpace(t))
                {
                    groupMatches.Add(true);
                    continue;
                }

                if (t.StartsWith('#') && ulong.TryParse(t[1..], NumberStyles.HexNumber, Thread.CurrentThread.CurrentCulture, out var filterUid) &&
                    filterUid == meta.Uid)
                {
                    groupMatches.Add(true);
                    continue;
                }

                if (t.StartsWith('?'))
                {
                    groupMatches.Add(((Magic)meta.Magic).ToString().Contains(t[1..], StringComparison.OrdinalIgnoreCase));
                    continue;
                }

                groupMatches.Add(meta.Filename.Contains(t, StringComparison.OrdinalIgnoreCase));
            }

            return groupMatches.All(x => x);
        }

        private void OnAssetListOnSelectionChanged(object sender, TreeViewEventArgs args) // Changed event args
        {
            if (args.Node == null || args.Node.Tag == null) return; // Check for null node or tag
            var selectedEntry = args.Node.Tag; // Get the stored object from Tag
            lock (_openedForges)
            {
                var stream = GetAssetStream(selectedEntry);
                if (stream != null)
                    try
                    {
                        PreviewAsset(stream);
                    }
                    catch (Exception e)
                    {
                        OnUiThread(() =>
                        {
                            _errorInfoControl.Text = e.ToString();
                            SetPreviewPanel(_errorInfoControl);
                            ApplyTheme(); // Re-apply theme to ensure error control is styled
                        });
                    }

                UpdateAbility(stream);
            }
        }

        private void UpdateAbility(AssetStream assetStream)
        {
            AssetType type;
            Magic magic;

            if (assetStream == null)
            {
                type = AssetType.Unknown;
                magic = 0;
            }
            else
            {
                type = MagicHelper.GetFiletype(assetStream.MetaData.Magic);
                magic = (Magic)assetStream.MetaData.Magic;
            }

            _bDumpAsBinHeader.Enabled = assetStream != null;
            _bDumpAsDdsHeader.Enabled = _bDumpAsPngHeader.Enabled = type == AssetType.Texture;
            _bDumpAsObjHeader.Enabled = type == AssetType.Mesh || MagicHelper.Equals(Magic.Mesh, magic);
        }

        private void OnUiThread(Action action)
        {
            if (!InvokeRequired)
                action();
            else
                BeginInvoke(action);
        }

        // New method to handle header label clicks for sorting
        private void HeaderLabel_Click(object sender, EventArgs e)
        {
            if (sender is Label clickedLabel && clickedLabel.Tag is SortColumn clickedColumn)
            {
                if (_settings.ActiveSortColumn == clickedColumn)
                {
                    // Toggle sort order if clicking the same column
                    _settings.ActiveSortOrder = (_settings.ActiveSortOrder == SortOrder.Ascending) ? SortOrder.Descending : SortOrder.Ascending;
                }
                else
                {
                    // Change sort column, reset order to ascending
                    _settings.ActiveSortColumn = clickedColumn;
                    _settings.ActiveSortOrder = SortOrder.Ascending;
                }
                _settings.Save(SETTINGS_FILENAME); // Save settings immediately
                SortAndPopulateAssetList(); // Re-sort and re-populate the list
            }
        }
    }
}
