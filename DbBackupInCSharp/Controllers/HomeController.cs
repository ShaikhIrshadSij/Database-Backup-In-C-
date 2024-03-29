﻿using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Configuration;
using System.Web.Mvc;

namespace DbBackupInCSharp.Controllers
{
    public class HomeController : Controller
    {
        //Install-Package Google.Apis.Sheets.v4 -Version 1.40.2.1636
        //Install-Package Google.Apis.Drive.v3 -Version 1.40.2.1631
        public static string[] DriveScope = { DriveService.Scope.Drive };
        public ActionResult Index() => View();
        public async Task<JsonResult> GenerateBackupFile()
        {
            try
            {
                await Task.Run(() =>
                {
                    string dbConnectionString = ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;
                    string backupFolderName = ConfigurationManager.AppSettings["BackUpFolder"].ToString();
                    if (!Directory.Exists(backupFolderName))
                        Directory.CreateDirectory(backupFolderName);
                    SqlConnectionStringBuilder sqlConnectionStringBuilder = new SqlConnectionStringBuilder(dbConnectionString);
                    var backupFileName = $"{backupFolderName}{sqlConnectionStringBuilder.InitialCatalog}-{DateTime.Now.ToString("yyyy-MM-dd")}.bak";
                    if (System.IO.File.Exists(backupFileName))
                        System.IO.File.Delete(backupFileName);
                    using (SqlConnection connection = new SqlConnection(sqlConnectionStringBuilder.ConnectionString))
                    {
                        string backupQuery = $"BACKUP DATABASE {sqlConnectionStringBuilder.InitialCatalog} TO DISK='{backupFileName}'";
                        using (SqlCommand command = new SqlCommand(backupQuery, connection))
                        {
                            connection.Open();
                            command.ExecuteNonQuery();
                        }
                    }
                    UploadOnDrive(backupFileName);
                });
                return Json(true, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(false, JsonRequestBehavior.AllowGet);
            }
        }
        public bool UploadOnDrive(string fileName)
        {
            string folderName = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(DateTime.Now.Month);
            string oldFolderName = ConfigurationManager.AppSettings["DatabaseBackupFolder"];
            string folderId = ConfigurationManager.AppSettings["DriveFolderId"].ToString();
            DriveService service = GetServiceForGoogleDrive();
            string newFolderId = string.Empty;
            if (folderName != oldFolderName)
            {
                DeleteFolderInDrive(folderId);
                newFolderId = CreateFolderInDrive(folderName);
            }
            else if (!CheckFolderExistOrNot(folderId))
                newFolderId = CreateFolderInDrive(folderName);
            string newFolderIds = newFolderId != "" ? newFolderId : ConfigurationManager.AppSettings["DriveFolderId"];
            var FileMetaData = new Google.Apis.Drive.v3.Data.File
            {
                Name = Path.GetFileName(fileName),
                MimeType = MimeMapping.GetMimeMapping(fileName),
                Parents = new List<string>
                        {
                            newFolderIds
                        }
            };
            FilesResource.CreateMediaUpload request;
            using (var stream = new FileStream(fileName, FileMode.Open))
            {
                request = service.Files.Create(FileMetaData, stream, FileMetaData.MimeType);
                request.Fields = "id";
                request.Upload();
            }
            if (System.IO.File.Exists(fileName))
                System.IO.File.Delete(fileName);
            return false;
        }
        public DriveService GetServiceForGoogleDrive()
        {
            UserCredential credential;
            using (var stream = new FileStream(Server.MapPath("~/client_secret_drive_api.json"), FileMode.Open, FileAccess.Read))
            {
                string folderPath = @"C:\";
                string filePath = Path.Combine(folderPath, "DriveServiceCredentials.json");
                credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.Load(stream).Secrets,
                    DriveScope,
                    "user",
                    CancellationToken.None,
                    new FileDataStore(filePath, true)).Result;
            }
            DriveService service = new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "DbBackup",
            });
            return service;
        }
        public string CreateFolderInDrive(string FolderName)
        {
            DriveService service = GetServiceForGoogleDrive();
            var FileMetaData = new Google.Apis.Drive.v3.Data.File();
            FileMetaData.Name = FolderName;
            FileMetaData.MimeType = "application/vnd.google-apps.folder";
            FilesResource.CreateRequest request;
            request = service.Files.Create(FileMetaData);
            request.Fields = "id";
            var data = request.Execute();
            Configuration webConfigApp = WebConfigurationManager.OpenWebConfiguration("~");
            webConfigApp.AppSettings.Settings["DriveFolderId"].Value = data.Id;
            webConfigApp.AppSettings.Settings["DatabaseBackupFolder"].Value = FolderName;
            webConfigApp.Save();
            return data.Id;
        }
        public bool DeleteFolderInDrive(string FolderId)
        {
            if (CheckFolderExistOrNot(FolderId))
            {
                DriveService service = GetServiceForGoogleDrive();
                FilesResource.DeleteRequest request;
                request = service.Files.Delete(FolderId);
                request.Fields = "id";
                request.Execute();
            }
            return true;
        }
        public bool CheckFolderExistOrNot(string FolderId)
        {
            bool IsExist = false;
            DriveService service = GetServiceForGoogleDrive();
            FilesResource.ListRequest FileListRequest = service.Files.List();
            FileListRequest.Fields = "nextPageToken, files(*)";
            IList<Google.Apis.Drive.v3.Data.File> files = FileListRequest.Execute().Files;
            files = files.Where(x => x.MimeType == "application/vnd.google-apps.folder" && x.Id == FolderId).ToList();
            if (files.Count > 0)
            {
                IsExist = true;
            }
            return IsExist;
        }
    }
}