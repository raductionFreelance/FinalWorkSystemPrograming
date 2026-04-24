using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace FinalWork
{
    public class FileItem
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public string Size { get; set; } = string.Empty;
    }

    public partial class MainWindow : Window
    {
        public ObservableCollection<FileItem> LeftFiles { get; set; } = new ObservableCollection<FileItem>();
        public ObservableCollection<FileItem> RightFiles { get; set; } = new ObservableCollection<FileItem>();

        private FileItem _buffer = null;
        private bool isMovingOperation = false;
        private string _sourceSide = "";

        public MainWindow()
        {
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal;
            InitializeComponent();
            LeftFileListView.ItemsSource = LeftFiles;
            RightFileListView.ItemsSource = RightFiles;
            PathBox1.Text = @"C:\";
            PathBox2.Text = @"C:\";
        }

        private async void OpenFile1(object sender, RoutedEventArgs e)
        {
            var selectedItem = LeftFileListView.SelectedItem as FileItem;
            if (selectedItem != null)
            {
                if (Directory.Exists(selectedItem.FullPath))
                    PathBox1.Text = selectedItem.FullPath;
                else
                {
                    try { Process.Start(new ProcessStartInfo(selectedItem.FullPath) { UseShellExecute = true }); }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                    return;
                }
            }
            await BtnScan_Click1(null, null);
        }

        private async void OpenFile2(object sender, RoutedEventArgs e)
        {
            var selectedItem = RightFileListView.SelectedItem as FileItem;
            if (selectedItem != null)
            {
                if (Directory.Exists(selectedItem.FullPath))
                    PathBox2.Text = selectedItem.FullPath;
                else
                {
                    try { Process.Start(new ProcessStartInfo(selectedItem.FullPath) { UseShellExecute = true }); }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                    return;
                }
            }
            await BtnScan_Click2(null, null);
        }

        private async void CloseFile1(object sender, RoutedEventArgs e)
        {
            var parent = Directory.GetParent(PathBox1.Text);
            if (parent != null)
            {
                PathBox1.Text = parent.FullName;
                await BtnScan_Click1(null, null);
            }
        }

        private async void CloseFile2(object sender, RoutedEventArgs e)
        {
            var parent = Directory.GetParent(PathBox2.Text);
            if (parent != null)
            {
                PathBox2.Text = parent.FullName;
                await BtnScan_Click2(null, null);
            }
        }

        private async Task BtnScan_Click1(object sender, RoutedEventArgs e)
        {
            if (!Directory.Exists(PathBox1.Text)) return;
            LeftFiles.Clear();
            var progress = new Progress<List<FileItem>>(batch => { foreach (var item in batch) LeftFiles.Add(item); });
            await ScanAsync(PathBox1.Text, progress);
        }

        private async Task BtnScan_Click2(object sender, RoutedEventArgs e)
        {
            if (!Directory.Exists(PathBox2.Text)) return;
            RightFiles.Clear();
            var progress = new Progress<List<FileItem>>(batch => { foreach (var item in batch) RightFiles.Add(item); });
            await ScanAsync(PathBox2.Text, progress);
        }

        private Task ScanAsync(string path, IProgress<List<FileItem>> progress)
        {
            return Task.Run(() =>
            {
                try
                {
                    var dirInfo = new DirectoryInfo(path);
                    var items = dirInfo.GetFileSystemInfos();
                    var batch = new List<FileItem>();
                    foreach (var item in items)
                    {
                        batch.Add(new FileItem
                        {
                            Name = item.Name,
                            FullPath = item.FullName,
                            Size = (item is FileInfo f) ? $"{f.Length / 1024} KB" : "<DIR>"
                        });
                        if (batch.Count >= 50) { progress.Report(batch); batch = new List<FileItem>(); }
                    }
                    if (batch.Count > 0) progress.Report(batch);
                }
                catch { }
            });
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (e.Key == Key.C) { isMovingOperation = false; PrepareBuffer(); }
                else if (e.Key == Key.X) { isMovingOperation = true; PrepareBuffer(); }
                else if (e.Key == Key.V) { ExecutePaste(); }
            }
        }

        private void PrepareBuffer()
        {
            ListView activeList = LeftFileListView.IsFocused ? LeftFileListView : (RightFileListView.IsFocused ? RightFileListView : null);
            if (activeList == null)
            {
                if (LeftFileListView.SelectedItem != null) activeList = LeftFileListView;
                else if (RightFileListView.SelectedItem != null) activeList = RightFileListView;
            }

            if (activeList != null && activeList.SelectedItem is FileItem selected)
            {
                _buffer = selected;
                _sourceSide = (activeList == LeftFileListView) ? "Left" : "Right";
            }
        }

        private async void ExecutePaste()
        {
            if (_buffer == null) return;
            string targetDir = (_sourceSide == "Left") ? PathBox2.Text : PathBox1.Text;
            string targetPath = System.IO.Path.Combine(targetDir, _buffer.Name);

            if (_buffer.FullPath.Equals(targetPath, StringComparison.OrdinalIgnoreCase)) return;

            try
            {
                await Task.Run(() =>
                {
                    if (File.Exists(_buffer.FullPath))
                    {
                        if (isMovingOperation) File.Move(_buffer.FullPath, targetPath, true);
                        else File.Copy(_buffer.FullPath, targetPath, true);
                    }
                    else if (Directory.Exists(_buffer.FullPath))
                    {
                        if (isMovingOperation) Directory.Move(_buffer.FullPath, targetPath);
                    }
                });
                await BtnScan_Click1(null, null);
                await BtnScan_Click2(null, null);
                if (isMovingOperation) _buffer = null;
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }
    }
}