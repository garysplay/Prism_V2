using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using JeremyAnsel.Media.WavefrontObj;
using Prism.Extensions;
using Prism.Render;
using Prism.Resources;
using RainbowForge;
using RainbowForge.Archive;
using RainbowForge.Components;
using RainbowForge.Core;
using RainbowForge.Core.Container;
using RainbowForge.Dump;
using RainbowForge.Image;
using RainbowForge.Link;
using RainbowForge.Model;
using RainbowForge.RenderPipeline;
using RainbowForge.Sound;
using SkiaSharp;
using System.Threading.Tasks;
using System.Diagnostics;
using System.ComponentModel; // Required for DefaultValueAttribute

namespace Prism
{
    public partial class PrismForm : Form
    {
        private ModelRenderer _renderer3d;
        private SurfaceRenderer _renderer2d;

        private List<Forge> _openedForges;
        private Forge _activeForge;
        private Dictionary<ulong, ulong> _flatArchiveEntryMap;

        // Define color sets for different themes
        private static class ThemeColors
        {
            public static class Light
            {
                public static readonly Color BackColor = SystemColors.Control;
                public static readonly Color ForeColor = SystemColors.ControlText;
                public static readonly Color ControlBackColor = SystemColors.Window;
                public static readonly Color MenuBackColor = SystemColors.Control;
                public static readonly Color ButtonBackColor = SystemColors.Control;
                public static readonly Color ErrorBackColor = Color.White;
                public static readonly Color ErrorForeColor = Color.Red;
                public static readonly Color HighlightColor = SystemColors.Highlight;
                public static readonly Color HighlightTextColor = SystemColors.HighlightText;
                public static readonly BorderStyle ControlBorderStyle = BorderStyle.Fixed3D;
            }

            public static class Dark
            {
                public static readonly Color BackColor = Color.FromArgb(28, 28, 28);
                public static readonly Color ForeColor = Color.FromArgb(230, 230, 230);
                public static readonly Color ControlBackColor = Color.FromArgb(40, 40, 40);
                public static readonly Color MenuBackColor = Color.FromArgb(35, 35, 35);
                public static readonly Color ButtonBackColor = Color.FromArgb(60, 60, 60);
                public static readonly Color ErrorBackColor = Color.FromArgb(70, 20, 20);
                public static readonly Color ErrorForeColor = Color.FromArgb(255, 150, 150);
                public static readonly Color HighlightColor = Color.FromArgb(80, 80, 80); // Darker highlight
                public static readonly Color HighlightTextColor = Color.White;
                public static readonly BorderStyle ControlBorderStyle = BorderStyle.FixedSingle;
            }

            public static class Blue
            {
                public static readonly Color BackColor = Color.FromArgb(20, 20, 40);
                public static readonly Color ForeColor = Color.FromArgb(200, 200, 255);
                public static readonly Color ControlBackColor = Color.FromArgb(30, 30, 60);
                public static readonly Color MenuBackColor = Color.FromArgb(25, 25, 50);
                public static readonly Color ButtonBackColor = Color.FromArgb(50, 50, 80);
                public static readonly Color ErrorBackColor = Color.FromArgb(80, 30, 30);
                public static readonly Color ErrorForeColor = Color.FromArgb(255, 180, 180);
                public static readonly Color HighlightColor = Color.FromArgb(70, 70, 100);
                public static readonly Color HighlightTextColor = Color.White;
                public static readonly BorderStyle ControlBorderStyle = BorderStyle.FixedSingle;
            }

            public static class HatsuneMiku // New: Hatsune Miku theme colors
            {
                public static readonly Color BackColor = Color.FromArgb(30, 40, 45); // Dark teal background
                public static readonly Color ForeColor = Color.FromArgb(170, 255, 230); // Light teal/aqua text
                public static readonly Color ControlBackColor = Color.FromArgb(45, 60, 65); // Slightly lighter control background
                public static readonly Color MenuBackColor = Color.FromArgb(35, 50, 55); // Menu background
                public static readonly Color ButtonBackColor = Color.FromArgb(60, 80, 85); // Button background
                public static readonly Color ErrorBackColor = Color.FromArgb(120, 40, 40); // Reddish error background
                public static readonly Color ErrorForeColor = Color.FromArgb(255, 200, 200); // Lighter red error text
                public static readonly Color HighlightColor = Color.FromArgb(90, 200, 180); // Bright teal highlight
                public static readonly Color HighlightTextColor = Color.Black; // Black text on highlight
                public static readonly BorderStyle ControlBorderStyle = BorderStyle.FixedSingle;
            }
        }

        private List<Entry> _allForgeEntries; // Store all entries for filtering

        [DefaultValue(null)] // Added DefaultValue attribute to suppress WFO1000 warning
        public Forge OpenedForge
        {
            get => _activeForge;
            set
            {
                _activeForge = value;
                _flatArchiveEntryMap = new Dictionary<ulong, ulong>();

                _allForgeEntries = _openedForges.SelectMany(f => f.Entries).ToList(); // Store all entries

                SortAndPopulateAssetList(); // Call sorting after populating _allForgeEntries
                UpdateAbility(null);

                if (_assetList.Nodes.Count > 0)
                {
                    _assetList.SelectedNode = _assetList.Nodes[0];
                }

                var sb = new StringBuilder();
                sb.Append($"{_allForgeEntries.Count:N0} entries");

                if (_allForgeEntries.Count > 0)
                {
                    sb.Append(" (");

                    var types = _allForgeEntries
                        .GroupBy(entry => (Magic)entry.MetaData.FileType)
                        .OrderByDescending(entries => entries.Count())
                        .Select(entries => $"{entries.Count():N0} {(Enum.IsDefined(typeof(Magic), entries.Key) ? entries.Key : $"{(uint)entries.Key:X8}")}")
                        .ToList();
                    sb.Append(string.Join(", ", types.Take(5)));

                    if (types.Count > 5)
                        sb.Append(", ...");

                    sb.Append(')');
                }

                _statusForgeInfo.Text = sb.ToString();
            }
        }

        private void DumpSelectionAsBin(string outputDir, object o)
        {
            var (_, assetMetaData, streamProvider) = GetAssetStream(o);
            using var stream = streamProvider.Invoke();
            DumpHelper.DumpBin(Path.Combine(outputDir, assetMetaData.Filename + ".bin"), stream.BaseStream);
        }

