/*---------------------------------------------------------------------------------------------
*  Copyright (c) Nicolas Jinchereau. All rights reserved.
*  Licensed under the MIT License. See License.txt in the project root for license information.
*--------------------------------------------------------------------------------------------*/

using System;
using System.Windows.Media.Imaging;
using System.Windows;
using System.IO;
using System.Diagnostics;
using Microsoft.Win32;
using System.Security.Principal;
using System.Runtime.InteropServices;

namespace ShowdownSoftware
{
    public static class Util
    {
        static byte[] imgBuffer = new byte[0];
        static double[] rowBuffer = new double[0];
        static double[] colBuffer = new double[0];

        public static Int32Rect GetContentRect(BitmapSource image, double threshold = 0.1)
        {
            int width = image.PixelWidth;
            int height = image.PixelHeight;

            int stride = image.PixelWidth * 4;
            int size = image.PixelHeight * stride;
            if(imgBuffer.Length != size) imgBuffer = new byte[size];
            image.CopyPixels(imgBuffer, stride, 0);

            if(rowBuffer.Length != height) rowBuffer = new double[height];
            if(colBuffer.Length != width) colBuffer = new double[width];
            
            Array.Clear(rowBuffer, 0, rowBuffer.Length);
            Array.Clear(colBuffer, 0, colBuffer.Length);
            
            int pixelCount = width * height;
            for(int i = 0; i < pixelCount; ++i)
            {
                int x = i % width;
                int y = i / width;

                var r = imgBuffer[i * 4 + 0];
                var g = imgBuffer[i * 4 + 1];
                var b = imgBuffer[i * 4 + 2];
                var brightness = GetBrightness(r, g, b);
                var inv = 1.0 - brightness;

                rowBuffer[y] += inv;
                colBuffer[x] += inv;
            }

            double maxRow = 0;
            for(int y = 0; y < rowBuffer.Length; ++y)
                maxRow = Math.Max(maxRow, rowBuffer[y]);

            if(maxRow > 0)
            {
                for(int y = 0; y < rowBuffer.Length; ++y)
                {
                    double val = rowBuffer[y] / maxRow;
                    rowBuffer[y] = 1.0f - (1.0 - val) * (1.0 - val);
                }
            }

            double maxCol = 0;
            for(int x = 0; x < colBuffer.Length; ++x)
                maxCol = Math.Max(maxCol, colBuffer[x]);

            if(maxCol > 0)
            {
                for(int x = 0; x < colBuffer.Length; ++x)
                {
                    double val = colBuffer[x] / maxCol;
                    colBuffer[x] = 1.0f - (1.0 - val) * (1.0 - val);
                }
            }

            int left = int.MaxValue;
            int top = int.MaxValue;
            int right = -1;
            int bottom = -1;

            for(int y = 0; y < rowBuffer.Length; ++y)
            {
                if(rowBuffer[y] >= threshold)
                {
                    top = Math.Min(top, y);
                    bottom = Math.Max(bottom, y);
                }
            }

            for(int x = 0; x < colBuffer.Length; ++x)
            {
                if(colBuffer[x] >= threshold)
                {
                    left = Math.Min(left, x);
                    right = Math.Max(right, x);
                }
            }
            
            if(left >= width) left = 0;
            if(top >= height) top = 0;
            if(right <= 0) right = width;
            if(bottom <= 0) bottom = height;
            
            var ret = new Int32Rect();
            ret.X = left;
            ret.Y = top;
            ret.Width = right - left + 1;
            ret.Height = bottom - top + 1;
            return ret;
        }

        public static double GetBrightness(byte r, byte g, byte b) {
            return (0.333 * r + 0.334 * g + 0.333 * b) / 255.0;
        }

        public static Int32Rect MakeClippedRect(Point a, Point b, int maxWidth, int maxHeight)
        {
            if(b.X < a.X) {
                var tmp = a.X;
                a.X = b.X;
                b.X = tmp;
            }

            if(b.Y < a.Y) {
                var tmp = a.Y;
                a.Y = b.Y;
                b.Y = tmp;
            }

            a.X = Math.Max(a.X, 0);
            a.Y = Math.Max(a.Y, 0);
            b.X = Math.Min(b.X, maxWidth);
            b.Y = Math.Min(b.Y, maxHeight);
            return new Int32Rect((int)a.X, (int)a.Y, (int)(b.X - a.X), (int)(b.Y - a.Y));
        }

        public static string GetSavePath(Window dialogOwner, string filename)
        {
            string ext = Path.GetExtension(filename);

            string filter = !string.IsNullOrEmpty(ext) ?
                ext.Substring(1).ToUpper() + " Files|*" + ext
                : "All Files|*.*";

            var dialog = new SaveFileDialog();
            dialog.Title = "Save File";
            dialog.Filter = filter;
            dialog.FileName = filename;
            dialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            
            bool? res = dialog.ShowDialog(dialogOwner);
            
            if(res.Value == false || string.IsNullOrEmpty(dialog.FileName))
                return null;
            
            return dialog.FileName;
        }

        public static string AppendFileTag(string filename, string tag)
        {
            var dir = Path.GetDirectoryName(filename);
            var title = Path.GetFileNameWithoutExtension(filename);
            var ext = Path.GetExtension(filename);
            return Path.Combine(dir, title + tag + ext);
        }

        public static BitmapEncoder EncoderForFile(string filename)
        {
            string ext = Path.GetExtension(filename).ToLower();
            
            if(ext == ".bmp")
                return new BmpBitmapEncoder();
            else if(ext == ".gif")
                return new GifBitmapEncoder();
            else if(ext == ".jpg" || ext == ".jpe" || ext == ".jpeg")
                return new JpegBitmapEncoder() { QualityLevel = 80 };
            else if(ext == ".png")
                return new PngBitmapEncoder() { Interlace = PngInterlaceOption.Off };
            else if(ext == ".tif" || ext == ".tiff")
                return new TiffBitmapEncoder();

            return null;
        }

