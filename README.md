Page.NET
========

This library is created to solve a problem in iOS, where no highperformance persistant storage options are availible. It uses a stream to create a pagefile/swapfile, which can store fixed length of binary data.

## Usage
A ``Page`` instance is created using a stream (any stream which supports seek, read and write can be used with a block size (each entries are blocks) and a max number of blocks

	using (var page = new Page(new MemoryStream(), 256, 300)) {
	}

The ``Page`` instance can then write, read and remove blocks. When a block is added, an int is send back, which should be used for later retrieving the object

	var pageIndex = page.Write(new byte[] { 1, 2, 3, 4});
	var items = page[pageIndex];

### Ranges
Range operations are supported and can be used for working with objects which span over multible blocks, or to erase/add a list of items.

### Objects
It is possible to store complex objects, as long as these are serializable. These can span across multible blocks, and therefore the reference object returned are a ``MemoryAddress`` object, which contains a list of block addresses, for reassembling the object.

	var pageIndex = page.Put(storePerson);
	var person = page.Get<Person>(pageIndex);

Both put and get can also be used for storing large binary arrays, which takes up more than one block.

To remove objects simply use ``Remove`` with the MemoryAddress as parameter

	page.Remove(address);

Maintenance
----------------

### Cleanup
*DO NOT USE YET, FIX IS COMING*
``CleanUp`` can be used for realigning all blocks in the pagefile (defrag), while still preserving their original index. This should not be necessary, since the page automaticly uses the first free block, so if an object is removed, it will create room for another one. 

### Clean
``Clean`` will completely wipe the pagefile, freeing up all blocks.

### Speed
These results are created using the ConsoleTest application included in the project, running on a MacBook Pro 13” Mid 2010, running Mono

	======= File =======
	Write x 1000: 13,7318 ms
	Read x 1000: 11,5619 ms
	Remove range 2 x 500: 24,7196 ms
	Put string x 1000: 113,0837 ms
	Get string x 1000: 87,237 ms
	Put large byte[] x 10..........: 5354,4426 ms
	Get large byte[] x 10: 233,7569 ms
	Clean x 1: 64,5035 ms
	Put large object x 10..........: 5272,781 ms
	Get large object x 10: 288,684 ms
	
	
	
	======= Memory =======
	Write x 1000: 4,3898 ms
	Read x 1000: 0,7551 ms
	Remove range 2 x 500: 2,4431 ms
	Put string x 1000: 21,576 ms
	Get string x 1000: 16,3819 ms
	Put large byte[] x 10..........: 5458,6115 ms
	Get large byte[] x 10: 43,1143 ms
	Clean x 1: 13,356 ms
	Put large object x 10..........: 4892,5297 ms
	Get large object x 10: 54,2149 ms