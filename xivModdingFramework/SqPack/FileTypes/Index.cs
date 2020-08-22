﻿// xivModdingFramework
// Copyright © 2018 Rafael Gonzalez - All Rights Reserved
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using xivModdingFramework.Cache;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Helpers;
using xivModdingFramework.Mods.FileTypes;

namespace xivModdingFramework.SqPack.FileTypes
{
    /// <summary>
    /// This class contains the methods that deal with the .index file type 
    /// </summary>
    public class Index
    {
        private const string IndexExtension = ".win32.index";
        private const string Index2Extension = ".win32.index2";
        private readonly DirectoryInfo _gameDirectory;
        private static SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1, 1);

        public Index(DirectoryInfo gameDirectory)
        {
            _gameDirectory = gameDirectory;
        }

        /// <summary>
        /// Update the dat count within the index files.
        /// </summary>
        /// <param name="dataFile">The data file to update the index for.</param>
        /// <param name="datNum">The dat number to update to.</param>
        public void UpdateIndexDatCount(XivDataFile dataFile, int datNum)
        {
            var datCount = (byte)(datNum + 1);

            var indexPaths = new[]
            {
                Path.Combine(_gameDirectory.FullName, $"{dataFile.GetDataFileName()}{IndexExtension}"),
                Path.Combine(_gameDirectory.FullName, $"{dataFile.GetDataFileName()}{Index2Extension}")
            };

            foreach (var indexPath in indexPaths)
            {
                _semaphoreSlim.Wait();
                try
                {
                    using (var bw = new BinaryWriter(File.OpenWrite(indexPath)))
                    {
                        bw.BaseStream.Seek(1104, SeekOrigin.Begin);
                        bw.Write(datCount);
                    }
                }
                finally
                {
                    _semaphoreSlim.Release();
                }
            }
        }

        /// <summary>
        /// Gets the dat count within the index files.
        /// </summary>
        /// <param name="dataFile">The data file to update the index for.</param>
        public (int Index1, int Index2) GetIndexDatCount(XivDataFile dataFile)
        {
            int index1 = 0, index2 = 0;

            var indexPaths = new[]
            {
                Path.Combine(_gameDirectory.FullName, $"{dataFile.GetDataFileName()}{IndexExtension}"),
                Path.Combine(_gameDirectory.FullName, $"{dataFile.GetDataFileName()}{Index2Extension}")
            };

            _semaphoreSlim.Wait();
            try
            {
                for (var i = 0; i < indexPaths.Length; i++)
                {
                    using (var br = new BinaryReader(File.OpenRead(indexPaths[i])))
                    {
                        br.BaseStream.Seek(1104, SeekOrigin.Begin);
                        if (i == 0)
                        {
                            index1 = br.ReadByte();
                        }
                        else
                        {
                            index2 = br.ReadByte();
                        }
                    }
                }
            } finally
            {
                _semaphoreSlim.Release();
            }

            return (index1, index2);
        }

        /// <summary>
        /// Gets the SHA1 hash for the file section
        /// </summary>
        /// <param name="dataFile">The data file to get the hash for</param>
        /// <returns>The byte array containing the hash value</returns>
        public byte[] GetIndexSection1Hash(DirectoryInfo indexPath)
        {
            byte[] sha1Bytes;

            _semaphoreSlim.Wait();
            try
            {
                using (var br = new BinaryReader(File.OpenRead(indexPath.FullName)))
                {
                    br.BaseStream.Seek(1040, SeekOrigin.Begin);
                    sha1Bytes = br.ReadBytes(20);
                }
            }
            finally
            {
                _semaphoreSlim.Release();
            }

            return sha1Bytes;
        }

        /// <summary>
        /// Gets the SHA1 hash for the file section
        /// </summary>
        /// <param name="dataFile">The data file to get the hash for</param>
        /// <returns>The byte array containing the hash value</returns>
        public byte[] GetIndexSection2Hash(DirectoryInfo indexPath)
        {
            byte[] sha1Bytes;

            _semaphoreSlim.Wait();
            try
            {
                using (var br = new BinaryReader(File.OpenRead(indexPath.FullName)))
                {
                    br.BaseStream.Seek(1116, SeekOrigin.Begin);
                    sha1Bytes = br.ReadBytes(20);
                }
            }
            finally
            {
                _semaphoreSlim.Release();
            }

            return sha1Bytes;
        }

        /// <summary>
        /// Gets the SHA1 hash for the file section
        /// </summary>
        /// <param name="dataFile">The data file to get the hash for</param>
        /// <returns>The byte array containing the hash value</returns>
        public byte[] GetIndexSection3Hash(DirectoryInfo indexPath)
        {
            byte[] sha1Bytes;

            _semaphoreSlim.Wait();
            try
            {
                using (var br = new BinaryReader(File.OpenRead(indexPath.FullName)))
                {
                    br.BaseStream.Seek(1188, SeekOrigin.Begin);
                    sha1Bytes = br.ReadBytes(20);
                }
            }
            finally
            {
                _semaphoreSlim.Release();
            }
            

            return sha1Bytes;
        }

        /// <summary>
        /// check files added by textools
        /// </summary>
        /// <param name="dataFile">XivDataFile</param>
        /// <returns></returns>
        public Task<bool> HaveFilesAddedByTexTools(XivDataFile dataFile)
        {
            return Task.Run(async () =>
            {
                var indexPath = Path.Combine(_gameDirectory.FullName, $"{dataFile.GetDataFileName()}{IndexExtension}");

                // These are the offsets to relevant data
                const int fileCountOffset = 1036;
                const int dataStartOffset = 2048;

                await _semaphoreSlim.WaitAsync();

                try
                {
                    using (var br = new BinaryReader(File.OpenRead(indexPath)))
                    {
                        br.BaseStream.Seek(fileCountOffset, SeekOrigin.Begin);
                        var fileCount = br.ReadInt32();

                        br.BaseStream.Seek(dataStartOffset, SeekOrigin.Begin);

                        // loop through each file entry
                        for (var i = 0; i < fileCount; i += 16)
                        {
                            br.BaseStream.Position += 8;
                            var offset = br.ReadInt32();
                            if (offset == -1)
                                return true;
                            br.BaseStream.Position += 4;
                        }
                    }
                }
                finally
                {
                    _semaphoreSlim.Release();
                }
                return false;
            });
        }

        public async Task<long> GetDataOffset(string fullPath)
        {
            var dataFile = IOUtil.GetDataFileFromPath(fullPath);

            var pathHash = HashGenerator.GetHash(fullPath.Substring(0, fullPath.LastIndexOf("/", StringComparison.Ordinal)));
            var fileHash = HashGenerator.GetHash(Path.GetFileName(fullPath));
            return await GetDataOffset(pathHash, fileHash, dataFile);

        }

        /// <summary>
        /// Retrieves all of the offsets for an arbitrary list of files in the FFXIV file system, using a batch operation for speed.
        /// </summary>
        /// <param name="files"></param>
        /// <returns></returns>
        public async Task<Dictionary<string, long>> GetDataOffsets(List<string> files)
        {
            // Here we need to do two things.
            // 1. Group the files by their data file.
            // 2. Hash the files into their folder/file hashes and build the dictionaries to pass to the private function.

            // Thankfully, we can just do all of that in one pass.

            // This is keyed by Data File => Folder hash => File Hash => Full File Path
            // This is used as dictionaries vs compound objects or lists b/c dictionary key look ups are immensely faster than
            // full list scans, when working with lists of potentially 10000+ files.
            Dictionary<XivDataFile, Dictionary<int, Dictionary<int, string>>> dict = new Dictionary<XivDataFile, Dictionary<int, Dictionary<int, string>>>();

            foreach(var file in files)
            {
                var dataFile = IOUtil.GetDataFileFromPath(file);
                var pathHash = HashGenerator.GetHash(file.Substring(0, file.LastIndexOf("/", StringComparison.Ordinal)));
                var fileHash = HashGenerator.GetHash(Path.GetFileName(file));

                
                if(!dict.ContainsKey(dataFile))
                {
                    dict.Add(dataFile, new Dictionary<int, Dictionary<int, string>>());
                }

                if(!dict[dataFile].ContainsKey(pathHash))
                {
                    dict[dataFile].Add(pathHash, new Dictionary<int, string>());
                }

                if(!dict[dataFile][pathHash].ContainsKey(fileHash))
                {
                    dict[dataFile][pathHash].Add(fileHash, file);
                }
            }

            var ret = new Dictionary<string, long>();
            foreach(var kv in dict)
            {
                var offsets = await GetDataOffsets(kv.Key, kv.Value);

                foreach(var kv2 in offsets)
                {
                    if(!ret.ContainsKey(kv2.Key))
                    {
                        ret.Add(kv2.Key, kv2.Value);
                    }
                }
            }

            return ret;
        }


