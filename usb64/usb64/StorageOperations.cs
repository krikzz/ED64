using System;
using System.IO;

namespace ed64usb
{
    /// <summary>
    /// Operations for managing ED64 SD storage over USB
    /// </summary>
    public static class StorageOperations
    {
        const int DEFAULT_FILE_BLOCKSIZE = 4096;

        private enum FatFsReturnCode
        {
            /// <summary>
            /// (0) Succeeded
            /// </summary>
            FR_OK = 0,
            /// <summary>
            /// (1) A hard error occurred in the low level disk I/O layer
            /// </summary>
            FR_DISK_ERR,
            /// <summary>
            /// (2) Assertion failed
            /// </summary>
            FR_INT_ERR,
            /// <summary>
            /// (3) The physical drive cannot work
            /// </summary>
            FR_NOT_READY,
            /// <summary>
            /// (4) Could not find the file
            /// </summary>
            FR_NO_FILE,
            /// <summary>
            /// (5) Could not find the path
            /// </summary>
            FR_NO_PATH,
            /// <summary>
            /// (6) The path name format is invalid
            /// </summary>
            FR_INVALID_NAME,
            /// <summary>
            /// (7) Access denied due to prohibited access or directory full
            /// </summary>
            FR_DENIED,
            /// <summary>
            /// (8) Access denied due to prohibited access
            /// </summary>
            FR_EXIST,
            /// <summary>
            /// (9) The file/directory object is invalid
            /// </summary>
            FR_INVALID_OBJECT,
            /// <summary>
            /// (10) The physical drive is write protected
            /// </summary>
            FR_WRITE_PROTECTED,
            /// <summary>
            /// (11) The logical drive number is invalid
            /// </summary>
            FR_INVALID_DRIVE,
            /// <summary>
            /// (12) The volume has no work area
            /// </summary>
            FR_NOT_ENABLED,
            /// <summary>
            /// (13) There is no valid FAT volume
            /// </summary>
            FR_NO_FILESYSTEM,
            /// <summary>
            /// (14) The f_mkfs() aborted due to any parameter error
            /// </summary>
            FR_MKFS_ABORTED,
            /// <summary>
            /// (15) Could not get a grant to access the volume within defined period
            /// </summary>
            FR_TIMEOUT,
            /// <summary>
            /// (16) The operation is rejected according to the file sharing policy
            /// </summary>
            FR_LOCKED,
            /// <summary>
            /// (17) LFN working buffer could not be allocated
            /// </summary>
            FR_NOT_ENOUGH_CORE,
            /// <summary>
            /// (18) Number of open files > _FS_LOCK
            /// </summary>
            FR_TOO_MANY_OPEN_FILES,
            /// <summary>
            /// (19) Given parameter is invalid
            /// </summary>
            FR_INVALID_PARAMETER
        }

        [Flags]
        private enum FatFsFileAttributes : byte
        {
            ReadOnly = 0x01,
            Hidden = 0x02,
            System = 0x04,
            Directory = 0x10,
            Archive = 0x20
        }

        [Flags]
        private enum FatFsFileMode : byte
        {
            OpenExisting = 0x00,
            Read = 0x01,
            Write = 0x02,
            CreateNew = 0x04,
            CreateAlways = 0x08,
            OpenAlways = 0x10,
            OpenAppend = 0x30
        }

        private struct DosDateTime
        {
            public ushort Date;
            public ushort Time;

            public int Year
            {
                get => ((Date >> 9) & 0x7F) + 1980;
                set => Date = (ushort)((Date & 0x1FF) | ((value - 1980) << 9));
            }

            public int Month
            {
                get => (Date >> 5) & 0xF;
                set => Date = (ushort)((Date & 0xFE1F) | (value << 5));
            }

            public int Day
            {
                get => Date & 0x1F;
                set => Date = (ushort)((Date & 0xFFE0) | value);
            }

            public int Hour
            {
                get => (Time >> 11) & 0x1F;
                set => Time = (ushort)((Time & 0x7FF) | (value << 11));
            }

            public int Minute
            {
                get => (Time >> 5) & 0x3F;
                set => Time = (ushort)((Time & 0xF81F) | (value << 5));
            }

