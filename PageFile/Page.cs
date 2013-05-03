using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace PageFile
{
	public class Page : IDisposable
	{
		public Page (Stream stream, int blockSize, int blockCount)
		{
			if (!stream.CanSeek)
				throw new Exception("Stream does not support seeking");
			if (!stream.CanWrite)
				throw new Exception("Can not write to stream");
			if (!stream.CanRead)
				throw new Exception("Can not read from stream");
			_stream = stream;
			_blockSize = blockSize;
			_pageAlloc = new bool[blockCount];
			_index = new int[blockCount];
		}

		private Stream _stream;
		private int _blockSize;
		private int[] _index;
		private bool[] _pageAlloc;

		public byte[] this[int index]
		{
			get {
				var realIndex = _index[index];
				var offset = _blockSize * realIndex;
				var result = new byte[_blockSize];
				_stream.Position = offset;
				_stream.Read(result, 0, _blockSize);
				return result;
			}
			set {
				_stream.Position = 0;
				var realIndex = _index[index];
				var offset = _blockSize * realIndex;
				_stream.Write(value, offset, _blockSize);
			}
		}

		public int Write(byte[] data)
		{
			lock(this) {
				for (int i = 0; i < _pageAlloc.Length; i++) {
					if (!_pageAlloc[i]) {
						var offset = i * _blockSize;
						_stream.Position = offset;
						_stream.Write(Pad(data), 0, _blockSize);
						_pageAlloc[i] = true;
						_index[i] = i;
						return i;
					}
				}
				throw new OutOfMemoryException("No more room in the page stream");
			}
		}

		public byte[] Pad(byte[] src)
		{
			int pad = _blockSize;
			int len = (src.Length + pad - 1) / pad * pad;
			Array.Resize(ref src, len);
			return src;
		}

		public int UsedBlocks
		{
			get {
				return _pageAlloc.Count(t => t);
			}
		}

		public int Size
		{
			get {
				return _blockSize * _pageAlloc.Length;
			}
		}

		public int RemoveRange(int count, int startCount = 0) {
			int itemCount = 0;
			int removed = 0;
			for (int i = 0; i < _pageAlloc.Length; i++) {
				if (_pageAlloc[i]) {
					if (itemCount >= startCount) {
						Remove(_index[i]);
						_index[i] = i;
						removed ++;
						if (removed == count) {
							break;
						}
					}
				}
			}
			return removed;
		}

		public void Clean()
		{
			lock(this) {
				_pageAlloc = new bool[_pageAlloc.Length];
				_index = new int[_pageAlloc.Length];
				_stream.Position = 0;
				_stream.Write(new byte[Size], 0, Size);
			}
		}

		public KeyValuePair<int, byte[]>[] GetItems(int offset, int count) {
			var found = 0;
			var items = new KeyValuePair<int, byte[]>[count];
			for (int i = 0; i < _pageAlloc.Length; i++) {
				if (_pageAlloc[i]) {
					if (found >= offset) {
						items[found - offset] = new KeyValuePair<int, byte[]>(_index[i], this[_index[i]]);
					}
					found++;
				}
			}
			return items;
		}

		public void CleanUp()
		{
			lock (this) {
				var newIndex = new int[_pageAlloc.Length];
				var newAlloc = new bool[_pageAlloc.Length];

				int allocs = 0;
				for (int i = 0; i < _pageAlloc.Length; i++) {
					if (_pageAlloc[i]) {
						_stream.Position = allocs * _blockSize;
						_stream.Write(this[i], 0, _blockSize);
						newIndex[_index[i]] = allocs;
						allocs++;
					}
				}
				var emptyBlocks = _pageAlloc.Length - allocs;
				var cleanBytes = new byte[emptyBlocks * _blockSize];
				_stream.Position = allocs * _blockSize;
				_stream.Write(cleanBytes, 0, cleanBytes.Length);
				_index = newIndex;
				_pageAlloc = newAlloc;
			}
		}

		public void Remove(int index)
		{
			var realIndex = _index[index];
			_pageAlloc[realIndex] = false;
			var offset = _blockSize * realIndex;
			_stream.Position = offset;
			_stream.Write(new byte[_blockSize], 0, _blockSize);
		}

		public void Dispose ()
		{
			_stream.Dispose();
		}
	}
}