        /// <summary>
        /// Retrieves all of the offsets for an arbitrary list of files within the same data file.
        /// </summary>
        /// <param name="dataFile"></param>
        /// <returns></returns>
        private async Task<Dictionary<string, long>> GetDataOffsets(XivDataFile dataFile, Dictionary<int, Dictionary<int, string>> FolderFiles)
        {
            var ret = new Dictionary<string, long>();
            return await Task.Run(() =>
            {
                var indexPath = Path.Combine(_gameDirectory.FullName, $"{dataFile.GetDataFileName()}{IndexExtension}");

                // These are the offsets to relevant data
                const int fileCountOffset = 1036;
                const int dataStartOffset = 2048;

                int count = 0;
                _semaphoreSlim.Wait();
                try
                {
                    using (var br = new BinaryReader(File.OpenRead(indexPath)))
                    {
                        br.BaseStream.Seek(fileCountOffset, SeekOrigin.Begin);
                        var fileCount = br.ReadInt32();

                        br.BaseStream.Seek(dataStartOffset, SeekOrigin.Begin);

                        // loop through each file entry
                        for (var i = 0; i < fileCount; i += 16)
                        {
                            var fileNameHash = br.ReadInt32();
                            var folderPathHash = br.ReadInt32();
                            long offset = br.ReadUInt32();
                            var unused = br.ReadInt32();

                            if (FolderFiles.ContainsKey(folderPathHash))
                            {
                                if(FolderFiles[folderPathHash].ContainsKey(fileNameHash))
                                {
                                    count++;
                                    ret.Add(FolderFiles[folderPathHash][fileNameHash], offset * 8);
                                }
                            }
                        }
                    }
                }
                finally
                {
                    _semaphoreSlim.Release();
                }

                return ret;
            });
        }
        /// <summary>
        /// Retrieves all of the offsets for an arbitrary list of files in the FFXIV file system, using a batch operation for speed.
        /// </summary>
        /// <param name="files"></param>
        /// <returns></returns>
        public async Task<Dictionary<string, long>> GetDataOffsetsIndex2(List<string> files)
        {
            // Here we need to do two things.
            // 1. Group the files by their data file.
            // 2. Get their file hashes.

            // Thankfully, we can just do all of that in one pass.

            // This is keyed by Data File => Folder hash => File Hash => Full File Path
            // This is used as dictionaries vs compound objects or lists b/c dictionary key look ups are immensely faster than
            // full list scans, when working with lists of potentially 10000+ files.
            Dictionary<XivDataFile, Dictionary<uint, string>> dict = new Dictionary<XivDataFile, Dictionary<uint, string>>();

            foreach (var file in files)
            {
                var dataFile = IOUtil.GetDataFileFromPath(file);
                var fullHash = (uint) HashGenerator.GetHash(file);


                if (!dict.ContainsKey(dataFile))
                {
                    dict.Add(dataFile, new Dictionary<uint, string>());
                }

                if (!dict[dataFile].ContainsKey(fullHash))
                {
                    dict[dataFile].Add(fullHash, file);
                }
            }

            var ret = new Dictionary<string, long>();
            foreach (var kv in dict)
            {
                var offsets = await GetDataOffsetsIndex2(kv.Key, kv.Value);

                foreach (var kv2 in offsets)
                {
                    if (!ret.ContainsKey(kv2.Key))
                    {
                        ret.Add(kv2.Key, kv2.Value);
                    }
                }
            }

            return ret;
        }


        /// <summary>
        /// Retrieves all of the offsets for an arbitrary list of files within the same data file, via their Index2 entries.
        /// </summary>
        /// <param name="dataFile"></param>
        /// <returns></returns>
        private async Task<Dictionary<string, long>> GetDataOffsetsIndex2(XivDataFile dataFile, Dictionary<uint, string> fileHashes)
        {
            var ret = new Dictionary<string, long>();
            return await Task.Run(async () =>
            {
                var index2Path = Path.Combine(_gameDirectory.FullName, $"{dataFile.GetDataFileName()}{Index2Extension}");


                var SegmentHeaders = new int[4];
                var SegmentOffsets = new int[4];
                var SegmentSizes = new int[4];

                // Segment header offsets
                SegmentHeaders[0] = 1028;                   // Files
                SegmentHeaders[1] = 1028 + (72 * 1) + 4;    // Unknown
                SegmentHeaders[2] = 1028 + (72 * 2) + 4;    // Unknown
                SegmentHeaders[3] = 1028 + (72 * 3) + 4;    // Folders


                await _semaphoreSlim.WaitAsync();
                try
                {

                    // Might as well grab the whole thing since we're doing a full scan.
                    byte[] originalIndex = File.ReadAllBytes(index2Path);

                    // Get all the segment header data
                    for (int i = 0; i < SegmentHeaders.Length; i++)
                    {
                        SegmentOffsets[i] = BitConverter.ToInt32(originalIndex, SegmentHeaders[i] + 4);
                        SegmentSizes[i] = BitConverter.ToInt32(originalIndex, SegmentHeaders[i] + 8);
                    }

                    int fileCount = SegmentSizes[0] / 8;

                    for (int i = 0; i < fileCount; i++)
                    {
                        int position = SegmentOffsets[0] + (i * 8);
                        uint iFullPathHash = BitConverter.ToUInt32(originalIndex, position);
                        uint iOffset = BitConverter.ToUInt32(originalIndex, position + 4);

                        // Index 2 is just in hash order, so find the spot where we fit in.
                        if (fileHashes.ContainsKey(iFullPathHash))
                        {
                            long offset = (long)iOffset;
                            ret.Add(fileHashes[iFullPathHash], offset * 8);
                        }
                    }
                }
                finally
                {
                    _semaphoreSlim.Release();
                }
                return ret;
            });
        }


        public async Task<long> GetDataOffsetIndex2(string fullPath)
        {
            var fullPathHash = HashGenerator.GetHash(fullPath);
            var uFullPathHash = BitConverter.ToUInt32(BitConverter.GetBytes(fullPathHash), 0);
            var dataFile = IOUtil.GetDataFileFromPath(fullPath);
            var index2Path = Path.Combine(_gameDirectory.FullName, $"{dataFile.GetDataFileName()}{Index2Extension}");

            var SegmentHeaders = new int[4];
            var SegmentOffsets = new int[4];
            var SegmentSizes = new int[4];

            // Segment header offsets
            SegmentHeaders[0] = 1028;                   // Files
            SegmentHeaders[1] = 1028 + (72 * 1) + 4;    // Unknown
            SegmentHeaders[2] = 1028 + (72 * 2) + 4;    // Unknown
            SegmentHeaders[3] = 1028 + (72 * 3) + 4;    // Folders


            await _semaphoreSlim.WaitAsync();
            try
            {

                // Dump the index into memory, since we're going to have to inject data.
                byte[] originalIndex = File.ReadAllBytes(index2Path);

                // Get all the segment header data
                for (int i = 0; i < SegmentHeaders.Length; i++)
                {
                    SegmentOffsets[i] = BitConverter.ToInt32(originalIndex, SegmentHeaders[i] + 4);
                    SegmentSizes[i] = BitConverter.ToInt32(originalIndex, SegmentHeaders[i] + 8);
                }

                int fileCount = SegmentSizes[0] / 8;

                for (int i = 0; i < fileCount; i++)
                {
                    int position = SegmentOffsets[0] + (i * 8);
                    uint iFullPathHash = BitConverter.ToUInt32(originalIndex, position);
                    uint iOffset = BitConverter.ToUInt32(originalIndex, position + 4);

                    // Index 2 is just in hash order, so find the spot where we fit in.
                    if (iFullPathHash == uFullPathHash)
                    {
                        long offset = (long)iOffset;

                        return offset * 8;
                    }
                }
            }
            finally
            {
                _semaphoreSlim.Release();
            }
            return 0;
        }