            public int Second
            {
                get => (Time & 0x1F) << 1;
                set => Time = (ushort)((Time & 0xFFE0) | (value >> 1));
            }
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

            ///// <summary>
            ///// The Date the file was last modified
            ///// </summary>
            ///// <remarks>
            ///// bit15:9
            ///// Year origin from 1980 (0..127)
            ///// bit8:5
            ///// Month(1..12)
            ///// bit4:0
            ///// Day(1..31)
            ///// </remarks>
            //public ushort ModifiedDate { get; set; } //TODO: Merge with time for DateTime (for C# goodness).

            ///// <summary>
            ///// The Time the file was last modified
            ///// </summary>
            ///// <remarks>
            ///// bit15:11
            ///// Hour(0..23)
            ///// bit10:5
            ///// Minute(0..59)
            ///// bit4:0
            ///// Second / 2 (0..29)
            ///// </remarks>
            //public ushort ModifiedTime { get; set; }

            /// <summary>
            /// The DateTime the file was last modified
            /// </summary>
            public DosDateTime ModifiedDateTime { get; set; }

            /// <summary>
            /// The file attributes
            /// </summary>
            public FatFsFileAttributes Attributes { get; set; }
        }

        /// <summary>
        /// Copies a file or directory between devices
        /// </summary>
        /// <param name="sourcePath">The source file or directory path</param>
        /// <param name="destinationPath">the destination file or directory path</param>
        public static void FileCopy(string sourcePath, string destinationPath)
        {
            sourcePath = sourcePath.Trim();
            destinationPath = destinationPath.Trim();
            if (!sourcePath.ToLower().StartsWith("sd:") && File.GetAttributes(sourcePath).HasFlag(FileAttributes.Directory))
            {
                DirectoryCopy(sourcePath, destinationPath);
                return;
            }
            if (destinationPath.EndsWith("/") || destinationPath.EndsWith("\\"))
            {
                destinationPath += Path.GetFileName(sourcePath);
            }
            Console.WriteLine($"copying file: {sourcePath} to {destinationPath}");
            byte[] fileData;
            if (sourcePath.ToLower().StartsWith("sd:"))
            {
                sourcePath = sourcePath.Substring(3); // remove "sd:" from path
                var fileinfo = GetFileInfo(sourcePath);
                Console.WriteLine($"FileInformation for {sourcePath}");
                Console.WriteLine($"  FileName {fileinfo.FileName}");
                Console.WriteLine($"  FileSize {fileinfo.FileSize}");
                Console.WriteLine($"  FileModified {new DateTime(fileinfo.ModifiedDateTime.Year, fileinfo.ModifiedDateTime.Month, fileinfo.ModifiedDateTime.Day, fileinfo.ModifiedDateTime.Hour, fileinfo.ModifiedDateTime.Minute, fileinfo.ModifiedDateTime.Second).ToString("o")}");
                Console.WriteLine($"  FileAttributes {fileinfo.Attributes}");
                fileData = new byte[fileinfo.FileSize];
                FileOpen(sourcePath, FatFsFileMode.Read);
                FileRead(fileData, 0, fileData.Length);
                FileClose();
            }
            else
            {
                fileData = File.ReadAllBytes(sourcePath);
            }
            if (destinationPath.ToLower().StartsWith("sd:"))
            {
                destinationPath = destinationPath.Substring(3); // remove "sd:" from path
                FileOpen(destinationPath, (FatFsFileMode)10); //(FatFsFileMode.CreateAlways | FatFsFileMode.Write)); //TODO: was 10, so presuming 0x0A to mean CreateAlways + Write!
                FileWrite(fileData, 0, fileData.Length);
                FileClose();
            }
            else
            {
                File.WriteAllBytes(destinationPath, fileData);
            }
        }

        private static void DirectoryCopy(string sourceDirectory, string destinationDirectory)
        {
            if (!sourceDirectory.EndsWith("/"))
            {
                sourceDirectory += "/";
            }
            if (!destinationDirectory.EndsWith("/"))
            {
                destinationDirectory += "/";
            }
            string[] directories = Directory.GetDirectories(sourceDirectory);
            for (int i = 0; i < directories.Length; i++)
            {
                DirectoryCopy(directories[i], destinationDirectory + Path.GetFileName(directories[i]));
            }
            string[] files = Directory.GetFiles(sourceDirectory);
            for (int j = 0; j < files.Length; j++)
            {
                FileCopy(files[j], destinationDirectory + Path.GetFileName(files[j]));
            }
        }


