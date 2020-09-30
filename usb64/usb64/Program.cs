using System;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Reflection;

namespace usb64
{
    class Program
    {

        static SerialPort port;
        static int pbar_interval = 0;
        static int pbar_ctr = 0;


        static void Main(string[] args)
        {

            Console.OutputEncoding = System.Text.Encoding.UTF8;

            Console.WriteLine("usb64 v" + Assembly.GetEntryAssembly().GetName().Version);

            try
            {
                usb64(args);

            }
            catch (Exception x)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("");
                Console.WriteLine("ERROR: " + x.Message);
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

        static void usb64(string[] args)
        {

            string rom_name = "default-usb-rom.v64";


            connect();


            long time = DateTime.Now.Ticks;

            for (int i = 0; i < args.Length; i++)
            {

                if (args[i].StartsWith("-fpga"))
                {
                    cmdFpga(args[i]);
                }

                if (args[i].StartsWith("-rom"))
                {
                    rom_name = extractArg(args[i]);
                    cmdLoadRom(args[i]);
                }

                if (args[i].StartsWith("-start"))
                {
                    usbCmdStartApp(rom_name);
                }

                if (args[i].StartsWith("-diag"))
                {
                    cmdDiag();
                }

                if (args[i].StartsWith("-drom"))
                {
                    cmdDumpRom(args[i]);
                }

                if (args[i].StartsWith("-screen"))
                {
                    cmdDumpScreen(args[i]);
                }


            }

            time = (DateTime.Now.Ticks - time) / 10000;
            Console.WriteLine("timez: {0:D}.{1:D3}", time / 1000, time % 1000);


        }

        static void cmdDumpScreen(string cmd)
        {

            byte[] data = usbCmdRamRD(0xA4400004, 512);//get get scrreen buffer address
            //Console.WriteLine(BitConverter.ToString(data));

            int addr = (data[0] << 24) | (data[1] << 16) | (data[2] << 8) | data[3];
            int len = 320 * 240 * 2;
            string arg = extractArg(cmd);

            data = usbCmdRamRD((UInt32)(0x80000000 | addr), len);

            File.WriteAllBytes(arg, data);
        }


        static void cmdDumpRom(string cmd)
        {
            byte [] data = usbCmdRomRD(0x10000000, 0x101000);
            string arg = extractArg(cmd);
            File.WriteAllBytes(arg, data);

        }

        static void cmdDiag()
        {
            byte[] buff1 = new byte[0x100000];
            byte[] buff2;

            

            Console.WriteLine("USB diag...");
            for (int i = 0; i < 0x800000; i += buff1.Length)
            {
                new Random().NextBytes(buff1);
                usbCmdRomWR(buff1, 0x10000000);
                buff2 = usbCmdRomRD(0x10000000, buff1.Length);

                for (int u = 0; u < buff1.Length; u++)
                {
                    if (buff1[u] != buff2[u]) throw new Exception("USB diag error: " + (i + u));
                }

               // Console.Write(".");
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("USB diag is complete!");
            Console.ResetColor();

        }

        static void connect()
        {
            string[] ports_list = SerialPort.GetPortNames();

            for(int i = 0;i < ports_list.Length; i++)
            {

                try
                {
                    port = new SerialPort(ports_list[i]);
                    port.Open();
                    port.ReadTimeout = 200;
                    port.WriteTimeout = 200;
                    usbCmdTest();
                    port.ReadTimeout = 2000;
                    port.WriteTimeout = 2000;
                    Console.WriteLine("ED64 found at port " + ports_list[i]);
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

        static bool isBootLoader(byte[] data)
        {
            bool bootloader = true;
            string boot_str = "EverDrive bootloader";
            for (int i = 0; i < boot_str.ToCharArray().Length; i++)
            {
                if (boot_str.ToCharArray()[i] != data[0x20 + i]) bootloader = false;
            }

            return bootloader;
        }


        static void cmdFpga(string cmd)
        {
            string arg = extractArg(cmd);
            byte[] data = File.ReadAllBytes(arg);
            usbCmdFpga(data);
        }

        static void cmdLoadRom(string cmd)
        {

            string fname = extractArg(cmd);
            byte[] data = File.ReadAllBytes(fname);
            bool is_emu_rom;


            if ((data[0] == 0x80 && data[1] == 0x37) || (data[1] == 0x80 && data[0] == 0x37))
            {
                is_emu_rom = false;
            }
            else
            {
                is_emu_rom = true;
            }

            UInt32 fill_val = isBootLoader(data) ? 0xffffffff : 0;
            UInt32 base_addr = 0x10000000;
            if (is_emu_rom) base_addr += 0x200000;

            usbCmdCmemFill(data.Length, fill_val);
            usbCmdRomWR(data, base_addr);
        }

        static string extractArg(string cmd)
        {
            return cmd.Substring(cmd.IndexOf("=") + 1);
        }

        //********************************************************************************* ED64 USB commands
        //*********************************************************************************

        static void usbCmdTx(char usb_cmd)
        {
            usbCmdTx(usb_cmd, 0, 0, 0);
        }

       

        static void usbCmdCmemFill(int rom_len, UInt32 val)
        {
            int crc_area = 0x100000 + 4096;
            if (rom_len >= crc_area) return;

            Console.Write("Fill mem...");
            usbCmdTx('c', 0x10000000, crc_area, val);
            usbCmdTest();
            Console.WriteLine("ok");

        }

        static void usbCmdTx(char usb_cmd, UInt32 addr, int len, UInt32 arg)
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

        static void usbCmdStartApp(string fname)
        {
            byte[] fname_bytes = Encoding.ASCII.GetBytes(fname);
            if (fname.Length >= 256) throw new Exception("file name is too long");
            byte[] buff = new byte[256];
            Array.Copy(fname_bytes, 0, buff, 0, fname_bytes.Length);

            //usbCmdTx('s');
            usbCmdTx('s', 0, 0, 1);

            usbWrite(buff);
        }

        static void usbCmdFpga(byte[] data)
        {
            data = fixDataSize(data);
            usbCmdTx('f', 0, data.Length, 0);

            Console.Write("FPGA config.");
            usbWrite(data);
            byte []resp = usbCmdRx('r');
            if (resp[4] != 0) throw new Exception("FPGA configuration error: 0x" + BitConverter.ToString(new byte[] { resp[4] }));
            Console.WriteLine("ok");
        }

        static byte[] usbCmdRx(char usb_cmd)
        {

            byte[] cmd = usbRead(16);
            if (cmd[0] != 'c') throw new Exception("Corrupted response");
            if (cmd[1] != 'm') throw new Exception("Corrupted response");
            if (cmd[2] != 'd') throw new Exception("Corrupted response");
            if (cmd[3] != usb_cmd) throw new Exception("Unexpected response");

            return cmd;

        }

        static void usbCmdTest()
        {
            usbCmdTx('t');
            usbCmdRx('r');
        }

        static byte[] usbCmdRomRD(UInt32 addr, int len)
        {

            usbCmdTx('R', addr, len, 0);

            Console.Write("ROM RD.");
            pbar_interval = len > 0x2000000 ? 0x100000 : 0x80000;
            long time = DateTime.Now.Ticks;
            byte[] data = usbRead(len);
            time = DateTime.Now.Ticks - time;
            Console.WriteLine("ok. speed: " + getSpeedStr(data.Length, time));
            return data;
        }

        static byte[] usbCmdRamRD(UInt32 addr, int len)
        {

            usbCmdTx('r', addr, len, 0);

            Console.Write("RAM RD.");
            pbar_interval = len > 0x2000000 ? 0x100000 : 0x80000;
            long time = DateTime.Now.Ticks;
            byte[] data = usbRead(len);
            time = DateTime.Now.Ticks - time;
            Console.WriteLine("ok. speed: " + getSpeedStr(data.Length, time));
            return data;
        }


        static byte[] usbCmdRomWR(byte[] data, UInt32 addr)
        {

            int len = data.Length;

            usbCmdTx('W', addr, len, 0);

            Console.Write("ROM WR.");
            pbar_interval = len > 0x2000000 ? 0x100000 : 0x80000;
            long time = DateTime.Now.Ticks;
            usbWrite(data, 0, len);
            time = DateTime.Now.Ticks - time;

            Console.WriteLine("ok. speed: " + getSpeedStr(data.Length, time));

            return data;
        }

        static string getSpeedStr(long len, long time)
        {
            time /= 10000;
            if (time == 0) time = 1;
            long speed = ((len / 1024) * 1000) / time;

            return ("" + speed + " KB/s");

        }


        static byte[] fixDataSize(byte []data)
        {
            if (data.Length % 512 == 0) return data;
            byte[] buff = new byte[data.Length / 512 * 512 + 512];
            for (int i = buff.Length - 512; i < buff.Length; i++) buff[i] = 0xff;
            Array.Copy(data, 0, buff, 0, data.Length);

            return buff;
        }

        //********************************************************************************* USB communication
        //*********************************************************************************

        static void usbRead(byte[] data, int offset, int len)
        {

            while (len > 0)
            {
                int block_size = 32768;
                if (block_size > len) block_size = len;
                int readed = port.Read(data, offset, block_size);
                len -= readed;
                offset += readed;
                pbarUpdate(readed);
            }

            pbarReset();
        }


        static byte[] usbRead(int len)
        {
            byte[] data = new byte[len];
            usbRead(data, 0, data.Length);
            return data;

        }


        static void usbWrite(byte[] data, int offset, int len)
        {

            while (len > 0)
            {
                int block_size = 32768;
                if (block_size > len) block_size = len;
                port.Write(data, (int)offset, (int)block_size);
                len -= block_size;
                offset += block_size;
                pbarUpdate(block_size);
            }

            pbarReset();

        }

        static void usbWrite(byte[] data)
        {
            usbWrite(data, 0, data.Length);
        }

        static void pbarUpdate(int val)
        {
            if (pbar_interval == 0) return;
            pbar_ctr += val;
            if (pbar_ctr < pbar_interval) return;

            pbar_ctr -= pbar_interval;
            Console.Write(".");
        }

        static void pbarReset()
        {
            pbar_interval = 0;
            pbar_ctr = 0;
        }
    }
}