        /// <summary>
        /// Gets the offset for the data in the .dat file
        /// </summary>
        /// <param name="hashedFolder">The hashed value of the folder path</param>
        /// <param name="hashedFile">The hashed value of the file name</param>
        /// <param name="dataFile">The data file to look in</param>
        /// <returns>The offset to the data</returns>
        public Task<long> GetDataOffset(int hashedFolder, int hashedFile, XivDataFile dataFile)
        {
            return Task.Run(async () =>
            {
                var indexPath = Path.Combine(_gameDirectory.FullName, $"{dataFile.GetDataFileName()}{IndexExtension}");
                long offset = 0;

                // These are the offsets to relevant data
                const int fileCountOffset = 1036;
                const int dataStartOffset = 2048;

                await _semaphoreSlim.WaitAsync();

                try
                {
                    using (var br = new BinaryReader(File.OpenRead(indexPath)))
                    {
                        br.BaseStream.Seek(fileCountOffset, SeekOrigin.Begin);
                        var fileCount = br.ReadInt32();

                        br.BaseStream.Seek(dataStartOffset, SeekOrigin.Begin);

                        // loop through each file entry
                        for (var i = 0; i < fileCount; br.ReadBytes(4), i += 16)
                        {
                            var fileNameHash = br.ReadInt32();

                            // check if the provided file name hash matches the current file name hash
                            if (fileNameHash == hashedFile)
                            {
                                var folderPathHash = br.ReadInt32();

                                // check if the provided folder path hash matches the current folder path hash
                                if (folderPathHash == hashedFolder)
                                {
                                    // this is the entry we are looking for, get the offset and break out of the loop
                                    offset = br.ReadUInt32();
                                    offset = offset * 8;
                                    break;
                                }

                                br.ReadBytes(4);
                            }
                            else
                            {
                                br.ReadBytes(8);
                            }
                        }
                    }
                }
                finally
                {
                    _semaphoreSlim.Release();
                }

                return offset;
            });
        }

        /// <summary>
        /// Gets the file dictionary for the data in the .dat file
        /// </summary>
        /// <param name="dataFile">The data file to look in</param>
        /// <returns>Dictionary containing (concatenated string of file+folder hashes, offset) </returns>
        public Task<Dictionary<string, long>> GetFileDictionary(XivDataFile dataFile)
        {
            return Task.Run(() =>
            {

                _semaphoreSlim.Wait();
                var fileDictionary = new Dictionary<string, long>();
                try
                {
                    var indexPath = Path.Combine(_gameDirectory.FullName, $"{dataFile.GetDataFileName()}{IndexExtension}");

                    // These are the offsets to relevant data
                    const int fileCountOffset = 1036;
                    const int dataStartOffset = 2048;

                    using (var br = new BinaryReader(File.OpenRead(indexPath)))
                    {
                        br.BaseStream.Seek(fileCountOffset, SeekOrigin.Begin);
                        var fileCount = br.ReadInt32();

                        br.BaseStream.Seek(dataStartOffset, SeekOrigin.Begin);

                        // loop through each file entry
                        for (var i = 0; i < fileCount; br.ReadBytes(4), i += 16)
                        {
                            var fileNameHash = br.ReadInt32();
                            var folderPathHash = br.ReadInt32();
                            long offset = br.ReadUInt32() * 8;

                            fileDictionary.Add($"{fileNameHash}{folderPathHash}", offset);
                        }
                    }
                } finally
                {
                    _semaphoreSlim.Release();
                }

                return fileDictionary;
            });
        }

        /// <summary>
        /// Checks whether the index file contains any of the folders passed in
        /// </summary>
        /// <remarks>
        /// Runs through the index file once checking if the hashed folder value exists in the dictionary
        /// then adds it to the list if it does.
        /// </remarks>
        /// <param name="hashNumDictionary">A Dictionary containing the folder hash and item number</param>
        /// <param name="dataFile">The data file to look in</param>
        /// <returns></returns>
        public Task<List<int>> GetFolderExistsList(Dictionary<int, int> hashNumDictionary, XivDataFile dataFile)
        {
            return Task.Run(async () =>
            {
                await _semaphoreSlim.WaitAsync();

                // HashSet because we don't want any duplicates
                var folderExistsList = new HashSet<int>();

                try
                {
                    // These are the offsets to relevant data
                    const int fileCountOffset = 1036;
                    const int dataStartOffset = 2048;

                    var indexPath = Path.Combine(_gameDirectory.FullName, $"{dataFile.GetDataFileName()}{IndexExtension}");

                    using (var br = new BinaryReader(File.OpenRead(indexPath)))
                    {
                        br.BaseStream.Seek(fileCountOffset, SeekOrigin.Begin);
                        var totalFiles = br.ReadInt32();

                        br.BaseStream.Seek(dataStartOffset, SeekOrigin.Begin);
                        for (var i = 0; i < totalFiles; br.ReadBytes(4), i += 16)
                        {
                            br.ReadBytes(4);

                            var folderPathHash = br.ReadInt32();

                            if (hashNumDictionary.ContainsKey(folderPathHash))
                            {
                                folderExistsList.Add(hashNumDictionary[folderPathHash]);

                                br.ReadBytes(4);
                            }
                            else
                            {
                                br.ReadBytes(4);
                            }
                        }
                    }
                }
                finally
                {
                    _semaphoreSlim.Release();
                }

                return folderExistsList.ToList();
            });
        }

        public async Task<bool> FileExists(string fullPath)
        {
            var dataFile = IOUtil.GetDataFileFromPath(fullPath);

            var pathHash = HashGenerator.GetHash(fullPath.Substring(0, fullPath.LastIndexOf("/", StringComparison.Ordinal)));
            var fileHash = HashGenerator.GetHash(Path.GetFileName(fullPath));
            return await FileExists(fileHash, pathHash, dataFile);
        }


        /// <summary>
        /// Tests if a given file path in FFXIV's internal directory structure
        /// is one that ships with FFXIV, or something added by the framework.
        /// </summary>
        /// <returns></returns>
        public async Task<bool> IsDefaultFilePath(string fullPath)
        {
            // The framework adds flag files alongside every custom created file.
            // This lets you check for them even if the Modlist gets corrupted/lost.
            var exists = await FileExists(fullPath);
            var hasFlag = await FileExists(fullPath + ".flag");

            // In order to be considered a DEFAULT file, the file must both EXIST *and* not have a flag.
            var stockFile = exists && !hasFlag;
            return stockFile;
        }

        /// <summary>
        /// Determines whether the given file path exists
        /// </summary>
        /// <param name="fileHash">The hashed file</param>
        /// <param name="folderHash">The hashed folder</param>
        /// <param name="dataFile">The data file</param>
        /// <returns>True if it exists, False otherwise</returns>
        public async Task<bool> FileExists(int fileHash, int folderHash, XivDataFile dataFile)
        {
            var indexPath = Path.Combine(_gameDirectory.FullName, $"{dataFile.GetDataFileName()}{IndexExtension}");

            // These are the offsets to relevant data
            const int fileCountOffset = 1036;
            const int dataStartOffset = 2048;

            bool exists = false;

            await _semaphoreSlim.WaitAsync();
            try
            {
                exists = await Task.Run(() =>
                {
                   using (var br = new BinaryReader(File.OpenRead(indexPath)))
                   {
                       br.BaseStream.Seek(fileCountOffset, SeekOrigin.Begin);
                       var numOfFiles = br.ReadInt32();

                       br.BaseStream.Seek(dataStartOffset, SeekOrigin.Begin);
                       for (var i = 0; i < numOfFiles; br.ReadBytes(4), i += 16)
                       {
                           var fileNameHash = br.ReadInt32();

                           if (fileNameHash == fileHash)
                           {
                               var folderPathHash = br.ReadInt32();

                               if (folderPathHash == folderHash)
                               {
                                   return true;
                               }

                               br.ReadBytes(4);
                           }
                           else
                           {
                               br.ReadBytes(8);
                           }
                       }
                   }
                   return false;
               });
            } finally
            {
                _semaphoreSlim.Release();
            }
            return exists;
        }

