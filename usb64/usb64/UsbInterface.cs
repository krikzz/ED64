using System;
using System.IO.Ports;
using System.Text;

namespace ed64usb
{
    public static class UsbInterface
    {

        private static SerialPort port;
        public static int ProgressBarTimerInterval { get; set; }
        public static int ProgressBarTimerCounter { get; set; }

        const int DEFAULT_BLOCK_SIZE = 32768;


        public static void Read(byte[] data, int offset, int length, int blockSize = DEFAULT_BLOCK_SIZE)
        {

            while (length > 0)
            {
                if (blockSize > length) blockSize = length;
                var bytesread = port.Read(data, offset, blockSize);
                length -= bytesread;
                offset += bytesread;
                ProgressBarTimer_Update(bytesread);
            }

            ProgressBarTimer_Reset();
        }

        public static byte[] Read(int length)
        {
            var data = new byte[length];
            Read(data, 0, data.Length);
            return data;

        }

        public static void Read(byte[] data)
        {
            Read(data, 0, data.Length);
        }

        public static void Write(byte[] data, int offset, int length, int blockSize = DEFAULT_BLOCK_SIZE)
        {

            while (length > 0)
            {
                if (blockSize > length) blockSize = length;
                port.Write(data, offset, blockSize);
                length -= blockSize;
                offset += blockSize;
                ProgressBarTimer_Update(blockSize);
            }

            ProgressBarTimer_Reset();

        }

        public static void Write(byte[] data)
        {
            Write(data, 0, data.Length);
        }

        public static void Write(string str)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(str);
            Write(bytes);
        }

        private static void ProgressBarTimer_Update(int value)
        {
            if (ProgressBarTimerInterval != 0)
            {
                ProgressBarTimerCounter += value;
            }

            if (ProgressBarTimerCounter > ProgressBarTimerInterval)
            {
                ProgressBarTimerCounter -= ProgressBarTimerInterval;
                Console.Write(".");
            }
        }

        private static void ProgressBarTimer_Reset()
        {
            ProgressBarTimerInterval = 0;
            ProgressBarTimerCounter = 0;
        }



        // *************************** Serial port connection ***************************

        public static void Connect()
        {
            var ports = SerialPort.GetPortNames();

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
