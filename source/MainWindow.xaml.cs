/*---------------------------------------------------------------------------------------------
*  Copyright (c) Nicolas Jinchereau. All rights reserved.
*  Licensed under the MIT License. See License.txt in the project root for license information.
*--------------------------------------------------------------------------------------------*/

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using WinForms = System.Windows.Forms;
using System.Windows.Interop;

namespace ShowdownSoftware
{
    public partial class MainWindow : Window
    {
        string currentFile;
        BitmapSource currentImage;
        System.Windows.Shapes.Rectangle selectionBorder;
        Int32Rect selection;
        Point dragStart;
        bool isDragging = false;

        public MainWindow() {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            selectionBorder = new System.Windows.Shapes.Rectangle();
            selectionBorder.Stroke = Brushes.Red;
            selectionBorder.StrokeDashArray = new DoubleCollection(new double[] { 8, 8 });
            selectionBorder.Width = 0;
            selectionBorder.Height = 0;
            selectionBorder.StrokeThickness = 1;
            overlay.Children.Add(selectionBorder);
            
            string[] args = Environment.GetCommandLineArgs();
            if(args.Length > 1){
                LoadFile(args[1]);
            }
        }

        public void LoadFile(string file, bool suggestCropping = true)
        {
            try
            {
                currentFile = file;

                var img = new BitmapImage();
                img.BeginInit();
                img.UriSource = new Uri(currentFile);
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.EndInit();
                currentImage = img;
                image.Source = img;
                
                ShrinkToWindow();
                SetImageScale((float)scaleSlider.Value);

                if(suggestCropping)
                    SelectContent();
                else
                    SelectAll();

                UpdateWindowTitle();
                lblStatus.Text = "Loaded image: " + file;
            }
            catch(Exception ex)
            {
                currentFile = null;
                currentImage = null;
                selectionBorder.Width = 0;
                selectionBorder.Height = 0;
                Canvas.SetTop(selectionBorder, 0);
                Canvas.SetLeft(selectionBorder, 0);
                MessageBox.Show(ex.ToString(), "Failed to open image");
            }
        }
        
        private void image_Drop(object sender, DragEventArgs e)
        {
            if(e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                LoadFile(files[0]);
            }
        }

        private void scaleSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if(currentImage == null)
                return;
            
            SetImageScale((float)e.NewValue);
        }

        void SetImageScale(float scale)
        {
            image.Width = currentImage.PixelWidth * scale;
            image.Height = currentImage.PixelHeight * scale;
            overlay.Width = image.Width;
            overlay.Height = image.Height;
            UpdateSelectionBorder(scale);
        }

        void UpdateSelectionBorder(float s)
        {
            selectionBorder.Width = selection.Width * s;
            selectionBorder.Height = selection.Height * s;
            Canvas.SetLeft(selectionBorder, selection.X * s);
            Canvas.SetTop(selectionBorder, selection.Y * s);
        }
        
        void UpdateWindowTitle()
        {
            if(!string.IsNullOrEmpty(currentFile))
                this.Title = Path.GetFileName(currentFile) + " - Clever Crop";
            else
                this.Title = "Clever Crop";
        }

