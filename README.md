Page.NET
========

This library is created to solve a problem in iOS, where no highperformance persistant storage options are availible. It uses a stream to create a pagefile/swapfile, which can store fixed length of binary data.

### Some random speedcomparisons
(better comparisons are coming).

* **Page.NET** 3 write ops, 2 deletes: 7ms
* **SQLite** 1 write op: 40ms

## Usage
A ``Page`` instance is created using a stream (any stream which supports seek, read and write can be used with a block size (each entries are blocks) and a max number of blocks

	using (var page = new Page(new MemoryStream(), 256, 300)) {
	}

The ``Page`` instance can then write, read and remove blocks. When a block is added, an int is send back, which should be used for later retrieving the object

	var pageIndex = page.Write(new byte[] { 1, 2, 3, 4});
	var items = page[pageIndex];

### Ranges
Range operations are supported and can be used for working with objects which span over multible blocks, or to erase/add a list of items.

Maintenance
----------------

### Cleanup
``CleanUp`` can be used for realigning all blocks in the pagefile (defrag), while still preserving their original index. This should not be necessary, since the page automaticly uses the first free block, so if an object is removed, it will create room for another one. 

### Clean
``Clean`` will completely wipe the pagefile, freeing up all blocks.