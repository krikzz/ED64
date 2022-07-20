using System;
using System.IO;

namespace ed64usb
{
    public static class StorageOperations
    {

        [Flags]
        private enum FatFsFileMode : byte
        {
            OpenExisting = 0x00,
            Read = 0x01,
            Write = 0x02,
            CreateNew = 0x04,
            CreateAlways = 0x08,
            OpenAlways = 0x10, //16,
            OpenAppend = 0x30  //48
        }

        private struct FileInformation
        {
            /// <summary>
            /// The name of the file.
            /// </summary>
            public string FileName { get; set; }

            /// <summary>
            /// The size of the file.
            /// </summary>
            public int FileSize { get; set; }

            public ushort ModifiedDate { get; set; } //TODO: Merge with time for DateTime (for C# goodness).

            public ushort ModifiedTime { get; set; }

            public byte Attributes { get; set; }
        }

        public static void FileCopy(string srcPath, string dstPath)
        {
            srcPath = srcPath.Trim();
            dstPath = dstPath.Trim();
            if (!srcPath.ToLower().StartsWith("sd:") && File.GetAttributes(srcPath).HasFlag(FileAttributes.Directory))
            {
                DirectoryCopy(srcPath, dstPath);
                return;
            }
            if (dstPath.EndsWith("/") || dstPath.EndsWith("\\"))
            {
                dstPath += Path.GetFileName(srcPath);
            }
            Console.WriteLine($"copying file: {srcPath} to {dstPath}");
            byte[] fileData;
            if (srcPath.ToLower().StartsWith("sd:"))
            {
                srcPath = srcPath.Substring(3); // remove "sd:" from path
                fileData = new byte[GetFileInfo(srcPath).FileSize];
                FileOpen(srcPath, FatFsFileMode.Read);
                FileRead(fileData, 0, fileData.Length);
                FileClose();
            }
            else
            {
                fileData = File.ReadAllBytes(srcPath);
            }
            if (dstPath.ToLower().StartsWith("sd:"))
            {
                dstPath = dstPath.Substring(3); // remove "sd:" from path
                FileOpen(dstPath, (FatFsFileMode.CreateAlways | FatFsFileMode.Write)); //TODO: was 10, so presuming 0x0A to mean CreateAlways + Write!
                FileWrite(fileData, 0, fileData.Length);
                FileClose();
            }
            else
            {
                File.WriteAllBytes(dstPath, fileData);
            }
        }

        private static void DirectoryCopy(string srcDir, string dstDir)
        {
            if (!srcDir.EndsWith("/"))
            {
                srcDir += "/";
            }
            if (!dstDir.EndsWith("/"))
            {
                dstDir += "/";
            }
            string[] directories = Directory.GetDirectories(srcDir);
            for (int i = 0; i < directories.Length; i++)
            {
                DirectoryCopy(directories[i], dstDir + Path.GetFileName(directories[i]));
            }
            string[] files = Directory.GetFiles(srcDir);
            for (int j = 0; j < files.Length; j++)
            {
                FileCopy(files[j], dstDir + Path.GetFileName(files[j]));
            }
        }


        private static void FileOpen(string path, FatFsFileMode mode)
        {
            CommandProcessor.CommandPacketTransmit(CommandProcessor.TransmitCommand.FileInfo, 0, path.Length, (uint)mode); //todo: check conversion of mode
            UsbInterface.Write(path);
            var response = CommandProcessor.TestCommunication();
            if (response != 0)
            {
                throw new Exception($"File open error: 0x{BitConverter.ToString(new byte[] { response })}");
            }
        }

        private static void FileRead(byte[] buff, int offset, int length)
        {
            CommandProcessor.CommandPacketTransmit(CommandProcessor.TransmitCommand.FileRead, 0, length, 0);
            while (length > 0)
            {
                int blockSize = 4096;
                if (blockSize > length)
                {
                    blockSize = length;
                }
                UsbInterface.Read(buff, offset, blockSize);
                offset += blockSize;
                length -= blockSize;
            }
            var response = CommandProcessor.TestCommunication();
            if (response != 0)
            {
                throw new Exception($"File read error: 0x{BitConverter.ToString(new byte[] { response })}");
            }
        }

        private static void FileWrite(byte[] buff, int offset, int length)
        {
            CommandProcessor.CommandPacketTransmit(CommandProcessor.TransmitCommand.FileWrite, 0, length, 0);
            while (length > 0)
            {
                int blockSize = 4096;
                if (blockSize > length)
                {
                    blockSize = length;
                }
                UsbInterface.Write(buff, offset, blockSize);
                offset += blockSize;
                length -= blockSize;
            }
            var response = CommandProcessor.TestCommunication();
            if (response != 0)
            {
                throw new Exception($"File write error: 0x{BitConverter.ToString(new byte[] { response })}");
            }
        }

        private static void FileClose()
        {
            CommandProcessor.CommandPacketTransmit(CommandProcessor.TransmitCommand.FileClose);
            var response = CommandProcessor.TestCommunication();
            if (response != 0)
            {
                throw new Exception($"File close error: 0x{BitConverter.ToString(new byte[] { response })}");
            }
        }

        private static FileInformation GetFileInfo(string path)
        {

            CommandProcessor.CommandPacketTransmit(CommandProcessor.TransmitCommand.FileInfo);
            UsbInterface.Write(path);
            var responseBytes = CommandProcessor.CommandPacketReceive();
            if (responseBytes[4] != 0)
            {
                throw new Exception($"File access error: 0x{BitConverter.ToString(new byte[] { responseBytes[4] })}");
            }

            return new FileInformation()
            {
                Attributes = responseBytes[5],
                FileSize = IntegerFromBytes(responseBytes, 8), //responseBytes @ offset 8 (4 bytes)
                ModifiedDate = UshortFromBytes(responseBytes, 12), //responseBytes @ offset 12 (2 bytes)
                ModifiedTime = UshortFromBytes(responseBytes, 14) //responseBytes @ offset 14 (2 bytes)
            };
        }

        private static int IntegerFromBytes(byte[] data, int offset) //TODO: probably a better way to convert endian.
        {
            //BitConverter.GetBytes(data).Reverse();
            return data[offset + 3] | (data[offset + 2] << 8) | (data[offset + 1] << 16) | (data[offset] << 24);
        }

        private static ushort UshortFromBytes(byte[] data, int offset) //TODO: probably a better way to convert endian.
        {
            return (ushort)(data[offset + 1] | (data[offset] << 8));
        }

    }
}