        public static System.Drawing.Bitmap BitmapSourceToBitmap(BitmapSource source)
        {
            var fmt = System.Drawing.Imaging.PixelFormat.Format32bppArgb;
            var bmp = new System.Drawing.Bitmap(source.PixelWidth, source.PixelHeight, fmt);
            var data = bmp.LockBits(new System.Drawing.Rectangle(0, 0, bmp.Size.Width, bmp.Size.Height), System.Drawing.Imaging.ImageLockMode.WriteOnly, fmt);
            source.CopyPixels(Int32Rect.Empty, data.Scan0, data.Height * data.Stride, data.Stride);
            bmp.UnlockBits(data);
            return bmp;
        }
        
        public static BitmapSource BitmapToBitmapSource(System.Drawing.Bitmap source)
        {
            return System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                source.GetHbitmap(), IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        }

        public static bool IsRunningAsAdmin()
        {
            WindowsIdentity id = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(id);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        
        const uint SHCNE_ASSOCCHANGED = 0x08000000;

        [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

        public static void RegisterApplication(bool allowElevatedRespawn)
        {
            if(!IsRunningAsAdmin())
            {
                if(allowElevatedRespawn)
                {
                    ProcessStartInfo info = new ProcessStartInfo();
                    info.UseShellExecute = true;
                    info.WorkingDirectory = Environment.CurrentDirectory;
                    info.Arguments = "/register";
                    info.FileName = System.Reflection.Assembly.GetExecutingAssembly().Location;
                    info.Verb = "runas";
                    Process.Start(info);
                }
                
                return;
            }

            var execPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var execName = Path.GetFileName(execPath);
            
            string[] extentions = {
                ".bmp", ".gif", ".jpg", ".jpe", ".jpeg", ".png", ".tif", ".tiff"
            };

            foreach(var ext in extentions)
            {
                using(var key = Registry.ClassesRoot.CreateSubKey("CleverCrop" + ext)) {
                    key.SetValue("", ext.Substring(1).ToUpper() + " File");
                }

                using(var key = Registry.ClassesRoot.CreateSubKey("CleverCrop" + ext + @"\shell")) {
                    key.SetValue("", "Open");
                }

                using(var key = Registry.ClassesRoot.CreateSubKey("CleverCrop" + ext + @"\shell\Open")) {
                    key.SetValue("", "Open");
                }

                using(var key = Registry.ClassesRoot.CreateSubKey("CleverCrop" + ext + @"\shell\Open\command")) {
                    key.SetValue("", $@"""{execPath}"" ""%1""");
                }
                
                using(var key = Registry.CurrentUser.CreateSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FileExts\{ext}\OpenWithProgids")) {
                    key.SetValue("CleverCrop" + ext, new byte[0], RegistryValueKind.None);
                }
            }
            
            // HKEY_CLASSES_ROOT
            using(var key = Registry.ClassesRoot.CreateSubKey($@"Applications\{execName}")) {
                key.SetValue("FriendlyAppName", "Clever Crop");
            }

            using(var key = Registry.ClassesRoot.CreateSubKey($@"Applications\{execName}\shell\Open")) {
                key.SetValue("", "Open with Clever Crop");
            }

            using(var key = Registry.ClassesRoot.CreateSubKey($@"Applications\{execName}\shell\Open\command")) {
                key.SetValue("", $@"""{execPath}"" ""%1""");
            }

            using(var key = Registry.ClassesRoot.CreateSubKey($@"Applications\{execName}\SupportedTypes")) {
                key.SetValue(".bmp", "BMP File");
                key.SetValue(".gif", "GIF File");
                key.SetValue(".jpg", "JPG File");
                key.SetValue(".jpe", "JPE File");
                key.SetValue(".jpeg", "JPEG File");
                key.SetValue(".png", "PNG File");
                key.SetValue(".tif", "TIF File");
                key.SetValue(".tiff", "TIFF File");
            }

            // HKEY_LOCAL_MACHINE
            using(var key = Registry.LocalMachine.CreateSubKey($@"SOFTWARE\Classes\Applications\{execName}")) {
                key.SetValue("FriendlyAppName", "Clever Crop");
            }

            using(var key = Registry.LocalMachine.CreateSubKey($@"SOFTWARE\Classes\Applications\{execName}\shell\Open")) {
                key.SetValue("", "Open with Clever Crop");
            }

            using(var key = Registry.LocalMachine.CreateSubKey($@"SOFTWARE\Classes\Applications\{execName}\shell\Open\command")) {
                key.SetValue("", $@"""{execPath}"" ""%1""");
            }

            using(var key = Registry.LocalMachine.CreateSubKey($@"SOFTWARE\Classes\Applications\{execName}\SupportedTypes")) {
                key.SetValue(".bmp", "BMP File");
                key.SetValue(".gif", "GIF File");
                key.SetValue(".jpg", "JPG File");
                key.SetValue(".jpe", "JPE File");
                key.SetValue(".jpeg", "JPEG File");
                key.SetValue(".png", "PNG File");
                key.SetValue(".tif", "TIF File");
                key.SetValue(".tiff", "TIFF File");
            }
            
            SHChangeNotify(SHCNE_ASSOCCHANGED, 0x0000, IntPtr.Zero, IntPtr.Zero);
        }

        public static Point Mul(this Point pt, float num) {
            return new Point(pt.X * num, pt.Y * num);
        }

        public static Point Div(this Point pt, float denom) {
            return new Point(pt.X / denom, pt.Y / denom);
        }
    }
}