        private void DumpSelectionAsObj(string outputDir, object o)
        {
            var (_, assetMetaData, streamProvider) = GetAssetStream(o);
            using var stream = streamProvider.Invoke();

            if (MagicHelper.GetFiletype(assetMetaData.Magic) == AssetType.Mesh)
            {
                var header = MeshHeader.Read(stream, _activeForge.Version);

                var compiledMeshObject = CompiledMeshObject.Read(stream, header);

                var obj = new ObjFile();

                var numObjects = compiledMeshObject.Objects.Count / compiledMeshObject.MeshHeader.NumLods;

                for (var objId = 0; objId < (_settings.ExportAllModelLods ? compiledMeshObject.Objects.Count : numObjects); objId++)
                {
                    var lod = objId / numObjects;

                    var objObject = compiledMeshObject.Objects[objId];
                    foreach (var face in objObject)
                    {
                        var objFace = new ObjFace
                        {
                            ObjectName = $"{assetMetaData.Filename + (_settings.ExportAllModelLods ? $"_lod{lod}_" : "_")}object{objId % numObjects}"
                        };

                        objFace.Vertices.Add(new ObjTriplet(face.A + 1, face.A + 1, face.A + 1));
                        objFace.Vertices.Add(new ObjTriplet(face.B + 1, face.B + 1, face.B + 1));
                        objFace.Vertices.Add(new ObjTriplet(face.C + 1, face.C + 1, face.C + 1));

                        obj.Faces.Add(objFace);
                    }
                }

                var container = compiledMeshObject.Container;
                for (var i = 0; i < container.Vertices.Length; i++)
                {
                    var vert = container.Vertices[i];
                    var color = container.Colors?[0, i] ?? new Color4(1, 1, 1, 1);

                    obj.Vertices.Add(new ObjVertex(vert.X, vert.Y, vert.Z, color.R, color.G, color.B, color.A));
                }

                foreach (var v in container.Normals)
                    obj.VertexNormals.Add(new ObjVector3(v.X, v.Y, v.Z));

                foreach (var v in container.TexCoords)
                    obj.TextureVertices.Add(new ObjVector3(v.X, v.Y));

                obj.WriteTo(Path.Combine(outputDir, assetMetaData.Filename + ".obj"));
            }
            else if (MagicHelper.Equals(Magic.Mesh, assetMetaData.Magic))
            {
                var header = Mesh.Read(stream);

                var boneModel = ObjFile.FromStream(ResourceHelper.GetResource("bone.obj"));

                var obj = new ObjFile();

                var i = 0;
                foreach (var bone in header.Bones)
                {
                    foreach (var face in boneModel.Faces)
                    {
                        var objFace = new ObjFace
                        {
                            ObjectName = $"Bone_{(Enum.IsDefined(typeof(BoneId), bone.Id) ? bone.Id : $"{(uint)bone.Id:X8}")}"
                        };

                        objFace.Vertices.Add(new ObjTriplet(face.Vertices[0].Vertex + i, face.Vertices[0].Texture + i, face.Vertices[0].Normal + i));
                        objFace.Vertices.Add(new ObjTriplet(face.Vertices[1].Vertex + i, face.Vertices[1].Texture + i, face.Vertices[1].Normal + i));
                        objFace.Vertices.Add(new ObjTriplet(face.Vertices[2].Vertex + i, face.Vertices[2].Texture + i, face.Vertices[2].Normal + i));

                        obj.Faces.Add(objFace);
                    }

                    foreach (var vertex in boneModel.Vertices)
                    {
                        var x = vertex.Position.X;
                        var y = vertex.Position.Y;
                        var z = vertex.Position.Z;

                        var nx = x * bone.Transformation.M11 + y * bone.Transformation.M21 + z * bone.Transformation.M31 + bone.Transformation.M41;
                        var ny = x * bone.Transformation.M12 + y * bone.Transformation.M22 + z * bone.Transformation.M32 + bone.Transformation.M42;
                        var nz = x * bone.Transformation.M13 + y * bone.Transformation.M23 + z * bone.Transformation.M33 + bone.Transformation.M43;

                        obj.Vertices.Add(new ObjVertex(nx, ny, nz));
                    }

                    foreach (var vertex in boneModel.VertexNormals)
                    {
                        var x = vertex.X;
                        var y = vertex.Y;
                        var z = vertex.Z;

                        var nx = x * bone.Transformation.M11 + y * bone.Transformation.M21 + z * bone.Transformation.M31 + bone.Transformation.M41;
                        var ny = x * bone.Transformation.M12 + y * bone.Transformation.M22 + z * bone.Transformation.M32 + bone.Transformation.M42;
                        var nz = x * bone.Transformation.M13 + y * bone.Transformation.M23 + z * bone.Transformation.M33 + bone.Transformation.M43;

                        obj.VertexNormals.Add(new ObjVector3(nx, ny, nz));
                    }

                    foreach (var vertex in boneModel.TextureVertices)
                    {
                        obj.TextureVertices.Add(new ObjVector3(vertex.X, vertex.Y, vertex.Z));
                    }

                    i += boneModel.Vertices.Count;
                }

                obj.WriteTo(Path.Combine(outputDir, assetMetaData.Filename + "_bones.obj"));
            }
        }

        private void DumpSelectionAsDds(string outputDir, object o)
        {
            var (_, assetMetaData, streamProvider) = GetAssetStream(o);
            using var stream = streamProvider.Invoke();

            if (MagicHelper.GetFiletype(assetMetaData.Magic) == AssetType.Texture)
            {
                var texture = Texture.Read(stream, _activeForge.Version);
                var surface = texture.ReadSurfaceBytes(stream);
                using var ddsStream = DdsHelper.GetDdsStream(texture, surface);

                DumpHelper.DumpBin(Path.Combine(outputDir, assetMetaData.Filename + ".dds"), ddsStream);
            }
        }

