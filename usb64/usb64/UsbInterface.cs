using System;
using System.IO.Ports;

namespace ed64usb
{
    public static class UsbInterface
    {

        private static SerialPort port;
        public static int pbar_interval = 0;
        public static int pbar_ctr = 0;

        // *************************** USB communication ***************************

        public static void Read(byte[] data, int offset, int length)
        {

            while (length > 0)
            {
                int block_size = 32768;
                if (block_size > length) block_size = length;
                int bytesread = port.Read(data, offset, block_size);
                length -= bytesread;
                offset += bytesread;
                PbarUpdate(bytesread);
            }

            PbarReset();
        }

        public static byte[] Read(int length)
        {
            byte[] data = new byte[length];
            Read(data, 0, data.Length);
            return data;

        }

        public static void Write(byte[] data, int offset, int len)
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

        public static void Write(byte[] data)
        {
            Write(data, 0, data.Length);
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



        // *************************** Serial port connection ***************************
        //TODO: move this back to the main class and call this class as non static (passing the serialport in)

        public static void Connect()
        {
            string[] ports = SerialPort.GetPortNames();

            foreach (var p in ports)
            {

                try
                {
                    port = new SerialPort(p);
                    port.Open();
                    port.ReadTimeout = 200;
                    port.WriteTimeout = 200;
                    CommandProcessor.TestCommunication();
                    port.ReadTimeout = 2000;
                    port.WriteTimeout = 2000;
                    Console.WriteLine($"Everdrive64 X-series found on serialport {p}");
                    return;
                }
                catch (Exception) { }

                ClosePort();

            }

            throw new Exception("Everdrive64 X-series device not found! \nCheck that the USB cable is connected and the console is powered on.");
        }

        public static void ClosePort()
        {
            if (port != null && port.IsOpen)
            {
                port.Close();
            }
        }


    }
}
