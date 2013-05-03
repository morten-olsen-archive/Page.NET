using System;
using System.Threading;
using System.Diagnostics;
using System.IO;
using PageFile;

namespace ConsoleTest
{
	class MainClass
	{
		public static void Main (string[] args)
		{			
			Thread.Sleep(3000);
			var time = new Stopwatch();
			time.Start();
			using (var page = new Page(new FileStream("test.exe", FileMode.OpenOrCreate, FileAccess.ReadWrite), 10, 300))
			{
				var intHolder = new int[10];
				{
					var pageIndex = page.Write(new byte[] { 1, 2, 3, 4});
					var items = page[pageIndex];
					intHolder[0] = pageIndex; // 0
				}
				
				
				{
					var pageIndex = page.Write(new byte[] { 1, 2, 3, 5});
					var items = page[pageIndex];
					intHolder[1] = pageIndex; // 1
				}
				
				{
					var pageIndex = page.RemoveRange(2);
					var newCount = page.UsedBlocks; // 0
				}
				
				{
					var pageIndex = page.Write(new byte[] { 1, 2, 3, 5});
					var items = page[pageIndex];
					intHolder[1] = pageIndex; // 0
				}

				{
					var hello = "Testing";
					var pageIndex = page.Put(hello);
					var result = page.Get<string>(pageIndex);
					var t = "";
				}
			}
			time.Stop();
			var finalTime = new TimeSpan(time.ElapsedTicks).TotalMilliseconds;
			Console.WriteLine(finalTime);
			Console.ReadKey();
		}
	}
}
