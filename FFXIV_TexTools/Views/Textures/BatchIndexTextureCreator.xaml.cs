using FFXIV_TexTools.Views.Controls;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Tga;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using xivModdingFramework.Textures;
using xivModdingFramework.Textures.FileTypes;

namespace FFXIV_TexTools.Views.Textures
{

    public partial class BatchIndexTextureCreator : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private string _inputDirectory;
        public string InputDirectory
        {
            get => _inputDirectory;
            set
            {
                _inputDirectory = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(InputDirectory)));
            }
        }

        private string _outputDirectory;
        public string OutputDirectory
        {
            get => _outputDirectory;
            set
            {
                _outputDirectory = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OutputDirectory)));
            }
        }

        private bool _convertEnabled = true;
        public bool ConvertEnabled
        {
            get => _convertEnabled;
            set
            {
                _convertEnabled = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ConvertEnabled)));
            }
        }

        private static TgaEncoder Encoder = new TgaEncoder()
        {
            BitsPerPixel = TgaBitsPerPixel.Pixel32,
            Compression = TgaCompression.None
        };

        public BatchIndexTextureCreator()
        {
            DataContext = this;
            InitializeComponent();
            Closing += OnClose;
        }

        private void OnClose(object sender, CancelEventArgs e)
        {
            if (Owner != null)
            {
                Owner.Activate();
            }
        }

        public static void ShowWindow(Window owner = null)
        {
            if (owner == null)
            {
                owner = MainWindow.GetMainWindow();
            }

            var wind = new BatchIndexTextureCreator()
            {
                Owner = owner,
            };
            wind.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            wind.Show();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void SelectInputDirectory_Click(object sender, RoutedEventArgs e)
        {
            using (var folderDialog = new FolderBrowserDialog())
            {
                if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    InputDirectory = folderDialog.SelectedPath;
                }
            }
        }

        private void SelectOutputDirectory_Click(object sender, RoutedEventArgs e)
        {
            using (var folderDialog = new FolderBrowserDialog())
            {
                if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    OutputDirectory = folderDialog.SelectedPath;
                }
            }
        }

        private async void Convert_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(InputDirectory) || !Directory.Exists(InputDirectory) ||
                string.IsNullOrWhiteSpace(OutputDirectory) || !Directory.Exists(OutputDirectory))
            {
                this.ShowError("Invalid Directory", "Please select valid input and output directories.");
                return;
            }

            ConvertEnabled = false;
            try
            {
                await ProcessDirectory(InputDirectory);
                System.Windows.MessageBox.Show("Conversion completed successfully.", "Success", (MessageBoxButton)MessageBoxButtons.OK, (MessageBoxImage)MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                this.ShowError("Conversion Error", "An error occurred while converting the textures:\n\n" + ex.Message);
            }
            finally
            {
                ConvertEnabled = true;
            }
        }

        private async Task ProcessDirectory(string currentDirectory)
        {
            foreach (string filePath in Directory.GetFiles(currentDirectory, "*.dds"))
            {
                await ConvertFile(filePath);
            }

            foreach (string subDir in Directory.GetDirectories(currentDirectory))
            {
                await ProcessDirectory(subDir);
            }
        }

        private string GetRelativePath(string fromPath, string toPath)
        {
            if (string.IsNullOrEmpty(fromPath)) throw new ArgumentNullException(nameof(fromPath));
            if (string.IsNullOrEmpty(toPath)) throw new ArgumentNullException(nameof(toPath));

            Uri fromUri = new Uri(fromPath);
            Uri toUri = new Uri(toPath);

            if (fromUri.Scheme != toUri.Scheme) return toPath; // path can't be made relative.

            Uri relativeUri = fromUri.MakeRelativeUri(toUri);
            string relativePath = Uri.UnescapeDataString(relativeUri.ToString());

            if (toUri.Scheme.Equals("file", StringComparison.InvariantCultureIgnoreCase))
            {
                relativePath = relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            }

            return relativePath;
        }

        private async Task ConvertFile(string filePath)
        {
            var relativePath = GetRelativePath(InputDirectory, filePath);
            var outputPath = Path.Combine(OutputDirectory, Path.ChangeExtension(relativePath, ".tga"));
            var outputDir = Path.GetDirectoryName(outputPath);

            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // Update Progress here

            var data = await Tex.GetPixelDataFromFile(filePath);

            var indexData = new byte[data.PixelData.Length];

            await TextureHelpers.CreateIndexTexture(data.PixelData, indexData, data.Width, data.Height);

            using (var image = Image.LoadPixelData<Rgba32>(indexData, data.Width, data.Height))
            {
                image.SaveAsTga(outputPath, Encoder);
            }

            // Update Progress here
        }
    }
}