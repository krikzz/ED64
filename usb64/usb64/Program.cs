using System;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Reflection;

namespace ed64usb
{
    internal class Program
    {
        private static SerialPort port;
        private static int pbar_interval = 0;
        private static int pbar_ctr = 0;

        private static void DrawProgramHeader()
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine("___________________________________________");
            Console.WriteLine();
            Console.WriteLine($"EverDrive64 x-series USB utility: V{Assembly.GetExecutingAssembly().GetName().Version}");
            Console.WriteLine("___________________________________________");
            Console.ResetColor();
        }

        private static void Main(string[] args)
        {

            DrawProgramHeader();

            try
            {
                Usb64(args);

            }
            catch (Exception exception)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("");
                Console.WriteLine($"ERROR: {exception.Message}");
                Console.ResetColor();
            }

            try
            {
                if (port != null && port.IsOpen)
                {
                    port.Close();
                }
            }
            catch (Exception) { };
        }

        private static void Usb64(string[] args)
        {

            var rom_name = string.Empty;


            Connect();


            long time = DateTime.Now.Ticks;

            foreach (string arg in args)
            {
                switch (arg)
                {
                    case string x when x.StartsWith("-fpga"):
                        CmdFpga(arg);
                        break;

                    case string x when x.StartsWith("-rom"):
                        rom_name = ExtractArg(arg);
                        CmdLoadRom(arg);
                        break;

                    case string x when x.StartsWith("-start"):
                        if (rom_name != string.Empty)
                        {
                            UsbCmdStartRom(rom_name);  //TODO: args could be in any order... need to handle
                        }
                        break;

                    case string x when x.StartsWith("-diag"):
                        CmdDiagnostics();
                        break;

                    case string x when x.StartsWith("-drom"):
                        CmdDumpRom(arg);
                        break;

                    case string x when x.StartsWith("-screen"):
                        CmdDumpScreenBuffer(arg);
                        break;

                    case string x when x.StartsWith("-debug"):
                        //EnterDebugMode();
                        Console.WriteLine("Not implemented yet...");
                        break;

                    default:
                        Console.WriteLine("Not implemented yet...");
                        break;
                }
            }

            time = (DateTime.Now.Ticks - time) / 10000;
            Console.WriteLine("timezone: {0:D}.{1:D3}", time / 1000, time % 1000);


        }

        private static void CmdDumpScreenBuffer(string cmd)
        {

            byte[] data = UsbCmdRamRead(0xA4400004, 512);// Get the scrreen buffer fom cartridge RAM

            int addr = (data[0] << 24) | (data[1] << 16) | (data[2] << 8) | data[3];
            int len = 320 * 240 * 2; //the Menu only supports 320x240 resolution
            string arg = ExtractArg(cmd);

            data = UsbCmdRamRead((uint)(0x80000000 | addr), len);
            File.WriteAllBytes(arg, ImageUtilities.ConvertToBitmap(320, 240, data));
        }

        private static void CmdDumpRom(string cmd)
        {
            byte[] data = UsbCmdRomRead(0x10000000, 0x101000);
            string arg = ExtractArg(cmd);
            File.WriteAllBytes(arg, data);

        }

        private static void CmdDiagnostics()
        {
            byte[] writeBuffer = new byte[0x100000];
            byte[] readBuffer;



            Console.WriteLine("Performing USB diagnostics...");
            for (int i = 0; i < 0x800000; i += writeBuffer.Length)
            {
                new Random().NextBytes(writeBuffer);
                UsbCmdRomWrite(writeBuffer, 0x10000000);
                readBuffer = UsbCmdRomRead(0x10000000, writeBuffer.Length);

                for (int u = 0; u < writeBuffer.Length; u++)
                {
                    if (writeBuffer[u] != readBuffer[u]) throw new Exception("USB diagnostics error: " + (i + u));
                }

            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("USB diagnostics is complete!");
            Console.ResetColor();

        }

        private static void Connect()
        {
            string[] ports_list = SerialPort.GetPortNames();

            for (int i = 0; i < ports_list.Length; i++)
            {

                try
                {
                    port = new SerialPort(ports_list[i]);
                    port.Open();
                    port.ReadTimeout = 200;
                    port.WriteTimeout = 200;
                    UsbCmdTest();
                    port.ReadTimeout = 2000;
                    port.WriteTimeout = 2000;
                    Console.WriteLine($"Everdrive64 X-series found on commport {ports_list[i]}");
                    return;
                }
                catch (Exception) { }

                try
                {
                    port.Close();
                    port = null;
                }
                catch (Exception) { }

            }

            throw new Exception("Everdrive64 X-series device not found! \nCheck that the USB cable is connected and the console is powered on.");
        }

        private static void CmdFpga(string cmd)
        {
            string arg = ExtractArg(cmd);
            byte[] data = File.ReadAllBytes(arg);
            UsbCmdFpga(data);
        }

        private static void CmdLoadRom(string cmd)
        {

            string fileName = ExtractArg(cmd);
            byte[] data = File.ReadAllBytes(fileName);
            bool is_emulator_rom;


            if ((data[0] == 0x80 && data[1] == 0x37) || (data[1] == 0x80 && data[0] == 0x37))
            {
                is_emulator_rom = false;
            }
            else
            {
                is_emulator_rom = true;
            }

            uint fill_val = IsBootLoader(data) ? 0xffffffff : 0;
            uint base_addr = 0x10000000;
            if (is_emulator_rom) base_addr += 0x200000;

            UsbCmdCmemFill(data.Length, fill_val);
            UsbCmdRomWrite(data, base_addr);
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

        private static string ExtractArg(string cmd)
        {
            return cmd.Substring(cmd.IndexOf("=") + 1);
        }

        // *************************** ED64 USB commands ***************************


        private static void UsbCmdCmemFill(int rom_len, uint val) //I am guessing this stands for cartridge memory?
        {
            int crc_area = 0x100000 + 4096;
            if (rom_len >= crc_area) return;

            Console.Write("Filling memory...");
            UsbCmdTransmit('c', 0x10000000, crc_area, val);
            UsbCmdTest();
            Console.WriteLine("ok");

        }

        /// <summary>
        /// Transmits a command to the USB port
        /// </summary>
        /// <param name="commandType">the command to send</param>
        /// <param name="address">Optional</param>
        /// <param name="length">Optional </param>
        /// <param name="argument">Optional</param>
        private static void UsbCmdTransmit(char commandType, uint address = 0, int length = 0, uint argument = 0)
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

            port.Write(cmd, 0, cmd.Length);
        }

        /// <summary>
        /// Starts a ROM on the cartridge
        /// </summary>
        /// <param name="fileName">The filename</param>
        private static void UsbCmdStartRom(string fileName)
        {
            byte[] fname_bytes = Encoding.ASCII.GetBytes(fileName);
            if (fileName.Length >= 256) throw new Exception("file name is too long");
            byte[] buff = new byte[256];
            Array.Copy(fname_bytes, 0, buff, 0, fname_bytes.Length);

            UsbCmdTransmit('s', 0, 0, 1);

            UsbWrite(buff);
        }

        private static void UsbCmdFpga(byte[] data)
        {
            data = FixDataSize(data);
            UsbCmdTransmit('f', 0, data.Length, 0);

            Console.Write("FPGA config.");
            UsbWrite(data);
            byte[] resp = UsbCmdReceive('r');
            if (resp[4] != 0) throw new Exception($"FPGA configuration error: 0x{BitConverter.ToString(new byte[] { resp[4] })}");
            Console.WriteLine("ok");
        }

        /// <summary>
        /// Receives a command response from the USB port
        /// </summary>
        /// <param name="responseType">the response type to validate</param>
        /// <returns>the full response in bytes</returns>
        private static byte[] UsbCmdReceive(char responseType)
        {

            byte[] cmd = UsbRead(16);
            if (cmd[0] != 'c') throw new Exception("Corrupted response.");
            if (cmd[1] != 'm') throw new Exception("Corrupted response.");
            if (cmd[2] != 'd') throw new Exception("Corrupted response.");
            if (cmd[3] != responseType) throw new Exception("Unexpected response.");

            return cmd;

        }

        /// <summary>
        /// Test that USB port is able to transmit and receive
        /// </summary>
        private static void UsbCmdTest()
        {
            UsbCmdTransmit('t');
            UsbCmdReceive('r');
        }

        /// <summary>
        /// Reads the Cartridge ROM
        /// </summary>
        /// <param name="startAddress">The start address</param>
        /// <param name="length">The length to read</param>
        /// <returns></returns>
        private static byte[] UsbCmdRomRead(uint startAddress, int length)
        {

            UsbCmdTransmit('R', startAddress, length, 0);

            Console.Write("ROM READ.");
            pbar_interval = length > 0x2000000 ? 0x100000 : 0x80000;
            long time = DateTime.Now.Ticks;
            byte[] data = UsbRead(length);
            time = DateTime.Now.Ticks - time;
            Console.WriteLine($"OK. speed: {GetSpeedString(data.Length, time)}");
            return data;
        }

        /// <summary>
        /// Reads the Cartridge RAM
        /// </summary>
        /// <param name="startAddress">The start address</param>
        /// <param name="length">The length to read</param>
        /// <returns></returns>
        private static byte[] UsbCmdRamRead(uint startAddress, int length)
        {

            UsbCmdTransmit('r', startAddress, length, 0);

            Console.Write("RAM READ.");
            pbar_interval = length > 0x2000000 ? 0x100000 : 0x80000;
            long time = DateTime.Now.Ticks;
            byte[] data = UsbRead(length);
            time = DateTime.Now.Ticks - time;
            Console.WriteLine($"OK. speed: {GetSpeedString(data.Length, time)}");
            return data;
        }

        /// <summary>
        /// Writes to the cartridge ROM
        /// </summary>
        /// <param name="data">The data to write</param>
        /// <param name="startAddress">The start address</param>
        /// <returns></returns>
        private static byte[] UsbCmdRomWrite(byte[] data, uint startAddress)
        {

            int len = data.Length;

            UsbCmdTransmit('W', startAddress, len, 0);

            Console.Write("ROM WR.");
            pbar_interval = len > 0x2000000 ? 0x100000 : 0x80000;
            long time = DateTime.Now.Ticks;
            UsbWrite(data, 0, len);
            time = DateTime.Now.Ticks - time;

            Console.WriteLine($"OK. speed: {GetSpeedString(data.Length, time)}");

            return data;
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

        // *************************** USB communication ***************************

        private static void UsbRead(byte[] data, int offset, int len)
        {

            while (len > 0)
            {
                int block_size = 32768;
                if (block_size > len) block_size = len;
                int bytesread = port.Read(data, offset, block_size);
                len -= bytesread;
                offset += bytesread;
                PbarUpdate(bytesread);
            }

            PbarReset();
        }

        private static byte[] UsbRead(int len)
        {
            byte[] data = new byte[len];
            UsbRead(data, 0, data.Length);
            return data;

        }

        private static void UsbWrite(byte[] data, int offset, int len)
        {

            while (len > 0)
            {
                int block_size = 32768;
                if (block_size > len) block_size = len;
                port.Write(data, offset, block_size);
                len -= block_size;
                offset += block_size;
                PbarUpdate(block_size);
            }

            PbarReset();

        }

        private static void UsbWrite(byte[] data)
        {
            UsbWrite(data, 0, data.Length);
        }

        private static void PbarUpdate(int val)
        {
            if (pbar_interval == 0) return;
            pbar_ctr += val;
            if (pbar_ctr < pbar_interval) return;

            pbar_ctr -= pbar_interval;
            Console.Write(".");
        }

        private static void PbarReset()
        {
            pbar_interval = 0;
            pbar_ctr = 0;
        }
    }
}
