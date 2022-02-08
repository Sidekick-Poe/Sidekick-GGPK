﻿using LibGGPK3;
using System;
using System.IO;
using System.Linq;
using System.Threading;

namespace GGPKFastDefragment {
	public class Program {
		public static void Main(string[] args) {
			Console.TreatControlCAsInput = true;
			Console.WriteLine("GGPKFastDefragment  Copyright (C) 2022 aianlinb."); // ©
			Console.WriteLine();
			if (args.Length == 0) {
				args = new string[1];
				Console.Write("Path To GGPK: ");
				args[0] = Console.ReadLine()!;
				Console.WriteLine();
			}
			if (!File.Exists(args[0])) {
				Console.WriteLine("File Not Found: " + args[0]);
				Console.WriteLine("Enter to exit . . .");
				Console.ReadLine();
				return;
			}

			Console.WriteLine("GGPK: " + args[0]);
			var size = new FileInfo(args[0]).Length;
			Console.WriteLine("GGPK size: " + size);
			Console.WriteLine("Reading ggpk file . . .");
			var ggpk = new GGPK(args[0]);
			var max = -1;
			var prog = -1;
			Console.WriteLine("Start defragment . . .");
			Console.WriteLine();
			var tsk = ggpk.FastDefragmentAsync(CancellationToken.None, new Progress<int>(i => {
				prog = i;
				if (prog > max) {
					max = prog;
				}
			}));
			while (prog < 0) {
				Thread.Sleep(100);
			}

			while (!tsk.IsCompleted) {
				Console.WriteLine($"Remaining FreeRecords to be filled: {prog}/{max}");
				Thread.Sleep(400);
			}
			Console.WriteLine($"Remaining FreeRecords to be filled: {prog}/{max}");
			Console.WriteLine();
			ggpk.Dispose();
			Console.WriteLine("Done!");
			var size2 = new FileInfo(args[0]).Length;
			Console.WriteLine("GGPK size: " + size2);
			Console.WriteLine("Reduced " + (size - size2) + " bytes");
			Console.WriteLine("Total size of remaining FreeRecords: " + ggpk.FreeRecords.Sum(f => f.Length));
			Console.WriteLine("Enter to exit . . .");
			Console.ReadLine();
		}
	}
}