        /// <summary>
        /// Determines whether the given folder path exists
        /// </summary>
        /// <param name="folderHash">The hashed folder</param>
        /// <param name="dataFile">The data file</param>
        /// <returns>True if it exists, False otherwise</returns>
        public async Task<bool> FolderExists(int folderHash, XivDataFile dataFile)
        {
            var indexPath = Path.Combine(_gameDirectory.FullName, $"{dataFile.GetDataFileName()}{IndexExtension}");

            // These are the offsets to relevant data
            const int fileCountOffset = 1036;
            const int dataStartOffset = 2048;

            await _semaphoreSlim.WaitAsync();
            try
            {
                return await Task.Run(() =>
                {
                    using (var br = new BinaryReader(File.OpenRead(indexPath)))
                    {
                        br.BaseStream.Seek(fileCountOffset, SeekOrigin.Begin);
                        var numOfFiles = br.ReadInt32();

                        br.BaseStream.Seek(dataStartOffset, SeekOrigin.Begin);
                        for (var i = 0; i < numOfFiles; br.ReadBytes(4), i += 16)
                        {
                            var fileNameHash = br.ReadInt32();

                            var folderPathHash = br.ReadInt32();

                            if (folderPathHash == folderHash)
                            {
                                return true;
                            }

                            br.ReadBytes(4);
                        }
                    }

                    return false;
                });
            } finally
            {
                _semaphoreSlim.Release();
            }
        }

        /// <summary>
        /// Gets all the file offsets in a given folder path
        /// </summary>
        /// <param name="hashedFolder">The hashed value of the folder path</param>
        /// <param name="dataFile">The data file to look in</param>
        /// <returns>A list of all of the offsets in the given folder</returns>
        public async Task<List<int>> GetAllFileOffsetsInFolder(int hashedFolder, XivDataFile dataFile)
        {
            var fileOffsetList = new List<int>();

            // These are the offsets to relevant data
            const int fileCountOffset = 1036;
            const int dataStartOffset = 2048;

            var indexPath = Path.Combine(_gameDirectory.FullName, $"{dataFile.GetDataFileName()}{IndexExtension}");

            await _semaphoreSlim.WaitAsync();
            try
            {
                await Task.Run(() =>
                {
                    using (var br = new BinaryReader(File.OpenRead(indexPath)))
                    {
                        br.BaseStream.Seek(fileCountOffset, SeekOrigin.Begin);
                        var totalFiles = br.ReadInt32();

                        br.BaseStream.Seek(dataStartOffset, SeekOrigin.Begin);
                        for (var i = 0; i < totalFiles; br.ReadBytes(4), i += 16)
                        {
                            br.ReadBytes(4);

                            var folderPathHash = br.ReadInt32();

                            if (folderPathHash == hashedFolder)
                            {
                                fileOffsetList.Add(br.ReadInt32() * 8);
                            }
                            else
                            {
                                br.ReadBytes(4);
                            }
                        }
                    }
                });
            }finally
            {
                _semaphoreSlim.Release();
            }

            return fileOffsetList;
        }

        /// <summary>
        /// Gets all the folder hashes in a given folder path
        /// </summary>
        /// <param name="dataFile">The data file to look in</param>
        /// <returns>A list of all of the folder hashes</returns>
        public async Task<List<int>> GetAllFolderHashes(XivDataFile dataFile)
        {
            var folderHashList = new HashSet<int>();

            // These are the offsets to relevant data
            const int fileCountOffset = 1036;
            const int dataStartOffset = 2048;

            var indexPath = Path.Combine(_gameDirectory.FullName, $"{dataFile.GetDataFileName()}{IndexExtension}");

            await _semaphoreSlim.WaitAsync();
            try
            {
                await Task.Run(() =>
                {
                    using (var br = new BinaryReader(File.OpenRead(indexPath)))
                    {
                        br.BaseStream.Seek(fileCountOffset, SeekOrigin.Begin);
                        var totalFiles = br.ReadInt32();

                        br.BaseStream.Seek(dataStartOffset, SeekOrigin.Begin);
                        for (var i = 0; i < totalFiles; br.ReadBytes(4), i += 16)
                        {
                            br.ReadBytes(4);

                            var folderPathHash = br.ReadInt32();

                            folderHashList.Add(folderPathHash);

                            br.ReadBytes(4);
                        }
                    }
                });
            } finally
            {
                _semaphoreSlim.Release();
            }

            return folderHashList.ToList();
        }

        /// <summary>
        /// Get all the hashed values of the files in a given folder 
        /// </summary>
        /// <param name="hashedFolder">The hashed value of the folder path</param>
        /// <param name="dataFile">The data file to look in</param>
        /// <returns>A list containing the hashed values of the files in the given folder</returns>
        public async Task<List<int>> GetAllHashedFilesInFolder(int hashedFolder, XivDataFile dataFile)
        {
            var fileHashesList = new List<int>();

            // These are the offsets to relevant data
            const int fileCountOffset = 1036;
            const int dataStartOffset = 2048;

            var indexPath = Path.Combine(_gameDirectory.FullName, $"{dataFile.GetDataFileName()}{IndexExtension}");

            await _semaphoreSlim.WaitAsync();
            try
            {
                await Task.Run(() =>
                {
                    using (var br = new BinaryReader(File.OpenRead(indexPath)))
                    {
                        br.BaseStream.Seek(fileCountOffset, SeekOrigin.Begin);
                        var totalFiles = br.ReadInt32();

                        br.BaseStream.Seek(dataStartOffset, SeekOrigin.Begin);
                        for (var i = 0; i < totalFiles; br.ReadBytes(4), i += 16)
                        {
                            var hashedFile = br.ReadInt32();

                            var folderPathHash = br.ReadInt32();

                            if (folderPathHash == hashedFolder)
                            {
                                fileHashesList.Add(hashedFile);
                            }

                            br.ReadBytes(4);
                        }
                    }
                });
            }
            finally
            {
                _semaphoreSlim.Release();
            }
            return fileHashesList;
        }

        /// <summary>
        /// Get all the file hash and file offset in a given folder 
        /// </summary>
        /// <param name="hashedFolder">The hashed value of the folder path</param>
        /// <param name="dataFile">The data file to look in</param>
        /// <returns>A list containing the hashed values of the files in the given folder</returns>
        public async Task<Dictionary<int, int>> GetAllHashedFilesAndOffsetsInFolder(int hashedFolder, XivDataFile dataFile)
        {
            var fileHashesDict = new Dictionary<int, int>();

            // These are the offsets to relevant data
            const int fileCountOffset = 1036;
            const int dataStartOffset = 2048;

            var indexPath = Path.Combine(_gameDirectory.FullName, $"{dataFile.GetDataFileName()}{IndexExtension}");

            await _semaphoreSlim.WaitAsync();
            try
            {
                await Task.Run(() =>
                {
                    using (var br = new BinaryReader(File.OpenRead(indexPath)))
                    {
                        br.BaseStream.Seek(fileCountOffset, SeekOrigin.Begin);
                        var totalFiles = br.ReadInt32();

                        br.BaseStream.Seek(dataStartOffset, SeekOrigin.Begin);
                        for (var i = 0; i < totalFiles; br.ReadBytes(4), i += 16)
                        {
                            var hashedFile = br.ReadInt32();

                            var folderPathHash = br.ReadInt32();

                            if (folderPathHash == hashedFolder)
                            {
                                fileHashesDict.Add(hashedFile, br.ReadInt32() * 8);
                            }
                            else
                            {
                                br.ReadBytes(4);
                            }
                        }
                    }
                });
            }
            finally
            {
                _semaphoreSlim.Release();
            }

            return fileHashesDict;
        }

