using System;
using System.IO;
using Mk8Editor.Core;

namespace Mk8Editor.App
{
    public static class UserMessages
    {
        public static string For(Exception error)
        {
            // Known application messages are English; OS exception messages may be localized.
            if (error is InvalidDataException || error is UserFileException) return error.Message;
            if (error is UnauthorizedAccessException) return "Access denied. Choose a folder you can read and write.";
            if (error is FileNotFoundException || error is DirectoryNotFoundException) return "The file or folder could not be found. Check its location and try again.";
            if (error is IOException) return "Unable to read or save the file. Check permissions, available disk space and whether another program is using it.";
            return "The operation could not be completed. Check the selected file and try again.";
        }
    }
}
