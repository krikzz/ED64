using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Ports;
using System.Text;

namespace ed64usb
{
    /// <summary>
    /// SafeSerialPort solves the issue of streams being unable to be closed 
    /// following a sudden disconnect to a port. 
    /// </summary>
    public class SafeSerialPort : SerialPort
    {
        private Stream baseStream;

        public SafeSerialPort()
            : base() { }
        public SafeSerialPort(string portName)
            : base(portName) { }

        public new void Open()
        {
            base.Open();
            baseStream = BaseStream;
            GC.SuppressFinalize(BaseStream);
        }

        public new void Dispose()
        {
            Dispose(true);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && (base.Container != null))
            {
                base.Container.Dispose();
            }
            try
            {
                if (baseStream.CanRead)
                {
                    baseStream.Close();
                    GC.ReRegisterForFinalize(baseStream);
                }
            }
            catch { } // ignore exception - bug with USB - serial adapters.

            base.Dispose(disposing);
        }
    }
}