        private void DumpSelectionAsPng(string outputDir, object o)
        {
            var (_, assetMetaData, streamProvider) = GetAssetStream(o);
            using var stream = streamProvider.Invoke();

            var texture = Texture.Read(stream, _activeForge.Version);
            using var image = Pfim.Pfim.FromStream(DdsHelper.GetDdsStream(texture, texture.ReadSurfaceBytes(stream)));
            using var bmp = image.CreateBitmap();

            if (_settings.FlipPngSpace)
                bmp.RotateFlip(RotateFlipType.RotateNoneFlipY);

            if ((texture.TexType == TextureType.Normal || texture.TexType == TextureType.Misc) &&
                texture.TexFormat == 0x6)
            {
                if (_settings.FlipPngGreenChannel || _settings.RecalculatePngBlueChannel)
                {
                    TextureUtil.PatchNormalMap(bmp, 1, _settings.RecalculatePngBlueChannel);
                }
            }

            bmp.Save(Path.Combine(outputDir, assetMetaData.Filename + ".png"), ImageFormat.Png);
        }

        private async void DumpSelectionAsWav(string outputDir, object o)
        {
            var (_, assetMetaData, streamProvider) = GetAssetStream(o);
            using var stream = streamProvider.Invoke();

            var sound = WemSound.Read(stream, _activeForge.Version);
            var wemPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.wem");
            var wavPath = Path.Combine(outputDir, assetMetaData.Filename + ".wav");

            try
            {
                // First export as WEM
                DumpHelper.DumpBin(wemPath, stream.BaseStream, sound.PayloadOffset, sound.PayloadLength);

                // Convert to WAV using VGMStream
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "vgmstream-cli.exe",
                        Arguments = $"-o \"{wavPath}\" \"{wemPath}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    throw new Exception("VGMStream conversion failed");
                }
            }
            finally
            {
                if (File.Exists(wemPath))
                    File.Delete(wemPath);
            }
        }

        private static AssetMetaData GetAssetMetaData(object o)
        {
            return o switch
            {
                Entry e => new AssetMetaData(e.Uid, e.MetaData.FileType, 0, e.MetaData.FileName),
                FlatArchiveEntry fae => new AssetMetaData(fae.MetaData.Uid, fae.MetaData.FileType, fae.MetaData.ContainerType, fae.MetaData.FileName),
                _ => null
            };
        }

        private AssetStream GetAssetStream(object o)
        {
            switch (o)
            {
                case Entry entry:
                    {
                        // Find the forge that contains this entry
                        var forge = _openedForges.FirstOrDefault(f =>
                            f.Entries.Any(e => e.Uid == entry.Uid));

                        if (forge == null) return null;

                        var container = forge.GetContainer(entry.Uid);

                        return container switch
                        {
                            ForgeAsset forgeAsset => new AssetStream(AssetStreamType.ForgeEntry, GetAssetMetaData(entry), () => forgeAsset.GetDataStream(forge)),
                            _ => new AssetStream(AssetStreamType.ForgeEntry, GetAssetMetaData(entry), () => forge.GetEntryStream(entry))
                        };
                    }
                case FlatArchiveEntry flatArchiveEntry:
                    {
                        var container = _activeForge.GetContainer(_flatArchiveEntryMap[flatArchiveEntry.MetaData.Uid]);
                        if (container is not ForgeAsset forgeAsset)
                            return null;

                        return new AssetStream(AssetStreamType.ArchiveEntry, GetAssetMetaData(flatArchiveEntry),
                            () =>
                            {
                                using var assetStream = forgeAsset.GetDataStream(_activeForge);
                                var arc = FlatArchive.Read(assetStream, _activeForge.Version);
                                return arc.GetEntryStream(assetStream.BaseStream, flatArchiveEntry.MetaData.Uid);
                            });
                    }
            }

            return null;
        }

