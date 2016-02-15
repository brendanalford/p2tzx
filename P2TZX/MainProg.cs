using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace P2TZX
{
    class MainProg
    {
        static bool batchConv = false;
        static bool doSubdirs = false;

        static void Main(string[] args)
        {
            Console.WriteLine("P2TZX - Command line ZX81 .P to .TZX file conversion tool");
            Console.WriteLine("(C) Brendan Alford 2016 (brendanalford@eircom.net)\n");

            bool debug = System.Diagnostics.Debugger.IsAttached;

            if (args.Length == 0 && !debug)
            {
                DisplayUsage();
                return;
            }

            string fileName = debug ? "crash.p" : ParseCommandLine(args);
           

            IConverter converter = new TZXConverter();
            try
            {
                if (batchConv)
                {
                    string[] files;
                    files = Directory.GetFiles(Directory.GetCurrentDirectory(), fileName, 
                        doSubdirs ? 
                        SearchOption.AllDirectories : 
                        SearchOption.TopDirectoryOnly);

                    foreach (string file in files)
                    {
                        converter.Convert(file);
                    }
                }
                else
                {
                    converter.Convert(fileName);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("ERROR: " + e.Message);
            }
        }

        static string ParseCommandLine(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i][0] == '-')
                {
                    string param = args[i].Substring(1).ToLower();
                    if (param.CompareTo("b") == 0 ||
                        param.CompareTo("batch") == 0)
                    {
                        batchConv = true;
                    }
                    if (param.CompareTo("s") == 0 ||
                        param.CompareTo("subdir") == 0)
                    {
                        doSubdirs = true;
                    }

                }
                else
                {
                    if (i + 1 < args.Length)
                    {
                        Console.Write("Too many parameters!");
                        DisplayUsage();
                        Environment.Exit(0);
                    }
                }
            }
            return args[args.Length - 1].ToLower();
        }

        static void DisplayUsage()
        {
            Console.WriteLine("\nUsage: P2TZX <options> <filename>\n");
            Console.WriteLine("Valid options are:");
            Console.WriteLine("-b, -batch   : Perform batch conversion (requires wildcard in filename)");
            Console.WriteLine("-s, -subdir  : Recurse all subdirectories (requires -b or -batch)\n");
        }
    }
}
