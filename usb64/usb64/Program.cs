using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace ed64usb
{
    internal class Program
    {
        private static void DrawProgramHeader()
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine("___________________________________________");
            Console.WriteLine();
            Console.WriteLine($"EverDrive64 X-Series OS USB utility: V{Assembly.GetExecutingAssembly().GetName().Version}");
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
            Console.WriteLine("-fpga=<filename> (Loads specified FPGA file).");
            Console.WriteLine("-rom=<filename> (Loads specified ROM).");
            Console.WriteLine("-forcerom=<filename> (Loads specified ROM, even though it is not of a known type (e.g. 64dd).");
            Console.WriteLine("-start[=<ROM filename>] (Used for ROM save file. Only required when different from '-rom=<filename>').");
            Console.WriteLine("-diag (Runs communications diagnostics.");
            Console.WriteLine("-drom=<filename> (Dumps loaded ROM to PC).");
            Console.WriteLine("-screen=<filename> (Dumps framebuffer as BMP to PC).");
            Console.WriteLine("-cp <source filepath> <destination filepath> (Copies a file between devices.");
            Console.WriteLine("      i.e. SD:\\<filepath> to C:\\<filepath> OR C:\\<filepath> to SD:\\<filepath>.");
            //Console.WriteLine("-unfdebug (Runs the unf Debugger).");
            Console.WriteLine("-save=<savetype> (Runs the ROM with a save type when not matched in the internal database)");
            Console.WriteLine("      Options: [None,Eeprom4k,Eeprom16k,Sram,Sram768k,FlashRam,Sram128k].");
            Console.WriteLine("-extra=<RTC-RegionType> (Runs the ROM with RTC or forced region)");
            Console.WriteLine("      Options: [Off,Rtc,NoRegion,All].");
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

        /// <summary>
        /// Finalizer
        /// </summary>
        ~Program()
        {
            // We should never get into this point. Getting here is an error of the developer!
            Console.WriteLine($"Error - {GetType().FullName} was not properly disposed!, Closing now...");
            UsbInterface.ClosePort(); //But just incase, ensure the serialport is closed, even when program crashes!
        }


        private static void HandleArguments(string[] args)
        {

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
                var romFilePath = string.Empty;
                var saveType = DeveloperRom.SaveType.None;
                var extraInfo = DeveloperRom.ExtraInfo.Off;
                var startFileName = string.Empty;
                var forceRom = false;
                var loadRom = false;
                var startRom = false;
                //var unfDebug = false;

                var time = DateTime.UtcNow.Ticks;

                foreach (string arg in args)
                {
                    switch (arg)
                    {
                        // case string x when x.StartsWith("-unfdebug"):
                        //     unfDebug = true;
                        //     break;

                        case string x when x.StartsWith("-fpga"):
                            Console.Write("Sending FPGA config... ");
                            var result = CommandProcessor.LoadFpga(ExtractSubArg(arg));
                            if (result)
                            {
                                Console.WriteLine("Success.");
                            }
                            break;

                        case string x when x.StartsWith("-save"):
                            Console.Write("Configuring ROM Save, ");
                            saveType = (DeveloperRom.SaveType)Enum.Parse(typeof(DeveloperRom.SaveType), ExtractSubArg(arg));

                            break;

                        case string x when x.StartsWith("-extra"):
                            Console.Write("Configuring ROM Save, ");
                            extraInfo = (DeveloperRom.ExtraInfo)Enum.Parse(typeof(DeveloperRom.ExtraInfo), ExtractSubArg(arg));

                            break;

                        case string x when x.StartsWith("-rom"):
                            Console.Write("Writing ROM, ");
                            romFilePath = ExtractSubArg(arg);
                            loadRom = true;

                            break;

                        case string x when x.StartsWith("-forcerom"):
                            Console.Write("Writing unknown file to ROM space, "); //Stops loader thinking that the ROM is for an emulator. Useful for 64DD tests.
                            romFilePath = ExtractSubArg(arg);
                            forceRom = true;
                            loadRom = true;
                            break;

                        case string x when x.StartsWith("-start"):
                            var filename = ExtractSubArg(arg, true);
                            if (!string.IsNullOrEmpty(filename))
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

                        case string x when x.StartsWith("-screen"):
                            Console.WriteLine("Reading Framebuffer.");
                            CommandProcessor.DumpScreenBuffer(ExtractSubArg(arg));
                            break;

                        case string x when x.StartsWith("-cp"):
                            Console.WriteLine("Transferring file.");
                            //TODO: would not be able to handle spaces in path! Check escape using quotes.
                            Console.WriteLine($"Arg count = {args.Length}");
                            foreach (var str in args)
                            {
                                Console.WriteLine($"subarg = {str}");
                            }
                            CommandProcessor.TransferFile(args[1], args[2]);
                            break;

                        default:
                            if (arg.StartsWith("-"))
                            {
                                Console.WriteLine($"'{arg}' Not implemented yet... check parameter is valid!");
                                Console.WriteLine();
                                DrawProgramHelp();
                            }
                            else if (args.Length == 1 && File.Exists(args[0]))
                            {
                                Console.WriteLine($"Presuming that '{Path.GetFileName(args[0])}' is a valid ROM. Will attempt to load and start it.");
                                CommandProcessor.LoadRom(args[0]);
                                CommandProcessor.StartRom(Path.GetFileName(arg));
                            }
                            break;
                    }
                }

                if (loadRom)
                {
                    CommandProcessor.LoadRom(romFilePath, saveType, extraInfo, forceRom);
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
                        //throw new Exception("Could not start ROM");
                        CommandProcessor.StartRom();
                    }

                }

                time = (DateTime.UtcNow.Ticks - time) / 10000;
                Console.WriteLine("Finished in: {0:D}.{1:D3} seconds.", time / 1000, time % 1000);

                // if (unfDebug)
                // {
                //     Console.Write("Starting unf debug session, ");
                //     //var debug = new Unf.Debuggger(UsbInterface.port);
                // }
            }

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
