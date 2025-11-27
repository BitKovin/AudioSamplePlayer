// MainWindow.xaml.cs
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Win32;
using NAudio.Wave;
using TagLib;

namespace SimpleAudioPlayer
{
    public partial class MainWindow : Window
    {
        private ObservableCollection<AudioFile> audioFiles = new ObservableCollection<AudioFile>();
        private ICollectionView filteredView; // ← This replaces filteredFiles + manual filtering
        private AudioPlayer audioPlayer = new AudioPlayer();
        private System.Timers.Timer progressTimer;
        private bool isSeeking;
        private string currentPlayingPath;

        public MainWindow()
        {
            InitializeComponent();

            // Bind directly to the full collection
            dgFiles.ItemsSource = audioFiles;

            // Create the filtered + sorted view
            filteredView = CollectionViewSource.GetDefaultView(audioFiles);
            filteredView.SortDescriptions.Add(new SortDescription("FileName", ListSortDirection.Ascending));

            // Initial bind (important!)
            dgFiles.ItemsSource = filteredView;

            progressTimer = new System.Timers.Timer(200);
            progressTimer.Elapsed += ProgressTimer_Elapsed;
            audioPlayer.PlaybackStopped += OnPlaybackStopped;
        }

        private async void BtnSelectDir_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                txtStatus.Text = "Loading files...";
                try
                {
                    var files = await Task.Run(() => FileFinder.FindAudioFiles(dialog.SelectedPath));
                    audioFiles.Clear();
                    foreach (var file in files)
                    {
                        audioFiles.Add(file);
                    }

                    // Apply initial filter (empty = show all)
                    ApplyFilter();
                    txtStatus.Text = $"Loaded {audioFiles.Count} files.";
                }
                catch (Exception ex)
                {
                    txtStatus.Text = $"Error loading files: {ex.Message}";
                }
            }
        }

        private void SeekRelative(double seconds)
        {
            if (audioPlayer.TotalTime > 0)
            {
                double newTime = Math.Max(0, Math.Min(audioPlayer.CurrentTime + seconds, audioPlayer.TotalTime));
                audioPlayer.Seek(newTime);
            }
        }

        private void DgFiles_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                if (audioPlayer.IsPlaying || audioPlayer.IsPaused)
                {
                    if (audioPlayer.IsPaused)
                        audioPlayer.Resume();
                    else
                        audioPlayer.Pause();
                    UpdatePlaybackButtons();
                }
                else if (dgFiles.SelectedItem is AudioFile)
                {
                    PlaySelected(false);
                }
                e.Handled = true;
            }
        }

        private void MenuSelectDir_Click(object sender, RoutedEventArgs e)
        {
            BtnSelectDir_Click(sender, e);
        }

        private void MenuExit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            string searchTerm = txtSearch.Text?.ToLower() ?? "";

            if (string.IsNullOrEmpty(searchTerm))
            {
                filteredView.Filter = null;
            }
            else
            {
                filteredView.Filter = item =>
                {
                    if (item is AudioFile af)
                    {
                        return af.FileName.ToLower().Contains(searchTerm) ||
                               (af.Description?.ToLower().Contains(searchTerm) ?? false);
                    }
                    return false;
                };
            }
        }

        private void DataGridRow_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is DataGridRow row && row.DataContext is AudioFile af)
            {
                dgFiles.SelectedItem = af;
                PlaySelected(false);
                e.Handled = true;
            }
        }

        private void DgFiles_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    PlaySelected(false);
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
            else if (e.Key == Key.Left)
            {
                e.Handled = true;
                SeekRelative(-2.0);
            }
            else if (e.Key == Key.Right)
            {
                e.Handled = true;
                SeekRelative(+2.0);
            }
        }

        private void BtnPlay_Click(object sender, RoutedEventArgs e)
        {
            if (audioPlayer.IsPaused)
            {
                audioPlayer.Resume();
            }
            else
            {
                PlaySelected(false);
            }
            UpdatePlaybackButtons();
        }

        private void BtnPause_Click(object sender, RoutedEventArgs e)
        {
            audioPlayer.Pause();
            UpdatePlaybackButtons();
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            audioPlayer.Stop();
            UpdatePlaybackButtons();
        }

        private void BtnPlaySegment_Click(object sender, RoutedEventArgs e)
        {
            PlaySelected(true);
        }

        private void PlaySelected(bool segment)
        {
            if (dgFiles.SelectedItem is not AudioFile af) return;

            if (!segment && af.Path == currentPlayingPath && audioPlayer.IsPlaying)
            {
                audioPlayer.Seek(0);
                return;
            }

            try
            {
                double start = double.TryParse(txtStart.Text, out var s) ? s : 0;
                double end = double.TryParse(txtEnd.Text, out var en) ? en : 0;
                if (segment && end > start)
                {
                    audioPlayer.PlaySegment(af.Path, start, end);
                }
                else
                {
                    audioPlayer.Play(af.Path);
                }
                audioPlayer.SetVolume((float)sldVolume.Value / 100f);
                lblCurrentFile.Content = $"Playing: {af.FileName}";
                currentPlayingPath = af.Path;
                progressTimer.Start();
                UpdatePlaybackButtons();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error playing file: {ex.Message}");
            }
        }

        private void OnPlaybackStopped(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                progressTimer.Stop();
                sldProgress.Value = 0;
                lblTime.Content = "00:00 / 00:00";
                lblCurrentFile.Content = "No file playing";
                currentPlayingPath = null;
                UpdatePlaybackButtons();
            });
        }

        private void ProgressTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    if (!isSeeking && audioPlayer.IsPlaying)
                    {
                        double progress = audioPlayer.CurrentTime / audioPlayer.TotalTime;
                        sldProgress.Value = progress * sldProgress.Maximum;
                        lblTime.Content = $"{TimeSpan.FromSeconds(audioPlayer.CurrentTime):mm\\:ss} / {TimeSpan.FromSeconds(audioPlayer.TotalTime):mm\\:ss}";
                    }
                });
            }
            catch { }
        }

        private void UpdatePlaybackButtons()
        {
            btnPlay.Content = audioPlayer.IsPaused ? "Resume" : "Play";
            btnPause.IsEnabled = audioPlayer.IsPlaying && !audioPlayer.IsPaused;
        }

        private void SldVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            audioPlayer.SetVolume((float)sldVolume.Value / 100f);
        }

        private void SldProgress_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            isSeeking = true;
        }

        private void SldProgress_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            isSeeking = false;
            var pos = e.GetPosition(sldProgress);
            double ratio = pos.X / sldProgress.ActualWidth;
            double seekTime = ratio * audioPlayer.TotalTime;
            audioPlayer.Seek(seekTime);
        }

        private void SldProgress_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (isSeeking)
            {
                double seekTime = sldProgress.Value / sldProgress.Maximum * audioPlayer.TotalTime;
                lblTime.Content = $"{TimeSpan.FromSeconds(seekTime):mm\\:ss} / {TimeSpan.FromSeconds(audioPlayer.TotalTime):mm\\:ss}";
            }
        }

        private void DgFiles_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && dgFiles.SelectedItem is AudioFile af)
            {
                DragDrop.DoDragDrop(dgFiles, new DataObject(DataFormats.FileDrop, new string[] { af.Path }), DragDropEffects.Copy);
            }
        }

        private void DgFiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgFiles.SelectedItem is AudioFile af)
            {
                txtEnd.Text = af.Duration.ToString("F2");
            }
        }
    }
}