        private void PreviewAsset(AssetStream assetStream)
        {
            switch (MagicHelper.GetFiletype(assetStream.MetaData.Magic))
            {
                case AssetType.Mesh:
                    {
                        using var stream = assetStream.StreamProvider.Invoke();
                        var header = MeshHeader.Read(stream, _activeForge.Version);
                        var mesh = CompiledMeshObject.Read(stream, header);

                        OnUiThread(() =>
                        {
                            SetPreviewPanel(_glControl);
                            using (_glControl.SuspendPainting())
                            {
                                _renderer3d.BuildModelQuads(mesh);
                                _renderer3d.SetTexture(null);
                                _renderer3d.SetPartBounds(header.ObjectBoundingBoxes.Take((int)(header.ObjectBoundingBoxes.Length / header.NumLods)).ToArray());
                            }
                        });

                        break;
                    }
                case AssetType.Sound:
                    {
                        // For Sound, populate the TreeView (_infoControl)
                        using var stream = assetStream.StreamProvider.Invoke();
                        var sound = WemSound.Read(stream, _activeForge.Version);

                        var rootNode = new TreeNode("Sound Info");
                        rootNode.Nodes.Add(CreateMetadataInfoNode(assetStream));

                        // Add sound-specific properties
                        rootNode.Nodes.Add(new TreeNode($"Payload Offset: {sound.PayloadOffset}"));
                        rootNode.Nodes.Add(new TreeNode($"Payload Length: {sound.PayloadLength}"));

                        OnUiThread(() =>
                        {
                            _infoControl.Nodes.Clear();
                            _infoControl.Nodes.Add(rootNode);
                            _infoControl.ExpandAll();
                            SetPreviewPanel(_infoControl);
                        });

                        break;
                    }
                case AssetType.Texture:
                    {
                        using var stream = assetStream.StreamProvider.Invoke();
                        var texture = Texture.Read(stream, _activeForge.Version);
                        using var image = Pfim.Pfim.FromStream(DdsHelper.GetDdsStream(texture, texture.ReadSurfaceBytes(stream)));

                        using (var skImage = image.CreateSkImage())
                            _renderer2d.SetTexture(SKBitmap.FromImage(skImage), new KeyValuePair<string, string>[]
                            {
                            new("Width", texture.Width.ToString()),
                            new("Height", texture.Height.ToString()),
                            new("Size Scalar", texture.Chan.ToString()),
                            new("Mips", texture.Mips.ToString()),
                            new("Blocks", texture.NumBlocks.ToString()),
                            new("Unk. Data 1 (flags?)", texture.Data1.ToString()),
                            new("Unk. Data 2", texture.Data2.ToString()),
                            new("Format", $"{DdsHelper.TextureFormats[texture.TexFormat]}"),
                            new("Type", Enum.IsDefined(typeof(TextureType), texture.TexType) ? texture.TexType.ToString() : $"{texture.TexType:X}")
                            });

                        OnUiThread(() => { SetPreviewPanel(_imageControl); });

                        break;
                    }
                case AssetType.FlatArchive when assetStream.StreamType == AssetStreamType.ArchiveEntry:
                    {
                        // First entry in a flat archive is a UidLinkContainer

                        using var stream = assetStream.StreamProvider.Invoke();
                        var container = UidLinkContainer.Read(stream, assetStream.MetaData.ContainerType);

                        var rootNode = new TreeNode("Flat Archive Info");
                        rootNode.Nodes.Add(CreateMetadataInfoNode(assetStream));

                        var uidLinkEntriesNode = new TreeNode(nameof(UidLinkContainer.UidLinkEntries));
                        foreach (var ule in container.UidLinkEntries)
                        {
                            uidLinkEntriesNode.Nodes.Add(CreateUidLinkEntryNode(ule));
                        }
                        rootNode.Nodes.Add(uidLinkEntriesNode);

                        OnUiThread(() =>
                        {
                            _infoControl.Nodes.Clear();
                            _infoControl.Nodes.Add(rootNode);
                            _infoControl.ExpandAll();
                            SetPreviewPanel(_infoControl);
                        });

                        break;
                    }
                default:
                    {
                        // For other types, populate the TreeView (_infoControl)
                        TreeNode rootNode = new TreeNode("Asset Info");
                        rootNode.Nodes.Add(CreateMetadataInfoNode(assetStream));

                        switch ((Magic)assetStream.MetaData.Magic)
                        {
                            case Magic.Mesh:
                                {
                                    using var stream = assetStream.StreamProvider.Invoke();
                                    var mp = Mesh.Read(stream);

                                    var meshNode = new TreeNode(nameof(Mesh));
                                    meshNode.Nodes.Add(new TreeNode($"Var1: {mp.Var1}"));
                                    meshNode.Nodes.Add(new TreeNode($"Var2: {mp.Var2}"));
                                    meshNode.Nodes.Add(new TreeNode($"Mesh UID: {mp.CompiledMeshObjectUid:X16}"));

                                    var bonesNode = new TreeNode(nameof(Mesh.Bones));
                                    foreach (var bone in mp.Bones)
                                    {
                                        bonesNode.Nodes.Add(new TreeNode($"ID: {(Enum.IsDefined(typeof(BoneId), bone.Id) ? bone.Id : $"{(uint)bone.Id:X8}")}"));
                                    }
                                    meshNode.Nodes.Add(bonesNode);

                                    var materialsNode = new TreeNode(nameof(Mesh.Materials));
                                    foreach (var materialUid in mp.Materials)
                                    {
                                        materialsNode.Nodes.Add(new TreeNode($"UID: {materialUid:X16}"));
                                    }
                                    meshNode.Nodes.Add(materialsNode);
                                    rootNode.Nodes.Add(meshNode);
                                    break;
                                }
                            case Magic.ShaderCodeModuleUserMaterial:
                            case Magic.ShaderCodeModulePostPro:
                                {
                                    using var stream = assetStream.StreamProvider.Invoke();
                                    var mp = Shader.Read(stream);
                                    rootNode.Nodes.Add(new TreeNode(nameof(Shader)));
                                    // Add shader-specific properties if available
                                    break;
                                }
                            case Magic.Material:
                                {
                                    using var stream = assetStream.StreamProvider.Invoke();
                                    var mc = Material.Read(stream);

                                    var materialNode = new TreeNode(nameof(Material));
                                    materialNode.Nodes.Add(CreateMipContainerReferenceNode("BaseTextureMapSpecs", mc.BaseTextureMapSpecs));
                                    materialNode.Nodes.Add(CreateMipContainerReferenceNode("SecondaryTextureMapSpecs", mc.SecondaryTextureMapSpecs));
                                    materialNode.Nodes.Add(CreateMipContainerReferenceNode("TertiaryTextureMapSpecs", mc.TertiaryTextureMapSpecs));
                                    rootNode.Nodes.Add(materialNode);
                                    break;
                                }
                            case Magic.TextureMapSpec:
                                {
                                    using var stream = assetStream.StreamProvider.Invoke();
                                    var mc = TextureMapSpec.Read(stream);

                                    var textureMapSpecNode = new TreeNode(nameof(TextureMapSpec));
                                    textureMapSpecNode.Nodes.Add(new TreeNode($"TextureMapUid: {mc.TextureMapUid:X16}"));
                                    textureMapSpecNode.Nodes.Add(new TreeNode($"TextureType: {mc.TextureType:X8}"));
                                    rootNode.Nodes.Add(textureMapSpecNode);
                                    break;
                                }
                            case Magic.TextureMap:
                                {
                                    using var stream = assetStream.StreamProvider.Invoke();
                                    var mc = TextureMap.Read(stream);

                                    var textureMapNode = new TreeNode(nameof(TextureMap));
                                    textureMapNode.Nodes.Add(new TreeNode($"Var1: {mc.Var1}"));
                                    textureMapNode.Nodes.Add(new TreeNode($"Var2: {mc.Var2}"));
                                    textureMapNode.Nodes.Add(new TreeNode($"Var3: {mc.Var3}"));
                                    textureMapNode.Nodes.Add(new TreeNode($"Var4: {mc.Var4}"));

                                    var mipSet1Node = new TreeNode(nameof(TextureMap.TexUidMipSet1));
                                    foreach (var uid in mc.TexUidMipSet1)
                                    {
                                        mipSet1Node.Nodes.Add(new TreeNode($"UID: {uid:X16}"));
                                    }
                                    textureMapNode.Nodes.Add(mipSet1Node);

                                    var mipSet2Node = new TreeNode(nameof(TextureMap.TexUidMipSet2));
                                    foreach (var uid in mc.TexUidMipSet2)
                                    {
                                        mipSet2Node.Nodes.Add(new TreeNode($"UID: {uid:X16}"));
                                    }
                                    textureMapNode.Nodes.Add(mipSet2Node);
                                    rootNode.Nodes.Add(textureMapNode);
                                    break;
                                }
                            case Magic.R6AIWorldComponent:
                                {
                                    using var stream = assetStream.StreamProvider.Invoke();
                                    var am = R6AIWorldComponent.Read(stream);

                                    var aiWorldComponentNode = new TreeNode(nameof(R6AIWorldComponent));
                                    var roomsNode = new TreeNode(nameof(R6AIWorldComponent.Rooms));
                                    foreach (var area in am.Rooms)
                                    {
                                        var areaNode = new TreeNode("Area");
                                        areaNode.Nodes.Add(new TreeNode($"Name: {area.Name}"));
                                        var uidsNode = new TreeNode("UIDs");
                                        foreach (var uid in area.Uids)
                                        {
                                            uidsNode.Nodes.Add(new TreeNode($"UID: {uid:X16}"));
                                        }
                                        areaNode.Nodes.Add(uidsNode);
                                        roomsNode.Nodes.Add(areaNode);
                                    }
                                    aiWorldComponentNode.Nodes.Add(roomsNode);
                                    rootNode.Nodes.Add(aiWorldComponentNode);
                                    break;
                                }
                        }

                        OnUiThread(() =>
                        {
                            _infoControl.Nodes.Clear();
                            _infoControl.Nodes.Add(rootNode);
                            _infoControl.ExpandAll();
                            SetPreviewPanel(_infoControl);
                        });

                        break;
                    }
            }
        }

