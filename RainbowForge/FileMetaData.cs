﻿using System;
using System.IO;
using System.Text;
using RainbowForge.Core;

namespace RainbowForge
{
    public class FileMetaData
    {
        public string FileName { get; }
        public byte[] EncodedMeta { get; }
        public uint ContainerType { get; }
        public uint FileType { get; }
        public ulong Uid { get; }

        private FileMetaData(string fileName, byte[] encodedMeta, uint containerType, uint fileType, ulong uid)
        {
            FileName = fileName;
            EncodedMeta = encodedMeta;
            ContainerType = containerType;
            FileType = fileType;
            Uid = uid;
        }

        public static FileMetaData Read(BinaryReader r, uint version)
        {
            switch (version)
            {
                case >= 32:
                    {
                        switch ((ContainerMagic)r.ReadUInt32())
                        {
                            case ContainerMagic.File2:
                            {
                                    var filenameLength = r.ReadUInt32();
                                    var filename = r.ReadBytes((int)filenameLength);
                                    var var1 = r.ReadUInt32();
                                    var fileType = r.ReadUInt32();
                                    var uid = r.ReadUInt64();

                                    var name = NameEncoding.DecodeName(filename, fileType, uid, 0, NameEncoding.FILENAME_ENCODING_FILE_KEY_STEP);
                                    return new FileMetaData(Encoding.ASCII.GetString(name), filename, var1, fileType, uid);
                            }
                            case ContainerMagic.File3:
                            {

                                    var filenameLength = r.ReadUInt16();
                                    var var1 = r.ReadUInt16();
                                    var var2 = r.ReadUInt32();
                                    var filename = r.ReadBytes((int)filenameLength);
                                    var fileType = r.ReadUInt32();
                                    var uid = r.ReadUInt64();

                                    var name = NameEncoding.DecodeName(filename, fileType, uid, 0, NameEncoding.FILENAME_ENCODING_FILE_KEY_STEP);
                                    return new FileMetaData(Encoding.ASCII.GetString(name), filename, var1, fileType, uid);
                            }
                            default:
                                throw new NotImplementedException($"Unsupported container magic");
                        }
                    }
                case 31:
                    {
                        switch ((ContainerMagic)r.ReadUInt32())
                        {
                            case ContainerMagic.File:
                                {
                                    var fileType = r.ReadUInt32();
                                    var var1 = r.ReadUInt32();
                                    var var2 = r.ReadUInt32();
                                    var uid = r.ReadUInt64();

                                    return new FileMetaData("", Array.Empty<byte>(), var1, fileType, uid);
                                }
                            case ContainerMagic.File2:
                                {
                                    var filenameLength = r.ReadUInt32();
                                    var filename = r.ReadBytes((int)filenameLength);
                                    var var1 = r.ReadUInt32();
                                    var fileType = r.ReadUInt32();
                                    var uid = r.ReadUInt64();

                                    var name = NameEncoding.DecodeName(filename, fileType, uid, 0, NameEncoding.FILENAME_ENCODING_FILE_KEY_STEP);
                                    return new FileMetaData(Encoding.ASCII.GetString(name), filename, var1, fileType, uid);
                                }
                            case ContainerMagic.File3:
                                {

                                    var filenameLength = r.ReadUInt16();
                                    var var1 = r.ReadUInt16();
                                    var var2 = r.ReadUInt32();
                                    var filename = r.ReadBytes((int)filenameLength);
                                    var fileType = r.ReadUInt32();
                                    var uid = r.ReadUInt64();

                                    var name = NameEncoding.DecodeName(filename, fileType, uid, 0, NameEncoding.FILENAME_ENCODING_FILE_KEY_STEP);
                                    return new FileMetaData(Encoding.ASCII.GetString(name), filename, var1, fileType, uid);
                                }
                            default:
                                throw new NotImplementedException($"Unsupported container magic");
                        }
                    }
                case 30:
                    {
                        switch ((ContainerMagic)r.ReadUInt32())
                        {
                            case ContainerMagic.File:
                                {
                                    var fileType = r.ReadUInt32();
                                    var var1 = r.ReadUInt32();
                                    var var2 = r.ReadUInt32();
                                    var uid = r.ReadUInt64();

                                    return new FileMetaData("", Array.Empty<byte>(), var1, fileType, uid);
                                }
                            case ContainerMagic.File2:
                                {
                                    var filenameLength = r.ReadUInt32();
                                    var filename = r.ReadBytes((int)filenameLength);
                                    var var1 = r.ReadUInt32();
                                    var fileType = r.ReadUInt32();
                                    var uid = r.ReadUInt64();

                                    var name = NameEncoding.DecodeName(filename, fileType, uid, 0, NameEncoding.FILENAME_ENCODING_FILE_KEY_STEP);
                                    return new FileMetaData(Encoding.ASCII.GetString(name), filename, var1, fileType, uid);
                                }
                            case ContainerMagic.File3:
                                {

                                    var filenameLength = r.ReadUInt16();
                                    var var1 = r.ReadUInt16();
                                    var var2 = r.ReadUInt32();
                                    var filename = r.ReadBytes((int)filenameLength);
                                    var fileType = r.ReadUInt32();
                                    var uid = r.ReadUInt64();

                                    var name = NameEncoding.DecodeName(filename, fileType, uid, 0, NameEncoding.FILENAME_ENCODING_FILE_KEY_STEP);
                                    return new FileMetaData(Encoding.ASCII.GetString(name), filename, var1, fileType, uid);
                                }
                            default:
                                throw new NotImplementedException($"Unsupported container magic");
                        }
                    }
                case <= 29:
                    {
                        switch ((ContainerMagic)r.ReadUInt32())
                        {
                            case ContainerMagic.File:
                                {
                                    var fileType = r.ReadUInt32();
                                    var var1 = r.ReadUInt32();
                                    var var2 = r.ReadUInt32();
                                    var uid = r.ReadUInt64();

                                    return new FileMetaData("", Array.Empty<byte>(), var1, fileType, uid);
                                }
                            case ContainerMagic.File2:
                                {
                                    var filenameLength = r.ReadUInt32();
                                    var filename = r.ReadBytes((int)filenameLength);
                                    var var1 = r.ReadUInt32();
                                    var fileType = r.ReadUInt32();
                                    var uid = r.ReadUInt64();

                                    var name = NameEncoding.DecodeName(filename, fileType, uid, 0, NameEncoding.FILENAME_ENCODING_FILE_KEY_STEP);
                                    return new FileMetaData(Encoding.ASCII.GetString(name), filename, var1, fileType, uid);
                                }
                            case ContainerMagic.File3:
                                {

                                    var filenameLength = r.ReadUInt16();
                                    var var1 = r.ReadUInt16();
                                    var var2 = r.ReadUInt32();
                                    var filename = r.ReadBytes((int)filenameLength);
                                    var fileType = r.ReadUInt32();
                                    var uid = r.ReadUInt64();

                                    var name = NameEncoding.DecodeName(filename, fileType, uid, 0, NameEncoding.FILENAME_ENCODING_FILE_KEY_STEP);
                                    return new FileMetaData(Encoding.ASCII.GetString(name), filename, var1, fileType, uid);
                                }
                            default:
                                throw new NotImplementedException($"Unsupported container magic");
                        }
                    }
                default:
                    throw new NotImplementedException($"Unsupported version {version}");
            }
        }
    }
}