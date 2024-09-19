// //////////////////////////////////////////////////////////////////////
// /// Original code is as follows:
// /// https://stackoverflow.com/questions/5026409/how-to-add-seek-and-position-capabilities-to-cryptostream
// ///
// /// MEMO:
// /// Streamを二重にしてもつ必要があるか？
// /// CreateDecryptorで複合化しようとするとエラーになる、当にこの処理は正しいのか？
// /// 各所でメモリアロケートしたがるので全部自前実装したい
// /// 線形ベクタが空バッファでいいとは本当か？
// //////////////////////////////////////////////////////////////////////
//
// using System;
// using System.IO;
// using System.Security.Cryptography;
//
// namespace UTJ
// {
//     public class SeekableAesStream : Stream
//     {
//         readonly Aes _provider;
//         readonly int blockSizeInByte = 0;
//         readonly byte[] outBuffer, nonce;
//         readonly byte[] emptyIvBuffer = new byte[16];
//      
//         Stream _baseStream;   
//         ICryptoTransform _encryptor;
//
//         public SeekableAesStream(string fullPath, byte[] password, ReadOnlySpan<byte> salt, bool allowWriting = false)
//         {
//             this._provider = Aes.Create();
//             this._provider.KeySize = 128;
//             this._provider.Mode = CipherMode.ECB;
//             this._provider.Padding = PaddingMode.None;
//             this._provider.IV = emptyIvBuffer;
//             
//             this.CreateStream(fullPath, password, salt, allowWriting);
//             
//             this.blockSizeInByte = this._provider.BlockSize / 8;
//             this.outBuffer = new byte[blockSizeInByte];
//             this.nonce = new byte[blockSizeInByte];
//         }
//
//         public void CreateStream(string fullPath, byte[] password, ReadOnlySpan<byte> salt, bool allowWriting = false)
//         {
//             if (allowWriting)
//                 this._baseStream = new FileStream(fullPath, FileMode.OpenOrCreate, FileAccess.Write);
//             else
//                 this._baseStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
//             
//             using (var key = new MyRfc2898DeriveBytes(password, salt, 100))
//             {
//                 var providerKey = key.GetBytes(_provider.KeySize / 8);
//                 this._provider.Key = providerKey;
//                 this._encryptor = _provider.CreateEncryptor(providerKey, emptyIvBuffer);
//             }
//         }
//
//         void Cipher(Span<byte> buffer, int offset, int count, long streamPos)
//         {
//             //find block number
//             var blockNumber = (streamPos / blockSizeInByte) + 1;
//             var keyPos = streamPos % blockSizeInByte;
//
//             var init = false;
//
//             for (int i = offset; i < count; i++)
//             {
//                 //encrypt the nonce to form next xor buffer (unique key)
//                 if (!init || (keyPos % blockSizeInByte) == 0)
//                 {
//                     BitConverter.TryWriteBytes(nonce, blockNumber);
//                     this._encryptor.TransformBlock(nonce, 0, nonce.Length, outBuffer, 0);
//                     if (init)
//                         keyPos = 0;
//                     init = true;
//                     blockNumber++;
//                 }
//
//                 buffer[i] ^= outBuffer[keyPos]; //simple XOR with generated unique key
//                 keyPos++;
//             }
//         }
//
//         public override bool CanRead => _baseStream.CanRead;
//         public override bool CanSeek => _baseStream.CanSeek;
//         public override bool CanWrite => _baseStream.CanWrite;
//         public override long Length => _baseStream.Length;
//
//         public override long Position
//         {
//             get => _baseStream.Position;
//             set => _baseStream.Position = value;
//         }
//
//         public override void Flush() => _baseStream.Flush();
//         public override void SetLength(long value) => _baseStream.SetLength(value);
//         public override long Seek(long offset, SeekOrigin origin) => _baseStream.Seek(offset, origin);
//
//         public override int Read(byte[] buffer, int offset, int count)
//         {
//             var streamPos = Position;
//             var ret = _baseStream.Read(buffer, offset, count);
//             Cipher(buffer, offset, count, streamPos);
//             return ret;
//         }
//
//         public override void Write(byte[] buffer, int offset, int count)
//         {
//             Cipher(buffer, offset, count, Position);
//             _baseStream.Write(buffer, offset, count);
//         }
//
//         protected override void Dispose(bool disposing)
//         {
//             if (disposing)
//             {
//                 this._encryptor?.Dispose();
//                 this._provider?.Dispose();
//                 this._baseStream?.Dispose();
//             }
//
//             base.Dispose(disposing);
//         }
//
//         public void Clear()
//         {
//             this._encryptor?.Dispose();
//             this._encryptor = null;
//         }
//     }
// }