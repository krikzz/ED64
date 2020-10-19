using System;
using System.IO;
using System.IO.Ports;
using System.Text;

namespace ed64usb
{
    public static class CommandProcessor
    {

        public const uint ROM_BASE_ADDRESS = 0x10000000;
        public const uint RAM_BASE_ADDRESS = 0x80000000;

        private enum Command : byte
        {
            FormatRomMemory = (byte)'c', //char format 'c' artridge memory?
            RomRead = (byte)'R', //char ROM 'R' ead
            RomWrite = (byte)'W', // char ROM 'W' rite
            RomStart = (byte)'s', //char ROM 's' tart
            TestConnection = (byte)'t', //char 't' est

            RamRead = (byte)'r', //char RAM 'r' ead
            //RamWrite = (byte)'w', //char RAM 'w' rite
            FpgaWrite = (byte)'f' //char 'f' pga write

        }

        /// <summary>
        /// Dumps the current framebuffer to a file in bitmap format
        /// </summary>
        /// <param name="filename">The file to be written to</param>
        public static void DumpScreenBuffer(string filename)
        {
            short width = 320; //the menu only currently supports 320x240 resolution
            short height = 240;

            byte[] data = RamRead(0xA4400004, 512); // get the framebuffer address from its pointer in cartridge RAM
            int address = (data[0] << 24) | (data[1] << 16) | (data[2] << 8) | data[3]; //convert endian (TODO: is this correct on linux?)
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
            byte[] data = RomRead(ROM_BASE_ADDRESS, 0x101000);
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
            UsbCmdTransmit(CommandProcessor.Command.FpgaWrite, 0, data.Length, 0);

            UsbInterface.Write(data);
            byte[] responseBytes = UsbCmdReceive('r');
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
            byte[] data = File.ReadAllBytes(filename);
            bool isEmulatorROM;


            if ((data[0] == 0x80 && data[1] == 0x37) || (data[1] == 0x80 && data[0] == 0x37)) //check the file header matches a valid N64 ROM.
            {
                isEmulatorROM = false;
            }
            else
            {
                isEmulatorROM = true;
            }

            uint fillValue = IsBootLoader(data) ? 0xffffffff : 0;
            uint baseAddress = ROM_BASE_ADDRESS;
            if (isEmulatorROM) baseAddress += 0x200000;

            FormatRomMemory(data.Length, fillValue);
            RomWrite(data, baseAddress);
        }

        /// <summary>
        /// Reads the Cartridge ROM
        /// </summary>
        /// <param name="startAddress">The start address</param>
        /// <param name="length">The length to read</param>
        /// <returns></returns>
        public static byte[] RomRead(uint startAddress, int length)
        {

            UsbCmdTransmit(CommandProcessor.Command.RomRead, startAddress, length, 0);

            UsbInterface.pbar_interval = length > 0x2000000 ? 0x100000 : 0x80000;
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

            UsbCmdTransmit(CommandProcessor.Command.RamRead, startAddress, length, 0);

            Console.Write("Reading RAM...");
            UsbInterface.pbar_interval = length > 0x2000000 ? 0x100000 : 0x80000;
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

            UsbCmdTransmit(CommandProcessor.Command.RomWrite, startAddress, length, 0);

            UsbInterface.pbar_interval = length > 0x2000000 ? 0x100000 : 0x80000;
            long time = DateTime.Now.Ticks;
            UsbInterface.Write(data, 0, length);
            time = DateTime.Now.Ticks - time;

            Console.WriteLine($"OK. speed: {GetSpeedString(data.Length, time)}");

            return data;
        }

        /// <summary>
        /// Starts a ROM on the cartridge
        /// </summary>
        /// <param name="fileName">The filename</param>
        public static void StartRom(string fileName)
        {
            
            if (fileName.Length >= 256)
            {
                throw new Exception("Filename exceeds the 256 character limit.");
            }
            byte[] fname_bytes = Encoding.ASCII.GetBytes(fileName);
            byte[] buff = new byte[256];
            Array.Copy(fname_bytes, 0, buff, 0, fname_bytes.Length); //TODO: why do we need to go around the house here? (copying something that is already an array, except perhaps it needs to be fixed to 256?!

            UsbCmdTransmit(CommandProcessor.Command.RomStart, 0, 0, 1);

            UsbInterface.Write(buff);
        }


        private static void FormatRomMemory(int romLength, uint value)
        {
            int crcArea = 0x100000 + 4096;
            if (romLength >= crcArea) return;

            Console.Write("Filling memory...");
            UsbCmdTransmit(CommandProcessor.Command.FormatRomMemory, ROM_BASE_ADDRESS, crcArea, value);
            TestCommunication();
            Console.WriteLine("ok");

        }

        /// <summary>
        /// Test that USB port is able to transmit and receive
        /// </summary>
        public static void TestCommunication()
        {
            UsbCmdTransmit(CommandProcessor.Command.TestConnection);
            UsbCmdReceive('r');
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


        // *************************** ED64 USB commands ***************************




        /// <summary>
        /// Transmits a command to the USB port
        /// </summary>
        /// <param name="commandType">the command to send</param>
        /// <param name="address">Optional</param>
        /// <param name="length">Optional </param>
        /// <param name="argument">Optional</param>
        private static void UsbCmdTransmit(CommandProcessor.Command commandType, uint address = 0, int length = 0, uint argument = 0)
        {

            byte[] cmd = new byte[16];
            length /= 512;

            cmd[0] = (byte)'c';
            cmd[1] = (byte)'m';
            cmd[2] = (byte)'d';
            cmd[3] = (byte)commandType;
            //TODO: any implications from linux for below?
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

            UsbInterface.Write(cmd, 0, cmd.Length); //TODO: any implications if we switch to UsbWrite()???
        }

        /// <summary>
        /// Receives a command response from the USB port
        /// </summary>
        /// <param name="responseType">the response type to validate</param>
        /// <returns>the full response in bytes</returns>
        private static byte[] UsbCmdReceive(char responseType)
        {

            byte[] cmd = UsbInterface.Read(16);
            if (cmd[0] != 'c') throw new Exception("Corrupted response.");
            if (cmd[1] != 'm') throw new Exception("Corrupted response.");
            if (cmd[2] != 'd') throw new Exception("Corrupted response.");
            if (cmd[3] != responseType) throw new Exception("Unexpected response.");

            return cmd;

        }



        private static string GetSpeedString(long len, long time)
        {
            time /= 10000;
            if (time == 0) time = 1;
            long speed = ((len / 1024) * 1000) / time;

            return ($"{speed} KB/s");

        }

        private static byte[] FixDataSize(byte[] data)
        {
            if (data.Length % 512 == 0) return data;
            byte[] buff = new byte[data.Length / 512 * 512 + 512];
            for (int i = buff.Length - 512; i < buff.Length; i++) buff[i] = 0xff;
            Array.Copy(data, 0, buff, 0, data.Length);

            return buff;
        }
    }
}
