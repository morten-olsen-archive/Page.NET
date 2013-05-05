using System;
using System.Threading;
using System.Diagnostics;
using System.IO;
using PageFile;

namespace ConsoleTest
{
	class MainClass
	{
		private static void StartSection(string name)
		{
			Console.WriteLine("{0}{0}{0}======= {1} =======", Environment.NewLine, name);
		}

		private static void TimedRun(string actionName, Action action, int runs, bool indicator = false)
		{
			Console.Write("{0} x {1}", actionName, runs);
			var time = new Stopwatch();
			for (int i = 0; i < runs; i++) {
				time.Start();
				action();
				time.Stop();
				if (indicator) {
					Console.Write(".");
				}
			}
			var finalTime = time.ElapsedMilliseconds;
			Console.WriteLine(": {0} ms", finalTime);
		}

		private static void TestPage(string name, Stream stream)
		{
			StartSection(name);
			using (var page = new Page(stream, 256, 41960))
			{
				var run = 0;
				var indexes = new int[1000];
				var stringIndex = new PageFile.Page.MemoryAddress[1000];

				TimedRun("Write", () => {
					indexes[run] = page.Write(new byte[] { 1, 2, 3, 4, 5, 6, 6, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25});
					run++;
				}, 1000);

				run = 0;

				TimedRun("Read", () => {
					var item = page[indexes[run]];
					run++;
				}, 1000);

				TimedRun ("Remove range 2", () => {
					page.RemoveRange(2);
				}, 500);
				
				run = 0;
				TimedRun("Put string", () => {
					stringIndex[run] = page.Put("Hello World");
					run++;
				}, 1000);
				
				run = 0;
				TimedRun("Get string", () => {
					page.Get<string>(stringIndex[run]);
					run++;
				}, 1000);

				var binaryData = new byte[1024 * 1024];
				var rnd = new Random();
				for (int i = 0; i < 1024*1024; i++) {
					binaryData[i] = (byte)rnd.Next(0,255);
				}

				run = 0;
				TimedRun("Put large byte[]", () => {
					stringIndex[run] = page.Put(binaryData);
					run++;
				}, 10, true);
				
				run = 0;
				TimedRun("Get large byte[]", () => {
                    stringIndex[run].Get();
					run++;
				}, 10);
				
				TimedRun("Clean", () => {
					page.Clean ();
					run++;
				}, 1);
				
				
				run = 0;
				TimedRun("Put large object", () => {
					stringIndex[run] = page.Put(new SampleObject(binaryData));
					run++;
				}, 10, true);
				
				run = 0;
				TimedRun("Get large object", () => {
                    stringIndex[run].Get<SampleObject>();
					run++;
				}, 10);

                TimedRun("Clean", () =>
                {
                    page.Clean();
                    run++;
                }, 1);

                TimedRun("Write and release", () =>
                {
                    using (var result = page.Put("Hello World"))
                    { }
                }, 1000);
			}
		}

		[Serializable]
		internal class SampleObject
		{
			public SampleObject(byte[] data)
			{
				Data = data;
			}

			public byte[] Data { get; set; }
		}

		public static void Main (string[] args)
		{			
			TestPage("File", new FileStream("page.bin", FileMode.OpenOrCreate, FileAccess.ReadWrite));
            TestPage("Memory", new MemoryStream());
                
			StartSection("Done");
			Console.ReadKey();
		}
	}
}