        /// <summary>
        /// Displays the hexadecimal representation of the first 1024 bytes of the asset stream.
        /// </summary>
        /// <param name="assetStream">The asset stream to preview.</param>
        private void PreviewHex(AssetStream assetStream)
        {
            const int bytesToRead = 1024; // Limit to first 1024 bytes for display
            var sb = new StringBuilder();

            using (var stream = assetStream.StreamProvider.Invoke())
            {
                var buffer = new byte[bytesToRead];
                int bytesRead = stream.BaseStream.Read(buffer, 0, bytesToRead);

                for (int i = 0; i < bytesRead; i += 16)
                {
                    // Append byte offset (e.g., 00000000)
                    sb.Append($"{i:X8}  ");

                    // Append hexadecimal values (e.g., 4D 5A 90 00 ...)
                    for (int j = 0; j < 16; j++)
                    {
                        if (i + j < bytesRead)
                        {
                            sb.Append($"{buffer[i + j]:X2} ");
                        }
                        else
                        {
                            sb.Append("   "); // Pad with spaces if less than 16 bytes on the last line
                        }
                        if (j == 7) sb.Append(" "); // Add an extra space in the middle for better readability
                    }
                    sb.Append(" ");

                    // Append ASCII representation (e.g., MZ....)
                    for (int j = 0; j < 16; j++)
                    {
                        if (i + j < bytesRead)
                        {
                            char c = (char)buffer[i + j];
                            // Replace non-printable characters with a dot
                            sb.Append(char.IsControl(c) || char.IsWhiteSpace(c) ? '.' : c);
                        }
                    }
                    sb.AppendLine(); // Move to the next line
                }
            }

            OnUiThread(() =>
            {
                _hexInfoControl.Text = sb.ToString();
                SetPreviewPanel(_hexInfoControl); // Set the hex view as the active preview panel
            });
        }

        private static TreeNode CreateUidLinkEntryNode(UidLinkEntry ule)
        {
            var node = new TreeNode("UidLinkEntry");
            node.Nodes.Add(new TreeNode($"UidLinkNode1: {(ule.UidLinkNode1 == null ? "null" : $"{ule.UidLinkNode1.LinkedUid:X16}")}"));
            node.Nodes.Add(new TreeNode($"UidLinkNode2: {(ule.UidLinkNode2 == null ? "null" : $"{ule.UidLinkNode2.LinkedUid:X16}")}"));
            return node;
        }

        private static TreeNode CreateMipContainerReferenceNode(string name, IEnumerable<TextureSelector> selectors)
        {
            var parentNode = new TreeNode(name);
            foreach (var mcr in selectors)
            {
                var node = new TreeNode("MipContainerReference");
                node.Nodes.Add(new TreeNode($"Var1: {mcr.Var1}"));
                node.Nodes.Add(new TreeNode($"MipTarget: {mcr.MipTarget:X8}"));
                node.Nodes.Add(new TreeNode($"MipContainerUid: {mcr.TextureMapSpecUid:X16}"));
                parentNode.Nodes.Add(node);
            }
            return parentNode;
        }

        private static TreeNode CreateMetadataInfoNode(AssetStream stream)
        {
            var node = new TreeNode("Metadata");
            node.Nodes.Add(new TreeNode($"Filename: {stream.MetaData.Filename}"));
            node.Nodes.Add(new TreeNode($"UID: {stream.MetaData.Uid:X16}"));
            node.Nodes.Add(new TreeNode($"FileType: {stream.MetaData.Magic:X8}"));
            node.Nodes.Add(new TreeNode($"Magic: {(Magic)stream.MetaData.Magic}"));
            node.Nodes.Add(new TreeNode($"AssetType: {MagicHelper.GetFiletype(stream.MetaData.Magic)}"));
            return node;
        }

        private void OpenForge(string[] filenames)
        {
            _openedForges = new List<Forge>();

            var tasks = filenames.Select(filename => Task.Run(() => Forge.GetForge(filename))).ToList();
            Task.WaitAll(tasks.ToArray());

            _openedForges.AddRange(tasks.Select(t => t.Result));
            OpenedForge = _openedForges[0]; // Set firstforge as active
            Text = $"Prism - {string.Join(", ", filenames.Select(Path.GetFileName))}";
        }

