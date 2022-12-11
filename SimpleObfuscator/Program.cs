using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using dnlib.DotNet;
using dnlib.DotNet.Writer;

namespace SimpleObfuscator
{
	internal class Program
	{
		const string characters = "ОO";
		const int namelength = 32;
		static List<string> nameCache = new List<string>();

		static string GenRandomName()
		{
again:
			Random random = new Random((int)DateTime.Now.Ticks);
			StringBuilder stringBuilder = new StringBuilder();

			for (int i = 0; i < namelength; i++)
			{
				int index = random.Next(characters.Length);
				stringBuilder.Append(characters[index]);
			}

			string name = stringBuilder.ToString();
			if (nameCache.Contains(name))
				goto again;

			return name;
		}

		static void Main(string[] args)
		{
			if (args.Length == 0)
			{
				Console.WriteLine("Usage: simpleobfuscator.exe <target assembly> <strong-name key>");
				return;
			}

			string path = args[0];
			string key = args[1];

			ModuleContext modCtx = ModuleDef.CreateModuleContext();
			ModuleDefMD module = ModuleDefMD.Load(path, modCtx);

			if (module == null)
			{
				Console.WriteLine("Cannot load module");
				return;
			}


			foreach (var type in module.Types)
			{
				type.Name = GenRandomName();
				type.Namespace = GenRandomName();

				foreach (var field in type.Fields)
					field.Name = GenRandomName();

				foreach (var property in type.Properties)
					property.Name = GenRandomName();

				foreach (var method in type.Methods)
				{
					method.Name = GenRandomName();
					foreach (var param in method.Parameters)
						param.Name = GenRandomName();

					if (method.Body != null)
						foreach (var @var in method.Body.Variables)
							@var.Name = GenRandomName();
				}
			}

			// write modified to file
			var opts = new ModuleWriterOptions(module);
			opts.InitializeStrongNameSigning(module, new StrongNameKey(key));
			string extension = path.Split(new char[] { '.' }).Last();
			string newFilename = path.Replace($".{extension}", $".{extension}.obf");
			module.Write(newFilename, opts);
		}
	}
}
