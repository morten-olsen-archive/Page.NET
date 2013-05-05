#region Using statements
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.Serialization.Formatters.Binary;
#endregion

namespace PageFile
{
    public class Page : IDisposable
    {
        #region Constructors
        public Page(Stream stream, int blockSize, int blockCount)
        {
            if (!stream.CanSeek)
                throw new Exception("Stream does not support seeking");
            if (!stream.CanWrite)
                throw new Exception("Can not write to stream");
            if (!stream.CanRead)
                throw new Exception("Can not read from stream");
            _stream = stream;
            _blockSize = blockSize;
            _pageAlloc = new int[blockCount];
            _index = new int[blockCount];
        }
        #endregion

        #region Fields
        private Stream _stream;
        private int _blockSize;
        private int[] _index;
        private int[] _pageAlloc;
        #endregion

        #region Properties
        public byte[] this[int index]
        {
            get
            {
                var realIndex = _index[index];
                var offset = _blockSize * realIndex;
                var result = new byte[_blockSize];
                _stream.Position = offset;
                _stream.Read(result, 0, _blockSize);
                return result;
            }
            set
            {
                _stream.Position = 0;
                var realIndex = _index[index];
                var offset = _blockSize * realIndex;
                _stream.Write(value, offset, _blockSize);
            }
        }

        public int UsedBlocks
        {
            get
            {
                return _pageAlloc.Count(t => t > 0);
            }
        }

        public int Size
        {
            get
            {
                return _blockSize * _pageAlloc.Length;
            }
        }
        #endregion

        #region Methods
        public void Lock(int index)
        {
            _pageAlloc[index] += 1;
        }

        public void Lock(MemoryAddress address)
        {
            foreach (var position in address.Positions)
            { Lock(position); }
        }

