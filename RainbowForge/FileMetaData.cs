using RainbowForge.Core;
using System;
using System.IO;
using System.Text;

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
            //Console.WriteLine($"Reading FileMetaData with version: {version}");

            switch (version)
            {
                case >= 32:
                    {

                        //var filenameLength = r.ReadUInt32();
                        //var filename = r.ReadBytes((int)filenameLength);
                        //var var1 = r.ReadUInt32();
                        //var fileType = r.ReadUInt32();
                        //var uid = r.ReadUInt64();
                        //var nameDecoded = NameEncoding.DecodeName(filename, fileType, uid, 0, NameEncoding.FILENAME_ENCODING_FILE_KEY_STEP);
                        //var name = Encoding.ASCII.GetString(nameDecoded);
                        //Console.WriteLine($"Version 30: filenameLength={filenameLength}, var1={var1}, fileType={fileType}, uid={uid}");
                        //Console.WriteLine($"Full name: {name}");
                        //return new FileMetaData(name, filename, var1, fileType, uid);
                        var filenameLength = r.ReadUInt16();
                        var var1 = r.ReadUInt16();
                        var var2 = r.ReadUInt32();
                        var filename = r.ReadBytes(filenameLength);
                        var fileType = r.ReadUInt32();
                        var uid = r.ReadUInt64();
                        var nameDecoded = NameEncoding.DecodeName(filename, fileType, uid, 0, NameEncoding.FILENAME_ENCODING_ENTRY_KEY_STEP);
                        var name = Encoding.ASCII.GetString(nameDecoded);
                        //Console.WriteLine($"File3: filenameLength={filenameLength}, var1={var1}, var2={var2}, fileType={fileType}, uid={uid}");
                        //Console.WriteLine($"Full name: {name}");
                        return new FileMetaData(name, filename, var1, fileType, uid);
                    }
                case 31:
                    {
                        var filenameLength = r.ReadUInt16();
                        var var1 = r.ReadUInt16();
                        var var2 = r.ReadUInt32();
                        var filename = r.ReadBytes((int)filenameLength);
                        var fileType = r.ReadUInt32();
                        var uid = r.ReadUInt64();
                        var name = Encoding.ASCII.GetString(filename);
                        Console.WriteLine($"Version 31: filenameLength={filenameLength}, var1={var1}, var2={var2}, fileType={fileType}, uid={uid}");
                        Console.WriteLine($"Full name: {name}");
                        return new FileMetaData(name, filename, var1, fileType, uid);
                    }
                case 30:
                    {
                        var filenameLength = r.ReadUInt32();
                        var filename = r.ReadBytes((int)filenameLength);
                        var var1 = r.ReadUInt32();
                        var fileType = r.ReadUInt32();
                        var uid = r.ReadUInt64();
                        var nameDecoded = NameEncoding.DecodeName(filename, fileType, uid, 0, NameEncoding.FILENAME_ENCODING_FILE_KEY_STEP);
                        var name = Encoding.ASCII.GetString(nameDecoded);
                        Console.WriteLine($"Version 30: filenameLength={filenameLength}, var1={var1}, fileType={fileType}, uid={uid}");
                        Console.WriteLine($"Full name: {name}");
                        return new FileMetaData(name, filename, var1, fileType, uid);
                    }
                case 29:
                    {
                        var fileType = r.ReadUInt32();
                        var var1 = r.ReadUInt32();
                        var var2 = r.ReadUInt32();
                        var uid = r.ReadUInt64();
                        Console.WriteLine($"Version 29: fileType={fileType}, var1={var1}, var2={var2}, uid={uid}");
                        return new FileMetaData("", Array.Empty<byte>(), var1, fileType, uid);
                    }
                default:
                    throw new NotImplementedException($"Unsupported version {version}");
            }
        }
    }
}