        private static void GenerateFileList(string[] forgeFiles)
        {
            var fileListPath = Path.Combine(Environment.CurrentDirectory, "filelist.txt");

            using var sw = new StreamWriter(fileListPath);
            foreach (var forge in forgeFiles)
            {
                sw.WriteLine(Path.GetFileName(forge));
                var currentForge = Forge.GetForge(forge);
                foreach (var entry in currentForge.Entries)
                {
                    var entryMetaData = GetAssetMetaData(entry);
                    sw.WriteLine("> " + entryMetaData.Uid.ToString("X16") + ": " + entryMetaData.Filename + "." + (Magic)entryMetaData.Magic);

                    if (MagicHelper.GetFiletype(entryMetaData.Magic) != AssetType.FlatArchive || currentForge.GetContainer(entryMetaData.Uid) is not ForgeAsset fa)
                        continue;

                    var archiveStream = fa.GetDataStream(currentForge);
                    var archive = FlatArchive.Read(archiveStream, currentForge.Version);

                    foreach (var archiveEntry in archive.Entries[1..]) // first archive entry always has the same UID, Magic and Name so we skip it
                    {
                        entryMetaData = GetAssetMetaData(archiveEntry);
                        sw.WriteLine(">> " + entryMetaData.Uid.ToString("X16") + ": " + entryMetaData.Filename + "." + (Magic)entryMetaData.Magic);
                    }
                }
            }

            MessageBox.Show("Successfully generated filelist.txt", "Done");
        }

        private void InitializeTheme()
        {
            // Theme is now controlled by settings, applied when settings are loaded/saved
            ApplyTheme();
        }

        private void ApplyTheme()
        {
            Color backColor, foreColor, controlBackColor, menuBackColor, buttonBackColor, errorBackColor, errorForeColor, highlightColor, highlightTextColor, lineColor;
            BorderStyle controlBorderStyle;

            // Determine colors based on the selected theme pack
            switch (_settings.ActiveThemePack)
            {
                case ThemePack.Dark:
                    backColor = ThemeColors.Dark.BackColor;
                    foreColor = ThemeColors.Dark.ForeColor;
                    controlBackColor = ThemeColors.Dark.ControlBackColor;
                    menuBackColor = ThemeColors.Dark.MenuBackColor;
                    buttonBackColor = ThemeColors.Dark.ButtonBackColor;
                    errorBackColor = ThemeColors.Dark.ErrorBackColor;
                    errorForeColor = ThemeColors.Dark.ErrorForeColor;
                    highlightColor = ThemeColors.Dark.HighlightColor;
                    highlightTextColor = ThemeColors.Dark.HighlightTextColor;
                    lineColor = ThemeColors.Dark.ForeColor;
                    controlBorderStyle = ThemeColors.Dark.ControlBorderStyle;
                    break;
                case ThemePack.Blue:
                    backColor = ThemeColors.Blue.BackColor;
                    foreColor = ThemeColors.Blue.ForeColor;
                    controlBackColor = ThemeColors.Blue.ControlBackColor;
                    menuBackColor = ThemeColors.Blue.MenuBackColor;
                    buttonBackColor = ThemeColors.Blue.ButtonBackColor;
                    errorBackColor = ThemeColors.Blue.ErrorBackColor;
                    errorForeColor = ThemeColors.Blue.ErrorForeColor;
                    highlightColor = ThemeColors.Blue.HighlightColor;
                    highlightTextColor = ThemeColors.Blue.HighlightTextColor;
                    lineColor = ThemeColors.Blue.ForeColor;
                    controlBorderStyle = ThemeColors.Blue.ControlBorderStyle;
                    break;
                case ThemePack.HatsuneMiku: // New: Hatsune Miku theme application
                    backColor = ThemeColors.HatsuneMiku.BackColor;
                    foreColor = ThemeColors.HatsuneMiku.ForeColor;
                    controlBackColor = ThemeColors.HatsuneMiku.ControlBackColor;
                    menuBackColor = ThemeColors.HatsuneMiku.MenuBackColor;
                    buttonBackColor = ThemeColors.HatsuneMiku.ButtonBackColor;
                    errorBackColor = ThemeColors.HatsuneMiku.ErrorBackColor;
                    errorForeColor = ThemeColors.HatsuneMiku.ErrorForeColor;
                    highlightColor = ThemeColors.HatsuneMiku.HighlightColor;
                    highlightTextColor = ThemeColors.HatsuneMiku.HighlightTextColor;
                    lineColor = ThemeColors.HatsuneMiku.ForeColor;
                    controlBorderStyle = ThemeColors.HatsuneMiku.ControlBorderStyle;
                    break;
                case ThemePack.Light: // Default Light theme
                default:
                    backColor = ThemeColors.Light.BackColor;
                    foreColor = ThemeColors.Light.ForeColor;
                    controlBackColor = ThemeColors.Light.ControlBackColor;
                    menuBackColor = ThemeColors.Light.MenuBackColor;
                    buttonBackColor = ThemeColors.Light.ButtonBackColor;
                    errorBackColor = ThemeColors.Light.ErrorBackColor;
                    errorForeColor = ThemeColors.Light.ErrorForeColor;
                    highlightColor = ThemeColors.Light.HighlightColor;
                    highlightTextColor = ThemeColors.Light.HighlightTextColor;
                    lineColor = SystemColors.ControlText;
                    controlBorderStyle = ThemeColors.Light.ControlBorderStyle;
                    break;
            }

            // Apply colors and styles
            BackColor = backColor;
            ForeColor = foreColor;
            Font = new Font("Segoe UI", 9);

            // Null checks added for controls before accessing their properties
            if (_assetList != null)
            {
                _assetList.BackColor = controlBackColor;
                _assetList.ForeColor = foreColor;
                _assetList.LineColor = lineColor;
                _assetList.BorderStyle = controlBorderStyle;
                _assetList.HotTracking = (_settings.ActiveThemePack != ThemePack.Light); // Enable hot tracking for dark/blue/miku themes
            }

            if (_assetListHeaderPanel != null) // Apply to the FlowLayoutPanel
            {
                _assetListHeaderPanel.BackColor = controlBackColor;
                _assetListHeaderPanel.ForeColor = foreColor;

                // Apply to individual header labels within the panel
                foreach (Control control in _assetListHeaderPanel.Controls)
                {
                    if (control is Label headerLabel)
                    {
                        headerLabel.BackColor = controlBackColor;
                        headerLabel.ForeColor = foreColor;
                    }
                }
            }


            if (_infoControl != null)
            {
                _infoControl.BackColor = controlBackColor;
                _infoControl.ForeColor = foreColor;
                _infoControl.BorderStyle = controlBorderStyle;
                _infoControl.LineColor = lineColor;
            }

            if (_searchTextBox != null)
            {
                _searchTextBox.BackColor = controlBackColor;
                _searchTextBox.ForeColor = foreColor;
                _searchTextBox.BorderStyle = controlBorderStyle;
            }

            if (_errorInfoControl != null)
            {
                _errorInfoControl.BackColor = errorBackColor;
                _errorInfoControl.ForeColor = errorForeColor;
                _errorInfoControl.BorderStyle = controlBorderStyle;
            }

            if (_hexInfoControl != null)
            {
                _hexInfoControl.BackColor = controlBackColor;
                _hexInfoControl.ForeColor = foreColor;
                _hexInfoControl.BorderStyle = controlBorderStyle;
            }

            if (_statusForgeInfo != null)
            {
                _statusForgeInfo.BackColor = backColor;
                _statusForgeInfo.ForeColor = foreColor;
            }

            // Apply theme to menus and buttons
            foreach (ToolStripItem item in Controls.OfType<MenuStrip>().First().Items)
            {
                if (item is ToolStripDropDownButton dropDown)
                {
                    dropDown.BackColor = menuBackColor;
                    dropDown.ForeColor = foreColor;
                    dropDown.DropDown.BackColor = menuBackColor;
                    dropDown.DropDown.ForeColor = foreColor;

                    foreach (ToolStripItem dropDownItem in dropDown.DropDownItems)
                    {
                        dropDownItem.BackColor = buttonBackColor;
                        dropDownItem.ForeColor = foreColor;
                        // Remove previous hover handlers to prevent duplicates
                        dropDownItem.MouseEnter -= (s, e) => ((ToolStripItem)s).BackColor = ThemeColors.Dark.HighlightColor;
                        dropDownItem.MouseLeave -= (s, e) => ((ToolStripItem)s).BackColor = ThemeColors.Dark.ButtonBackColor;
                        dropDownItem.MouseEnter -= (s, e) => ((ToolStripItem)s).BackColor = ThemeColors.Light.HighlightColor;
                        dropDownItem.MouseLeave -= (s, e) => ((ToolStripItem)s).BackColor = ThemeColors.Light.ButtonBackColor;
                        dropDownItem.MouseEnter -= (s, e) => ((ToolStripItem)s).BackColor = ThemeColors.Blue.HighlightColor;
                        dropDownItem.MouseLeave -= (s, e) => ((ToolStripItem)s).BackColor = ThemeColors.Blue.ButtonBackColor;
                        dropDownItem.MouseEnter -= (s, e) => ((ToolStripItem)s).BackColor = ThemeColors.HatsuneMiku.HighlightColor; // New: Hatsune Miku hover
                        dropDownItem.MouseLeave -= (s, e) => ((ToolStripItem)s).BackColor = ThemeColors.HatsuneMiku.ButtonBackColor; // New: Hatsune Miku hover

                        // Add new hover handler based on current theme
                        dropDownItem.MouseEnter += (s, e) => ((ToolStripItem)s).BackColor = highlightColor;
                        dropDownItem.MouseLeave += (s, e) => ((ToolStripItem)s).BackColor = buttonBackColor;
                    }
                }
            }

            Controls.OfType<StatusStrip>().FirstOrDefault()?.ApplyTheme(backColor, foreColor);

            UpdateHeaderSortIndicators(); // Update header indicators after theme applies
        }