        /// <summary>
        /// Deletes a file descriptor/stub from the Index files.
        /// </summary>
        /// <param name="fullPath">Full internal file path to the file that should be deleted.</param>
        /// <param name="dataFile">Which data file to use</param>
        /// <returns></returns>
        public async Task<bool> DeleteFileDescriptor(string fullPath, XivDataFile dataFile, bool updateCache = true)
        {
            await _semaphoreSlim.WaitAsync();

            try
            {

                // Test both index files for write access.
                try
                {
                    var indexPath = Path.Combine(_gameDirectory.FullName, $"{dataFile.GetDataFileName()}{IndexExtension}");
                    using (var fs = new FileStream(indexPath, FileMode.Open))
                    {
                        var canRead = fs.CanRead;
                        var canWrite = fs.CanWrite;
                        if (!canRead || !canWrite)
                        {
                            throw new Exception();
                        }
                    }
                    var index2Path = Path.Combine(_gameDirectory.FullName, $"{dataFile.GetDataFileName()}{Index2Extension}");
                    using (var fs = new FileStream(index2Path, FileMode.Open))
                    {
                        var canRead = fs.CanRead;
                        var canWrite = fs.CanWrite;
                        if (!canRead || !canWrite)
                        {
                            throw new Exception();
                        }
                    }
                }
                catch
                {
                    throw new Exception("Unable to update Index files.  File(s) are currently in use.");
                }


                fullPath = fullPath.Replace("\\", "/");
                var pathHash = HashGenerator.GetHash(fullPath.Substring(0, fullPath.LastIndexOf("/", StringComparison.Ordinal)));
                var fileHash = HashGenerator.GetHash(Path.GetFileName(fullPath));
                var uPathHash = BitConverter.ToUInt32(BitConverter.GetBytes(pathHash), 0);
                var uFileHash = BitConverter.ToUInt32(BitConverter.GetBytes(fileHash), 0);
                var fullPathHash = HashGenerator.GetHash(fullPath);
                var uFullPathHash = BitConverter.ToUInt32(BitConverter.GetBytes(fullPathHash), 0);

                var SegmentHeaders = new int[4];
                var SegmentOffsets = new int[4];
                var SegmentSizes = new int[4];

                // Segment header offsets
                SegmentHeaders[0] = 1028;                   // Files
                SegmentHeaders[1] = 1028 + (72 * 1) + 4;    // Unknown
                SegmentHeaders[2] = 1028 + (72 * 2) + 4;    // Unknown
                SegmentHeaders[3] = 1028 + (72 * 3) + 4;    // Folders


                // Index 1 Closure
                {
                    var indexPath = Path.Combine(_gameDirectory.FullName, $"{dataFile.GetDataFileName()}{IndexExtension}");

                    // Dump the index into memory, since we're going to have to inject data.
                    byte[] originalIndex = File.ReadAllBytes(indexPath);
                    byte[] modifiedIndex = new byte[originalIndex.Length - 16];

                    // Get all the segment header data
                    for (int i = 0; i < SegmentHeaders.Length; i++)
                    {
                        SegmentOffsets[i] = BitConverter.ToInt32(originalIndex, SegmentHeaders[i] + 4);
                        SegmentSizes[i] = BitConverter.ToInt32(originalIndex, SegmentHeaders[i] + 8);
                    }

                    int fileCount = SegmentSizes[0] / 16;

                    // Search for appropriate location to inject data.
                    var deleteLocation = 0;

                    for (int i = 0; i < fileCount; i++)
                    {
                        int position = SegmentOffsets[0] + (i * 16);
                        uint iHash = BitConverter.ToUInt32(originalIndex, position);
                        uint iPathHash = BitConverter.ToUInt32(originalIndex, position + 4);
                        uint iOffset = BitConverter.ToUInt32(originalIndex, position + 8);

                        if (iHash == uFileHash && iPathHash == uPathHash)
                        {
                            deleteLocation = position;
                            break;
                        }
                    }

                    // If the file was already deleted, nothing to do here.
                    if (deleteLocation == 0)
                    {
                        return false;
                    }

                    byte[] DataToDelete = new byte[16];
                    Array.Copy(originalIndex, deleteLocation, DataToDelete, 0, 16);

                    // Split the file at the injection point.
                    int remainder = originalIndex.Length - deleteLocation - 16;
                    Array.Copy(originalIndex, 0, modifiedIndex, 0, deleteLocation);
                    Array.Copy(originalIndex, deleteLocation + 16, modifiedIndex, deleteLocation, remainder);


                    // Update the segment headers.
                    for (int i = 0; i < SegmentHeaders.Length; i++)
                    {
                        // Update Segment 0 Size.
                        if (i == 0)
                        {
                            SegmentSizes[i] -= 16;
                            Array.Copy(BitConverter.GetBytes(SegmentSizes[i]), 0, modifiedIndex, SegmentHeaders[i] + 8, 4);

                        }
                        // Update other segments' offsets.
                        else
                        {
                            SegmentOffsets[i] -= 16;
                            Array.Copy(BitConverter.GetBytes(SegmentOffsets[i]), 0, modifiedIndex, SegmentHeaders[i] + 4, 4);
                        }
                    }
                    // Update the folder structure
                    var folderCount = SegmentSizes[3] / 16;
                    bool foundFolder = false;

                    for (int i = 0; i < folderCount; i++)
                    {
                        int position = SegmentOffsets[3] + (i * 16);
                        uint iHash = BitConverter.ToUInt32(modifiedIndex, position);
                        uint iOffset = BitConverter.ToUInt32(modifiedIndex, position + 4);
                        uint iFolderSize = BitConverter.ToUInt32(modifiedIndex, position + 8);

                        // Update folder offset
                        if (iOffset > deleteLocation)
                        {
                            Array.Copy(BitConverter.GetBytes(iOffset - 16), 0, modifiedIndex, position + 4, 4);
                        }

                        // Update folder size
                        if (iHash == uPathHash)
                        {
                            if (iFolderSize == 0)
                            {
                                // No more files in the folder, the folder needs to be deleted from the listing
                                // (0 size folders are not listed, even if they're parent folders for other folders)
                                remainder = modifiedIndex.Length - position - 16;
                                Array.Copy(modifiedIndex, position + 16, modifiedIndex, position, remainder);

                                var newIndex = new byte[modifiedIndex.Length - 16];
                                Array.Copy(modifiedIndex, 0, modifiedIndex, 0, newIndex.Length);
                                foundFolder = true;
                            }
                            else
                            {
                                foundFolder = true;
                                Array.Copy(BitConverter.GetBytes(iFolderSize - 16), 0, modifiedIndex, position + 8, 4);
                            }
                        }
                    }

                    if (!foundFolder)
                    {
                        // This is a pretty weird state to get here.
                        // The file had to exist, but the folder it was in had to *not* exist.
                        // This is tentatively a non-error, as we should really continue and purge the
                        // Index2 Entry for the file as well, but it's definitely a weird/invalid index state 
                        // that got us here.
                    }


                    // Update SHA-1 Hashes.
                    SHA1 sha = new SHA1CryptoServiceProvider();
                    byte[] shaHash;
                    for (int i = 0; i < SegmentHeaders.Length; i++)
                    {
                        //Segment
                        shaHash = sha.ComputeHash(modifiedIndex, SegmentOffsets[i], SegmentSizes[i]);
                        Array.Copy(shaHash, 0, modifiedIndex, SegmentHeaders[i] + 12, 20);
                    }

                    // Compute Hash of the header segment
                    shaHash = sha.ComputeHash(modifiedIndex, 0, 960);
                    Array.Copy(shaHash, 0, modifiedIndex, 960, 20);



                    // Write file
                    File.WriteAllBytes(indexPath, modifiedIndex);
                }

                // Index 2 Closure
                {
                    var index2Path = Path.Combine(_gameDirectory.FullName, $"{dataFile.GetDataFileName()}{Index2Extension}");

                    // Dump the index into memory, since we're going to have to inject data.
                    byte[] originalIndex = File.ReadAllBytes(index2Path);
                    byte[] modifiedIndex = new byte[originalIndex.Length - 8];

                    // Get all the segment header data
                    for (int i = 0; i < SegmentHeaders.Length; i++)
                    {
                        SegmentOffsets[i] = BitConverter.ToInt32(originalIndex, SegmentHeaders[i] + 4);
                        SegmentSizes[i] = BitConverter.ToInt32(originalIndex, SegmentHeaders[i] + 8);
                    }

                    int fileCount = SegmentSizes[0] / 8;

                    // Search for appropriate location to inject data.
                    var deleteLocation = 0;

                    for (int i = 0; i < fileCount; i++)
                    {
                        int position = SegmentOffsets[0] + (i * 8);
                        uint iFullPathHash = BitConverter.ToUInt32(originalIndex, position);
                        uint iOffset = BitConverter.ToUInt32(originalIndex, position + 4);

                        // Index 2 is just in hash order, so find the spot where we fit in.
                        if (iFullPathHash == uFullPathHash)
                        {
                            deleteLocation = position;
                        }
                    }

                    // It's possible a valid file doesn't have an Index 2 entry, just skip it in that case.
                    if (deleteLocation > 0)
                    {

                        byte[] DataToDelete = new byte[8];
                        Array.Copy(originalIndex, deleteLocation, DataToDelete, 0, 8);


                        // Split the file at the injection point.
                        int remainder = originalIndex.Length - deleteLocation - 8;
                        Array.Copy(originalIndex, 0, modifiedIndex, 0, deleteLocation);
                        Array.Copy(originalIndex, deleteLocation + 8, modifiedIndex, deleteLocation, remainder);



                        // Update the segment headers.
                        for (int i = 0; i < SegmentHeaders.Length; i++)
                        {
                            // Update Segment 0 Size.
                            if (i == 0)
                            {
                                SegmentSizes[i] -= 8;
                                Array.Copy(BitConverter.GetBytes(SegmentSizes[i]), 0, modifiedIndex, SegmentHeaders[i] + 8, 4);

                            }
                            // Update other segments' offsets.
                            else
                            {
                                // Index 2 doesn't have all 4 segments.
                                if (SegmentOffsets[i] != 0)
                                {
                                    SegmentOffsets[i] -= 8;
                                    Array.Copy(BitConverter.GetBytes(SegmentOffsets[i]), 0, modifiedIndex, SegmentHeaders[i] + 4, 4);
                                }
                            }
                        }

                        // Update SHA-1 Hashes.
                        SHA1 sha = new SHA1CryptoServiceProvider();
                        byte[] shaHash;
                        for (int i = 0; i < SegmentHeaders.Length; i++)
                        {
                            if (SegmentSizes[i] > 0)
                            {
                                //Segment
                                byte[] oldHash = new byte[20];
                                Array.Copy(originalIndex, SegmentHeaders[i] + 12, oldHash, 0, 20);

                                shaHash = sha.ComputeHash(modifiedIndex, SegmentOffsets[i], SegmentSizes[i]);
                                Array.Copy(shaHash, 0, modifiedIndex, SegmentHeaders[i] + 12, 20);
                            }
                        }

                        // Compute Hash of the header segment
                        shaHash = sha.ComputeHash(modifiedIndex, 0, 960);
                        Array.Copy(shaHash, 0, modifiedIndex, 960, 20);

                        // Write file
                        File.WriteAllBytes(index2Path, modifiedIndex);
                    }
                }

            }
            finally
            {
                _semaphoreSlim.Release();
            }


            if (!fullPath.Contains(".flag"))
            {
                await DeleteFileDescriptor(fullPath + ".flag", dataFile, false);
            }

            // This is a metadata entry being deleted, we'll need to restore the metadata entries back to default.
            if (fullPath.EndsWith(".meta"))
            {
                var root = await XivCache.GetFirstRoot(fullPath);
                await ItemMetadata.RestoreDefaultMetadata(root);
            }


            if (updateCache)
            {
                // Queue us for updating.
                XivCache.QueueDependencyUpdate(fullPath);
            }

            return true;
        }


