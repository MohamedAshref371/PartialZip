using System.Collections.Generic;
using System.Threading.Tasks;

namespace PartialZip
{
    public class PartialZipDownloader
    {
        /// <summary>
        /// Returns a list of all filenames in the remote .zip archive
        /// </summary>
        /// <param name="archiveUrl">URL of the .zip archive</param>
        /// <returns>List of filenames</returns>
        public static async Task<IEnumerable<string>> GetFileList(string archiveUrl)
        {
            PartialZipSession downloader = new PartialZipSession(archiveUrl);
            await downloader.Open();

            return downloader.GetFileList();
        }

        /// <summary>
        /// Downloads a specific file from a remote .zip archive
        /// </summary>
        /// <param name="archiveUrl">URL of the .zip archive</param>
        /// <param name="filePath">Path of the file</param>
        /// <returns>File content</returns>
        public static async Task<byte[]> DownloadFile(string archiveUrl, string filePath)
        {
            PartialZipSession downloader = new PartialZipSession(archiveUrl);
            await downloader.Open();
            byte[] content = await downloader.DownloadFile(filePath);

            return content;
        }
    }
}