        /// <summary>
        /// Populates the _assetList TreeView with asset entries, including hierarchical display for FlatArchives.
        /// </summary>
        /// <param name="entries">The collection of asset entries to display.</param>
        private void PopulateAssetList(IEnumerable<Entry> entries)
        {
            _assetList.BeginUpdate();
            _assetList.Nodes.Clear();

            foreach (var entry in entries)
            {
                var node = CreateAssetTreeNode(entry);
                _assetList.Nodes.Add(node);
            }
            _assetList.EndUpdate();
        }

        /// <summary>
        /// Applies the current sorting criteria to a list of entries.
        /// </summary>
        /// <param name="entries">The list of entries to sort.</param>
        /// <returns>The sorted list of entries.</returns>
        private List<Entry> ApplySorting(IEnumerable<Entry> entries)
        {
            IOrderedEnumerable<Entry> sortedEntries;

            switch (_settings.ActiveSortColumn)
            {
                case SortColumn.Filename:
                    sortedEntries = _settings.ActiveSortOrder == SortOrder.Ascending
                        ? entries.OrderBy(e => GetAssetMetaData(e)?.Filename)
                        : entries.OrderByDescending(e => GetAssetMetaData(e)?.Filename);
                    break;
                case SortColumn.Type:
                    sortedEntries = _settings.ActiveSortOrder == SortOrder.Ascending
                        ? entries.OrderBy(e => MagicHelper.GetFiletype(GetAssetMetaData(e)?.Magic ?? 0).ToString())
                        : entries.OrderByDescending(e => MagicHelper.GetFiletype(GetAssetMetaData(e)?.Magic ?? 0).ToString());
                    break;
                case SortColumn.Size:
                    sortedEntries = _settings.ActiveSortOrder == SortOrder.Ascending
                        ? entries.OrderBy(e => (long)e.Size) // Simplified to use Entry.Size directly
                        : entries.OrderByDescending(e => (long)e.Size); // Simplified to use Entry.Size directly
                    break;
                case SortColumn.Uid:
                    sortedEntries = _settings.ActiveSortOrder == SortOrder.Ascending
                        ? entries.OrderBy(e => GetAssetMetaData(e)?.Uid)
                        : entries.OrderByDescending(e => GetAssetMetaData(e)?.Uid);
                    break;
                default:
                    sortedEntries = entries.OrderBy(e => GetAssetMetaData(e)?.Filename); // Default to filename ascending
                    break;
            }
            return sortedEntries.ToList();
        }

        /// <summary>
        /// Sorts the main asset list based on current settings and repopulates the TreeView.
        /// </summary>
        private void SortAndPopulateAssetList()
        {
            if (_allForgeEntries == null) return;
            PopulateAssetList(ApplySorting(_allForgeEntries));
            UpdateHeaderSortIndicators(); // Update header text with arrows
        }