        /// <summary>
        /// Adds a new file descriptor/stub into the Index files.
        /// </summary>
        /// <param name="fullPath">Full path to the new file.</param>
        /// <param name="dataOffset">Raw DAT file offset to use for the new file.</param>
        /// <param name="dataFile">Which data file set to use.</param>
        /// <returns></returns>
        public async Task<bool> AddFileDescriptor(string fullPath, long dataOffset, XivDataFile dataFile, bool updateCache = true)
        {
            if(!fullPath.Contains(".flag"))
            {
                await AddFileDescriptor(fullPath + ".flag", -1, dataFile, false);
            }

            uint uOffset = (uint)(dataOffset / 8);
            await _semaphoreSlim.WaitAsync();
            try
            {
                // Test both index files for write access.
                try
                {
                    var indexPath = Path.Combine(_gameDirectory.FullName, $"{dataFile.GetDataFileName()}{IndexExtension}");
                    using (var fs = new FileStream(indexPath, FileMode.Open))
                    {
                        var canRead = fs.CanRead;
                        var canWrite = fs.CanWrite;
                        if (!canRead || !canWrite)
                        {
                            throw new Exception();
                        }
                    }
                    var index2Path = Path.Combine(_gameDirectory.FullName, $"{dataFile.GetDataFileName()}{Index2Extension}");
                    using (var fs = new FileStream(index2Path, FileMode.Open))
                    {
                        var canRead = fs.CanRead;
                        var canWrite = fs.CanWrite;
                        if (!canRead || !canWrite)
                        {
                            throw new Exception();
                        }
                    }
                }
                catch
                {
                    throw new Exception("Unable to update Index files.  File(s) are currently in use.");
                }

                fullPath = fullPath.Replace("\\", "/");
                var pathHash = HashGenerator.GetHash(fullPath.Substring(0, fullPath.LastIndexOf("/", StringComparison.Ordinal)));
                var fileHash = HashGenerator.GetHash(Path.GetFileName(fullPath));
                var uPathHash = BitConverter.ToUInt32(BitConverter.GetBytes(pathHash), 0);
                var uFileHash = BitConverter.ToUInt32(BitConverter.GetBytes(fileHash), 0);
                var fullPathHash = HashGenerator.GetHash(fullPath);
                var uFullPathHash = BitConverter.ToUInt32(BitConverter.GetBytes(fullPathHash), 0);

                var SegmentHeaders = new int[4];
                var SegmentOffsets = new int[4];
                var SegmentSizes = new int[4];

                // Segment header offsets
                SegmentHeaders[0] = 1028;                   // Files
                SegmentHeaders[1] = 1028 + (72 * 1) + 4;    // Unknown
                SegmentHeaders[2] = 1028 + (72 * 2) + 4;    // Unknown
                SegmentHeaders[3] = 1028 + (72 * 3) + 4;    // Folders


                // Index 1 Closure
                {
                    var indexPath = Path.Combine(_gameDirectory.FullName, $"{dataFile.GetDataFileName()}{IndexExtension}");

                    // Dump the index into memory, since we're going to have to inject data.
                    byte[] originalIndex = File.ReadAllBytes(indexPath);

                    // Get all the segment header data
                    for (int i = 0; i < SegmentHeaders.Length; i++)
                    {
                        SegmentOffsets[i] = BitConverter.ToInt32(originalIndex, SegmentHeaders[i] + 4);
                        SegmentSizes[i] = BitConverter.ToInt32(originalIndex, SegmentHeaders[i] + 8);
                    }

                    int fileCount = SegmentSizes[0] / 16;

                    // Search for appropriate location to inject data.
                    bool foundFolder = false;
                    var injectLocation = SegmentOffsets[0] + SegmentSizes[0];

                    for (int i = 0; i < fileCount; i++)
                    {
                        int position = SegmentOffsets[0] + (i * 16);
                        uint iHash = BitConverter.ToUInt32(originalIndex, position);
                        uint iPathHash = BitConverter.ToUInt32(originalIndex, position + 4);
                        uint iOffset = BitConverter.ToUInt32(originalIndex, position + 8);

                        if (iPathHash == uPathHash)
                        {
                            foundFolder = true;

                            if (iHash == uFileHash)
                            {
                                // File already exists.  Just update the data offset.
                                _semaphoreSlim.Release();
                                await UpdateDataOffset(dataOffset, fullPath, updateCache);
                                await _semaphoreSlim.WaitAsync();
                                return false;
                            }
                            else if (iHash > uFileHash)
                            {
                                injectLocation = position;
                                break;
                            }
                        }
                        else if (iPathHash > uPathHash)
                        {
                            // This is where the folder should go, it just has no files currently.
                            injectLocation = position;
                            break;
                        }
                        else
                        {
                            // End of folder - inject file here if we haven't yet.
                            if (foundFolder == true)
                            {
                                injectLocation = position;
                                break;
                            }
                        }
                    }


                    var totalInjectSize = foundFolder ? 16 : 32;
                    byte[] modifiedIndex = new byte[originalIndex.Length + totalInjectSize];

                    // Split the file at the injection point.
                    int remainder = originalIndex.Length - injectLocation;
                    Array.Copy(originalIndex, 0, modifiedIndex, 0, injectLocation);
                    Array.Copy(originalIndex, injectLocation, modifiedIndex, injectLocation + 16, remainder);


                    // Update the segment headers.
                    for (int i = 0; i < SegmentHeaders.Length; i++)
                    {
                        // Update Segment 0 Size.
                        if (i == 0)
                        {
                            SegmentSizes[i] += 16;
                            Array.Copy(BitConverter.GetBytes(SegmentSizes[i]), 0, modifiedIndex, SegmentHeaders[i] + 8, 4);

                        }
                        // Update other segments' offsets.
                        else
                        {

                            SegmentOffsets[i] += 16;
                            Array.Copy(BitConverter.GetBytes(SegmentOffsets[i]), 0, modifiedIndex, SegmentHeaders[i] + 4, 4);

                            // We need to create the folder as well.
                            if (i == 3 && !foundFolder)
                            {
                                SegmentSizes[i] += 16;
                                Array.Copy(BitConverter.GetBytes(SegmentSizes[i]), 0, modifiedIndex, SegmentHeaders[i] + 8, 4);
                            }
                        }
                    }

                    // Set the actual Injected Data
                    Array.Copy(BitConverter.GetBytes(fileHash), 0, modifiedIndex, injectLocation, 4);
                    Array.Copy(BitConverter.GetBytes(pathHash), 0, modifiedIndex, injectLocation + 4, 4);
                    Array.Copy(BitConverter.GetBytes(uOffset), 0, modifiedIndex, injectLocation + 8, 4);

                    // Update the folder structure
                    var folderCount = SegmentSizes[3] / 16;

                    for (int i = 0; i < folderCount; i++)
                    {
                        int position = SegmentOffsets[3] + (i * 16);
                        uint iHash = BitConverter.ToUInt32(modifiedIndex, position);
                        uint iOffset = BitConverter.ToUInt32(modifiedIndex, position + 4);
                        uint iFolderSize = BitConverter.ToUInt32(modifiedIndex, position + 8);

                        // Update folder offset
                        if (iOffset > injectLocation)
                        {
                            Array.Copy(BitConverter.GetBytes(iOffset + 16), 0, modifiedIndex, position + 4, 4);
                        }

                        // Folder exists, but needs its size updated.
                        if (iHash == uPathHash)
                        {
                            Array.Copy(BitConverter.GetBytes(iFolderSize + 16), 0, modifiedIndex, position + 8, 4);
                        }
                        else if (foundFolder == false && iHash > uPathHash)
                        {
                            foundFolder = true;
                            // This is where we need to cut the index the second time to make room for the folder data.
                            remainder = modifiedIndex.Length - position - 16;
                            Array.Copy(modifiedIndex, position, modifiedIndex, position + 16, remainder);

                            // The new folder entry now goes at the 16 bytes starting at position
                            Array.Copy(BitConverter.GetBytes(uPathHash), 0, modifiedIndex, position, 4);
                            Array.Copy(BitConverter.GetBytes(injectLocation), 0, modifiedIndex, position + 4, 4);
                            Array.Copy(BitConverter.GetBytes(16), 0, modifiedIndex, position + 8, 4);
                            Array.Copy(BitConverter.GetBytes(16), 0, modifiedIndex, position + 12, 4);
                        }
                    }

                    // Update SHA-1 Hashes.
                    SHA1 sha = new SHA1CryptoServiceProvider();
                    byte[] shaHash;
                    for (int i = 0; i < SegmentHeaders.Length; i++)
                    {
                        //Segment
                        shaHash = sha.ComputeHash(modifiedIndex, SegmentOffsets[i], SegmentSizes[i]);
                        Array.Copy(shaHash, 0, modifiedIndex, SegmentHeaders[i] + 12, 20);
                    }

                    // Compute Hash of the header segment
                    shaHash = sha.ComputeHash(modifiedIndex, 0, 960);
                    Array.Copy(shaHash, 0, modifiedIndex, 960, 20);



                    // Write file
                    File.WriteAllBytes(indexPath, modifiedIndex);
                }

                // Index 2 Closure
                {
                    var index2Path = Path.Combine(_gameDirectory.FullName, $"{dataFile.GetDataFileName()}{Index2Extension}");

                    // Dump the index into memory, since we're going to have to inject data.
                    byte[] originalIndex = File.ReadAllBytes(index2Path);
                    byte[] modifiedIndex = new byte[originalIndex.Length + 16];

                    // Get all the segment header data
                    for (int i = 0; i < SegmentHeaders.Length; i++)
                    {
                        SegmentOffsets[i] = BitConverter.ToInt32(originalIndex, SegmentHeaders[i] + 4);
                        SegmentSizes[i] = BitConverter.ToInt32(originalIndex, SegmentHeaders[i] + 8);
                    }

                    int fileCount = SegmentSizes[0] / 8;

                    // Search for appropriate location to inject data.
                    var injectLocation = SegmentOffsets[0] + SegmentSizes[0];

                    for (int i = 0; i < fileCount; i++)
                    {
                        int position = SegmentOffsets[0] + (i * 8);
                        uint iFullPathHash = BitConverter.ToUInt32(originalIndex, position);
                        uint iOffset = BitConverter.ToUInt32(originalIndex, position + 4);

                        // Index 2 is just in hash order, so find the spot where we fit in.
                        if (iFullPathHash > uFullPathHash)
                        {
                            injectLocation = position;
                            break;
                        }
                    }

                    // Split the file at the injection point.
                    int remainder = originalIndex.Length - injectLocation;
                    Array.Copy(originalIndex, 0, modifiedIndex, 0, injectLocation);
                    Array.Copy(originalIndex, injectLocation, modifiedIndex, injectLocation + 8, remainder);


                    // Update the segment headers.
                    for (int i = 0; i < SegmentHeaders.Length; i++)
                    {
                        // Update Segment 0 Size.
                        if (i == 0)
                        {
                            SegmentSizes[i] += 8;
                            Array.Copy(BitConverter.GetBytes(SegmentSizes[i]), 0, modifiedIndex, SegmentHeaders[i] + 8, 4);

                        }
                        // Update other segments' offsets.
                        else
                        {
                            // Index 2 doesn't have all 4 segments.
                            if (SegmentOffsets[i] != 0)
                            {
                                SegmentOffsets[i] += 8;
                                Array.Copy(BitConverter.GetBytes(SegmentOffsets[i]), 0, modifiedIndex, SegmentHeaders[i] + 4, 4);
                            }
                        }
                    }

                    // Set the actual Injected Data
                    Array.Copy(BitConverter.GetBytes(uFullPathHash), 0, modifiedIndex, injectLocation, 4);
                    Array.Copy(BitConverter.GetBytes(uOffset), 0, modifiedIndex, injectLocation + 4, 4);

                    // Update SHA-1 Hashes.
                    SHA1 sha = new SHA1CryptoServiceProvider();
                    byte[] shaHash;
                    for (int i = 0; i < SegmentHeaders.Length; i++)
                    {
                        if (SegmentSizes[i] > 0)
                        {
                            //Segment
                            shaHash = sha.ComputeHash(modifiedIndex, SegmentOffsets[i], SegmentSizes[i]);
                            Array.Copy(shaHash, 0, modifiedIndex, SegmentHeaders[i] + 12, 20);
                        }
                    }
                    // Compute Hash of the header segment
                    shaHash = sha.ComputeHash(modifiedIndex, 0, 960);
                    Array.Copy(shaHash, 0, modifiedIndex, 960, 20);


                    // Write file
                    File.WriteAllBytes(index2Path, modifiedIndex);

                }

            }
            finally
            {
                _semaphoreSlim.Release();
            }

            if(updateCache)
            {
                // Queue us for updating.
                XivCache.QueueDependencyUpdate(fullPath);
            }


            return true;
        }


