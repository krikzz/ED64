using System;
using System.IO.Ports;

namespace ed64usb
{
    public static class UsbInterface
    {

        private static SerialPort port;
        public static int ProgressBarTimerInterval = 0;
        public static int ProgressBarTimerCounter = 0;


        public static void Read(byte[] data, int offset, int length)
        {

            while (length > 0)
            {
                int blockSize = 32768;
                if (blockSize > length) blockSize = length;
                int bytesread = port.Read(data, offset, blockSize);
                length -= bytesread;
                offset += bytesread;
                progressBarTimer_Update(bytesread);
            }

            progressBarTimer_Reset();
        }

        public static byte[] Read(int length)
        {
            byte[] data = new byte[length];
            Read(data, 0, data.Length);
            return data;

        }

        private static void Write(byte[] data, int offset, int length)
        {

            while (length > 0)
            {
                int blockSize = 32768;
                if (blockSize > length) blockSize = length;
                port.Write(data, offset, blockSize);
                length -= blockSize;
                offset += blockSize;
                progressBarTimer_Update(blockSize);
            }

            progressBarTimer_Reset();

        }

        public static void Write(byte[] data)
        {
            Write(data, 0, data.Length);
        }

        private static void progressBarTimer_Update(int value)
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

        private static void progressBarTimer_Reset()
        {
            ProgressBarTimerInterval = 0;
            ProgressBarTimerCounter = 0;
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
