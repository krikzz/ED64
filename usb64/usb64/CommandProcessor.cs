using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ed64usb
{
    public static class CommandProcessor
    {

        public const uint ROM_BASE_ADDRESS = 0x10000000; //X-Series
        public const uint RAM_BASE_ADDRESS = 0x80000000; //X-Series

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
            //CommsReplyLegacy = (byte)'k', //TODO: tell users to update their old OS!

        }

        /// <summary>
        /// Dumps the current framebuffer to a file in bitmap format
        /// </summary>
        /// <param name="filename">The file to be written to</param>
        public static void DumpScreenBuffer(string filename)
        {
            short width = 320; //the OS menu only currently supports 320x240 resolution
            short height = 240;

            byte[] data = RamRead(0xA4400004, 512); // get the framebuffer address from its pointer in cartridge RAM
            int address = (data[0] << 24) | (data[1] << 16) | (data[2] << 8) | data[3]; //convert endian (TODO: is there a better way?)
            int length = width * height * 2;

            data = RamRead((uint)(RAM_BASE_ADDRESS | address), length); // Get the framebuffer data from cartridge RAM
            File.WriteAllBytes(filename, ImageUtilities.ConvertToBitmap(width, height, data));
        }

        /// <summary>
        /// Dumps the current ROM to a file
        /// </summary>
        /// <param name="filename">The filename</param>
        public static void DumpRom(string filename)
        {
            byte[] data = RomRead(ROM_BASE_ADDRESS, 0x101000); //1052672 bytes (just over 1MB) what about larger ROMs?
            File.WriteAllBytes(filename, data);

        }

        /// <summary>
        /// Check that the ROM can be wriien and read.
        /// </summary>
        public static void RunDiagnostics()
        {
            byte[] writeBuffer = new byte[0x100000];
            byte[] readBuffer;

            for (int i = 0; i < 0x800000; i += writeBuffer.Length)
            {
                new Random().NextBytes(writeBuffer);
                RomWrite(writeBuffer, ROM_BASE_ADDRESS);
                readBuffer = RomRead(ROM_BASE_ADDRESS, writeBuffer.Length);

                for (int u = 0; u < writeBuffer.Length; u++)
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
            byte[] data = File.ReadAllBytes(filename);

            data = FixDataSize(data);
            CommandPacketTransmit(TransmitCommand.FpgaWrite, 0, data.Length, 0);

            UsbInterface.Write(data);
            byte[] responseBytes = CommandPacketReceive(ReceiveCommand.CommsReply);
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
        public static void LoadRom(string filename)
        {
            if (File.Exists(filename))
            {
                using (FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read))
                {
                    using (BinaryReader br = new BinaryReader(fs))
                    {
                        List<byte> romBytes = new List<byte>();
                        uint baseAddress = ROM_BASE_ADDRESS;

                        // We cannot rely on the filename for the format to be correct, so it is best to check the first 4 bytes of the ROM
                        var header = br.ReadUInt32(); // Reading the the bytes as a UInt32 simplifies the code below, but at the expense of changing the byte format.
                        br.BaseStream.Position = 0; // Reset the stream position for when we need to read the full ROM.

                        switch (header)
                        {
                            case 0x40123780: // BigEndian - Native (if reading the bytes in order, it would be 0x80371240)
                                Console.WriteLine("Rom format (BigEndian - Native).");
                                // No Conversion necessary, just load the file.
                                romBytes.AddRange(br.ReadBytes((int)fs.Length));
                                break;
                            case 0x12408037: //Byte Swapped (if reading the bytes in order, it would be 0x37804012)
                                Console.WriteLine("Rom format (Byte Swapped).");
                                // Swap each 2 bytes to make it Big Endian
                                {
                                    byte[] chunk;
                                    chunk = br.ReadBytes(2).Reverse().ToArray();

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
                                    byte[] chunk;
                                    chunk = br.ReadBytes(4).Reverse().ToArray();

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

                        uint fillValue = IsBootLoader(romBytes.ToArray()) ? 0xffffffff : 0;
                        
                        FillCartridgeRomSpace(romBytes.ToArray().Length, fillValue);
                        RomWrite(romBytes.ToArray(), baseAddress);
                    }
                }
            }
        }


        public static void DebugCommand()
        {
            Console.WriteLine("Debug capabilities not implemented yet...!");
        }


        /// <summary>
        /// Reads the Cartridge ROM
        /// </summary>
        /// <param name="startAddress">The start address</param>
        /// <param name="length">The length to read</param>
        /// <returns></returns>
        public static byte[] RomRead(uint startAddress, int length)
        {

            CommandPacketTransmit(TransmitCommand.RomRead, startAddress, length, 0);

            UsbInterface.ProgressBarTimerInterval = length > 0x2000000 ? 0x100000 : 0x80000;
            long time = DateTime.Now.Ticks;
            byte[] data = UsbInterface.Read(length);
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
        public static byte[] RamRead(uint startAddress, int length)
        {

            CommandPacketTransmit(TransmitCommand.RamRead, startAddress, length, 0);

            Console.Write("Reading RAM...");
            UsbInterface.ProgressBarTimerInterval = length > 0x2000000 ? 0x100000 : 0x80000;
            long time = DateTime.Now.Ticks;
            byte[] data = UsbInterface.Read(length);
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
        public static byte[] RomWrite(byte[] data, uint startAddress)
        {

            int length = data.Length;

            CommandPacketTransmit(TransmitCommand.RomWrite, startAddress, length, 0);

            UsbInterface.ProgressBarTimerInterval = length > 0x2000000 ? 0x100000 : 0x80000;
            long time = DateTime.Now.Ticks;
            UsbInterface.Write(data);
            time = DateTime.Now.Ticks - time;

            Console.WriteLine($"OK. speed: {GetSpeedString(data.Length, time)}"); //TODO: this should be in the main program! or at least return the time!

            return data;
        }

        /// <summary>
        /// Starts a ROM on the cartridge
        /// </summary>
        /// <param name="fileName">The filename</param>
        public static void StartRom(string fileName)
        {

            if (fileName.Length < 256)
            {
                byte[] filenameBytes = Encoding.ASCII.GetBytes(fileName);
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
            int crcArea = 0x100000 + 4096;
            if (romLength >= crcArea) return;

            Console.Write("Filling memory...");
            CommandPacketTransmit(TransmitCommand.RomFillCartridgeSpace, ROM_BASE_ADDRESS, crcArea, value);
            TestCommunication();
            Console.WriteLine("ok");

        }

        /// <summary>
        /// Test that USB port is able to transmit and receive
        /// </summary>
        public static void TestCommunication()
        {
            CommandPacketTransmit(TransmitCommand.TestConnection);
            CommandPacketReceive(ReceiveCommand.CommsReply);
        }

        private static bool IsBootLoader(byte[] data)
        {
            bool bootloader = true;
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

            byte[] cmd = new byte[16];
            length /= 512;

            cmd[0] = (byte)'c';
            cmd[1] = (byte)'m';
            cmd[2] = (byte)'d';
            cmd[3] = (byte)commandType;

            cmd[4] = (byte)(address >> 24);
            cmd[5] = (byte)(address >> 16);
            cmd[6] = (byte)(address >> 8);
            cmd[7] = (byte)(address >> 0);

            cmd[8] = (byte)(length >> 24);
            cmd[9] = (byte)(length >> 16);
            cmd[10] = (byte)(length >> 8);
            cmd[11] = (byte)(length >> 0);

            cmd[12] = (byte)(argument >> 24);
            cmd[13] = (byte)(argument >> 16);
            cmd[14] = (byte)(argument >> 8);
            cmd[15] = (byte)(argument >> 0);

            //Console.WriteLine($"bitwise Command {BitConverter.ToString(cmd)}");

            UsbInterface.Write(cmd);

            // TODO: there is no reason why the below doesn't work, however it generally times out.
            //var commandPacket = new List<byte>();

            //commandPacket.AddRange(Encoding.ASCII.GetBytes("cmd"));
            //commandPacket.Add((byte)commandType);
            //commandPacket.AddRange(BitConverter.GetBytes(address).Reverse()); //Big Endian
            //commandPacket.AddRange(BitConverter.GetBytes(length).Reverse()); //Big Endian
            //commandPacket.AddRange(BitConverter.GetBytes(argument).Reverse()); //Big Endian
            //Console.WriteLine($"List Command {BitConverter.ToString(commandPacket.ToArray())}");

            //UsbInterface.Write(commandPacket.ToArray());


        }

        /// <summary>
        /// Receives a command response from the USB port
        /// </summary>
        /// <param name="responseType">the response type to validate</param>
        /// <returns>the full response in bytes</returns>
        private static byte[] CommandPacketReceive(ReceiveCommand receiveCommand)
        {

            byte[] cmd = UsbInterface.Read(16);
            if (cmd[0] != 'c') throw new Exception("Corrupted response.");
            if (cmd[1] != 'm') throw new Exception("Corrupted response.");
            if (cmd[2] != 'd') throw new Exception("Corrupted response.");
            if (cmd[3] != (byte)receiveCommand) throw new Exception("Unexpected response.");

            return cmd;

        }



        private static string GetSpeedString(long length, long time)
        {
            time /= 10000;
            if (time == 0) time = 1;
            long speed = ((length / 1024) * 1000) / time;

            return ($"{speed} KB/s");

        }

        private static byte[] FixDataSize(byte[] data)
        {
            if (data.Length % 512 != 0)
            {
                byte[] buff = new byte[data.Length / 512 * 512 + 512];
                for (int i = buff.Length - 512; i < buff.Length; i++) buff[i] = 0xff;
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
