using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
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
				Console.WriteLine("Usage: simpleobfuscator.exe --deobfuscate <target assembly> <preserved strings>");
				Console.WriteLine("Make sure the the path does not contain spaces");
				return;
			}

			if (args.Contains("--deobfuscate"))
				goto deobfuscation;

			string path = args[0];
			string key = args[1];

			ModuleContext modCtx = ModuleDef.CreateModuleContext();
			ModuleDefMD module = ModuleDefMD.Load(path, modCtx);

			if (module == null)
			{
				Console.WriteLine("Cannot load module");
				return;
			}

			//int i = 1;
			foreach (var type in module.GetTypes())
			{
				var attr = type.CustomAttributes;
				if (type.IsSpecialName || type.IsRuntimeSpecialName || type.IsGlobalModuleType || attr.Any(e => e.TypeFullName.Contains("CompilerGenerated")))
					continue;

				//attr.ToList().ForEach(e => Console.WriteLine($"{e.AttributeType.Name}\n"));
				//Console.WriteLine("\n");

				bool excludeObfuscation_class = false;
				if (attr.Any(e => e.AttributeType.Name == nameof(ObfuscationAttribute)))
				{
					excludeObfuscation_class = (bool)attr.First(e => e.AttributeType.Name == nameof(ObfuscationAttribute)).NamedArguments.First(e => e.Name == "Exclude").Value == true;
				}

				//if (attr.Any(e => e.AttributeType.Name == nameof(ObfuscationAttribute)))
				//{
				//	Console.WriteLine($"found: {attr.ToList().Select(e => e.AttributeType.Name).ToString()}");
				//}

				// test code
				//bool ahasInterfaces = false;
				//var ainterfaces = type.Interfaces;
				//if (ainterfaces.Count > 0) // dont rename interface methods (they are name dependent)
				//	ahasInterfaces = true;
				//if (!ahasInterfaces)
				//continue;

				//Console.Write($"\n\n{ainterfaces[0].Interface.Name},,,,{type.Name} |||");
				//ainterfaces[0].Interface.ResolveTypeDef()?.Methods?.ToList().ForEach(e => Console.WriteLine(e.Name));
				//continue;

				bool hasInterfaces = false;
				var interfaces = type.Interfaces;
				if (interfaces.Count > 0) // dont rename interface methods (they are name dependent)
					hasInterfaces = true;

				//if (interfaces == null || interfaces.Count > 0)
				//{
				//	foreach (var iface in interfaces)
				//	{
				//		if (iface.Interface.Name == "IDisposable")
				//			hasInterfaces = true;
				//	}
				//}

				bool serializable = false;
				if (type.IsSerializable)
					serializable = true;

				if (!excludeObfuscation_class)
				{
					string typename = GenRandomName();
					preserved_data.Add((type.Name, typename));
					type.Name = typename;
				}

				string typenamespace = GenRandomName();

				preserved_data.Add((type.Namespace, typenamespace));
				type.Namespace = typenamespace;

				if (serializable)
					goto skip_field_obfuscation;

				if (type.IsInterface)
					continue; // skip method naming of interfaces

				foreach (var field in type.Fields)
				{
					if (field.CustomAttributes.Any(e => e.AttributeType.Name == nameof(ObfuscationAttribute)))
					{
						if ((bool)field.CustomAttributes.First(e => e.AttributeType.Name == nameof(ObfuscationAttribute)).NamedArguments.First(e => e.Name == "Exclude").Value == true)
							continue;
					}

					string fieldname = GenRandomName();
					preserved_data.Add((field.Name, fieldname));
					field.Name = fieldname;
				}

				foreach (var property in type.Properties)
				{
					if (property.CustomAttributes.Any(e => e.AttributeType.Name == nameof(ObfuscationAttribute)))
					{
						if ((bool)property.CustomAttributes.First(e => e.AttributeType.Name == nameof(ObfuscationAttribute)).NamedArguments.First(e => e.Name == "Exclude").Value == true)
							continue;
					}

					string propertyname = GenRandomName();
					preserved_data.Add((property.Name, propertyname));
					property.Name = propertyname;
				}

skip_field_obfuscation:
				foreach (var method in type.Methods)
				{
					bool isIfaceMethod = false;
					if (hasInterfaces)
					{
						foreach (var iface in interfaces)
						{
							if (iface.Interface.ResolveTypeDef()?.Methods?.Any(m => m.Name == method.Name) is true)
							{
								isIfaceMethod = true;
								break;
							}
						}
					}
					//if (hasInterfaces && method.Name == "Dispose")
					//	continue;

					bool excludeObfuscation = false;
					if (method.CustomAttributes.Any(e => e.AttributeType.Name == nameof(ObfuscationAttribute)))
					{
						excludeObfuscation = (bool)method.CustomAttributes.First(e => e.AttributeType.Name == nameof(ObfuscationAttribute)).NamedArguments.First(e => e.Name == "Exclude").Value == true;
					}

					if (method.IsRuntimeSpecialName || method.IsConstructor)
						continue;

					bool isHarmonyMethod = false;

					if (!(method.Name == "OnLoaded" || method.Name == "OnUnloaded" || method.Name == "Transpiler" || method.Name == "Prefix" || method.Name == "Postfix" || method.Name == "Prepare" || method.Name == "Finalizer" || method.Name == "TargetMethod" || method.Name == "TargetMethods" || method.Name == "Cleanup"))
					{
						if (!isIfaceMethod && !excludeObfuscation)
						{
							string methodname = GenRandomName();
							preserved_data.Add((method.Name, methodname));
							method.Name = methodname;
						}
					}
					else
					{
						isHarmonyMethod = true;
					}

					if (!isHarmonyMethod)
						foreach (var param in method.Parameters)
						{
							if (param.ParamDef.CustomAttributes.Any(e => e.AttributeType.Name == nameof(ObfuscationAttribute)))
							{
								if ((bool)param.ParamDef.CustomAttributes.First(e => e.AttributeType.Name == nameof(ObfuscationAttribute)).NamedArguments.First(e => e.Name == "Exclude").Value == true)
									continue;
							}

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
			return;

deobfuscation:
			Console.WriteLine("Mode: Deobfuscation");

			string path2 = args[0];
			string key2 = args[1];

			ModuleContext modCtx2 = ModuleDef.CreateModuleContext();
			ModuleDefMD module2 = ModuleDefMD.Load(path2, modCtx2);

			if (module2 == null)
			{
				Console.WriteLine("Cannot load module");
				return;
			}
		}
	}
}
