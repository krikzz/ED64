using System;
using System.Text;
using System.Reflection;
using System.IO;

namespace ed64usb
{
    internal class Program
    {

        //public enum CartOsType
        //{
        //    V3_Official,
        //    V3_Unofficial,
        //    X7_Official,
        //    Unknown
        //}

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

        private static void DrawProgramHelp()
        {
            Console.WriteLine("___________________________________________");
            Console.WriteLine("Parameter list:");
            Console.WriteLine("___________________________________________");
            Console.WriteLine("A single parameter of:");
            Console.WriteLine("<ROM filename>");
            Console.WriteLine();
            Console.WriteLine("Single or multiple parameters consisting of:");
            Console.WriteLine();
            Console.WriteLine("-fpga=<filename>");
            Console.WriteLine("-rom=<filename>");
            Console.WriteLine("-start");
            Console.WriteLine("-diag");
            Console.WriteLine("-drom=<filename>");
            Console.WriteLine("-screen=<filename>");
            //Console.WriteLine("-debug");
            Console.WriteLine();


        }

        private static void DrawProgramFooter()
        {
            Console.WriteLine();
            Console.WriteLine("___________________________________________");
            Console.WriteLine("Get support at 'https://krikzz.com'");
        }

        private static void Main(string[] args)
        {
            try
            {
                DrawProgramHeader();

                UsbInterface.Connect();
                HandleArguments(args);

                

            }
            catch (Exception exception)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("");
                Console.WriteLine($"ERROR: {exception.Message}");
                Console.ResetColor();
            }
            finally
            {
                DrawProgramFooter();

                UsbInterface.ClosePort();
            }

        }

        ~Program()
        {
            UsbInterface.ClosePort();
        }


        private static void HandleArguments(string[] args)
        {

            var romFilePath = string.Empty;
            var startFileName = string.Empty;
            var startRom = false;
            var debugRom = false;

            long time = DateTime.Now.Ticks;



            if (args.Length == 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine();
                Console.WriteLine("No valid arguments were provided!");
                Console.ResetColor();
                DrawProgramHelp();
            }
            else
            {

                foreach (string arg in args)
                {
                    switch (arg)
                    {
                        case string x when x.StartsWith("-fpga"):
                            Console.Write("Sending FPGA config... ");
                            var result = CommandProcessor.LoadFpga(ExtractSubArg(arg));
                            if (result)
                            {
                                Console.WriteLine("Success.");
                            }
                            break;

                        case string x when x.StartsWith("-rom"):
                            Console.Write("Writing ROM, ");
                            romFilePath = ExtractSubArg(arg);
                            CommandProcessor.LoadRom(romFilePath);
                            break;

                        case string x when x.StartsWith("-start"):
                            var filename = ExtractSubArg(arg, true);
                            if ( !string.IsNullOrEmpty(filename))
                            {
                                startFileName = filename; //this allows specifying a save file of a different name to the loaded ROM.
                            }
                            startRom = true; //args could be in any order... wait until we have handled all arguments first.
                            break;

                        case string x when x.StartsWith("-diag"):
                            Console.WriteLine("Performing USB diagnostics...");
                            CommandProcessor.RunDiagnostics();
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine("USB diagnostics is complete!");
                            Console.ResetColor();
                            break;

                        case string x when x.StartsWith("-drom"):
                            Console.Write("Reading ROM...");
                            CommandProcessor.DumpRom(ExtractSubArg(arg));
                            break;

                        //case string x when x.StartsWith("-dram"):
                        //    Console.Write("Reading RAM...");
                        //    var startAddress = CommandProcessor.RAM_BASE_ADDRESS;
                        //    var length = 512; //first chunk //TODO: we would need to handle more than just the first 512 bytes!
                        //    CommandProcessor.RamRead(startAddress, length);
                        //    File.WriteAllBytes(ExtractSubArg(arg));
                        //    break;

                        case string x when x.StartsWith("-screen"):
                            Console.WriteLine("Reading Framebuffer.");
                            CommandProcessor.DumpScreenBuffer(ExtractSubArg(arg));
                            break;

                        case string x when x.StartsWith("-debug"):
                            debugRom = true;
                            break;

                        default:
                            if (arg.StartsWith("-"))
                            {
                                Console.WriteLine($"'{arg}' Not implemented yet... check parameter is valid!");
                                Console.WriteLine();
                                DrawProgramHelp();
                            }
                            else if (File.Exists(arg))
                            {
                                Console.WriteLine($"Presuming that '{Path.GetFileName(arg)}' is a valid ROM. Will attempt to load and start it.");
                                CommandProcessor.LoadRom(arg);
                                CommandProcessor.StartRom(Path.GetFileName(arg));
                            }
                            break;
                    }
                }

                if (debugRom)
                {
                    CommandProcessor.DebugCommand();
                }

                if (startRom)
                {
                    if (!string.IsNullOrEmpty(startFileName))
                    {
                        CommandProcessor.StartRom(startFileName);
                    }
                    else if (!string.IsNullOrEmpty(romFilePath))
                    {
                        CommandProcessor.StartRom(Path.GetFileName(romFilePath));
                    }
                    else
                    {
                        throw new Exception("Could not start ROM");
                    }
                    
                }
            }

            time = (DateTime.Now.Ticks - time) / 10000;
            Console.WriteLine("Finished in: {0:D}.{1:D3} seconds.", time / 1000, time % 1000);


        }

        private static string ExtractSubArg(string arg, bool optional = false, char delimiter = '=')
        {
            var subArg = arg.Substring(arg.IndexOf(delimiter) + 1);

            if (string.IsNullOrEmpty(subArg) && !optional)
            {
                throw new Exception($"The {arg} argument is incomplete!");
            }
            return subArg;
        }
    }
}