        private static void FileOpen(string filePath, FatFsFileMode fileMode)
        {
            CommandProcessor.CommandPacketTransmit(CommandProcessor.TransmitCommand.FileOpen, 0, filePath.Length, (uint)fileMode); //todo: check conversion of mode
            UsbInterface.Write(filePath);
            var response = CommandProcessor.TestCommunication();
            if (response != (int)FatFsReturnCode.FR_OK)
            {
                throw new Exception($"File open error: {((FatFsReturnCode)response).ToString()}");
            }
        }

        private static void FileRead(byte[] fileData, int offset, int fileLength)
        {
            CommandProcessor.CommandPacketTransmit(CommandProcessor.TransmitCommand.FileRead, 0, fileLength, 0);
            UsbInterface.Read(fileData, offset, fileLength, DEFAULT_FILE_BLOCKSIZE);

            var response = CommandProcessor.TestCommunication();
            if (response != (int)FatFsReturnCode.FR_OK)
            {
                throw new Exception($"File read error: {((FatFsReturnCode)response).ToString()}");
            }
        }

        private static void FileWrite(byte[] fileData, int offset, int fileLength)
        {
            CommandProcessor.CommandPacketTransmit(CommandProcessor.TransmitCommand.FileWrite, 0, fileLength, 0);
            UsbInterface.Write(fileData, offset, fileLength, DEFAULT_FILE_BLOCKSIZE);

            var response = CommandProcessor.TestCommunication();
            if (response != (int)FatFsReturnCode.FR_OK)
            {
                throw new Exception($"File write error: {((FatFsReturnCode)response).ToString()}");
            }
        }

        private static void FileClose()
        {
            CommandProcessor.CommandPacketTransmit(CommandProcessor.TransmitCommand.FileClose);
            var response = CommandProcessor.TestCommunication();
            if (response != (int)FatFsReturnCode.FR_OK)
            {
                throw new Exception($"File close error: {((FatFsReturnCode)response).ToString()}");
            }
        }

        private static FileInformation GetFileInfo(string path)
        {

            CommandProcessor.CommandPacketTransmit(CommandProcessor.TransmitCommand.FileInfo, 0, path.Length, 0);
            UsbInterface.Write(path);
            var responseBytes = CommandProcessor.CommandPacketReceive();
            if (responseBytes[4] != (int)FatFsReturnCode.FR_OK)
            {
                throw new Exception($"File access error: { ((FatFsReturnCode)responseBytes[4]).ToString()}");
            }

            return new FileInformation()
            {
                Attributes = (FatFsFileAttributes)responseBytes[5],
                // TODO: what are the 2 bytes that are not decoded?
                FileSize = Int32FromBytes(responseBytes, 8), //responseBytes @ offset 8 (4 bytes)
                ModifiedDateTime = new DosDateTime()
                {
                    Date = UInt16FromBytes(responseBytes, 12), //responseBytes @ offset 12 (2 bytes)
                    Time = UInt16FromBytes(responseBytes, 14) //responseBytes @ offset 14 (2 bytes)
                }
            };
        }

        private static int Int32FromBytes(byte[] data, int offset) //TODO: probably a better way to convert endian.
        {
            //byte[] tempBytes = new byte[4];
            //Array.Copy(data, offset, tempBytes, 0, 4);
            //Array.Reverse(tempBytes);
            //return BitConverter.ToInt32(tempBytes);

            return data[offset + 3] | (data[offset + 2] << 8) | (data[offset + 1] << 16) | (data[offset] << 24);
        }

        private static ushort UInt16FromBytes(byte[] data, int offset) //TODO: probably a better way to convert endian.
        {
            //byte[] tempBytes = new byte[2];
            //Array.Copy(data, offset, tempBytes, 0, 2);
            //Array.Reverse(tempBytes);
            //return BitConverter.ToUInt16(tempBytes);
            
            return (ushort)(data[offset + 1] | (data[offset] << 8));
        }
    }
}
