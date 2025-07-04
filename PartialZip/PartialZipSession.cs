﻿using PartialZip.Exceptions;
using PartialZip.Models;
using PartialZip.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace PartialZip
{
    public class PartialZipSession
    {
        private string _archiveUrl;

        private HttpService _httpService;
        private DeflateService _deflateService;

        private PartialZipInfo info;
        private bool _isOpen = false;

        public PartialZipSession(string archiveUrl)
        {
            this._archiveUrl = archiveUrl;

            this._httpService = new HttpService(this._archiveUrl);
            this._deflateService = new DeflateService();
        }

        /// <summary>
        /// Opens the archive and reads its structure.
        /// </summary>
        public async Task Open()
        {
            info = await PrivateOpen();
            _isOpen = true;
        }

        /// <summary>
        /// Returns a list of all filenames in the remote .zip archive
        /// </summary>
        /// <returns>List of filenames</returns>
        public IEnumerable<string> GetFileList()
        {
            if (!_isOpen)
                throw new InvalidOperationException("The archive must be opened before getting the file list. Call Open() first.");

            return info.CentralDirectory.Select(cd => cd.FileName).OrderBy(f => f);
        }

        /// <summary>
        /// Downloads a specific file from a remote .zip archive
        /// </summary>
        /// <param name="filePath">Path of the file</param>
        /// <returns>File content</returns>
        public async Task<byte[]> DownloadFile(string filePath)
        {
            if (!_isOpen)
                throw new InvalidOperationException("The archive must be opened before downloading files. Call Open() first.");

            byte[] content = await Download(info, filePath);
            return content;
        }

        private async Task<PartialZipInfo> PrivateOpen()
        {
            bool supportsPartialZip = await this._httpService.SupportsPartialZip();

            if (!supportsPartialZip)
                throw new PartialZipNotSupportedException("The web server does not support PartialZip as byte ranges are not accepted.");

            PartialZipInfo info = new PartialZipInfo();

            info.Length = await this._httpService.GetContentLength();

            byte[] eocdBuffer = await this._httpService.GetRange(info.Length - EndOfCentralDirectory.Size, info.Length - 1);
            info.EndOfCentralDirectory = new EndOfCentralDirectory(eocdBuffer);

            ulong startCD, endCD;

            if (info.EndOfCentralDirectory.IsZip64)
            {
                byte[] eocdLocator64Buffer = await this._httpService.GetRange(info.Length - EndOfCentralDirectory.Size - EndOfCentralDirectoryLocator64.Size, info.Length - EndOfCentralDirectory.Size);
                info.EndOfCentralDirectoryLocator64 = new EndOfCentralDirectoryLocator64(eocdLocator64Buffer);

                byte[] eocd64Buffer = await this._httpService.GetRange(info.EndOfCentralDirectoryLocator64.EndOfCentralDirectory64StartOffset, info.EndOfCentralDirectoryLocator64.EndOfCentralDirectory64StartOffset + EndOfCentralDirectory64.Size - 1);
                info.EndOfCentralDirectory64 = new EndOfCentralDirectory64(eocd64Buffer);

                (startCD, endCD) = (info.EndOfCentralDirectory64.CentralDirectoryStartOffset, info.EndOfCentralDirectory64.CentralDirectoryStartOffset + info.EndOfCentralDirectory64.CentralDirectorySize + EndOfCentralDirectory64.Size - 1);
                info.CentralDirectoryEntries = info.EndOfCentralDirectory64.CentralDirectoryRecordCount;
            }
            else
            {
                (startCD, endCD) = (info.EndOfCentralDirectory.CentralDirectoryStartOffset, info.EndOfCentralDirectory.CentralDirectoryStartOffset + info.EndOfCentralDirectory.CentralDirectorySize + EndOfCentralDirectory.Size - 1);
                info.CentralDirectoryEntries = info.EndOfCentralDirectory.CentralDirectoryRecordCount;
            }

            byte[] cdBuffer = await this._httpService.GetRange(startCD, endCD);
            info.CentralDirectory = CentralDirectoryHeader.GetFromBuffer(cdBuffer, info.CentralDirectoryEntries);

            return info;
        }

        private async Task<byte[]> Download(PartialZipInfo info, string filePath)
        {
            CentralDirectoryHeader cd = info.CentralDirectory.FirstOrDefault(c => c.FileName == filePath);

            if (cd != null)
            {
                (ulong uncompressedSize, ulong compressedSize, ulong headerOffset, uint diskNum) = cd.GetFileInfo();

                byte[] localFileBuffer = await this._httpService.GetRange(headerOffset, headerOffset + LocalFileHeader.Size - 1);
                LocalFileHeader localFileHeader = new LocalFileHeader(localFileBuffer);

                ulong start = headerOffset + LocalFileHeader.Size + localFileHeader.FileNameLength + localFileHeader.ExtraFieldLength;
                byte[] compressedContent = await this._httpService.GetRange(start, start + compressedSize - 1);

                switch (localFileHeader.Compression)
                {
                    case 0:
                        return compressedContent;
                    case 8:
                        return this._deflateService.Inflate(compressedContent);
                    default:
                        throw new PartialZipUnsupportedCompressionException("Unknown compression.");
                }
            }

            throw new PartialZipFileNotFoundException($"Could not find file {filePath} in archive.");
        }
    }
}
