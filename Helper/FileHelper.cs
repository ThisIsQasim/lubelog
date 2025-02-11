﻿namespace CarCareTracker.Helper
{
    public interface IFileHelper
    {
        string GetFullFilePath(string currentFilePath, bool mustExist = true);
        string MoveFileFromTemp(string currentFilePath, string newFolder);
        bool DeleteFile(string currentFilePath);
    }
    public class FileHelper: IFileHelper
    {
        private readonly IWebHostEnvironment _webEnv;
        public FileHelper(IWebHostEnvironment webEnv)
        {
            _webEnv = webEnv;
        }
        public string GetFullFilePath(string currentFilePath, bool mustExist = true)
        {
            if (currentFilePath.StartsWith("/"))
            {
                currentFilePath = currentFilePath.Substring(1);
            }
            string oldFilePath = Path.Combine(_webEnv.WebRootPath, currentFilePath);
            if (File.Exists(oldFilePath))
            {
                return oldFilePath;
            } else if (!mustExist)
            {
                return oldFilePath;
            }
            {
                return string.Empty;
            }
        }
        public string MoveFileFromTemp(string currentFilePath, string newFolder)
        {
            string tempPath = "temp/";
            if (string.IsNullOrWhiteSpace(currentFilePath) || !currentFilePath.StartsWith("/temp/")) //file is not in temp directory.
            {
                return currentFilePath;
            }
            if (currentFilePath.StartsWith("/")) { 
                currentFilePath = currentFilePath.Substring(1);
            }
            string uploadPath = Path.Combine(_webEnv.WebRootPath, newFolder);
            string oldFilePath = Path.Combine(_webEnv.WebRootPath, currentFilePath);
            if (!Directory.Exists(uploadPath))
                Directory.CreateDirectory(uploadPath);
            string newFileUploadPath = oldFilePath.Replace(tempPath, newFolder);
            if (File.Exists(oldFilePath))
            {
                File.Move(oldFilePath, newFileUploadPath);
            }
            string newFilePathToReturn = "/" + currentFilePath.Replace(tempPath, newFolder);
            return newFilePathToReturn;
        }
        public bool DeleteFile(string currentFilePath)
        {
            if (currentFilePath.StartsWith("/"))
            {
                currentFilePath = currentFilePath.Substring(1);
            }
            string filePath = Path.Combine(_webEnv.WebRootPath, currentFilePath);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            if (!File.Exists(filePath)) //verify file no longer exists.
            {
                return true;
            } else
            {
                return false;
            }
        }
    }
}