        private void image_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if(!isDragging && !image.IsMouseCaptured)
            {
                dragStart = e.GetPosition(image);
                isDragging = true;
                image.CaptureMouse();
            }
        }
        
        private void image_MouseMove(object sender, MouseEventArgs e)
        {
            if(isDragging)
            {
                float s = (float)scaleSlider.Value;
                
                selection = Util.MakeClippedRect(
                    dragStart.Div(s), e.GetPosition(image).Div(s),
                    currentImage.PixelWidth, currentImage.PixelHeight);

                UpdateSelectionBorder(s);
            }
        }
        
        private void image_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if(isDragging)
            {
                float s = (float)scaleSlider.Value;

                selection = Util.MakeClippedRect(
                    dragStart.Div(s), e.GetPosition(image).Div(s),
                    currentImage.PixelWidth, currentImage.PixelHeight);

                UpdateSelectionBorder(s);
                
                isDragging = false;
                image.ReleaseMouseCapture();
            }
        }
        
        private void btnCrop_Click(object sender, RoutedEventArgs e) {
            CropImage();
        }
        
        private void thresholdSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if(currentImage == null)
                return;

            SelectContent();
        }
        
        private void btnBrowseClear_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var nativeWindow = new WinForms.NativeWindow();
            nativeWindow.AssignHandle(new WindowInteropHelper(this).Handle);

            var oldSaveLoc = Properties.Settings.Default.exportPath;

            var dlg = new WinForms.FolderBrowserDialog();

            if(!string.IsNullOrEmpty(oldSaveLoc))
                dlg.SelectedPath = oldSaveLoc;
            else
                dlg.SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

            var result = dlg.ShowDialog(nativeWindow);
            if(result == WinForms.DialogResult.OK)
            {
                string newSavePath = dlg.SelectedPath;
                
                if(!string.IsNullOrEmpty(newSavePath))
                {
                    txtExportPath.Text = newSavePath;
                    Properties.Settings.Default.exportPath = newSavePath;
                    Properties.Settings.Default.Save();
                }
            }
        }
        
        private void btnBrowseClear_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            txtExportPath.Text = "";
            Properties.Settings.Default.exportPath = "";
            Properties.Settings.Default.Save();
        }

        private void btnRotateCW_Click(object sender, RoutedEventArgs e) {
            RotateCW();
        }

        private void btnRotateCCW_Click(object sender, RoutedEventArgs e) {
            RotateCCW();
        }

        private void btnCopyImage_Click(object sender, RoutedEventArgs e) {
            CopyImage();
        }
        
        private void mnuOpen_Click(object sender, RoutedEventArgs e) {
            OpenImage();
        }

        private void mnuRevert_Click(object sender, RoutedEventArgs e) {
            RevertImage();
        }

        private void mnuSave_Click(object sender, RoutedEventArgs e) {
            SaveImage();
        }

        private void mnuSaveAs_Click(object sender, RoutedEventArgs e) {
            SaveImageAs();
        }

        private void mnuSaveCopy_Click(object sender, RoutedEventArgs e) {
            SaveImageCopy();
        }

        private void mnuExportCopy_Click(object sender, RoutedEventArgs e) {
            SaveImageToExportPath();
        }

        private void mnuClose_Click(object sender, RoutedEventArgs e) {
            CloseImage();
        }

        private void mnuExit_Click(object sender, RoutedEventArgs e) {
            Application.Current.Shutdown();
        }

        private void mnuSelectAll_Click(object sender, RoutedEventArgs e) {
            SelectAll();
        }

        private void mnuSelectContent_Click(object sender, RoutedEventArgs e) {
            SelectContent();
        }
        
        private void mnuCrop_Click(object sender, RoutedEventArgs e) {
            CropImage();
        }

        private void mnuRotateCW_Click(object sender, RoutedEventArgs e) {
            RotateCW();
        }

        private void mnuRotateCCW_Click(object sender, RoutedEventArgs e) {
            RotateCCW();
        }

        private void mnuCopy_Click(object sender, RoutedEventArgs e) {
            CopyImage();
        }

        private void mnuZoomIn_Click(object sender, RoutedEventArgs e) {
            ZoomIn();
        }

        private void mnuZoomOut_Click(object sender, RoutedEventArgs e) {
            ZoomOut();
        }

        private void mnuShrinkToWindow_Click(object sender, RoutedEventArgs e) {
            ShrinkToWindow();
        }

        private void mnuFitToWindow_Click(object sender, RoutedEventArgs e) {
            FitToWindow();
        }

        private void mnuActualSize_Click(object sender, RoutedEventArgs e) {
            ZoomToActualSize();
        }
        
        private void mnuRegisterTypes_Click(object sender, RoutedEventArgs e) {
            Util.RegisterApplication(true);
        }
        
        void OpenImage()
        {
            string ext = Path.GetExtension(currentFile);
            
            string[] extensions = {
                "*.bmp", "*.gif", "*.jpg", "*.jpe", "*.jpeg", "*.png", "*.tif", "*.tiff"
            };
            
            var ex1 = string.Join(", ", extensions);
            var ex2 = string.Join(";", extensions);
            
            var dialog = new Microsoft.Win32.OpenFileDialog();
            dialog.Title = "Open File";
            dialog.Filter = $"Image Files ({ex1})|{ex2}"; //"Image Files (*.bmp, *.jpg, ...)|*.bmp;*.jpg;..."
            dialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            
            bool? res = dialog.ShowDialog(this);

            if(res.HasValue && res.Value && !string.IsNullOrEmpty(dialog.FileName)) {
                LoadFile(dialog.FileName);
            }
        }

        void RevertImage()
        {
            if(!string.IsNullOrEmpty(currentFile))
                LoadFile(currentFile);
        }

        void SaveImage()
        {
            if(currentImage == null)
                return;

            SaveImageToPath(currentFile, false);
        }

        void SaveImageAs()
        {
            if(currentImage == null)
                return;
            
            SaveImageToPath(Util.GetSavePath(this, Path.GetFileName(currentFile)), true);
        }

        bool PromptOverwrite(string filepath)
        {
            var res = MessageBox.Show(
                "'" + filepath + "' already exists.\r\nDo you want to replace it?",
                "Confirm Overwriting File",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Warning);

            return res == MessageBoxResult.OK;
        }

        void SaveImageCopy()
        {
            if(currentImage == null)
                return;
            
            var savePath = Util.AppendFileTag(currentFile, "_cropped");

            if(File.Exists(savePath) && !PromptOverwrite(savePath))
                return;
            
            SaveImageToPath(savePath, false);
        }

        void SaveImageToExportPath()
        {
            string savePath = null;
            
            if(string.IsNullOrEmpty(Properties.Settings.Default.exportPath))
            {
                MessageBox.Show("Export path not set");
            }
            else
            {
                string filename = Path.GetFileName(currentFile);
                savePath = Path.Combine(Properties.Settings.Default.exportPath, filename);

                if(File.Exists(savePath) && !PromptOverwrite(savePath))
                    return;

                SaveImageToPath(savePath, false);
            }
        }

        void SaveImageToPath(string savePath, bool updateCurrentFile)
        {
            if(currentImage == null)
                return;

            try
            {
                Cursor = Cursors.Wait;

                if(!string.IsNullOrEmpty(savePath))
                {
                    using(var stream = new FileStream(savePath, FileMode.Create))
                    {
                        BitmapEncoder encoder = Util.EncoderForFile(savePath);
                        
                        if(encoder != null)
                        {
                            encoder.Frames.Add(BitmapFrame.Create(currentImage));
                            encoder.Save(stream);

                            if(updateCurrentFile)
                            {
                                currentFile = savePath;
                                UpdateWindowTitle();
                            }

                            lblStatus.Text = "Saved image: " + savePath;
                        }
                        else
                        {
                            MessageBox.Show("Unsupported file type: " + Path.GetExtension(savePath));
                        }
                    }
                }
            }
            catch(Exception ex) {
                lblStatus.Text = "Failed to save image: " + ex.Message;
            }
            finally {
                Cursor = Cursors.Arrow;
            }
        }

        void CloseImage()
        {
            if(currentImage == null)
                return;
            
            currentFile = null;
            currentImage = null;
            image.Source = null;
            selectionBorder.Width = 0;
            selectionBorder.Height = 0;
            Canvas.SetTop(selectionBorder, 0);
            Canvas.SetLeft(selectionBorder, 0);
            selection = Int32Rect.Empty;
            dragStart = new Point();
            isDragging = false;
            if(image.IsMouseCaptured) image.ReleaseMouseCapture();
            scaleSlider.Value = 1.0f;
            thresholdSlider.Value = 0.1f;
            UpdateWindowTitle();
        }
        
        void RotateCW()
        {
            if(currentImage == null)
                return;

            var bitmap = Util.BitmapSourceToBitmap(currentImage);
            bitmap.RotateFlip(System.Drawing.RotateFlipType.Rotate90FlipNone);
            currentImage = Util.BitmapToBitmapSource(bitmap);

            float s = (float)scaleSlider.Value;
            image.Source = currentImage;
            image.Width = currentImage.PixelWidth * s;
            image.Height = currentImage.PixelHeight * s;
            overlay.Width = image.Width;
            overlay.Height = image.Height;
            selection = new Int32Rect((int)currentImage.Width - selection.Height - selection.Y, selection.X, selection.Height, selection.Width);
            UpdateSelectionBorder(s);
        }

        void RotateCCW()
        {
            if(currentImage == null)
                return;

            var bitmap = Util.BitmapSourceToBitmap(currentImage);
            bitmap.RotateFlip(System.Drawing.RotateFlipType.Rotate270FlipNone);
            currentImage = Util.BitmapToBitmapSource(bitmap);

            float s = (float)scaleSlider.Value;
            image.Source = currentImage;
            image.Width = currentImage.PixelWidth * s;
            image.Height = currentImage.PixelHeight * s;
            overlay.Width = image.Width;
            overlay.Height = image.Height;
            selection = new Int32Rect(selection.Y, (int)currentImage.Height - selection.Width - selection.X, selection.Height, selection.Width);
            UpdateSelectionBorder(s);
        }

        void SelectContent()
        {
            if(currentImage == null)
                return;

            selection = Util.GetContentRect(currentImage, Math.Round(thresholdSlider.Value, 2));
            UpdateSelectionBorder((float)scaleSlider.Value);
        }

        void SelectAll()
        {
            if(currentImage == null)
                return;

            selection = new Int32Rect(0, 0, currentImage.PixelWidth, currentImage.PixelHeight);
            UpdateSelectionBorder((float)scaleSlider.Value);
        }

        void CropImage()
        {
            if(currentImage == null)
                return;

            float s = (float)scaleSlider.Value;

            currentImage = new CroppedBitmap(currentImage, selection);
            image.Source = currentImage;

            image.Width = currentImage.PixelWidth * s;
            image.Height = currentImage.PixelHeight * s;
            overlay.Width = image.Width;
            overlay.Height = image.Height;

            SelectAll();
        }

        void CopyImage()
        {
            if(currentImage == null)
                return;

            Clipboard.SetImage(currentImage);
        }
        
        void ZoomIn() {
            scaleSlider.Value = Math.Min(scaleSlider.Value + scaleSlider.LargeChange, scaleSlider.Maximum);
        }

        void ZoomOut() {
            scaleSlider.Value = Math.Max(scaleSlider.Value - scaleSlider.LargeChange, scaleSlider.Minimum);
        }
        
        void FitToWindow()
        {
            if(currentImage == null)
                return;

            float sx = (float)(svImage.ActualWidth - SystemParameters.VerticalScrollBarWidth) / currentImage.PixelWidth;
            float sy = (float)(svImage.ActualHeight - SystemParameters.HorizontalScrollBarHeight) / currentImage.PixelHeight;
            float s = Math.Min(sx, sy);
            s = (float)Math.Min(Math.Max(s, scaleSlider.Minimum), scaleSlider.Maximum);
            scaleSlider.Value = s;
        }

        void ShrinkToWindow()
        {
            if(currentImage == null)
                return;

            float sx = Math.Min((float)(svImage.ActualWidth - SystemParameters.VerticalScrollBarWidth) / currentImage.PixelWidth, 1.0f);
            float sy = Math.Min((float)(svImage.ActualHeight - SystemParameters.HorizontalScrollBarHeight) / currentImage.PixelHeight, 1.0f);
            float s = Math.Min(sx, sy);
            s = (float)Math.Min(Math.Max(s, scaleSlider.Minimum), scaleSlider.Maximum);
            scaleSlider.Value = s;
        }

        void ZoomToActualSize() {
            scaleSlider.Value = 1.0f;
        }
    }
}
