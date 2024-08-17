//
// Rfc2898DeriveBytes.cs: RFC2898 (PKCS#5 v2) Key derivation for Password Based Encryption 
//
// Author:
//	Sebastien Pouliot (sebastien@ximian.com)
//
// (C) 2003 Motus Technologies Inc. (http://www.motus.com)
// Copyright (C) 2004-2007 Novell, Inc (http://www.novell.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//


using System;
using System.Security.Cryptography;

namespace UTJ
{
	public class MyRfc2898DeriveBytes : DeriveBytes
	{
		private const int defaultIterations = 1000;

		private int _iteration;
		private HMACSHA1 _hmac;
		private byte[] _buffer;
		private int _pos;
		private int _f;
		byte[] _tempBuffer;

		// constructors
		public MyRfc2898DeriveBytes(byte[] password, ReadOnlySpan<byte> salt, int iterations)
		{
			if (password == null)
				throw new ArgumentNullException("password");
			if (salt == null)
				throw new ArgumentNullException("salt");
			if (salt.Length < 8)
				throw new ArgumentException("Salt < 8 bytes");

			IterationCount = iterations;
			_hmac = new HMACSHA1(password); // TODO: use Span instead of byte[]
			_tempBuffer = new byte[salt.Length + 4];
			_buffer = new byte[salt.Length + 4];
			salt.CopyTo(_tempBuffer);
			//Buffer.BlockCopy(salt, 0, _tempBuffer, 0, salt.Length);
		}

		// properties

		public int IterationCount
		{
			get { return _iteration; }
			set { _iteration = value < 1 ? defaultIterations : value; }
		}

		//private byte[] F(byte[] s, int c, int i)
		private void F(byte[] u1, Span<byte> s, int c, int i)
		{
			var length = s.Length;
			s[length - 4] = (byte)(i >> 24);
			s[length - 3] = (byte)(i >> 16);
			s[length - 2] = (byte)(i >> 8);
			s[length - 1] = (byte)i;

			// this is like j=0
			//byte[] u1 = _hmac.ComputeHash(s);
			_hmac.TryComputeHash(s, u1, out var bytesWritten);
			var data = u1;
			// so we start at j=1
			Span<byte> un = stackalloc byte[data.Length];
			for (int j = 1; j < c; j++)
			{
				//byte[] un = _hmac.ComputeHash(data);
				if (_hmac.TryComputeHash(data, un, out var written))
				{
					// xor
					for (int k = 0; k < 20; k++)
						u1[k] = (byte)(u1[k] ^ un[k]);
					//data = un;
					un.CopyTo(data);
				}
			}

			//return u1;
		}

		public override byte[] GetBytes(int cb)
		{
			if (cb < 1)
				throw new ArgumentOutOfRangeException("cb");

			int l = cb / 20; // HMACSHA1 == 160 bits == 20 bytes
			int r = cb % 20; // remainder
			if (r != 0)
				l++; // rounding up

			byte[] result = new byte [cb];
			int rpos = 0;
			if (_pos > 0)
			{
				int count = Math.Min(20 - _pos, cb);
				Buffer.BlockCopy(_buffer, _pos, result, 0, count);
				if (count >= cb)
					return result;

				// If we are going to go over the boundaries
				// of the result on our l-1 iteration, reduce
				// l to compensate.
				if (((l - 1) * 20 + count) > result.Length)
					l--;

				_pos = 0;
				rpos = count;
			}

			var data = this._tempBuffer;
			for (int i = 1; i <= l; i++)
			{
				//_buffer = F(data, _iteration, ++_f);
				F(_buffer, data, _iteration, ++_f);
				// we may not need the complete last block
				int count = ((i == l) ? result.Length - rpos : 20);
				Buffer.BlockCopy(_buffer, _pos, result, rpos, count);
				rpos += _pos + count;
				_pos = ((count == 20) ? 0 : count);
			}

			return result;
		}

		public override void Reset()
		{
			_buffer = null;
			_pos = 0;
			_f = 0;
		}
	}
}

