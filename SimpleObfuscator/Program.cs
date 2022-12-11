using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using dnlib.DotNet;
using dnlib.DotNet.Writer;

namespace SimpleObfuscator
{
	internal class Program
	{
		const string characters = "ОO";
		const int namelength = 32;
		static List<string> nameCache = new List<string>();
		static List<(string, string)> preserved_data = new List<(string, string)>();

		static string GenRandomName()
		{
again:
			Random random = new Random(Guid.NewGuid().GetHashCode());
			StringBuilder stringBuilder = new StringBuilder();

			for (int i = 0; i < namelength; i++)
			{
				int index = random.Next(characters.Length);
				stringBuilder.Append(characters[index]);
			}

			string name = stringBuilder.ToString();
			if (nameCache.Contains(name))
			{
				goto again;
			}
			nameCache.Add(name);

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

			int i = 1;
			foreach (var type in module.GetTypes())
			{
				var attr = type.CustomAttributes;
				if (type.IsSpecialName || type.IsRuntimeSpecialName || type.IsGlobalModuleType || attr.Any(e => e.TypeFullName.Contains("CompilerGenerated")))
					continue;

				bool isDisposable = false;
				var interfaces = type.Interfaces;
				if (interfaces == null || interfaces.Count > 0)
				{
					foreach (var iface in interfaces)
					{
						if (iface.Interface.Name == "IDisposable")
							isDisposable = true;
					}
				}

				string typename = GenRandomName();
				string typenamespace = GenRandomName();

				preserved_data.Add((type.Name, typename));
				type.Name = typename;

				preserved_data.Add((type.Namespace, typenamespace));
				type.Namespace = typenamespace;

				foreach (var field in type.Fields)
				{
					string fieldname = GenRandomName();
					preserved_data.Add((field.Name, fieldname));
					field.Name = fieldname;
				}

				foreach (var property in type.Properties)
				{
					string propertyname = GenRandomName();
					preserved_data.Add((property.Name, propertyname));
					property.Name = propertyname;
				}

				foreach (var method in type.Methods)
				{
					if (isDisposable && method.Name == "Dispose")
						continue;

					if (method.IsRuntimeSpecialName || method.IsConstructor)
						continue;

					bool isHarmonyMethod = false;

					if (!(method.Name == "OnLoaded" || method.Name == "OnUnloaded" || method.Name == "Transpiler" || method.Name == "Prefix" || method.Name == "Postfix" || method.Name == "Prepare" || method.Name == "Finalizer" || method.Name == "TargetMethod" || method.Name == "TargetMethods" || method.Name == "Cleanup"))
					{
						string methodname = GenRandomName();
						preserved_data.Add((method.Name, methodname));
						method.Name = methodname;
					}
					else
					{
						isHarmonyMethod = true;
					}

					if (!isHarmonyMethod)
						foreach (var param in method.Parameters)
						{
							if (!(param.Name == "__instance" || param.Name == "__result" || param.Name == "__state" || param.Name == "___fields" || param.Name == "__args" || param.Name == "__originalMethod" || param.Name == "__runOriginal"))
							{
								string paramname = GenRandomName();
								preserved_data.Add((param.Name, paramname));
								param.Name = paramname;
							}
						}

					if (method.Body != null)
						foreach (var @var in method.Body.Variables)
						{
							string @varname = GenRandomName();
							preserved_data.Add((@var.Name, @varname));
							@var.Name = @varname;
						}
				}
			}

			StringBuilder stringBuilder = new StringBuilder();
			foreach (var item in preserved_data)
			{
				stringBuilder.AppendLine($"{item.Item2} = {item.Item1}");
			}
			string filename = path.Split(new char[] { '/', '\\' }).Last();
			string _newFilename = path.Replace($"{filename}", $"{filename}_preserved.txt");
			File.WriteAllText(_newFilename, stringBuilder.ToString());

			// write modified to file
			var opts = new ModuleWriterOptions(module);
			opts.InitializeStrongNameSigning(module, new StrongNameKey(key));
			string extension = path.Split(new char[] { '.' }).Last();
			string newFilename = path.Replace($".{extension}", $".{extension}.obf");
			module.Write(newFilename, opts);
			Console.WriteLine("Done");
		}
	}
}
