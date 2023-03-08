﻿using System;
using System.Runtime.CompilerServices;
using MonoMod.Utils;

namespace BepInEx.IL2CPP
{
	/// <summary>
	/// 
	///     Based on https://github.com/kubo/funchook
	/// </summary>
	internal abstract class MemoryBuffer
	{
		/// <summary>
		///     Common page size on Unix and Windows (4k).
		/// </summary>
		protected const int PAGE_SIZE = 0x1000;

		/// <summary>
		///     Allocation granularity on Windows (but can be reused in other implementations).
		/// </summary>
		protected const int ALLOCATION_UNIT = 0x100000;

		private static MemoryBuffer instance;
		public static MemoryBuffer Instance => instance ??= Init();

		public abstract IntPtr Allocate(IntPtr func);
		public abstract void Free(IntPtr buffer);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		protected static long RoundDown(long num, long unit)
		{
			return num & ~(unit - 1);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		protected static long RoundUp(long num, long unit)
		{
			return (num + unit - 1) & ~ (unit - 1);
		}

		private static MemoryBuffer Init()
		{
			if (PlatformHelper.Is(Platform.Windows))
				return new WindowsMemoryBuffer();
			if (PlatformHelper.Is(Platform.Unix))
				return new UnixMemoryBuffer();
			throw new NotImplementedException();
		}
	}
}