using System;
using System.Collections.Generic;
using System.Text;

namespace ed64usb
{
    public static class CommandPacket
    {
        public enum Command : byte
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
    }
}
