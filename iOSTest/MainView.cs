using System;
using MonoTouch.UIKit;
using System.Threading;
using System.Diagnostics;
using PageFile;
using System.IO;
using MonoTouch.SQLite;

namespace iOSTest
{
	public class MainView : UIViewController
	{
		private string GetPath(string name)
		{
			string docsDir = Environment.GetFolderPath (Environment.SpecialFolder.Personal);
			string path = Path.Combine (docsDir, name);

			return path;
		}
		public override void ViewDidLoad ()
		{
			base.ViewDidLoad ();
			ThreadPool.QueueUserWorkItem((obj) => {
				//Thread.Sleep(3000);
				{
					var time = new Stopwatch();
					using (var page = new Page(new FileStream(GetPath("test.bin"), FileMode.OpenOrCreate, FileAccess.ReadWrite), 256, 300))
					{
						time.Start();
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
						time.Stop();
						var finalTime = new TimeSpan(time.ElapsedTicks).TotalMilliseconds;
						var t = "";
					}
				}
				{
					if (File.Exists(GetPath("test.db"))) {
						File.Delete(GetPath("test.db"));
					}
					var sql = new SQLiteConnection(GetPath("test.db"));
					sql.CreateTable<SearchItem>();
					
					var time = new Stopwatch();
					time.Start();
					sql.Insert(new SearchItem() { SomeThing = "test" });
					time.Stop();
					var finalTime = new TimeSpan(time.ElapsedTicks).TotalMilliseconds;
					var t = "";
				}
			});
		}

		
		public class SearchItem {
			[PrimaryKey][AutoIncrement]
			public int ItemId { get; set; }
			public string SomeThing;
		}
	}
}

