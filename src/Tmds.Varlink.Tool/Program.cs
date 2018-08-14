using System;
using System.IO;
using System.Threading.Tasks;
using Org.Varlink;

namespace Tmds.Varlink.Tool
{
    class Program
    {
        static async Task Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Specify path of varlink interface file as an argument.");
                return;
            }
            string arg = args[0];
            if (File.Exists(arg))
            {
                GenerateCodeForInterface(File.ReadAllText(arg));
            }
            else
            {
                using (var service = new Service(arg))
                {
                    var info = await service.GetInfoAsync();
                    foreach (string interfaceName in info.interfaces)
                    {
                        string interfaceDescription =
                            (await service.GetInterfaceDescriptionAsync(new GetInterfaceDescriptionArgs { @interface = interfaceName })).description;
                        GenerateCodeForInterface(interfaceDescription);
                    }
                }
            }
        }

        private static void GenerateCodeForInterface(string interfaceDescription)
        {
            Interface interf = Parser.Parse(interfaceDescription);
            string filename = $"{interf.Name}.cs";
            File.WriteAllText(filename, new Generator().Generate(interf));
            System.Console.WriteLine($"Written {filename}");
        }
    }
}