        /// <summary>
        /// Updates the text of the header labels with sort arrows.
        /// </summary>
        private void UpdateHeaderSortIndicators()
        {
            // Reset all headers first
            _filenameHeaderLabel.Text = "Filename";
            _typeHeaderLabel.Text = "Type";
            _sizeHeaderLabel.Text = "Size";
            _uidHeaderLabel.Text = "UID";

            string arrow = _settings.ActiveSortOrder == SortOrder.Ascending ? " ▲" : " ▼";

            switch (_settings.ActiveSortColumn)
            {
                case SortColumn.Filename:
                    _filenameHeaderLabel.Text += arrow;
                    break;
                case SortColumn.Type:
                    _typeHeaderLabel.Text += arrow;
                    break;
                case SortColumn.Size:
                    _sizeHeaderLabel.Text += arrow;
                    break;
                case SortColumn.Uid:
                    _uidHeaderLabel.Text += arrow;
                    break;
            }
        }

        /// <summary>
        /// Creates a header TreeNode for the asset list.
        /// This is now primarily for internal use if needed, as the visual header is a separate Label.
        /// </summary>
        /// <returns>A TreeNode representing the header row.</returns>
        private TreeNode CreateHeaderNode()
        {
            // This method is no longer directly used to create a node for the TreeView,
            // as the header is now a separate FlowLayoutPanel.
            // Keeping it for potential future internal use or if some legacy code still calls it.
            return new TreeNode("Header Node - Not Visible");
        }

        /// <summary>
        /// Creates a TreeNode for an asset entry, handling FlatArchive expansion.
        /// </summary>
        /// <param name="o">The asset object (Entry or FlatArchiveEntry).</param>
        /// <returns>A TreeNode representing the asset.</returns>
        private TreeNode CreateAssetTreeNode(object o)
        {
            var meta = GetAssetMetaData(o);
            if (meta == null) return null;

            // Define column widths for a "CSV-like" appearance
            // These widths should match the header label widths for visual alignment
            const int filenameWidth = 40; // Corresponds to _filenameHeaderLabel.Width
            const int typeWidth = 15;     // Corresponds to _typeHeaderLabel.Width
            const int sizeWidth = 10;     // Corresponds to _sizeHeaderLabel.Width
            const int uidWidth = 18;      // Corresponds to _uidHeaderLabel.Width

            // Format filename, truncate if too long
            var filename = meta.Filename.Length > filenameWidth ? meta.Filename.Substring(0, filenameWidth - 3) + "..." : meta.Filename;
            filename = filename.PadRight(filenameWidth);

            // Format type
            var type = GetAssetTypeString(meta);
            type = type.Length > typeWidth ? type.Substring(0, typeWidth - 3) + "..." : type;
            type = type.PadRight(typeWidth);

            // Format size
            var size = ((o is Entry e) ? e.Size : (o is FlatArchiveEntry fae ? (uint)fae.PayloadLength : 0)).ToFileSizeString();
            size = size.Length > sizeWidth ? size.Substring(0, sizeWidth - 3) + "..." : size;
            size = size.PadRight(sizeWidth);

            // Format UID
            var uid = $"0x{meta.Uid:X16}";
            uid = uid.PadRight(uidWidth);


            var nodeText = $"{filename} {type} {size} {uid}";
            var node = new TreeNode(nodeText);
            node.Tag = o; // Store the original object

            // If it's a top-level Entry that is a FlatArchive, add dummy node for expansion
            if (o is Entry entry && MagicHelper.GetFiletype(entry.MetaData.FileType) == AssetType.FlatArchive)
            {
                // Add a dummy node to make it expandable
                node.Nodes.Add(new TreeNode("Loading..."));
            }

            return node;
        }

        /// <summary>
        /// Handles the BeforeExpand event for the TreeView to populate FlatArchive contents.
        /// </summary>
        private void AssetList_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            if (e.Node.Tag is Entry entry && MagicHelper.GetFiletype(entry.MetaData.FileType) == AssetType.FlatArchive)
            {
                // Clear the dummy node
                e.Node.Nodes.Clear();

                // Load and add actual FlatArchive entries
                var forge = _openedForges.FirstOrDefault(f => f.Entries.Any(fe => fe.Uid == entry.Uid));
                if (forge != null)
                {
                    var container = forge.GetContainer(entry.Uid);
                    if (container is ForgeAsset forgeAsset)
                    {
                        using var assetStream = forgeAsset.GetDataStream(forge);
                        var fa = FlatArchive.Read(assetStream, forge.Version);

                        foreach (var archiveEntry in fa.Entries) // Iterate all entries, including the first one
                        {
                            _flatArchiveEntryMap[archiveEntry.MetaData.Uid] = entry.Uid; // Map inner entry to outer archive UID
                            e.Node.Nodes.Add(CreateAssetTreeNode(archiveEntry));
                        }
                    }
                }
            }
        }


        /// <summary>
        /// Helper to get the display string for asset type.
        /// </summary>
        private string GetAssetTypeString(AssetMetaData meta)
        {
            if (Enum.IsDefined(typeof(Magic), meta.Magic))
            {
                var m = (Magic)meta.Magic;
                if (m == Magic.Metadata)
                {
                    // Find the forge that contains this entry
                    var forge = _openedForges.FirstOrDefault(f =>
                        f.Entries.Any(e => e.Uid == meta.Uid));

                    if (forge != null)
                    {
                        var container = forge.GetContainer(meta.Uid);
                        if (container is Hash)
                            return $"[{nameof(Hash)}]";
                        if (container is Descriptor)
                            return $"[{nameof(Descriptor)}]";
                        if (container is ForgeAsset)
                            return m.ToString();
                    }
                }
                return m.ToString();
            }
            return meta.Magic.ToString("X");
        }
    }

    // Extension method to apply theme to StatusStrip (moved here for better accessibility within the partial class)
    public static class StatusStripExtensions
    {
        public static void ApplyTheme(this StatusStrip statusStrip, Color backColor, Color foreColor)
        {
            if (statusStrip == null) return;
            statusStrip.BackColor = backColor;
            statusStrip.ForeColor = foreColor;
            foreach (ToolStripItem item in statusStrip.Items)
            {
                item.BackColor = backColor;
                item.ForeColor = foreColor;
            }
        }
    }
}