        public int Write(byte[] data)
        {
            lock (this)
            {
                for (int i = 0; i < _pageAlloc.Length; i++)
                {
                    if (_pageAlloc[i] == 0)
                    {
                        var offset = i * _blockSize;
                        _stream.Position = offset;
                        _stream.Write(Pad(data), 0, _blockSize);
                        _pageAlloc[i] = 1;
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
            return Pad(src, pad);
        }

        public byte[] Pad(byte[] src, int pad)
        {
            int len = (src.Length + pad - 1) / pad * pad;
            Array.Resize(ref src, len);
            return src;
        }

        public int RemoveRange(int count, int startCount = 0)
        {
            int itemCount = 0;
            int removed = 0;
            for (int i = 0; i < _pageAlloc.Length; i++)
            {
                if (_pageAlloc[i] > 0)
                {
                    if (itemCount >= startCount)
                    {
                        Release(_index[i]);
                        _index[i] = i;
                        removed++;
                        if (removed == count)
                        {
                            break;
                        }
                    }
                }
            }
            return removed;
        }

        public MemoryAddress Put(byte[] data)
        {
            var dataEntries = Split(data);
            var memory = new MemoryAddress(this);
            memory.Positions = new int[dataEntries.Length];
            for (int i = 0; i < dataEntries.Length; i++)
            {
                memory.Positions[i] = Write(dataEntries[i]);
            }
            return memory;
        }

        public byte[] Get(MemoryAddress address)
        {
            var buffer = new byte[address.Positions.Length * _blockSize];
            for (int i = 0; i < address.Positions.Length; i++)
            {
                Buffer.BlockCopy(this[address.Positions[i]], 0, buffer, i * _blockSize, _blockSize);
            }
            return buffer;
        }

        public MemoryAddress Put(object data)
        {
            BinaryFormatter bf = new BinaryFormatter();
            using (var memory = new MemoryStream())
            {
                bf.Serialize(memory, data);
                using (BinaryReader br = new BinaryReader(memory))
                {
                    memory.Position = 0;
                    var binary = br.ReadBytes((int)memory.Length);
                    return Put(binary);
                }
            }
        }

        public T Get<T>(MemoryAddress address)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                var binary = Get(address);
                memory.Write(binary, 0, binary.Length);
                memory.Position = 0;
                BinaryFormatter bf = new BinaryFormatter();
                var result = bf.Deserialize(memory);
                return (T)result;
            }
        }

        public void Clean()
        {
            lock (this)
            {
                _pageAlloc = new int[_pageAlloc.Length];
                _index = new int[_pageAlloc.Length];
                _stream.Position = 0;
                _stream.Write(new byte[Size], 0, Size);
            }
        }

        public KeyValuePair<int, byte[]>[] GetItems(int offset, int count)
        {
            var found = 0;
            var items = new KeyValuePair<int, byte[]>[count];
            for (int i = 0; i < _pageAlloc.Length; i++)
            {
                if (_pageAlloc[i] > 0)
                {
                    if (found >= offset)
                    {
                        items[found - offset] = new KeyValuePair<int, byte[]>(_index[i], this[_index[i]]);
                    }
                    found++;
                }
            }
            return items;
        }

        /*public void CleanUp()
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
        }*/

        public void Release(int index)
        {
            var realIndex = _index[index];
            _pageAlloc[realIndex] -= 1;
            if (_pageAlloc[realIndex] == 0)
            {
                var offset = _blockSize * realIndex;
                _stream.Position = offset;
                _stream.Write(new byte[_blockSize], 0, _blockSize);
            }
        }

        public void Release(MemoryAddress address)
        {
            foreach (var position in address.Positions)
            {
                Release(position);
            }
        }

        public void Dispose()
        {
            _stream.Dispose();
        }

        private byte[][] Split(byte[] data)
        {
            var result = new List<byte[]>();
            var count = (int)Math.Ceiling((double)data.Length / (double)_blockSize);
            data = Pad(data, count * _blockSize);
            for (int i = 0; i < count; i++)
            {
                var buffer = new byte[_blockSize];
                Buffer.BlockCopy(data, i * _blockSize, buffer, 0, buffer.Length);
                result.Add(buffer);
            }
            return result.ToArray();
        }
        #endregion

        #region Classes
        public class MemoryAddress : IDisposable
        {
            internal MemoryAddress(Page page)
            { _page = page; }

            Page _page;

            public int[] Positions { get; internal set; }

            public byte[] Get()
            { return _page.Get(this); }

            public T Get<T>()
            { return _page.Get<T>(this); }

            public void Lock()
            { _page.Lock(this); }

            public void Release()
            { Dispose(); }

            public void Dispose()
            { _page.Release(this); }
        }
        #endregion
    }
}

#region Pony
//                     ▓▓▓
//                       ▓▓▓▒▒▒▓
//                    ▓▓▒▒▒▒▒▒▒▓      ▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓
//                  ▓▓▒▒▓▓▒▒▒▒▒▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓
//                ▓▓▒▒▒▓▒▒▒▒▒▒▒▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓       ▓▓▓▓▓▓▓
//               ▓▓▒▒▒▓▒▒▒▒▒▒▒▒▓▓▓▓▓▓▓▒▒▒▒▒▒▒▒▒▓▓▓▓▓▓▓▓▓       ▓▓▓▓▓
//             ▓▓▒▒▒▒▓▒▒▒▒▒▒▒▒▓▓▓▓▓▓▓▓▓▓▒▒▒▒▒▒▒▒▒▒▒▓▓▓▓▓▓▓▓▓      ▓▓
//            ▓▓▒▒▒▒▓▒▒▒▒▒▒▒▒▒▓▓▓▓▓▓▓▓▓▓▓▓▓▓▒▒▒▒▒▒▒▒▒▓▓▓▓▓▓▓▓
//           ▓▓▒▒▒▒▓▒▒▒▒▒▒▒▒▒▓░░░░░░▓▓▓▓▒▒▒▓▓▓▓▒▒▒▒▒▒▒▒▒▓▓▓▓▓▓▓
//          ▓▒▒▒▒▒▓▒▒▒▒▒▒▒▒▒▓░░░░░░░░░▓▓▓▓▒▒▒▓▓▓▒▒▒▒▒▒▒▒▒▒▓▓▓▓▓▓
//          ▓▒▒▒▒▒▓▒▒▒▒▒▒▒▒▒▓▓░░░░░░░░░▓▓▓▓▓▒▒▒▓▓▓▒▒▒▒▒▒▒▒▒▒▓▓▓▓▓
//          ▓▒▒▒▒▒▓▒▒▒▒▒▒▒▒▒▒▒▓░░░░░░░░░░▓▓▓▓▓▒▒▒▓▓▒▒▒▒▒▒▒▒▒▒▒▓▓▓▓
//          ▓▒▒▒▒▒▓▒▒▒▒▒▒▒▒▒▒▒▒▓░░░░░▒░░░░░▓▓▓▓▓▒▒▓▓▒▒▒▒▒▒▒▒▒▒▒▓▓▓▓
//         ▓▓▒▒▒▒▒▓▒▒▒▒▒▒▒▒▒▒▒▒▒▓▓▓░░░▒░░░░░▓▓▓▓▓▒▒▒▓▒▒▒▒▒▒▒▒▒▒▒▒▓▓▓
//        ▓▒▓▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▓▓░░░▒░░░░░░▓▓▓▓▒▒▒▓▒▒▒▒▒▒▒▒▒▒▒▒▓▓
//       ▓▒▒▓▒▒▒▒▒▒▒▒▒█▒▒▒▒▒▒▒▒▒▒▒▒▒▓░░▒▒░░░░░░▓▓▓▓▒▒▒▒▒▒▒▒▒▒▒▒▒▒▓▒▓
//      ▓▒▒▒▓▒▒▒▒▒▄▒▒▒█▄▀▀▀▀▀▄▄▒▒▒▒▒▒▓▒▒▒░░░░░░░▓▓▓▓▒▒▒▒▒▒▒▒▒▒▒▒▓ ▓▒▓
//     ▓▒▒▒▒▓▒▒▒▒▒▀▄▄▀            ▀▄▒▒▒▒▓▒▒▒░░▓░░░░░▓▓▓▓▒▒▒▒▒▒▒▒▒▒▒▓ ▓▒▓
//    ▓▒▒▒▒▓▒▒▒▒▄▒▄▀                 █▒▒▒▓▓▒▒░▓▒▓░░░░░▓▓▓▒▒▒▒▒▒▒▒▒▒▒▓  ▓▒▓
//    ▓▒▒▒▓▓▒▒▒▒▒██          ▓▓▓▓▓▓▓▓▒▒▒▓▓▒▒▓▒▒▓░░░░░▓▓▓▒▒▒▒▒▒▒▒▒▒▓   ▓▒▓
//   ▓▒▒▒▓▓▓▒▒▒▒▒█         ▓▓▓▓▓▓▓▓▓▓▒▒▒▓▓▒▓▒▒▒▓░░░░░▓▓▓▒▒▒▒▒▒▒▒▒▒▓   ▓▒▓
//  ▓▒▒▓▓▓▓▓▒▒▒▒▒        ▓▓▓▓▓       █▒▒▒▒▓▒▓▒▒▒▒▓░░░░░▓▓▓▒▒▒▒▒▒▒▒▒▓     ▓
//  ▓▒▓▓▓▓▓▓▒▒▒▒▒       ▓▓▓▓▓       ▄█▒▒▒▒▒▓▒▒▒▒▒▓░░░░░▓▓▓▒▒▒▒▒▒▒▒▒▓
//  ▓▓▓▓▓▓▓▓▒▒▒▒▒      ▓▓▓▓▓      ▄███▒▒▒▒▒▒▒▒▒▒▒▓░░░░░░▓▓▒▒▒▒▒▒▒▒▓
//  ▓▓▓▓▓▓▓▓▒▒▒▒▒      ▓▓▓▓██▄▄▄████▒▒▒▒▒▒▒▒▒▒▓▓▓▓▓░░░░░▓▓▒▒▒▒▒▒▒▓
//  ▓▓▓▓▓▓▓▓▓▒▒▒▒▒     ▓▓▓▓████   ██▒▒▒▒▒▒▒▒▒▓▓▓    ▓░░░░░▓▓▒▒▒▒▒▒▓
//  ▓▓▓▓▓▓▓▓▓▒▒▒▒▒▒    ▓▓▓▓████▄▄██▒▒▒▒▒▒▒▒▓▓▓    ██▓░░░░░░▓▒▒▒▒▒▒▓
// ▓▓▓▓▓▓▓▓▓▓▒▒▒▒▒▒    ▓▓▓▓███████▒▒▒▒▒▒▒▒▓▓▓  ▄███  ▓▓░░░░░▓▒▒▒▒▒▓
//  ▓▓▓▓▓▓▓▓▓▓▒▒▒▒▒▒▒   ▓▓▓▓█████▒▒▒▒▒▒▒▒▒▓▓ █▀▀██      ▓▓░░░░▒▒▒▒▒▓
//  ▓▓▓▓▓▓▓▓▓▒▓▒▒▒▒▒▒▒  ▓▓▓▓▓███▒▒▒▒▒▒▒▒▒▒▓▓█▄▄█           ▓▓▓░▒▒▒▒▒▓
//  ▓▓▓▓▓▓▓▓▓▒▒▓▒▒▒▒▒▒▒▒▒▒▓▓▓▒▒▒▒▒▒▒▒▒▒▓▒▒▓███                 ▓▓▓▒▒▒▒▓
//   ▓▓▓▓▓▓▓▓▒▒▒▓▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▓▒▒██                       ▓▓▓▒▓
//    ▓▓▓▓▓▓▓▒▒▒▒▓▓▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▓▓▓▓▓                          ▓▓
//    ▓▓▓▓▓▓▓▒▒▒▒▒▒▓▒▒▒▒▒▒▒▒▒▒▒▒▒▓▓▒▒▒▒▒▒▒▒▒▒▒▓
//     ▓▓▓▓▓▓▒▒▒▒▒▒▒▓▓▒▒▒▒▒▒▒▒▒▒░▓▓▓▒▒▒▒▒▒▒▒▒▒▓
//      ▓▓▓▓▓▒▒▒▒▒▒▒▒▒▓▓▓▒▒▒▒▒▒▒░░▓▓▓▓▓▒▒▒▒▓▓▓
//       ▓▓▓▓▒▒▒▒▒▒▒▒▒▒▒▒▓▓▓▒▒▒▒░░░▓▓▓▓
//         ▓▓▓▒▒▒▒▒▒▒▒▒▒▒▒▓░▓▓▒▒▒░░░▓
//          ▓▓▒▒▒▒▒▒▒▒▒▒▒▒▓░░▓▓▓▓▒▒▓▓
//            ▓▒▒▒▒▒▒▒▒▒▒▒▓░░░▒▒▓▓▓▒▓▓
//            ▓▒▒▒▒▒▒▒▒▒▒▓░░░▒▒▓▒▒▒▓▓▓
//             ▓▒▒▒▒▒▒▒▒▓░░░▒▒▒▓▒▒▒▓▓▓▓
//             ▓▒▒▒▒▒▒▒▓░░▒▒▒▒▓▒▒▒▓▓▓▓
//             ▓▒▒▒▒▒▒▓░░▒▒▒▒▓▒▒▒▓▓▓▓▓
//              ▓▒▒▒▒▓░░▒▒▒▓▓▒▒▒▓▓▓▓▓▓
//              ▓▒▒▒▓░░▒▒▒▓▓▒▒▒▓▓▓▓▓▓
//               ▓▒▓░░▒▒▒▓▒▒▒▓▓▓▓▓▓▓
//                ▓▓░▒▒▓ ▓▒▒▒▓▓▓▓▓▓
//                ▓░░▒▓ ▓▒▒▒▓▓▓▓▓▓
//                 ▓░▒▓ ▓▒▒▓▓▓▓▓▓
//                  ▓▒▓ ▓▒▓▓▓▓▓
//                   ▓▓ ▓▓▓▓▓▓
//                       ▓▓▓▓▓
//                        ▓▓▓
//                        ▓▓
//                         ▓
#endregion