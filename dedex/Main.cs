using System;
using dex.net;
using System.Text;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using ICSharpCode.SharpZipLib.Zip;

namespace dedex
{
	class MainClass
	{
		public static void Main (string[] args)
		{
			ClassDisplayOptions displayOptions = ClassDisplayOptions.ClassName;
			IDexWriter dexWriter = null;
			var classPattern = "*";
			var factory = new WritersFactory ();

			var dexFiles = new List<FileInfo> ();
			var tempFiles = new List<FileInfo> ();
			var apkFiles = new List<string> ();

			string currentOption = null;
			foreach (var arg in args) {
				// Parse the option arguments
				if (currentOption != null) {
					switch (currentOption) {
						case "-c":
							classPattern = arg;
							break;

						case "-d":
							var displayOptionStrings = arg.Split(new char[]{','});
							foreach (var displayOption in displayOptionStrings) {
								switch (displayOption.ToLower ()) {
									case "classes":
										displayOptions |= ClassDisplayOptions.ClassAnnotations |
														  ClassDisplayOptions.ClassName |
														  ClassDisplayOptions.ClassDetails;
										break;

									case "methods":
										displayOptions |= ClassDisplayOptions.MethodAnnotations |
														  ClassDisplayOptions.Methods;
										break;
								
									case "fields":
										displayOptions |= ClassDisplayOptions.Fields;
										break;
								
									case "opcodes":
										displayOptions |= ClassDisplayOptions.Methods | ClassDisplayOptions.OpCodes;
										break;
								
									case "all":
										displayOptions |= ClassDisplayOptions.ClassAnnotations | ClassDisplayOptions.ClassName |
												ClassDisplayOptions.ClassDetails | ClassDisplayOptions.Fields | ClassDisplayOptions.MethodAnnotations |
												ClassDisplayOptions.Methods | ClassDisplayOptions.OpCodes;
										break;

									default:
										throw new ArgumentException("Unsupported display option " + displayOption);
								}
							}
							break;
						
						case "-w":
							var writerName = arg.Trim();
							dexWriter = factory.GetWriter(writerName);
							break;

						default:
							throw new ArgumentException("Unsupported argument " + currentOption);
					}

					currentOption = null;
				}

				// An option flag
				if (arg.StartsWith ("-")) {
					currentOption = arg;
					continue;
				}

				// Build the list of dex files to disassemble
				if (Path.GetExtension(arg).ToLower().Equals(".dex")) {
					var dexFile = new FileInfo (arg);
					if (dexFile.Exists) {
						dexFiles.Add (dexFile);
					} else {
						Console.WriteLine ("Couldn't file file {0}", arg);
					}
				} else if (Path.GetExtension(arg).ToLower().Equals(".apk")) {
					// Unzip the classes.dex into a temporary location
					var apkFile = new FileInfo (arg);
					if (apkFile.Exists) {
						var zip = new ZipFile (arg);
						var entry = zip.GetEntry ("classes.dex");

						if (entry != null) {
							var zipStream = zip.GetInputStream (entry);
							var tempFileName = Path.GetTempFileName ();

							var buffer = new byte[4096];
							using (var writer = File.Create(tempFileName)) {
								int bytesRead;
								while ((bytesRead = zipStream.Read(buffer, 0, 4096)) > 0) {
									writer.Write (buffer, 0, bytesRead);
								}
							}
							var tempFile = new FileInfo (tempFileName);
							dexFiles.Add (tempFile);
							tempFiles.Add (tempFile);
							apkFiles.Add (arg);
						} else {
							Console.WriteLine ("No classes.dex in {0}", arg);
						}
					} else {
						Console.WriteLine ("Couldn't file file {0}", arg);
					}
				}
			}

			if (dexFiles.Count == 0)
				PrintHelp();

			// Set default writer
			if (dexWriter == null) {
				dexWriter = factory.GetWriter(factory.GetWriters()[0]);
			}

			// Process all DEX files
			try {
				int tempCount = 0;
				foreach (var dexFile in dexFiles) {
					using (var dex = new Dex(dexFile.Open(FileMode.Open)) ) {
						var classesToDisplay = new Regex("^" + classPattern.Replace("*", ".*") + "$");

						dexWriter.dex = dex;

						string filename = dexFile.Name;
						if (Path.GetExtension(filename).EndsWith("tmp")) {
							filename = apkFiles[tempCount++];
						}
						filename = Path.GetFileName(filename);

						// Write out the file name as header
						Console.WriteLine();
						Console.WriteLine(filename);
						foreach (var c in filename)
							Console.Write("=");
						Console.WriteLine('\n');

						// Write out each class
						foreach (var dexClass in dex.GetClasses()) {
							if (classesToDisplay.IsMatch(dexClass.Name)) {
								dexWriter.WriteOutClass(dexClass, displayOptions, Console.Out);
							}
						}
					}
				}
			} finally {
				foreach (var tempFile in tempFiles) {
					tempFile.Delete ();
				}
			}
		}

		public static void PrintHelp ()
		{
			var languages = string.Join (", ", new WritersFactory ().GetWriters ());

			Console.WriteLine("Usage:\n\tdedex [options] <file.dex|apk> [file2.dex ... fileN.dex]\n");
			Console.WriteLine("\t-c <pattern>. Display only classes matching the pattern. * is a wildcard");
			Console.WriteLine("\t-d <display[,display...]>. Options are All, Classes, Methods, Fields, OpCodes");
			Console.WriteLine("\t-w <language>. One of " + languages);
			Environment.Exit(1);
		}

	}
}
