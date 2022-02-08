﻿using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LibBundle3 {
	public unsafe class Bundle : IDisposable {
		[StructLayout(LayoutKind.Sequential, Size = 60, Pack = 1)]
		public struct Header {
			public int uncompressed_size;
			public int compressed_size;
			public int head_size; // chunk_count * 4 + 48
			public Oodle.Compressor compressor; // Leviathan == 13
			public int unknown; // 1
			public long size_decompressed; // == uncompressed_size
			public long size_compressed; // == compressed_size
			public int chunk_count;
			public int chunk_size; // 256KB == 262144
			public int unknown3; // 0
			public int unknown4; // 0
			public int unknown5; // 0
			public int unknown6; // 0

			public int GetLastChunkSize() {
				return uncompressed_size - (chunk_size * (chunk_count - 1));
			}
		}

		public int UncompressedSize => header.uncompressed_size;
		public int CompressedSize => header.compressed_size;

		protected readonly Stream baseStream;
		protected readonly bool streamLeaveOpen; // If false, close the baseStream when dispose

		protected Header header;
		protected int[] compressed_chunk_sizes;

		public byte[]? CachedData;

		public Bundle(string filePath) : this(File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read)) { }

		[SkipLocalsInit]
		public Bundle(Stream stream, bool leaveOpen = false) {
			baseStream = stream ?? throw new ArgumentNullException(nameof(stream));
			streamLeaveOpen = leaveOpen;
			stream.Seek(0, SeekOrigin.Begin);

			var p = stackalloc byte[60];

			stream.Read(new(p, 60));
			header = Marshal.PtrToStructure<Header>((IntPtr)p); // Read bundle header

			compressed_chunk_sizes = new int[header.chunk_count];
			fixed (int* p2 = compressed_chunk_sizes)
				stream.Read(new(p2, header.chunk_count << 2));
		}

		/// <summary>
		/// Read the whole data of the bundle
		/// </summary>
		/// <returns>Data read, or <see cref="CachedData"/> if not null</returns>
		public virtual byte[] ReadData() {
			if (CachedData != null)
				return CachedData;
			return ReadChunks(0, header.chunk_count);
		}

		/// <summary>
		/// Call <see cref="ReadData()"/> and cache it into <see cref="CachedData"/>
		/// </summary>
		public virtual void ReadDataAndCache() {
			CachedData = ReadData();
		}

		/// <summary>
		/// Read the data with the given offset and length
		/// </summary>
		/// <exception cref="ArgumentOutOfRangeException"></exception>
		public virtual Memory<byte> ReadData(int offset, int length) {
			var endOffset = offset + length;
			if (offset < 0 || offset >= header.uncompressed_size)
				throw new ArgumentOutOfRangeException(nameof(offset));
			if (length < 0 || endOffset > header.uncompressed_size)
				throw new ArgumentOutOfRangeException(nameof(length));

			if (CachedData != null)
				return new(CachedData, offset, length);
			var start = offset / header.chunk_size;
			var end = (endOffset - 1) / header.chunk_size;
			return new(ReadChunks(start, end - start + 1), offset % header.chunk_size, length);
		}

		/// <summary>
		/// Read chunks (with size of <see cref="Header.chunk_size"/>) start with the chunk with index of <paramref name="start"/> and combine them to a <see cref="byte[]"/>
		/// </summary>
		/// <param name="start">Index of a chunk</param>
		/// <param name="count">Number of chunks to read</param>
		/// <exception cref="ArgumentOutOfRangeException"></exception>
		public virtual byte[] ReadChunks(int start, int count = 1) {
			if (start < 0 || start >= compressed_chunk_sizes.Length)
				throw new ArgumentOutOfRangeException(nameof(start));
			if (count < 0 || start + count > compressed_chunk_sizes.Length)
				throw new ArgumentOutOfRangeException(nameof(count));

			var ofs = 0;
			for (var i = 0; i < start; ++i)
				ofs += compressed_chunk_sizes[i];
			baseStream.Seek(12 + header.head_size + ofs, SeekOrigin.Begin);

			if (count == 0)
				return Array.Empty<byte>();

			var result = new byte[header.chunk_size * count];

			var tmpp = new byte[header.chunk_size + 64];

			ofs = 0;
			fixed (byte* p = result, tmp = tmpp) {
				count = start + count;
				for (var i = start; i < count; ++i) {
					for (var len = 0; len < compressed_chunk_sizes[i];)
						len += baseStream.Read(new(tmp + len, compressed_chunk_sizes[i] - len));
					if (i == compressed_chunk_sizes.Length - 1)
						Oodle.OodleLZ_Decompress(tmp, compressed_chunk_sizes[i], p + ofs, header.GetLastChunkSize(), 0);
					else
						Oodle.OodleLZ_Decompress(tmp, compressed_chunk_sizes[i], p + ofs, header.chunk_size, 0);
					ofs += header.chunk_size;
				}
			}

			return result;
		}

		/// <summary>
		/// Save the bundle with new contents
		/// </summary>
		public virtual void SaveData(ReadOnlySpan<byte> newData, Oodle.CompressionLevel compressionLevel = Oodle.CompressionLevel.Normal) {
			CachedData = null;

			header.size_decompressed = header.uncompressed_size = newData.Length;
			header.chunk_count = header.uncompressed_size / header.chunk_size;
			if (header.uncompressed_size % header.chunk_size != 0)
				++header.chunk_count;
			header.head_size = header.chunk_count * 4 + 48;

			baseStream.Seek(12 + header.head_size, SeekOrigin.Begin);
			header.compressed_size = 0;
			var chunkSizes = new int[header.chunk_count];
			var compressed = new byte[header.chunk_size + 64];
			fixed (byte* cp = compressed, p = newData) {
				var ptr = p;
				var count = header.chunk_count - 1;
				for (var i = 0; i < count; ++i) {
					var l = (int)Oodle.OodleLZ_Compress(header.compressor, ptr, header.chunk_size, cp, compressionLevel);
					ptr += header.chunk_size;
					header.compressed_size += chunkSizes[i] = l;
					baseStream.Write(compressed, 0, l);
				}
				var l2 = (int)Oodle.OodleLZ_Compress(header.compressor, ptr, header.GetLastChunkSize(), cp, compressionLevel);
				header.compressed_size += chunkSizes[^1] = l2;
				baseStream.Write(compressed, 0, l2);
			}
			header.size_compressed = header.compressed_size;

			baseStream.Seek(0, SeekOrigin.Begin);
			var tmp = new byte[60];
			fixed (byte* p = tmp)
				Marshal.StructureToPtr(header, (IntPtr)p, false);
			baseStream.Write(tmp, 0, 60);
			fixed (int* p = chunkSizes)
				baseStream.Write(new(p, chunkSizes.Length * sizeof(int)));

			baseStream.SetLength(12 + header.head_size + header.compressed_size);
			baseStream.Flush();
		}

		public virtual void Dispose() {
			GC.SuppressFinalize(this);
			if (!streamLeaveOpen)
				baseStream.Close();
		}

		~Bundle() {
			Dispose();
		}
	}
}