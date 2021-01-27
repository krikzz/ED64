using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ed64usb
{
    public static class CommandProcessor
    {

        public const uint ROM_BASE_ADDRESS = 0x10000000; //X-Series only
        public const uint RAM_BASE_ADDRESS = 0x80000000; //X-Series only
        public const string MINIMUM_OS_VERSION = "3.05";
        public const int MAX_ROM_SIZE = 0x3DEC800; //TODO: find the max size.

        private enum TransmitCommand : byte
        {
            RomFillCartridgeSpace = (byte)'c', //char ROM fill 'c' artridge space
            RomRead = (byte)'R', //char ROM 'R' ead
            RomWrite = (byte)'W', // char ROM 'W' rite
            RomStart = (byte)'s', //char ROM 's' tart
            TestConnection = (byte)'t', //char 't' est

            RamRead = (byte)'r', //char RAM 'r' ead
            //RamWrite = (byte)'w', //char RAM 'w' rite
            FpgaWrite = (byte)'f' //char 'f' pga write

        }

        public enum ReceiveCommand : byte
        {
            CommsReply = (byte)'r',
            CommsReplyLegacy = (byte)'k',

        }

        /// <summary>
        /// Dumps the current framebuffer to a file in bitmap format
        /// </summary>
        /// <param name="filename">The file to be written to</param>
        public static void DumpScreenBuffer(string filename)
        {
            //TODO: the OS menu only currently supports 320x240 resolution, but should be read from the appropriate RAM register for forward compatibility! 
            // See https://n64brew.dev/wiki/Video_Interface for how this possibily could be improved.
            short width = 320; //TODO: the OS menu only currently supports 320x240 resolution, but should be read from the appropriate RAM register for forward compatibility! 
            short height = 240;

            var data = RamRead(0xA4400004, 512); // get the framebuffer address from its pointer in cartridge RAM (requires reading the whole 512 byte buffer, otherwise USB comms will fail)
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(data, 0, 4); //convert endian (we only need the first 4 bytes)
            }
            var framebufferAddress = BitConverter.ToInt32(data, 0);
            var length = width * height * 2;

            data = RamRead((uint)(RAM_BASE_ADDRESS | framebufferAddress), length); // Get the framebuffer data from cartridge RAM
            File.WriteAllBytes(filename, ImageUtilities.ConvertToBitmap(width, height, data));       
        }

        /// <summary>
        /// Dumps the current ROM to a file
        /// </summary>
        /// <param name="filename">The filename</param>
        public static void DumpRom(string filename, int size = MAX_ROM_SIZE)
        {
            if (size <= MAX_ROM_SIZE)
            {
                var data = RomRead(ROM_BASE_ADDRESS, size);
                //if (BitConverter.IsLittleEndian) //convert endian on Windows (to keep BigEndian)
                //{
                //    for (int i = 0; i < data.Length; i += 4)
                //    {
                //        Array.Reverse(data, i, 4);
                //       //TODO: could possibily trim the trailing zeros here!
                //    }
                //}

                File.WriteAllBytes(filename, data); //this is little endian on windows, not good!
            }
            else
            {
                Console.WriteLine("Unsupported ROM size.");
            }
        }

        /// <summary>
        /// Check that the ROM can be wriien and read.
        /// </summary>
        public static void RunDiagnostics()
        {
            byte[] writeBuffer = new byte[0x100000]; //create a 8MB array
            byte[] readBuffer;

            for (int i = 0; i < 0x800000; i += writeBuffer.Length) //for each 8MB in an 64MB range
            {
                new Random().NextBytes(writeBuffer); //randomly fill the 8MB array
                RomWrite(writeBuffer, ROM_BASE_ADDRESS);
                readBuffer = RomRead(ROM_BASE_ADDRESS, writeBuffer.Length);

                for (int u = 0; u < writeBuffer.Length; u++) //ensure that the bytes set match the bytes received.
                {
                    if (writeBuffer[u] != readBuffer[u]) throw new Exception("USB diagnostics error: " + (i + u));
                }
            }
        }

        /// <summary>
        /// Loads an FPGA RBF file
        /// </summary>
        /// <param name="filename">The filename to load</param>
        /// <returns></returns>
        public static bool LoadFpga(string filename)
        {
            var data = File.ReadAllBytes(filename);

            data = FixDataSize(data);
            CommandPacketTransmit(TransmitCommand.FpgaWrite, 0, data.Length, 0);

            UsbInterface.Write(data);
            var responseBytes = CommandPacketReceive();
            if (responseBytes[4] != 0)
            {
                throw new Exception($"FPGA configuration error: 0x{BitConverter.ToString(new byte[] { responseBytes[4] })}");
            }
            return true;
        }

        /// <summary>
        /// Loads a ROM
        /// </summary>
        /// <param name="filename">The filename to load</param>
        public static void LoadRom(string filename, bool diskDrive = false)
        {
            if (File.Exists(filename))
            {
                using (FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read))
                {
                    using (BinaryReader br = new BinaryReader(fs))
                    {
                        var romBytes = new List<byte>();
                        var baseAddress = ROM_BASE_ADDRESS;

                        if (diskDrive == true)
                        {
                            romBytes.AddRange(br.ReadBytes((int)fs.Length));
                        }
                        else
                        {
                            // We cannot rely on the filename for the format to be correct, so it is best to check the first 4 bytes of the ROM
                            var header = br.ReadUInt32(); // Reading the the bytes as a UInt32 simplifies the code below, but at the expense of changing the byte format.
                            br.BaseStream.Position = 0; // Reset the stream position for when we need to read the full ROM.

                            switch (header)
                            {
                                case 0x40123780: // BigEndian - Native (if reading the bytes in order, it would be 0x80371240)
                                    Console.Write("Rom format (BigEndian - Native).");
                                    // No Conversion necessary, just load the file.
                                    romBytes.AddRange(br.ReadBytes((int)fs.Length));
                                    break;
                                case 0x12408037: //Byte Swapped (if reading the bytes in order, it would be 0x37804012)
                                    Console.WriteLine("Rom format (Byte Swapped).");
                                    // Swap each 2 bytes to make it Big Endian
                                    {
                                        var chunk = br.ReadBytes(2).Reverse().ToArray();

                                        while (chunk.Length > 0)
                                        {
                                            romBytes.AddRange(chunk);
                                            chunk = br.ReadBytes(2).Reverse().ToArray();
                                        }

                                        romBytes.AddRange(chunk);
                                    }
                                    break;

                                case 0x80371240: // Little Endian (if reading the bytes in order, it would be 0x40123780)
                                    Console.WriteLine("Rom format (Little Endian).");
                                    // Reverse each 4 bytes to make it Big Endian
                                    {
                                        var chunk = br.ReadBytes(4).Reverse().ToArray();

                                        while (chunk.Length > 0)
                                        {
                                            romBytes.AddRange(chunk);
                                            chunk = br.ReadBytes(4).Reverse().ToArray();
                                        }

                                        romBytes.AddRange(chunk);
                                    }
                                    break;

                                default:
                                    Console.WriteLine("Unrecognised Rom Format: {0:X}, presuming emulator ROM.", header);
                                    baseAddress += 0x200000;
                                    break;
                            }

                            var fillValue = IsBootLoader(romBytes.ToArray()) ? 0xffffffff : 0;

                            FillCartridgeRomSpace(romBytes.ToArray().Length, fillValue);
                            RomWrite(romBytes.ToArray(), baseAddress);
                        }
                    }
            }
        }

        /// <summary>
        /// Reads the Cartridge ROM
        /// </summary>
        /// <param name="startAddress">The start address</param>
        /// <param name="length">The length to read</param>
        /// <returns></returns>
        private static byte[] RomRead(uint startAddress, int length)
        {

            CommandPacketTransmit(TransmitCommand.RomRead, startAddress, length, 0);

            UsbInterface.ProgressBarTimerInterval = length > 0x2000000 ? 0x100000 : 0x80000;
            var time = DateTime.Now.Ticks;
            var data = UsbInterface.Read(length);
            time = DateTime.Now.Ticks - time;
            Console.WriteLine($"OK. speed: {GetSpeedString(data.Length, time)}"); //TODO: this should be in the main program! or at least return the time!
            return data;
        }

        /// <summary>
        /// Reads the Cartridge RAM
        /// </summary>
        /// <param name="startAddress">The start address</param>
        /// <param name="length">The length to read</param>
        /// <returns></returns>
        private static byte[] RamRead(uint startAddress, int length)
        {

            CommandPacketTransmit(TransmitCommand.RamRead, startAddress, length, 0);

            Console.Write("Reading RAM...");
            UsbInterface.ProgressBarTimerInterval = length > 0x2000000 ? 0x100000 : 0x80000;
            var time = DateTime.Now.Ticks;
            var data = UsbInterface.Read(length);
            time = DateTime.Now.Ticks - time;
            Console.WriteLine($"OK. speed: {GetSpeedString(data.Length, time)}"); //TODO: this should be in the main program! or at least return the time!
            return data;
        }

        /// <summary>
        /// Writes to the cartridge ROM
        /// </summary>
        /// <param name="data">The data to write</param>
        /// <param name="startAddress">The start address</param>
        /// <returns></returns>
        private static byte[] RomWrite(byte[] data, uint startAddress)
        {

            var length = data.Length;

            CommandPacketTransmit(TransmitCommand.RomWrite, startAddress, length, 0);

            UsbInterface.ProgressBarTimerInterval = length > 0x2000000 ? 0x100000 : 0x80000;
            var time = DateTime.Now.Ticks;
            UsbInterface.Write(data);
            time = DateTime.Now.Ticks - time;

            Console.WriteLine($"OK. speed: {GetSpeedString(data.Length, time)}"); //TODO: this should be in the main program! or at least return the time!

            return data;
        }

        /// <summary>
        /// Starts a ROM on the cartridge
        /// </summary>
        /// <param name="fileName">The filename (optional)</param>
        /// <remarks> The filename (optional) is used for creating a save file on the SD card</remarks>
        public static void StartRom(string fileName = "")
        {

            if (fileName.Length < 256)
            {
                var filenameBytes = Encoding.ASCII.GetBytes(fileName);
                Array.Resize(ref filenameBytes, 256); //The packet must be 256 bytes in length, so resize it.

                CommandPacketTransmit(TransmitCommand.RomStart, 0, 0, 1);
                UsbInterface.Write(filenameBytes);
            }
            else
            { 
                throw new Exception("Filename exceeds the 256 character limit.");
            }

        }


        private static void FillCartridgeRomSpace(int romLength, uint value)
        {
            var crcArea = 0x100000 + 4096;
            if (romLength < crcArea)
            {

                Console.Write("Filling memory...");
                CommandPacketTransmit(TransmitCommand.RomFillCartridgeSpace, ROM_BASE_ADDRESS, crcArea, value);
                TestCommunication();
                Console.WriteLine("ok");
            }

        }

        /// <summary>
        /// Test that USB port is able to transmit and receive
        /// </summary>
        public static void TestCommunication()
        {
            CommandPacketTransmit(TransmitCommand.TestConnection);
            CommandPacketReceive();
        }

        private static bool IsBootLoader(byte[] data)
        {
            var bootloader = true;
            const string BOOT_MESSAGE = "EverDrive bootloader";
            for (int i = 0; i < BOOT_MESSAGE.ToCharArray().Length; i++)
            {
                if (BOOT_MESSAGE.ToCharArray()[i] != data[0x20 + i]) bootloader = false;
            }

            return bootloader;
        }


        /// <summary>
        /// Transmits a command to the USB port
        /// </summary>
        /// <param name="commandType">the command to send</param>
        /// <param name="address">Optional</param>
        /// <param name="length">Optional </param>
        /// <param name="argument">Optional</param>
        private static void CommandPacketTransmit(TransmitCommand commandType, uint address = 0, int length = 0, uint argument = 0)
        {
            length /= 512; //Must take into account buffer size.

            var commandPacket = new List<byte>();

            commandPacket.AddRange(Encoding.ASCII.GetBytes("cmd"));
            commandPacket.Add((byte)commandType);
            if (BitConverter.IsLittleEndian)
            { //Convert to Big Endian
                commandPacket.AddRange(BitConverter.GetBytes(address).Reverse());
                commandPacket.AddRange(BitConverter.GetBytes(length).Reverse());
                commandPacket.AddRange(BitConverter.GetBytes(argument).Reverse());
            }
            else
            {
                commandPacket.AddRange(BitConverter.GetBytes(address));
                commandPacket.AddRange(BitConverter.GetBytes(length));
                commandPacket.AddRange(BitConverter.GetBytes(argument));
            }

            UsbInterface.Write(commandPacket.ToArray());

        }

        /// <summary>
        /// Receives a command response from the USB port
        /// </summary>
        /// <returns>the full response in bytes</returns>
        private static byte[] CommandPacketReceive()
        {

            var cmd = UsbInterface.Read(16);
            if (Encoding.ASCII.GetString(cmd).ToLower().StartsWith("cmd") || Encoding.ASCII.GetString(cmd).ToLower().StartsWith("RSP"))
            {
                switch ((ReceiveCommand)cmd[3])
                {
                    case ReceiveCommand.CommsReply:
                        return cmd;
                    case ReceiveCommand.CommsReplyLegacy: //Certain ROM's may reply that used the old OSes without case sensitivity on the test commnad, this ensures they are handled.
                        throw new Exception($"Outdated OS, please update to {MINIMUM_OS_VERSION} or above!");
                    default:
                        throw new Exception("Unexpected response received from USB port.");
                }
            }
            else
            {
                throw new Exception("Corrupted response received from USB port.");
            }
        }



        private static string GetSpeedString(long length, long time)
        {
            time /= 10000;
            if (time == 0) time = 1;
            var speed = ((length / 1024) * 1000) / time;

            return ($"{speed} KB/s");

        }

        private static byte[] FixDataSize(byte[] data)
        {
            if (data.Length % 512 != 0)
            {
                var buff = new byte[data.Length / 512 * 512 + 512];
                for (int i = buff.Length - 512; i < buff.Length; i++)
                {
                    buff[i] = 0xff;
                }
                Array.Copy(data, 0, buff, 0, data.Length);

                return buff;
            }
            else
            {
                return data;
            }
        }
    }
}
