using System;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Reflection;

namespace usb64
{
    internal class Program
    {
        private static SerialPort port;
        private static int pbar_interval = 0;
        private static int pbar_ctr = 0;

        private static void Main(string[] args)
        {

            Console.OutputEncoding = Encoding.UTF8;

            Console.WriteLine($"usb64 version:{ Assembly.GetEntryAssembly().GetName().Version }");

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

            string rom_name = "default-usb-rom.v64";


            Connect();


            long time = DateTime.Now.Ticks;

            for (int i = 0; i < args.Length; i++)
            {

                if (args[i].StartsWith("-fpga"))
                {
                    CmdFpga(args[i]);
                }

                if (args[i].StartsWith("-rom"))
                {
                    rom_name = ExtractArg(args[i]);
                    CmdLoadRom(args[i]);
                }

                if (args[i].StartsWith("-start"))
                {
                    UsbCmdStartRom(rom_name);
                }

                if (args[i].StartsWith("-diag"))
                {
                    CmdDiagnostics();
                }

                if (args[i].StartsWith("-drom"))
                {
                    CmdDumpRom(args[i]);
                }

                if (args[i].StartsWith("-screen"))
                {
                    CmdDumpScreenBuffer(args[i]);
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
            File.WriteAllBytes(arg, ImageUtilities.ConvertToBitmap(320,240, data));
        }

        private static void CmdDumpRom(string cmd)
        {
            byte [] data = UsbCmdRomRead(0x10000000, 0x101000);
            string arg = ExtractArg(cmd);
            File.WriteAllBytes(arg, data);

        }

        private static void CmdDiagnostics()
        {
            byte[] writeBuffer = new byte[0x100000];
            byte[] readBuffer;

            

            Console.WriteLine("USB diagnostics...");
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
                    Console.WriteLine($"ED64 found on commport {ports_list[i]}");
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

            throw new Exception("ED64 not found");
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

        private static void UsbCmdTx(char usb_cmd)
        {
            UsbCmdTx(usb_cmd, 0, 0, 0);
        }

        private static void UsbCmdCmemFill(int rom_len, uint val) //I am guessing this stands for cartridge memory?
        {
            int crc_area = 0x100000 + 4096;
            if (rom_len >= crc_area) return;

            Console.Write("Filling memory...");
            UsbCmdTx('c', 0x10000000, crc_area, val);
            UsbCmdTest();
            Console.WriteLine("ok");

        }

        private static void UsbCmdTx(char usb_cmd, uint addr, int len, uint arg)
        {
            
            byte[] cmd = new byte[16];
            len /= 512;

            cmd[0] = (byte)'c';
            cmd[1] = (byte)'m';
            cmd[2] = (byte)'d';
            cmd[3] = (byte)usb_cmd;

            cmd[4] = (byte)(addr >> 24);
            cmd[5] = (byte)(addr >> 16);
            cmd[6] = (byte)(addr >> 8);
            cmd[7] = (byte)(addr >> 0);

            cmd[8] = (byte)(len >> 24);
            cmd[9] = (byte)(len >> 16);
            cmd[10] = (byte)(len >> 8);
            cmd[11] = (byte)(len >> 0);

            cmd[12] = (byte)(arg >> 24);
            cmd[13] = (byte)(arg >> 16);
            cmd[14] = (byte)(arg >> 8);
            cmd[15] = (byte)(arg >> 0);

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

            UsbCmdTx('s', 0, 0, 1);

            UsbWrite(buff);
        }

        private static void UsbCmdFpga(byte[] data)
        {
            data = FixDataSize(data);
            UsbCmdTx('f', 0, data.Length, 0);

            Console.Write("FPGA config.");
            UsbWrite(data);
            byte []resp = UsbCmdRx('r');
            if (resp[4] != 0) throw new Exception($"FPGA configuration error: 0x{BitConverter.ToString(new byte[] { resp[4] })}");
            Console.WriteLine("ok");
        }

        private static byte[] UsbCmdRx(char usb_cmd)
        {

            byte[] cmd = UsbRead(16);
            if (cmd[0] != 'c') throw new Exception("Corrupted response");
            if (cmd[1] != 'm') throw new Exception("Corrupted response");
            if (cmd[2] != 'd') throw new Exception("Corrupted response");
            if (cmd[3] != usb_cmd) throw new Exception("Unexpected response");

            return cmd;

        }

        private static void UsbCmdTest()
        {
            UsbCmdTx('t');
            UsbCmdRx('r');
        }

        /// <summary>
        /// Reads the Cartridge ROM
        /// </summary>
        /// <param name="startAddress">The start address</param>
        /// <param name="length">The length to read</param>
        /// <returns></returns>
        private static byte[] UsbCmdRomRead(uint startAddress, int length)
        {

            UsbCmdTx('R', startAddress, length, 0);

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

            UsbCmdTx('r', startAddress, length, 0);

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

            UsbCmdTx('W', startAddress, len, 0);

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