        /// <summary>
        /// Handles updating both indexes in a safe way.
        /// </summary>
        /// <param name="offset"></param>
        /// <param name="fullPath"></param>
        /// <returns></returns>
        public async Task<int> UpdateDataOffset(long offset, string fullPath, bool updateCache = true)
        {
            var oldOffset = 0;

            await _semaphoreSlim.WaitAsync();
            try
            {
                var dataFile = IOUtil.GetDataFileFromPath(fullPath);

                var indexPath = Path.Combine(_gameDirectory.FullName, $"{dataFile.GetDataFileName()}{IndexExtension}");
                var index2Path = Path.Combine(_gameDirectory.FullName, $"{dataFile.GetDataFileName()}{Index2Extension}");

                // Test both index files for write access.

                try
                {
                    using (var fs = new FileStream(indexPath, FileMode.Open))
                    {
                        var canRead = fs.CanRead;
                        var canWrite = fs.CanWrite;
                        if (!canRead || !canWrite)
                        {
                            throw new Exception();
                        }
                    }
                    using (var fs = new FileStream(index2Path, FileMode.Open))
                    {
                        var canRead = fs.CanRead;
                        var canWrite = fs.CanWrite;
                        if (!canRead || !canWrite)
                        {
                            throw new Exception();
                        }
                    }
                }
                catch
                {
                    throw new Exception("Unable to update Index files.  File(s) are currently in use.");
                }


                // Now attempt to write.
                oldOffset = await UpdateIndex(offset, fullPath, dataFile);
                await UpdateIndex2(offset, fullPath, dataFile);
            }
            finally
            {
                _semaphoreSlim.Release();
            }


            if (updateCache)
            {

                // Queue us up for dependency pre-calcluation, since we're a modded file.
                XivCache.QueueDependencyUpdate(fullPath);

            }



            return oldOffset;
        }

