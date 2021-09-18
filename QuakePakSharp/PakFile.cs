using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace QuakePakSharp
{
    public class PakFile
    {
        public class Entry
        {
            private string _name;

            /// <summary>
            /// Name of this entry. Max size is 56 characters. Any directory paths should be with '/' slash.
            /// </summary>
            public string Name
            {
                get => _name;
                set
                {
                    if (value.Length > 56)
                        throw new InvalidDataException("Maximum name size is 56 characters");

                    _name = value;
                }
            }

            /// <summary>
            /// Binary data of this entry
            /// </summary>
            public byte[] Data { get; set; } = Array.Empty<byte>();

            public int Size => Data?.Length ?? 0;


            public Entry() { }
            public Entry(string name, byte[] data)
            {
                this._name = name;
                this.Data = data;
            }
        }

        public List<Entry> Entries { get; private set; } = new List<Entry>();

        /// <summary>
        /// Finds an entry by name, including directory.
        /// Returns null if entry is not found.
        /// </summary>
        public Entry FindEntryByName(string name)
        {
            return Entries.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Tries to finds an entry by name, including directory.
        /// Returns true if entry was found.
        /// </summary>
        public bool TryFindEntryByName(string name,out Entry entry)
        {
            entry = Entries.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
            return entry != null;
        }

        /// <summary>
        /// Gets all files by extension. Must contain '.' period.
        /// </summary>
        public IEnumerable<Entry> GetAllEntriesByExtension(string extension)
        {
            return Entries.Where(e => string.Equals(Path.GetExtension(e.Name),extension,StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Get the total size of all the files in the pak.
        /// </summary>
        public int GetTotalSize() => Entries.Sum(e => e.Size);

        /// <summary>
        /// Saves this pak to a file
        /// </summary>
        public void Save(string filename)
        {
            using (var fs = new FileStream(filename, FileMode.OpenOrCreate))
                Save(fs);
        }

        /// <summary>
        /// Saves this pak to a file
        /// </summary>
        public async Task SaveAsync(string filename)
        {
            using (var fs = new FileStream(filename, FileMode.OpenOrCreate))
                await SaveAsync(fs);
        }

        /// <summary>
        /// Saves this pak to a stream.
        /// </summary>
        /// <param name="stream"></param>
        public void Save(Stream stream) => Task.Run(() => SaveAsync(stream)).GetAwaiter().GetResult();
        /// <summary>
        /// Saves this pak to a stream.
        /// </summary>
        public async Task SaveAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                // Write header
                writer.Write(new char[] { 'P', 'A', 'C', 'K' });
                writer.Write(12 + GetTotalSize()); // Index offset
                writer.Write(Entries.Count * 64); // Index size

                ms.Position = 0;
                await ms.ReadAsync(stream, 12, false, cancellationToken);

                // Write files & build index
                ms.Position = 0;
                for (var i = 0; i < Entries.Count; i++)
                {
                    var entry = Entries[i];

                    writer.WriteFixedSizeNullTerminatedString(56, entry.Name);
                    writer.Write((uint)stream.Position);
                    writer.Write((uint)entry.Data.Length);

                    if(entry.Data != null && entry.Data.Length > 0)
                        await stream.WriteAsync(entry.Data, cancellationToken);
                }

                // Write index
                var indexSize = ms.Position;
                ms.Position = 0;
                await ms.ReadAsync(stream, (int)indexSize, false, cancellationToken);
            }
        }

        /// <summary>
        /// Loads a pak file from a file.
        /// </summary>
        public static PakFile FromFile(string filename) {
            using (var fs = new FileStream(filename, FileMode.Open))
                return FromStream(fs);
        }
        /// <summary>
        /// Loads a pak file from a file.
        /// </summary>
        public static async Task<PakFile> FromFileAsync(string filename,CancellationToken cancellationToken = default)
        {
            using (var fs = new FileStream(filename, FileMode.Open))
                return await FromStreamAsync(fs, cancellationToken);
        }

        /// <summary>
        /// Loads a pak file from a stream.
        /// </summary>
        public static PakFile FromStream(Stream stream)
        {
            return Task.Run(() => FromStreamAsync(stream)).GetAwaiter().GetResult();
        }
        /// <summary>
        /// Loads a pak file from a stream.
        /// </summary>
        public static async Task<PakFile> FromStreamAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            var pak = new PakFile();

            using (var ms = new MemoryStream())
            using (var reader = new BinaryReader(ms))
            {
                // Read header
                await stream.ReadAsync(ms, 12, cancellationToken: cancellationToken);

                // Check magic number
                var magicNumber = reader.ReadChars(4);
                if (magicNumber[0] != 'P' || magicNumber[1] != 'A' || magicNumber[2] != 'C' || magicNumber[3] != 'K')
                    throw new InvalidDataException("Not a valid PACK file");

                var indexOffset = reader.ReadUInt32();
                var indexSize = reader.ReadUInt32();

                // Read index
                stream.Position = indexOffset;
                await stream.ReadAsync(ms, (int)indexSize, cancellationToken: cancellationToken);

                // Read all the files
                var count = indexSize / 64;
                for (var i = 0; i < count; i++)
                {
                    var name = reader.ReadFixedSizeNullTerminatedString(56);
                    var offset = reader.ReadUInt32();
                    var size = reader.ReadUInt32();

                    var entry = new Entry();
                    entry.Name = name;
                    entry.Data = new byte[size];
                    stream.Position = offset;

                    await stream.ReadAsync(entry.Data, 0, (int)size, cancellationToken);

                    pak.Entries.Add(entry);
                }
            }

            return pak;
        }
    }
}
