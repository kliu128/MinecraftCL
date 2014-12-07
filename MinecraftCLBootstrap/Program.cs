using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace MinecraftCLBootstrap
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.Title = "Minecraft CL Bootstrap";
            Console.WriteLine("MinecraftCL Bootstrap v" + Application.ProductVersion);
            Console.WriteLine();
            Console.WriteLine("MinecraftCL will be starting soon.");
            Console.WriteLine();
            Console.WriteLine(".NET Version       = " + System.Environment.Version);
            if (File.Exists(@"\.mcl\MinecraftCL.exe"))
            {

            }
            Console.WriteLine("MinecraftCL Status = ");
            Console.ReadLine();
        }
    }
}