        /// <summary>
        /// Updates the .index files offset for a given item.
        /// </summary>
        /// <param name="offset">The new offset to be used.</param>
        /// <param name="fullPath">The internal path of the file whos offset is to be updated.</param>
        /// <param name="dataFile">The data file to update the index for</param>
        /// <returns>The offset which was replaced.</returns>
        private async Task<int> UpdateIndex(long offset, string fullPath, XivDataFile dataFile)
        {
            // Semaphore for this function is handled by UpdateDataOffset.
            fullPath = fullPath.Replace("\\", "/");
            var folderHash =
                HashGenerator.GetHash(fullPath.Substring(0, fullPath.LastIndexOf("/", StringComparison.Ordinal)));
            var fileHash = HashGenerator.GetHash(Path.GetFileName(fullPath));
            var oldOffset = 0;

            // These are the offsets to relevant data
            const int fileCountOffset = 1036;
            const int dataStartOffset = 2048;

            var indexPath = Path.Combine(_gameDirectory.FullName, $"{dataFile.GetDataFileName()}{IndexExtension}");

            await Task.Run(() =>
            {
                using (var index = File.Open(indexPath, FileMode.Open))
                {
                    using (var br = new BinaryReader(index))
                    {
                        using (var bw = new BinaryWriter(index))
                        {
                            br.BaseStream.Seek(fileCountOffset, SeekOrigin.Begin);
                            var numOfFiles = br.ReadInt32();

                            br.BaseStream.Seek(dataStartOffset, SeekOrigin.Begin);
                            for (var i = 0; i < numOfFiles; br.ReadBytes(4), i += 16)
                            {
                                var fileNameHash = br.ReadInt32();

                                if (fileNameHash == fileHash)
                                {
                                    var folderPathHash = br.ReadInt32();

                                    if (folderPathHash == folderHash)
                                    {
                                        oldOffset = br.ReadInt32();
                                        bw.BaseStream.Seek(br.BaseStream.Position - 4, SeekOrigin.Begin);
                                        uint uOffset = (uint)(offset / 8);
                                        bw.Write(uOffset);
                                        break;
                                    }

                                    br.ReadBytes(4);
                                }
                                else
                                {
                                    br.ReadBytes(8);
                                }
                            }
                        }
                    }
                }
            });

            return oldOffset;
        }

        /// <summary>
        /// Updates the .index2 files offset for a given item.
        /// </summary>
        /// <param name="offset">The new offset to be used.</param>
        /// <param name="fullPath">The internal path of the file whos offset is to be updated.</param>
        /// <param name="dataFile">The data file to update the index for</param>
        /// <returns>The offset which was replaced.</returns>
        private async Task UpdateIndex2(long offset, string fullPath, XivDataFile dataFile)
        {
            // Semaphore for this function is handled by UpdateDataOffset.
            fullPath = fullPath.Replace("\\", "/");
            var pathHash = HashGenerator.GetHash(fullPath);

            // These are the offsets to relevant data
            const int fileCountOffset = 1036;
            const int dataStartOffset = 2048;

            var index2Path = Path.Combine(_gameDirectory.FullName, $"{dataFile.GetDataFileName()}{Index2Extension}");

            await Task.Run(() =>
            {
                using (var index = File.Open(index2Path, FileMode.Open))
                {
                    using (var br = new BinaryReader(index))
                    {
                        using (var bw = new BinaryWriter(index))
                        {
                            br.BaseStream.Seek(fileCountOffset, SeekOrigin.Begin);
                            var numOfFiles = br.ReadInt32();

                            br.BaseStream.Seek(dataStartOffset, SeekOrigin.Begin);
                            for (var i = 0; i < numOfFiles; i += 8)
                            {
                                var fullPathHash = br.ReadInt32();

                                if (fullPathHash == pathHash)
                                {
                                    bw.BaseStream.Seek(br.BaseStream.Position, SeekOrigin.Begin);
                                    uint uOffset = (uint)(offset / 8);
                                    bw.Write(uOffset);
                                    break;
                                }

                                br.ReadBytes(4);
                            }
                        }
                    }
                }
            });
        }

        /// <summary>
        /// Checks to see whether the index file is locked
        /// </summary>
        /// <param name="dataFile">The data file to check</param>
        /// <returns>True if locked</returns>
        public bool IsIndexLocked(XivDataFile dataFile)
        {
            var fileName = dataFile.GetDataFileName();
            var isLocked = false;

            var indexPath = Path.Combine(_gameDirectory.FullName, $"{fileName}{IndexExtension}");
            var index2Path = Path.Combine(_gameDirectory.FullName, $"{fileName}{Index2Extension}");

            FileStream stream = null;
            FileStream stream1 = null;

            try
            {
                stream = File.Open(indexPath, FileMode.Open);
                stream1= File.Open(index2Path, FileMode.Open);
            }
            catch (Exception e)
            {
                isLocked = true;
            }
            finally
            {
                stream?.Dispose();
                stream?.Close();
                stream1?.Dispose();
                stream1?.Close();
            }

            return isLocked;
        }
    }
}