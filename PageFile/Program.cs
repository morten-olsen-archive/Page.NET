using System;
using System.IO;

namespace PageFile
{
	class MainClass
	{
		public static void Main (string[] args)
		{
			using (var page = new Page(new MemoryStream(), 30, 30))
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
				
				Console.ReadKey();
			}
		}
	}
}
