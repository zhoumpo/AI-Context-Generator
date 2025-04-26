// MainWindow.xaml.cs
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using System.Diagnostics;

namespace AI_Context_Generator
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private string _selectedDirectory;
        private string _logText;
        private int _progressValue;
        private bool _isScanComplete;
        private string _outputFilePath;

        public event PropertyChangedEventHandler PropertyChanged;

        public string SelectedDirectory
        {
            get => _selectedDirectory;
            set
            {
                _selectedDirectory = value;
                OnPropertyChanged(nameof(SelectedDirectory));
            }
        }

        public string LogText
        {
            get => _logText;
            set
            {
                _logText = value;
                OnPropertyChanged(nameof(LogText));
            }
        }

        public int ProgressValue
        {
            get => _progressValue;
            set
            {
                _progressValue = value;
                OnPropertyChanged(nameof(ProgressValue));
            }
        }

        public bool IsScanComplete
        {
            get => _isScanComplete;
            set
            {
                _isScanComplete = value;
                OnPropertyChanged(nameof(IsScanComplete));
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            IsScanComplete = false;
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                ValidateNames = false,
                CheckFileExists = false,
                CheckPathExists = true,
                FileName = "Select Folder",
                Title = "Select a folder to scan"
            };

            if (dialog.ShowDialog() == true)
            {
                SelectedDirectory = Path.GetDirectoryName(dialog.FileName);
            }
        }

        private async void ScanButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SelectedDirectory))
            {
                MessageBox.Show("Please select a directory first.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!Directory.Exists(SelectedDirectory))
            {
                MessageBox.Show("The selected directory does not exist.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            IsScanComplete = false;
            LogText = "";
            ProgressValue = 0;

            var excludePatterns = ExtensionsTextBox.Text
                .Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(ext => ext.Trim())
                .Where(ext => !string.IsNullOrEmpty(ext))
                .ToList();

            StatusText.Text = "Scanning...";

            try
            {
                await Task.Run(() => ScanAndCreateMarkdown(excludePatterns));
                StatusText.Text = "Scan complete!";
                IsScanComplete = true;
                MessageBox.Show($"Codebase scan complete! File saved at: {_outputFilePath}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusText.Text = "Error occurred";
                MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                AppendLog($"Error: {ex.Message}");
            }
        }

        private void ScanAndCreateMarkdown(List<string> excludePatterns)
        {
            _outputFilePath = Path.Combine(SelectedDirectory, "codebase.md");
            var markdown = new StringBuilder();

            markdown.AppendLine("# Codebase Documentation");
            markdown.AppendLine($"Generated on: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            markdown.AppendLine($"Directory: {SelectedDirectory}");
            markdown.AppendLine();
            markdown.AppendLine("## Project Structure");
            markdown.AppendLine();

            // Patterns to always exclude
            var defaultExcludePatterns = new List<string>
            {
                "bin", "obj", "node_modules", ".git", ".vs", ".idea",
                "packages", "dist", "build", ".exe", ".dll", ".pdb", ".cache",
                ".png", ".jpg", ".jpeg", ".gif", ".ico", ".svg", ".mp4", ".mp3",
                ".zip", ".tar", ".gz", ".rar", ".7z", ".bin", ".lock",
                // Godot-specific binary/generated files to exclude
                ".mono", ".stex", ".scn", ".ctex",
                ".dds", ".wav", ".ogg"
            };

            // Combine user patterns with default patterns
            var allExcludePatterns = defaultExcludePatterns.Concat(excludePatterns).ToList();

            // Get all files recursively
            var allFiles = Directory.GetFiles(SelectedDirectory, "*.*", SearchOption.AllDirectories)
                .Where(file => !ShouldExcludeFile(file, allExcludePatterns))
                .Where(file => IsLikelyCodeFile(file))
                .ToList();

            if (!allFiles.Any())
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    AppendLog("No code files found in the selected directory.");
                });
                return;
            }

            // Generate directory tree
            markdown.AppendLine("```");
            markdown.AppendLine(GenerateDirectoryTree(SelectedDirectory, allExcludePatterns));
            markdown.AppendLine("```");
            markdown.AppendLine();
            markdown.AppendLine("## File Contents");
            markdown.AppendLine();

            int fileCount = 0;
            foreach (var file in allFiles)
            {
                try
                {
                    string relativePath = Path.GetRelativePath(SelectedDirectory, file);
                    markdown.AppendLine($"### {relativePath}");
                    markdown.AppendLine();

                    string extension = Path.GetExtension(file).ToLower().TrimStart('.');
                    string languageHint = GetLanguageHint(extension);

                    markdown.AppendLine($"```{languageHint}");
                    string content = File.ReadAllText(file);
                    markdown.AppendLine(content);
                    markdown.AppendLine("```");
                    markdown.AppendLine();

                    fileCount++;
                    int progress = (int)((fileCount * 100.0) / allFiles.Count);

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        ProgressValue = progress;
                        AppendLog($"Processed: {relativePath}");
                    });
                }
                catch (Exception ex)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        AppendLog($"Error reading file {file}: {ex.Message}");
                    });
                }
            }

            File.WriteAllText(_outputFilePath, markdown.ToString());

            Application.Current.Dispatcher.Invoke(() =>
            {
                ProgressValue = 100;
                AppendLog($"Created codebase.md with {fileCount} files.");
            });
        }

        private bool ShouldExcludeFile(string filePath, List<string> excludePatterns)
        {
            string fileName = Path.GetFileName(filePath);
            string directoryName = Path.GetDirectoryName(filePath) ?? "";
            string extension = Path.GetExtension(filePath).ToLower();

            // Check if the file or its directory matches any exclude pattern
            foreach (var pattern in excludePatterns)
            {
                // Case-insensitive matching
                if (fileName.Contains(pattern, StringComparison.OrdinalIgnoreCase) ||
                    directoryName.Contains(pattern, StringComparison.OrdinalIgnoreCase) ||
                    extension.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsLikelyCodeFile(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLower();

            // Common code file extensions
            var codeExtensions = new HashSet<string>
            {
                ".cs", ".vb", ".fs", ".csproj", ".sln", ".xaml", ".axaml", // .NET
                ".java", ".kt", ".scala", ".gradle", // JVM
                ".js", ".jsx", ".ts", ".tsx", ".vue", ".html", ".htm", ".css", ".scss", ".less", // Web
                ".py", ".pyw", ".pyi", // Python
                ".c", ".cpp", ".h", ".hpp", ".cc", ".cxx", // C/C++
                ".rs", ".toml", // Rust
                ".go", ".mod", // Go
                ".php", ".phtml", // PHP
                ".rb", ".erb", // Ruby
                ".swift", ".m", ".mm", // Apple
                ".sql", ".psql", // Database
                ".sh", ".bash", ".ps1", ".bat", ".cmd", // Scripts
                ".json", ".xml", ".yaml", ".yml", ".config", ".ini", ".env", // Config
                ".md", ".markdown", ".txt", ".csv", // Documentation
                ".dockerfile", ".dockerignore", ".gitignore", ".editorconfig", // Development files
                
                // Godot Engine specific files
                ".gd", ".gdscript", // GDScript
                ".tscn", ".tres", ".escn", // Scene and resource files
                ".godot", // Godot project file
                ".import", // Import settings
                ".shader", ".gdshader", // Shader files
                ".gdns", ".gdnlib", // GDNative files
                ".cfg" // Configuration files
            };

            if (codeExtensions.Contains(extension))
            {
                return true;
            }

            // Check for files without extensions that might be code (scripts, etc.)
            string fileName = Path.GetFileName(filePath).ToLower();
            var commonCodeFileNames = new HashSet<string>
            {
                "dockerfile", "makefile", "rakefile", "jenkinsfile", "vagrantfile",
                "gemfile", "readme", "license", "contributing",
                "project.godot", "export_presets.cfg", "environment" // Godot-specific
            };

            return commonCodeFileNames.Contains(fileName);
        }

        private string GenerateDirectoryTree(string path, List<string> excludePatterns, string indent = "")
        {
            var builder = new StringBuilder();
            string folderName = Path.GetFileName(path);

            // Skip if this directory should be excluded
            if (excludePatterns.Any(pattern => path.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
            {
                return string.Empty;
            }

            if (!string.IsNullOrEmpty(folderName))
            {
                builder.AppendLine($"{indent}{folderName}/");
            }

            string childIndent = indent + "    ";

            // Add directories
            foreach (var directory in Directory.GetDirectories(path))
            {
                builder.Append(GenerateDirectoryTree(directory, excludePatterns, childIndent));
            }

            // Add files
            foreach (var file in Directory.GetFiles(path))
            {
                if (!ShouldExcludeFile(file, excludePatterns) && IsLikelyCodeFile(file))
                {
                    string fileName = Path.GetFileName(file);
                    builder.AppendLine($"{childIndent}{fileName}");
                }
            }

            return builder.ToString();
        }

        private string GetLanguageHint(string extension)
        {
            return extension.ToLower() switch
            {
                // .NET languages
                "cs" => "csharp",
                "vb" => "vbnet",
                "fs" => "fsharp",
                "xaml" => "xml",
                "axaml" => "xml",
                "csproj" => "xml",
                "vbproj" => "xml",
                "fsproj" => "xml",
                "sln" => "text",

                // Web technologies
                "js" => "javascript",
                "jsx" => "javascript",
                "ts" => "typescript",
                "tsx" => "typescript",
                "html" => "html",
                "htm" => "html",
                "css" => "css",
                "scss" => "scss",
                "less" => "less",
                "vue" => "vue",

                // Other languages
                "py" => "python",
                "java" => "java",
                "kt" => "kotlin",
                "scala" => "scala",
                "c" => "c",
                "cpp" => "cpp",
                "h" => "cpp",
                "hpp" => "cpp",
                "cc" => "cpp",
                "cxx" => "cpp",
                "rs" => "rust",
                "go" => "go",
                "php" => "php",
                "rb" => "ruby",
                "swift" => "swift",

                // Godot Engine files
                "gd" => "gdscript",
                "gdscript" => "gdscript",
                "tscn" => "ini",  // Scene files use a format similar to INI
                "tres" => "ini",  // Resource files use a format similar to INI
                "escn" => "ini",  // External scene files
                "godot" => "ini", // Project settings
                "import" => "ini", // Import settings
                "shader" => "glsl", // Shaders use GLSL-like syntax
                "gdshader" => "glsl",
                "gdns" => "ini", // GDNative script
                "gdnlib" => "ini", // GDNative library

                // Shell and scripts
                "sh" => "bash",
                "bash" => "bash",
                "ps1" => "powershell",
                "bat" => "batch",
                "cmd" => "batch",

                // Data and config
                "json" => "json",
                "xml" => "xml",
                "yaml" => "yaml",
                "yml" => "yaml",
                "toml" => "toml",
                "ini" => "ini",
                "cfg" => "ini",
                "config" => "xml",
                "env" => "properties",

                // Documentation
                "md" => "markdown",
                "markdown" => "markdown",
                "txt" => "text",

                // Database
                "sql" => "sql",
                "psql" => "sql",

                // Default
                _ => extension
            };
        }

        private void AppendLog(string message)
        {
            LogText += message + Environment.NewLine;
        }

        private void OpenOutputButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_outputFilePath) && File.Exists(_outputFilePath))
            {
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = _outputFilePath,
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to open file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}