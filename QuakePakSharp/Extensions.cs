using System;
using System.Buffers;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace QuakePakSharp
{
    static class Extensions
    {
        public static string ReadFixedSizeNullTerminatedString(this BinaryReader reader, int size)
        {
            var sb = new StringBuilder(size);
            var chars = reader.ReadChars(size);

            for (var i = 0; i < chars.Length; i++)
            {
                var c = chars[i];
                if (c != 0)
                    sb.Append(c);
                else
                    break;
            }

            return sb.ToString();
        }

        public static void WriteFixedSizeNullTerminatedString(this BinaryWriter writer,int size,string str)
        {
            writer.Write(str.ToCharArray());
            for (var i = str.Length; i < size; i++)
                writer.Write((byte)0);
        }

        public static async Task ReadAsync(this Stream stream, Stream targetStream, int count, bool resetPosition=true, CancellationToken cancellationToken = default)
        {
            const int bufferLength = 1024 * 32; // 32kb
            var pool = ArrayPool<byte>.Shared;
            var buffer = pool.Rent(bufferLength);
            var startingPosition = targetStream.Position;

            try
            {
                var bytesLeft = count;

                while (bytesLeft > 0)
                {
                    var requestedRead = Math.Min(bufferLength, bytesLeft); // Calculate how much to read
                    var actualRead = await stream.ReadAsync(buffer, 0, requestedRead, cancellationToken);

                    // Throw exception if the requested amount was not available to be read
                    if (actualRead < requestedRead)
                        throw new InvalidDataException($"Unexpected end of stream");

                    // Write to the target stream
                    await targetStream.WriteAsync(buffer, 0, actualRead, cancellationToken);

                    bytesLeft -= actualRead; // Decrement the amount of bytes left to read
                }

                if (resetPosition)
                    targetStream.Position = startingPosition;
            }
            finally
            {
                pool.Return(buffer);
            }
        }
    }
}
